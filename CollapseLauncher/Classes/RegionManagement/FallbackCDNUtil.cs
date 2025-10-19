﻿using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.Http;
using Hi3Helper.Http.Legacy;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Velopack.Sources;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

#nullable enable
namespace CollapseLauncher
{
    public class UpdateManagerHttpAdapter : IFileDownloader
    {
        private async Task DownloadFileInner(string            url,
                                             string            targetFile,
                                             Action<int>       progress,
                                             string?           authorization = null,
                                             CancellationToken cancelToken   = default)
        {
            // Initialize new proxy-aware HttpClient
            HttpClientBuilder builder = new HttpClientBuilder()
                                                           .UseLauncherConfig()
                                                           .SetAllowedDecompression(DecompressionMethods.None);

            if (!string.IsNullOrEmpty(authorization))
            {
                builder.AddHeader("Authorization", authorization);
            }

            using HttpClient client = builder.Create();

            DownloadClient              downloadClient = DownloadClient.CreateInstance(client);
            EventHandler<DownloadEvent> progressEvent  = (_, b) => progress((int)b.ProgressPercentage);
            try
            {
                FallbackCDNUtil.DownloadProgress += progressEvent;
                string relativePath = GetRelativePathOnly(url);
                await FallbackCDNUtil.DownloadCDNFallbackContent(downloadClient,
                                                                 targetFile,
                                                                 AppCurrentDownloadThread,
                                                                 relativePath,
                                                                 cancelToken
                                                                );
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                throw;
            }
            finally
            {
                FallbackCDNUtil.DownloadProgress -= progressEvent;
            }
        }

        public async Task<byte[]> DownloadBytesInner(string url, string? authorization = null, string? accept = null, double timeout = 30.0)
        {
            string             relativePath = GetRelativePathOnly(url);
            await using Stream stream = await FallbackCDNUtil.TryGetCDNFallbackStream(relativePath, true, CancellationToken.None);
            byte[]             buffer = new byte[stream.Length];
            await stream.ReadExactlyAsync(buffer);
            return buffer;
        }

        public async Task<string> DownloadStringInner(string url, string? authorization = null, string? accept = null,  double timeout = 30.0)
        {
            string             relativePath = GetRelativePathOnly(url);
            await using Stream stream = await FallbackCDNUtil.TryGetCDNFallbackStream(relativePath, true, CancellationToken.None);
            byte[]             buffer = new byte[stream.Length];
            await stream.ReadExactlyAsync(buffer);
            return Encoding.UTF8.GetString(buffer);
        }

        private string[]? _cdnBaseUrls;
        private string GetRelativePathOnly(string url)
        {
            // Populate the CDN Base URLs if the field is null
            _cdnBaseUrls ??= CDNList.Select(x => x.URLPrefix).ToArray();

            // Get URL span and iterate through the CDN Base URLs
            ReadOnlySpan<char> urlSpan = url.AsSpan();
            for (int i = _cdnBaseUrls.Length - 1; i >= 0; i--)
            {
                // Get the index of the base URL. If not found (-1), then continue
                int indexOf = urlSpan.IndexOf(_cdnBaseUrls[i], StringComparison.OrdinalIgnoreCase);
                if (indexOf < 0) continue;

                // Otherwise, slice the urlSpan based on the index and return the sliced relative URL.
                return urlSpan[(indexOf + _cdnBaseUrls[i].Length)..].ToString();
            }

            // If it's not a CDN-based url, return it anyway.
            return url;
        }

        public async Task DownloadFile(string                       url,
                                       string                       targetFile,
                                       Action<int>                  progress,
                                       IDictionary<string, string>? headers     = null,
                                       double                       timeout     = 30,
                                       CancellationToken            cancelToken = default)
            => await DownloadFileInner(url,
                                       targetFile,
                                       progress,
                                       headers.TryGetValueIgnoreCase("Authorization"),
                                       cancelToken);

        public async Task<byte[]> DownloadBytes(string url, IDictionary<string, string>? headers = null, double timeout = 30)
            => await DownloadBytesInner(url, headers.TryGetValueIgnoreCase("Authorization"), null, timeout);

