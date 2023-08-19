using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Shared.Region;
using Squirrel.Sources;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public class UpdateManagerHttpAdapter : IFileDownloader
    {
        public async Task DownloadFile(string url, string targetFile, Action<int> progress, string authorization = null, string accept = null)
        {
            Http _httpClient = new Http(true);
            EventHandler<DownloadEvent> progressEvent = (_, b) => progress((int)b.ProgressPercentage);
            try
            {
                FallbackCDNUtil.DownloadProgress += progressEvent;
                await FallbackCDNUtil.DownloadCDNFallbackContent(_httpClient, targetFile, AppCurrentDownloadThread, GetRelativePathOnly(url), default);
            }
            catch { throw; }
            finally
            {
                FallbackCDNUtil.DownloadProgress -= progressEvent;
                _httpClient?.Dispose();
            }
        }

        public async Task<byte[]> DownloadBytes(string url, string authorization = null, string accept = null)
        {
            Http _httpClient = new Http(true);
            MemoryStream fs = new MemoryStream();
            try
            {
                await FallbackCDNUtil.DownloadCDNFallbackContent(_httpClient, fs, GetRelativePathOnly(url), default);
                return fs.ToArray();
            }
            catch { throw; }
            finally
            {
                fs?.Dispose();
                _httpClient?.Dispose();
            }
        }

        public async Task<string> DownloadString(string url, string authorization = null, string accept = null)
        {
            Http _httpClient = new Http(true);
            MemoryStream fs = new MemoryStream();
            try
            {
                await FallbackCDNUtil.DownloadCDNFallbackContent(_httpClient, fs, GetRelativePathOnly(url), default);
                return Encoding.UTF8.GetString(fs.ToArray());
            }
            catch { throw; }
            finally
            {
                fs?.Dispose();
                _httpClient?.Dispose();
            }
        }

        private string GetRelativePathOnly(string url)
        {
            string toCompare = FallbackCDNUtil.GetPreferredCDN().URLPrefix;
            return url.AsSpan(toCompare.Length).ToString();
        }
    }

    internal static class FallbackCDNUtil
    {
        private static HttpClient _client = new HttpClient();
        public static event EventHandler<DownloadEvent> DownloadProgress;

        public static async Task DownloadCDNFallbackContent(Http httpInstance, string outputPath, int parallelThread, string relativeURL, CancellationToken token)
        {
            // Get the preferred CDN first and try get the content
            CDNURLProperty preferredCDN = GetPreferredCDN();
            bool isSuccess = await TryGetCDNContent(preferredCDN, httpInstance, outputPath, relativeURL, parallelThread, token);

            // If successful, then return
            if (isSuccess) return;

            // If the fail return code occurred by the token, then throw cancellation exception
            token.ThrowIfCancellationRequested();

            // If not, then continue to get the content from another CDN
            foreach (CDNURLProperty fallbackCDN in CDNList.Where(x => !x.Equals(preferredCDN)))
            {
                isSuccess = await TryGetCDNContent(fallbackCDN, httpInstance, outputPath, relativeURL, parallelThread, token);

                // If successful, then return
                if (isSuccess) return;
            }

            // If all of them failed, then throw an exception
            if (!isSuccess)
            {
                throw new AggregateException($"All available CDNs aren't reachable for your network while getting content: {relativeURL}. Please check your internet!");
            }
        }

        public static async Task DownloadCDNFallbackContent(Http httpInstance, Stream outputStream, string relativeURL, CancellationToken token)
        {
            // Argument check
            PerformStreamCheckAndSeek(outputStream);

            // Get the preferred CDN first and try get the content
            CDNURLProperty preferredCDN = GetPreferredCDN();
            bool isSuccess = await TryGetCDNContent(preferredCDN, httpInstance, outputStream, relativeURL, token);

            // If successful, then return
            if (isSuccess) return;

            // If the fail return code occurred by the token, then throw cancellation exception
            token.ThrowIfCancellationRequested();

            // If not, then continue to get the content from another CDN
            foreach (CDNURLProperty fallbackCDN in CDNList.Where(x => !x.Equals(preferredCDN)))
            {
                isSuccess = await TryGetCDNContent(fallbackCDN, httpInstance, outputStream, relativeURL, token);

                // If successful, then return
                if (isSuccess) return;
            }

            // If all of them failed, then throw an exception
            if (!isSuccess)
            {
                throw new AggregateException($"All available CDNs aren't reachable for your network while getting content: {relativeURL}. Please check your internet!");
            }
        }

        private static void PerformStreamCheckAndSeek(Stream outputStream)
        {
            // Throw if output stream can't write and seek
            if (!outputStream.CanWrite) throw new ArgumentException($"outputStream must be writable!", "outputStream");
            if (!outputStream.CanSeek) throw new ArgumentException($"outputStream must be seekable!", "outputStream");

            // Reset the outputStream position
            outputStream.Position = 0;
        }

        private static async ValueTask<bool> TryGetCDNContent(CDNURLProperty cdnProp, Http httpInstance, Stream outputStream, string relativeURL, CancellationToken token)
        {
            try
            {
                // Subscribe the progress to the adapter
                httpInstance.DownloadProgress += HttpInstance_DownloadProgressAdapter;

                // Get the URL Status then return boolean and and URLStatus
                (bool, string) urlStatus = await TryGetURLStatus(cdnProp, httpInstance, relativeURL, token);

                // If URL status is false, then return false
                if (!urlStatus.Item1) return false;

                // Continue to get the content and return true if successful
                await httpInstance.Download(urlStatus.Item2, outputStream, null, null, token);
                return true;
            }
            // Handle the error and log it. If fails, then log it and return false
            catch (Exception ex)
            {
                LogWriteLine($"Failed while getting CDN content from: {cdnProp.Name} (prefix: {cdnProp.URLPrefix}) (relPath: {relativeURL})\r\n{ex}", LogType.Error, true);
                return false;
            }
            // Finally, unsubscribe the progress from the adapter
            finally
            {
                httpInstance.DownloadProgress -= HttpInstance_DownloadProgressAdapter;
            }
        }

        private static async ValueTask<bool> TryGetCDNContent(CDNURLProperty cdnProp, Http httpInstance, string outputPath, string relativeURL, int parallelThread, CancellationToken token)
        {
            try
            {
                // Subscribe the progress to the adapter
                httpInstance.DownloadProgress += HttpInstance_DownloadProgressAdapter;

                // Get the URL Status then return boolean and and URLStatus
                (bool, string) urlStatus = await TryGetURLStatus(cdnProp, httpInstance, relativeURL, token);

                // If URL status is false, then return false
                if (!urlStatus.Item1) return false;

                // Continue to get the content and return true if successful
                if (!cdnProp.PartialDownloadSupport)
                {
                    // If the CDN marked to not supporting the partial download, then use single thread mode download.
                    await httpInstance.Download(urlStatus.Item2, outputPath, true, null, null, token);
                    return true;
                }
                await httpInstance.Download(urlStatus.Item2, outputPath, (byte)parallelThread, true, token);
                await httpInstance.Merge();
                return true;
            }
            // Handle the error and log it. If fails, then log it and return false
            catch (Exception ex)
            {
                LogWriteLine($"Failed while getting CDN content from: {cdnProp.Name} (prefix: {cdnProp.URLPrefix}) (relPath: {relativeURL})\r\n{ex}", LogType.Error, true);
                return false;
            }
            // Finally, unsubscribe the progress from the adapter
            finally
            {
                httpInstance.DownloadProgress -= HttpInstance_DownloadProgressAdapter;
            }
        }

        private static async Task<(bool, string)> TryGetURLStatus(CDNURLProperty cdnProp, Http httpInstance, string relativeURL, CancellationToken token)
        {
            // Concat the URL Prefix and Relative URL
            string absoluteURL = ConverterTool.CombineURLFromString(cdnProp.URLPrefix, relativeURL);

            LogWriteLine($"Getting CDN Content from: {cdnProp.Name} at URL: {absoluteURL}", LogType.Default, true);

            // Try check the status of the URL
            (int, bool) returnCode = await httpInstance.GetURLStatus(absoluteURL, token);

            // If it's not a successful code, then return false
            if (!returnCode.Item2)
            {
                LogWriteLine($"CDN content from: {cdnProp.Name} (prefix: {cdnProp.URLPrefix}) (relPath: {relativeURL}) has returned error code: {returnCode.Item1}", LogType.Error, true);
                return (false, absoluteURL);
            }

            // Otherwise, return true
            return (true, absoluteURL);
        }

        public static CDNURLProperty GetPreferredCDN()
        {
            // Get the CurrentCDN index
            int cdnIndex = GetAppConfigValue("CurrentCDN").ToInt();

            // Fallback to the first CDN if index < 0 or > length of the list
            if (cdnIndex < 0 || cdnIndex > CDNList.Count - 1)
            {
                cdnIndex = 0;
                SetAndSaveConfigValue("CurrentCDN", 0);
            }

            // Return the CDN property as per index
            return CDNList[cdnIndex];
        }

        public static async ValueTask<T> DownloadAsJSONType<T>(string URL, JsonSerializerContext context, CancellationToken token) =>
            await _client.GetFromJsonAsync<T>(URL, new JsonSerializerOptions()
            {
                TypeInfoResolver = context
            }, token);

        public static async ValueTask<Stream> DownloadAsStream(string URL, CancellationToken token) => await _client.GetStreamAsync(URL, token);

        // Re-send the events to the static DownloadProgress
        private static void HttpInstance_DownloadProgressAdapter(object sender, DownloadEvent e) => DownloadProgress?.Invoke(sender, e);
    }
}
