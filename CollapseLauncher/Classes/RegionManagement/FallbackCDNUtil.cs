using CollapseLauncher.Helper;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Http.Legacy;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

#if !USEVELOPACK
using System.Text;
using Squirrel.Sources;
#else
using System.Text;
using Velopack.Sources;
#endif

namespace CollapseLauncher
{
    public class UpdateManagerHttpAdapter : IFileDownloader
    {
#if !USEVELOPACK
        public async Task DownloadFile(string url, string targetFile, Action<int> progress, string authorization = null, string accept = null)
#else
#nullable enable
        public async Task DownloadFile(string url, string targetFile, Action<int> progress, string? authorization = null, string? accept = null, CancellationToken cancelToken = default)
#endif
        {
            // Initialize new proxy-aware HttpClient
            using HttpClient client = new HttpClientBuilder()
                .UseLauncherConfig()
                .SetAllowedDecompression(DecompressionMethods.None)
                .Create();

            DownloadClient downloadClient = DownloadClient.CreateInstance(client);
            EventHandler<DownloadEvent> progressEvent = (_, b) => progress((int)b.ProgressPercentage);
            try
            {
                FallbackCDNUtil.DownloadProgress += progressEvent;
                await FallbackCDNUtil.DownloadCDNFallbackContent(downloadClient, targetFile, AppCurrentDownloadThread,
                                                                 GetRelativePathOnly(url),
                                                             #if !USEVELOPACK
                    default
                                                             #else
                                                                 cancelToken
                                                             #endif
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

#if !USEVELOPACK
        public async Task<byte[]> DownloadBytes(string url, string authorization = null, string accept = null)
#else
        public async Task<byte[]> DownloadBytes(string url, string? authorization = null, string? accept = null)
#endif
        {
            await using BridgedNetworkStream stream = await FallbackCDNUtil.TryGetCDNFallbackStream(GetRelativePathOnly(url), default, true);
            byte[] buffer = new byte[stream.Length];
            await stream.ReadExactlyAsync(buffer);
            return buffer;
        }

#if !USEVELOPACK
        public async Task<string> DownloadString(string url, string authorization = null, string accept = null)
#else
        public async Task<string> DownloadString(string url, string? authorization = null, string? accept = null)
#endif
        {
            await using BridgedNetworkStream stream = await FallbackCDNUtil.TryGetCDNFallbackStream(GetRelativePathOnly(url), default, true);
            byte[] buffer = new byte[stream.Length];
            await stream.ReadExactlyAsync(buffer);
            return Encoding.UTF8.GetString(buffer);
        }

        private string GetRelativePathOnly(string url)
        {
            string toCompare = FallbackCDNUtil.GetPreferredCDN().URLPrefix;
            return url.AsSpan(toCompare.Length).ToString();
        }
    }

#if USEVELOPACK
#nullable restore
#endif

    internal readonly struct CDNUtilHTTPStatus
    {
        internal readonly HttpStatusCode StatusCode;
        internal readonly bool IsSuccessStatusCode;
        internal readonly bool IsInitializationError;
        internal readonly Uri AbsoluteURL;
        internal readonly HttpResponseMessage Message;
        internal CDNUtilHTTPStatus(HttpResponseMessage message) : this(false)
        {
            Message = message;
            StatusCode = Message.StatusCode;
            IsSuccessStatusCode = Message.IsSuccessStatusCode;
            AbsoluteURL = Message.RequestMessage?.RequestUri;
        }

        private CDNUtilHTTPStatus(bool isInitializationError) => IsInitializationError = isInitializationError;

        internal static CDNUtilHTTPStatus CreateInitializationError() => new CDNUtilHTTPStatus(true);
    }

    internal readonly struct UrlStatus
    {
        internal readonly HttpStatusCode StatusCode;
        internal readonly bool IsSuccessStatusCode;

        internal UrlStatus(HttpResponseMessage message)
            : this(message.StatusCode, message.IsSuccessStatusCode) { }

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

        static FallbackCDNUtil()
        {
            InitializeHttpClient();
        }

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

            LogWriteLine($"[FallbackCDNUtil::ReinitializeHttpClient()] HttpClient under FallbackCDNUtil has been succesfully initialized", LogType.Default, true);
        }

        public static event EventHandler<DownloadEvent> DownloadProgress;

        public static async Task DownloadCDNFallbackContent(DownloadClient downloadClient, string outputPath, int parallelThread, string relativeURL, CancellationToken token)
        {
            // Get the preferred CDN first and try get the content
            CDNURLProperty preferredCDN = GetPreferredCDN();
            SentryHelper.AppCdnOption = preferredCDN.Name;
            bool isSuccess = await TryGetCDNContent(preferredCDN, downloadClient, outputPath, relativeURL, parallelThread, token);

            // If successful, then return
            if (isSuccess) return;

            // If the fail return code occurred by the token, then throw cancellation exception
            token.ThrowIfCancellationRequested();

            // If not, then continue to get the content from another CDN
            foreach (CDNURLProperty fallbackCDN in CDNList.Where(x => !x.Equals(preferredCDN)))
            {
                isSuccess = await TryGetCDNContent(fallbackCDN, downloadClient, outputPath, relativeURL, parallelThread, token);

                // If successful, then return
                if (isSuccess)
                {
                    var i = CDNList.IndexOf(fallbackCDN);
                    SetAndSaveConfigValue("CurrentCDN", i);
                    return;
                }
            }

            // If all of them failed, then throw an exception
            if (!isSuccess)
            {
                var ex = new AggregateException($"All available CDNs aren't reachable for your network while getting content: {relativeURL}. Please check your internet!");
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                throw ex;
            }
        }

        public static async Task DownloadCDNFallbackContent(DownloadClient downloadClient, Stream outputStream, string relativeURL, CancellationToken token)
        {
            // Argument check
            PerformStreamCheckAndSeek(outputStream);

            // Get the preferred CDN first and try get the content
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

                // If successful, then return
                if (isSuccess)
                {
                    var i = CDNList.IndexOf(fallbackCDN);
                    SetAndSaveConfigValue("CurrentCDN", i);
                    return;
                }
            }

            // If all of them failed, then throw an exception
            if (!isSuccess)
            {
                throw new AggregateException($"All available CDNs aren't reachable for your network while getting content: {relativeURL}. Please check your internet!");
            }
        }

        public static async Task<BridgedNetworkStream> TryGetCDNFallbackStream(string relativeURL, CancellationToken token = default, bool isForceUncompressRequest = false)
        {
            // Get the preferred CDN first and try get the content
            CDNURLProperty preferredCDN = GetPreferredCDN();
            BridgedNetworkStream contentStream = await TryGetCDNContent(preferredCDN, relativeURL, token, isForceUncompressRequest);

            // If successful, then return
            if (contentStream != null) return contentStream;

            // If the fail return code occurred by the token, then throw cancellation exception
            token.ThrowIfCancellationRequested();

            // If not, then continue to get the content from another CDN
            foreach (CDNURLProperty fallbackCDN in CDNList.Where(x => !x.Equals(preferredCDN)))
            {
                // Reassign and try get the CDN stream
                contentStream = await TryGetCDNContent(fallbackCDN, relativeURL, token, isForceUncompressRequest);

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
            if (!outputStream.CanWrite) throw new ArgumentException($"outputStream must be writable!", "outputStream");
            if (!outputStream.CanSeek) throw new ArgumentException($"outputStream must be seekable!", "outputStream");

            // Reset the outputStream position
            outputStream.Position = 0;
        }

        private static async Task<BridgedNetworkStream> TryGetCDNContent(CDNURLProperty cdnProp, string relativeURL, CancellationToken token, bool isForceUncompressRequest)
        {
            try
            {
                // Get the URL Status then return boolean and and URLStatus
                CDNUtilHTTPStatus urlStatus = await TryGetURLStatus(cdnProp, relativeURL, token, isForceUncompressRequest);

                // If URL status is false, then return null
                if (urlStatus.IsInitializationError || !urlStatus.IsSuccessStatusCode) return null;

                // Continue to get the content and return the stream if successful
                return await GetHttpStreamFromResponse(urlStatus.Message, token);
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
                // Get the URL Status then return boolean and and URLStatus
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
                // Get the URL Status then return boolean and and URLStatus
                (bool, string) urlStatus = await TryGetURLStatus(cdnProp, downloadClient, relativeURL, token);

                // If URL status is false, then return false
                if (!urlStatus.Item1) return false;

                // Start stopwatch
                stopwatch.Start();

                // Continue to get the content and return true if successful
                if (!cdnProp.PartialDownloadSupport)
                {
                    // If the CDN marked to not supporting the partial download, then use single thread mode download.
                    using FileStream stream = File.Create(outputPath);
                    await downloadClient.DownloadAsync(urlStatus.Item2, stream, false, HttpInstanceDownloadProgressAdapter, cancelToken:token);
                    return true;
                }
                await downloadClient.DownloadAsync(urlStatus.Item2, outputPath, true, progressDelegateAsync: HttpInstanceDownloadProgressAdapter, maxConnectionSessions: parallelThread, cancelToken: token);
                return true;
            }
            // Handle the error and log it. If fails, then log it and return false
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
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
            string absoluteURL = ConverterTool.CombineURLFromString(cdnProp.URLPrefix, relativeURL);

            LogWriteLine($"Getting CDN Content from: {cdnProp.Name} at URL: {absoluteURL}", LogType.Default, true);

            // Try check the status of the URL
            (HttpStatusCode, bool) returnCode = await downloadClient.GetURLStatus(absoluteURL, token);

            // If it's not a successful code, then return false
            if (!returnCode.Item2)
            {
                LogWriteLine($"CDN content from: {cdnProp.Name} (prefix: {cdnProp.URLPrefix}) (relPath: {relativeURL}) has returned error code: {returnCode.Item1} ({(int)returnCode.Item1})", LogType.Error, true);
                return (false, absoluteURL);
            }

            // Otherwise, return true
            return (true, absoluteURL);
        }

        private static async ValueTask<CDNUtilHTTPStatus> TryGetURLStatus(CDNURLProperty cdnProp, string relativeURL, CancellationToken token, bool isUncompressRequest)
        {
            try
            {
                // Concat the URL Prefix and Relative URL
                string absoluteURL = ConverterTool.CombineURLFromString(cdnProp.URLPrefix, relativeURL);

                LogWriteLine($"Getting CDN Content from: {cdnProp.Name} at URL: {absoluteURL}", LogType.Default, true);

                // Try check the status of the URL
                HttpResponseMessage responseMessage = await GetURLHttpResponse(absoluteURL, token, isUncompressRequest);

                // If it's not a successful code, log the information
                if (!responseMessage.IsSuccessStatusCode)
                    LogWriteLine($"CDN content from: {cdnProp.Name} (prefix: {cdnProp.URLPrefix}) (relPath: {relativeURL}) has returned an error code: {responseMessage.StatusCode} ({(int)responseMessage.StatusCode})", LogType.Error, true);

                // Then return the status code
                return new CDNUtilHTTPStatus(responseMessage);
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"CDN content from: {cdnProp.Name} (prefix: {cdnProp.URLPrefix}) (relPath: {relativeURL}) has failed to initialize due to an exception:\r\n{ex}", LogType.Error, true);
                return CDNUtilHTTPStatus.CreateInitializationError();
            }
        }
        
        public static CDNURLProperty GetPreferredCDN()
        {
            // Get the CurrentCDN index
            var cdnIndex = GetAppConfigValue("CurrentCDN").ToInt();

            // Fallback to the first CDN if index < 0 or > length of the list
            if (cdnIndex < 0 || cdnIndex > CDNList.Count - 1)
            {
                cdnIndex = 0;
                SetAndSaveConfigValue("CurrentCDN", 0);
            }

            // Return the CDN property as per index
            return CDNList[cdnIndex];
        }

        public static async Task<HttpResponseMessage> GetURLHttpResponse(string URL, CancellationToken token, bool isForceUncompressRequest = false)
            => isForceUncompressRequest ? await _clientNoCompression.GetURLHttpResponse(URL, HttpMethod.Get, token)
                                        : await _client.GetURLHttpResponse(URL, HttpMethod.Get, token);

#nullable enable
        public static async Task<T?> DownloadAsJSONType<T>(string? URL, JsonTypeInfo<T> typeInfo, CancellationToken token)
            => await _client.GetFromJsonAsync(URL, typeInfo, token);

        public static async Task<HttpResponseMessage> GetURLHttpResponse(this HttpClient client, string url, HttpMethod? httpMethod = null, CancellationToken token = default)
            => await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);