        public async Task<string> DownloadString(string                       url, 
                                                 IDictionary<string, string>? headers = null,
                                                 double                       timeout = 30)
            => await DownloadStringInner(url,
                                         headers?.TryGetValueIgnoreCase("Authorization"),
                                         null,
                                         timeout);
    }

    internal readonly struct CDNUtilHTTPStatus
    {
        internal readonly HttpStatusCode       StatusCode;
        internal readonly bool                 IsSuccessStatusCode;
        internal readonly bool                 IsInitializationError;
        internal readonly Uri?                 AbsoluteURL;
        internal readonly HttpResponseMessage? Message;
        internal CDNUtilHTTPStatus(HttpResponseMessage message) : this(false)
        {
            Message = message;
            StatusCode = Message.StatusCode;
            IsSuccessStatusCode = Message.IsSuccessStatusCode;
            AbsoluteURL = Message.RequestMessage?.RequestUri;
        }

        private CDNUtilHTTPStatus(bool isInitializationError) => IsInitializationError = isInitializationError;

        internal static CDNUtilHTTPStatus CreateInitializationError() => new(true);
    }

    internal readonly struct UrlStatus
    {
        internal readonly HttpStatusCode StatusCode;
        internal readonly bool           IsSuccessStatusCode;
        internal readonly long?          FileSize = null;

        internal UrlStatus(HttpResponseMessage message)
            : this(message.StatusCode, message.IsSuccessStatusCode)
        {
            FileSize = message.Content.Headers.ContentLength;
        }

        internal UrlStatus(HttpStatusCode statusCode, bool isSuccessStatusCode)
        {
            StatusCode = statusCode;
            IsSuccessStatusCode = isSuccessStatusCode;
        }
    }

    internal static class FallbackCDNUtil
    {
        private static HttpClient _client;

        private static HttpClient _clientNoCompression;

        // ReSharper disable once NotNullOrRequiredMemberIsNotInitialized
    #pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        static FallbackCDNUtil()
        {
            InitializeHttpClient();
        }
    #pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        // ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        // ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        public static void InitializeHttpClient()
        {
            _client?.Dispose();
            _clientNoCompression?.Dispose();

            _client = new HttpClientBuilder()
                     .UseLauncherConfig()
                     .Create();

            _clientNoCompression = new HttpClientBuilder()
                                  .UseLauncherConfig()
                                  .SetAllowedDecompression(DecompressionMethods.None)
                                  .Create();

            LogWriteLine("[FallbackCDNUtil::ReinitializeHttpClient()] HttpClient under FallbackCDNUtil has been successfully initialized", LogType.Default, true);
        }
        // ReSharper enable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        // ReSharper enable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

        public static event EventHandler<DownloadEvent>? DownloadProgress;

        public static async Task DownloadCDNFallbackContent(DownloadClient downloadClient, string outputPath, int parallelThread, string relativeURL, CancellationToken token)
        {
            // Get the preferred CDN first and try to get the content
            CDNURLProperty preferredCDN = GetPreferredCDN();
            bool isSuccess = await TryGetCDNContent(preferredCDN, downloadClient, outputPath, relativeURL, parallelThread, token);

            // If successful, then return
            if (isSuccess) return;

            // If the fail return code occurred by the token, then throw cancellation exception
            token.ThrowIfCancellationRequested();

            // If not, then continue to get the content from another CDN
            foreach (CDNURLProperty fallbackCDN in CDNList.Where(x => !x.Equals(preferredCDN)))
            {
                isSuccess = await TryGetCDNContent(fallbackCDN, downloadClient, outputPath, relativeURL, parallelThread, token);

                if (!isSuccess)
                {
                    continue;
                }

                // If successful, then return
                int i = CDNList.IndexOf(fallbackCDN);
                SetAndSaveConfigValue("CurrentCDN", i);
                return;
            }

            // If all of them failed, then throw an exception
            if (!isSuccess)
            {
                AggregateException ex = new($"All available CDNs aren't reachable for your network while getting content: {relativeURL}. Please check your internet!");
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                throw ex;
            }
        }

