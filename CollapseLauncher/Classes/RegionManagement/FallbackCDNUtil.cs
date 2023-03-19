using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Shared.Region;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    internal static class FallbackCDNUtil
    {
        public static event EventHandler<DownloadEvent> DownloadProgress;

        public static async Task DownloadCDNFallbackContent(Http httpInstance, Stream outputStream, string relativeURL, CancellationToken token)
        {
            // Argument check
            if (!outputStream.CanWrite) throw new ArgumentException($"outputStream must be writable!", "outputStream");
            if (!outputStream.CanSeek) throw new ArgumentException($"outputStream must be seekable!", "outputStream");

            // Reset the outputStream position
            outputStream.Position = 0;

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

        private static async ValueTask<bool> TryGetCDNContent(CDNURLProperty cdnProp, Http httpInstance, Stream outputStream, string relativeURL, CancellationToken token)
        {
            try
            {
                // Subscribe the progress to the adapter
                httpInstance.DownloadProgress += HttpInstance_DownloadProgressAdapter;
                // Concat the URL Prefix and Relative URL
                string absoluteURL = ConverterTool.CombineURLFromString(cdnProp.URLPrefix, relativeURL);

                LogWriteLine($"Getting CDN Content from: {cdnProp.Name} at URL: {absoluteURL}", LogType.Default, true);

                // Try check the status of the URL
                (int, bool) returnCode = await httpInstance.GetURLStatus(absoluteURL, token);

                // If it's not a successful code, then return false
                if (!returnCode.Item2)
                {
                    LogWriteLine($"CDN content from: {cdnProp.Name} (prefix: {cdnProp.URLPrefix}) (relPath: {relativeURL}) has returned error code: {returnCode.Item1}", LogType.Error, true);
                    return false;
                }

                // Continue to get the content and return true if successful
                await httpInstance.Download(absoluteURL, outputStream, null, null, token);
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

        private static CDNURLProperty GetPreferredCDN()
        {
            // Get the CurrentCDN index
            int cdnIndex = GetAppConfigValue("CurrentCDN").ToInt();
            // Return the CDN property as per index
            return CDNList[cdnIndex];
        }

        // Re-send the events to the static DownloadProgress
        private static void HttpInstance_DownloadProgressAdapter(object sender, DownloadEvent e) => DownloadProgress?.Invoke(sender, e);
    }
}