        public static async ValueTask<UrlStatus> GetURLStatusCode(string URL, CancellationToken token)
             => await _client.GetURLStatusCode(URL, token);

        public static async ValueTask<UrlStatus> GetURLStatusCode(this HttpClient client, string url, CancellationToken token = default)
        {
            using HttpResponseMessage message = await client.GetURLHttpResponse(url, HttpMethod.Get, token);
            return new UrlStatus(message);
        }
#nullable restore

        public static async Task<BridgedNetworkStream> GetHttpStreamFromResponse(string URL, CancellationToken token)
        {
            HttpResponseMessage responseMsg = await GetURLHttpResponse(URL, token);
            return await GetHttpStreamFromResponse(responseMsg, token);
        }

        public static async Task<BridgedNetworkStream> GetHttpStreamFromResponse(HttpResponseMessage responseMsg, CancellationToken token)
            => await BridgedNetworkStream.CreateStream(responseMsg, token);

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
                        // Get the URL Status then return boolean and and URLStatus
                        CDNUtilHTTPStatus urlStatus = await TryGetURLStatus(CDNList[i], fileAsPingTarget, tokenSource.Token, true);
                        latencyAvgArr[j] = !(isSuccess = urlStatus.IsSuccessStatusCode && !urlStatus.IsInitializationError) ? long.MaxValue : stopwatch.ElapsedMilliseconds;
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

            string relativeURL = URL.Substring(indexOf);

            CDNURLProperty preferredCDN = GetPreferredCDN();
            string cdnParentURL = preferredCDN.URLPrefix;
            URL = ConverterTool.CombineURLFromString(cdnParentURL, relativeURL);
            return URL;
        }
    }
}