        public static async Task DownloadCDNFallbackContent(DownloadClient downloadClient, Stream outputStream, string relativeURL, CancellationToken token)
        {
            // Argument check
            PerformStreamCheckAndSeek(outputStream);

            // Get the preferred CDN first and try to get the content
            CDNURLProperty preferredCDN = GetPreferredCDN();
            bool isSuccess = await TryGetCDNContent(preferredCDN, downloadClient, outputStream, relativeURL, token);

            // If successful, then return
            if (isSuccess) return;

            // If the fail return code occurred by the token, then throw cancellation exception
            token.ThrowIfCancellationRequested();

            // If not, then continue to get the content from another CDN
            foreach (CDNURLProperty fallbackCDN in CDNList.Where(x => !x.Equals(preferredCDN)))
            {
                isSuccess = await TryGetCDNContent(fallbackCDN, downloadClient, outputStream, relativeURL, token);

                if (!isSuccess)
                {
                    continue;
                }

                // If successful, then return
                int i = CDNList.IndexOf(fallbackCDN);
                SetAndSaveConfigValue("CurrentCDN", i);
                return;
            }

            // If all of them failed, then throw an exception
            if (!isSuccess)
            {
                throw new AggregateException($"All available CDNs aren't reachable for your network while getting content: {relativeURL}. Please check your internet!");
            }
        }

        public static async Task<Stream> TryGetCDNFallbackStream(string relativeURL, bool isForceUncompressRequest = false, CancellationToken token = default)
        {
            // Get the preferred CDN first and try to get the content
            CDNURLProperty preferredCDN = GetPreferredCDN();
            Stream? contentStream = await TryGetCDNContent(preferredCDN, relativeURL, isForceUncompressRequest, token);

            // If successful, then return
            if (contentStream != null) return contentStream;

            // If the fail return code occurred by the token, then throw cancellation exception
            token.ThrowIfCancellationRequested();

            // If not, then continue to get the content from another CDN
            foreach (CDNURLProperty fallbackCDN in CDNList.Where(x => !x.Equals(preferredCDN)))
            {
                // Reassign and try to get the CDN stream
                contentStream = await TryGetCDNContent(fallbackCDN, relativeURL, isForceUncompressRequest, token);

                // If the stream returns a null, then continue
                if (contentStream == null) continue;

                // Otherwise, return the stream
                return contentStream;
            }

            // Throw if any attempt was failed
            throw new TimeoutException($"All available CDNs aren't reachable for your network while getting content: {relativeURL}. Please check your internet!");
        }

        private static void PerformStreamCheckAndSeek(Stream outputStream)
        {
            // Throw if output stream can't write and seek
            if (!outputStream.CanWrite) throw new ArgumentException("outputStream must be writable!", nameof(outputStream));
            if (!outputStream.CanSeek) throw new ArgumentException("outputStream must be seekable!", nameof(outputStream));

            // Reset the outputStream position
            outputStream.Position = 0;
        }

        private static async Task<Stream?> TryGetCDNContent(CDNURLProperty cdnProp, string relativeURL, bool isForceUncompressRequest, CancellationToken token)
        {
            try
            {
                HttpClient clientToUse = isForceUncompressRequest
                    ? _clientNoCompression
                    : _client;

                CDNCacheResult result = await clientToUse
                   .TryGetCachedStreamFrom(cdnProp.URLPrefix.CombineURLFromString(relativeURL),
                                           HttpMethod.Get,
                                           token);

                return result.Stream;
            }
            // Handle the error and log it. If fails, then log it and return false
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Failed while getting CDN content from: {cdnProp.Name} (prefix: {cdnProp.URLPrefix}) (relPath: {relativeURL})\r\n{ex}", LogType.Error, true);
                return null;
            }
        }

        private static async ValueTask<bool> TryGetCDNContent(CDNURLProperty cdnProp, DownloadClient downloadClient, Stream outputStream, string relativeURL, CancellationToken token)
        {
            DownloadEvent DownloadClientAdapter = new DownloadEvent();
            Stopwatch stopwatch = new Stopwatch();

            try
            {
                // Get the URL Status then return boolean and URLStatus
                (bool, string) urlStatus = await TryGetURLStatus(cdnProp, downloadClient, relativeURL, token);

                // If URL status is false, then return false
                if (!urlStatus.Item1) return false;

                // Start stopwatch
                stopwatch.Start();

                // Continue to get the content and return true if successful
                await downloadClient.DownloadAsync(urlStatus.Item2, outputStream, false, HttpInstanceDownloadProgressAdapter, cancelToken:token);
                return true;
            }
            // Handle the error and log it. If fails, then log it and return false
            catch (Exception ex)
            {
                LogWriteLine($"Failed while getting CDN content from: {cdnProp.Name} (prefix: {cdnProp.URLPrefix}) (relPath: {relativeURL})\r\n{ex}", LogType.Error, true);
                return false;
            }
            finally
            {
                stopwatch.Stop();
            }

            void HttpInstanceDownloadProgressAdapter(int read, DownloadProgress downloadProgress)
            {
                DownloadClientAdapter.SizeToBeDownloaded = downloadProgress.BytesTotal;
                DownloadClientAdapter.SizeDownloaded = downloadProgress.BytesDownloaded;
                DownloadClientAdapter.Read = read;

                long speed = (long)(downloadProgress.BytesDownloaded / stopwatch.Elapsed.TotalSeconds);
                DownloadClientAdapter.Speed = speed;
                
                DownloadProgress?.Invoke(null, DownloadClientAdapter);
            }
        }

        private static async ValueTask<bool> TryGetCDNContent(CDNURLProperty cdnProp, DownloadClient downloadClient, string outputPath, string relativeURL, int parallelThread, CancellationToken token)
        {
            DownloadEvent DownloadClientAdapter = new DownloadEvent();
            Stopwatch stopwatch = new Stopwatch();

            try
            {
                // Get the URL Status then return boolean and URLStatus
                (bool, string) urlStatus = await TryGetURLStatus(cdnProp, downloadClient, relativeURL, token);

                // If URL status is false, then return false
                if (!urlStatus.Item1) return false;

                // Start stopwatch
                stopwatch.Start();

                // Continue to get the content and return true if successful
                if (!cdnProp.PartialDownloadSupport)
                {
                    // If the CDN marked to not supporting the partial download, then use single thread mode download.
                    await using FileStream stream = File.Create(outputPath);
                    await downloadClient.DownloadAsync(urlStatus.Item2, stream, false, HttpInstanceDownloadProgressAdapter, cancelToken:token);
                    return true;
                }
                await downloadClient.DownloadAsync(urlStatus.Item2, outputPath, true, progressDelegateAsync: HttpInstanceDownloadProgressAdapter, maxConnectionSessions: parallelThread, cancelToken: token);
                return true;
            }
            // Handle the error and log it. If fails, then log it and return false
            catch (Exception ex)
            {
                LogWriteLine($"Failed while getting CDN content from: {cdnProp.Name} (prefix: {cdnProp.URLPrefix}) (relPath: {relativeURL})\r\n{ex}", LogType.Error, true);
                return false;
            }
            finally
            {
                stopwatch.Stop();
            }

            void HttpInstanceDownloadProgressAdapter(int read, DownloadProgress downloadProgress)
            {
                DownloadClientAdapter.SizeToBeDownloaded = downloadProgress.BytesTotal;
                DownloadClientAdapter.SizeDownloaded = downloadProgress.BytesDownloaded;
                DownloadClientAdapter.Read = read;

                long speed = (long)(downloadProgress.BytesDownloaded / stopwatch.Elapsed.TotalSeconds);
                DownloadClientAdapter.Speed = speed;

                DownloadProgress?.Invoke(null, DownloadClientAdapter);
            }
        }

        private static async ValueTask<(bool, string)> TryGetURLStatus(CDNURLProperty cdnProp, DownloadClient downloadClient, string relativeURL, CancellationToken token)
        {
            // Concat the URL Prefix and Relative URL
            string absoluteURL = cdnProp.URLPrefix.CombineURLFromString(relativeURL);

            LogWriteLine($"Getting CDN Content from: {cdnProp.Name} at URL: {absoluteURL}", LogType.Default, true);

            // Try check the status of the URL
            (HttpStatusCode, bool) returnCode = await downloadClient.GetURLStatus(absoluteURL, token);

            if (returnCode.Item2)
            {
                return (true, absoluteURL);
            }

            // If it's not a successful code, then return false
            LogWriteLine($"CDN content from: {cdnProp.Name} (prefix: {cdnProp.URLPrefix}) (relPath: {relativeURL}) has returned error code: {returnCode.Item1} ({(int)returnCode.Item1})", LogType.Error, true);
            return (false, absoluteURL);

            // Otherwise, return true
        }

        private static async ValueTask<CDNUtilHTTPStatus> TryGetURLStatus(CDNURLProperty cdnProp, string relativeURL, CancellationToken token, bool isUncompressRequest)
        {
            try
            {
                // Concat the URL Prefix and Relative URL
                string absoluteURL = cdnProp.URLPrefix.CombineURLFromString(relativeURL);

                LogWriteLine($"Getting CDN Content from: {cdnProp.Name} at URL: {absoluteURL}", LogType.Default, true);

                // Try check the status of the URL
                using CancellationTokenSource timeoutCts = new CancellationTokenSource(5000);
                using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
                HttpResponseMessage httpResponse = await GetURLHttpResponse(absoluteURL, linkedCts.Token, isUncompressRequest, 1);

                // If it's not a successful code, log the information
                if (!httpResponse.IsSuccessStatusCode)
                    LogWriteLine($"CDN content from: {cdnProp.Name} (prefix: {cdnProp.URLPrefix}) (relPath: {relativeURL}) has returned an error code: {httpResponse.StatusCode} ({(int)httpResponse.StatusCode})", LogType.Error, true);

                // Then return the status code
                return new CDNUtilHTTPStatus(httpResponse);
            }
            catch (Exception ex)
            {
                LogWriteLine($"CDN content from: {cdnProp.Name} (prefix: {cdnProp.URLPrefix}) (relPath: {relativeURL}) has failed to initialize due to an exception:\r\n{ex}", LogType.Error, true);
                await SentryHelper.ExceptionHandlerAsync(ex);
                return CDNUtilHTTPStatus.CreateInitializationError();
            }
        }
        
        public static CDNURLProperty GetPreferredCDN()
        {
            // Get the CurrentCDN index
            int cdnIndex = GetAppConfigValue("CurrentCDN").ToInt();

            // Fallback to the first CDN if index < 0 or > length of the list
            if (cdnIndex >= 0 && cdnIndex <= CDNList.Count - 1)
            {
                return CDNList[cdnIndex];
            }

            cdnIndex = 0;
            SetAndSaveConfigValue("CurrentCDN", 0);

            // Return the CDN property as per index
            return CDNList[cdnIndex];
        }
        
        #nullable enable
        public static async Task<HttpResponseMessage> GetURLHttpResponse(string Url,
                                                                          CancellationToken token,
                                                                          bool isForceUncompressedRequest = false,
                                                                          int maxRetries = 3,
                                                                          int delayMilliseconds = 1000)
        {
            int attempt = 0;

            while (attempt < maxRetries)
            {
                try
                {
                    HttpResponseMessage hR;
                    if (isForceUncompressedRequest)
                    {
                        hR = await _clientNoCompression.GetURLHttpResponse(Url, HttpMethod.Get, token);
                    }
                    else
                    {
                        hR = await _client.GetURLHttpResponse(Url, HttpMethod.Get, token);
                    }

                    return hR;
                }
                catch (Exception ex)
                {
                    LogWriteLine("Failed to get URL response from: " + Url + "\r\n" + ex, LogType.Error, true);
                    if (attempt >= maxRetries - 1)
                    {
                        throw;
                    }
                }

                attempt++;
                await Task.Delay(delayMilliseconds, token);
            }

            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }
        
        public static async Task<HttpResponseMessage> GetURLHttpResponse(this HttpClient client,
                                                                         string url,
                                                                         HttpMethod? httpMethod = null,
                                                                         CancellationToken token = default,
                                                                         int maxRetries = 3,
                                                                         int delayMilliseconds = 1000)
        {
            int attempt = 0;

            while (attempt < maxRetries)
            {
                try
                {
                    return await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogWriteLine("Failed to get URL response from: " + url + "\r\n" + ex, LogType.Error, true);
                    if (attempt >= maxRetries - 1)
                    {
                        throw;
                    }
                }

                attempt++;
                await Task.Delay(delayMilliseconds, token);
            }

            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }

        public static async Task<T?> DownloadAsJSONType<T>(string?           url,
                                                           JsonTypeInfo<T?>  typeInfo,
                                                           CancellationToken token)
            where T : class
        {
            return await _client.GetFromCachedJsonAsync(url, typeInfo, null, token);
        }

        public static async ValueTask<UrlStatus> GetURLStatusCode(string url, CancellationToken token)
             => await _client.GetURLStatusCode(url, token);

        public static async ValueTask<UrlStatus> GetURLStatusCode(this HttpClient client, string url, CancellationToken token = default)
        {
            using HttpResponseMessage message = await client.GetURLHttpResponse(url, HttpMethod.Get, token);
            return new UrlStatus(message);
        }
#nullable restore

        public static async Task<Stream> GetHttpStreamFromResponse(string URL, CancellationToken token)
        {
            HttpResponseMessage responseMsg = await GetURLHttpResponse(URL, token);
            return await GetHttpStreamFromResponse(responseMsg, token);
        }

        public static async Task<Stream> GetHttpStreamFromResponse(HttpResponseMessage responseMsg, CancellationToken token)
            => (await responseMsg.TryGetCachedStreamFrom(token)).Stream;

        public static async ValueTask<long[]> GetCDNLatencies(CancellationTokenSource tokenSource, int pingCount = 1)
        {
            const string fileAsPingTarget = "stable/release";

            // Get the latency index
            long[] latencies = new long[CDNList.Count];

            // Warming up
            foreach (CDNURLProperty cdnProperty in CDNList) await TryGetURLStatus(cdnProperty, fileAsPingTarget, tokenSource.Token, true);

            using (tokenSource)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                for (int i = 0; i < latencies.Length; i++)
                {
                    long[] latencyAvgArr = new long[pingCount];
                    bool isSuccess = true;
                    for (int j = 0; j < latencyAvgArr.Length; j++)
                    {
                        // If once is failed, then assign the max value and skip the entire check
                        if (!isSuccess)
                        {
                            latencyAvgArr[j] = long.MaxValue;
                            continue;
                        }

                        // Restart the stopwatch
                        stopwatch.Restart();
                        // Get the URL Status then return boolean and URLStatus
                        CDNUtilHTTPStatus urlStatus = await TryGetURLStatus(CDNList[i], fileAsPingTarget, tokenSource.Token, true);
                        latencyAvgArr[j] = !(isSuccess = urlStatus is { IsSuccessStatusCode: true, IsInitializationError: false }) ? long.MaxValue : stopwatch.ElapsedMilliseconds;
                    }

                    // Get the average latency of the CDN.
                    long latencyAvg = latencyAvgArr.Length > 1 ? (long)latencyAvgArr.Average() : latencyAvgArr[0];
                    latencies[i] = latencyAvg;
                }
            }

            return latencies;
        }

        public static async ValueTask<long> GetContentLength(string url, CancellationToken token) =>
            (await _clientNoCompression.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token)).Content.Headers.ContentLength ?? 0;

        public static string TryGetAbsoluteToRelativeCDNURL(string URL, string searchIndexStr)
        {
            int indexOf = URL.IndexOf(searchIndexStr, StringComparison.Ordinal);
            if (indexOf < 0)
                return URL;

            string relativeURL = URL[indexOf..];

            CDNURLProperty preferredCDN = GetPreferredCDN();
            string cdnParentURL = preferredCDN.URLPrefix;
            URL = cdnParentURL.CombineURLFromString(relativeURL);
            return URL;
        }
    }
}
