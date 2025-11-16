using CollapseLauncher.Interfaces;
using Hi3Helper.EncTool;
using Hi3Helper.Http;
using Hi3Helper.Shared.Region;
using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable IDE0290
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.GameManagement.WpfPackage;

internal partial class WpfPackageContext
{
    private async ValueTask<bool> StartUpdateCheckAsyncCore()
    {
        try
        {
            ArgumentException.ThrowIfNullOrEmpty(GamePath);

            // Resets token
            ResetCancelToken();

            ChangesInProgress                = true;
            IsCheckAlreadyPerformed          = true;
            Status.IsProgressAllIndetermined = true;

            // Cancel if no update is available
            if (!IsUpdateAvailable ||
                WpfPackageData == null)
            {
                return true;
            }

            // Get client and URL
            HttpClient client       = FallbackCDNUtil.GetGlobalHttpClient(false);
            string     url          = await GetPackageStatsAndUrl(client);
            string     localPath    = Path.Combine(GamePath, Path.GetFileName(url));
            long       downloadSize = ProgressAllSizeTotal;
            FileInfo   fileInfo     = new(localPath);

            while (true)
            {
                ResetProgress();

                // Try to verify the package first.
                // If fails, (re)download the package.
                if (IsSkipPackageVerification ||
                    (fileInfo.Exists &&
                     fileInfo.Length == downloadSize &&
                     await IsPackageHashMatch(fileInfo,
                                              WpfPackageData.PackageMD5Hash,
                                              _localCts.Token)))
                {
                    goto StartExtract;
                }

                // Download the package
                await DownloadPackage(client,
                                      url,
                                      localPath,
                                      downloadSize,
                                      CancellationToken.None);
            }

            StartExtract:
            long totalToExtractSize = 0;
            await using (FileStream fileStream = fileInfo.OpenRead())
            {
                totalToExtractSize += LauncherConfig.IsEnforceToUse7zipOnExtract
                ? GetArchiveUncompressedSizeNative7Zip(fileStream)
                : GetArchiveUncompressedSizeManaged(fileStream);
            }
            ProgressAllSizeTotal     = totalToExtractSize;
            ProgressPerFileSizeTotal = totalToExtractSize;
            ResetProgress();

            InstallPackageExtractorDelegate extractDelegate =
                LauncherConfig.IsEnforceToUse7zipOnExtract
                    ? ExtractUsingNative7Zip
                    : ExtractUsingManagedZip;

            await extractDelegate(() => fileInfo.Open(FileMode.Open,
                                                      FileAccess.Read,
                                                      FileShare.ReadWrite),
                                  GamePath,
                                  _localCts.Token);

            // Set version to save the config
            CurrentInstalledVersion = CurrentAvailableVersion;
        }
        catch when (_localCts.Token.IsCancellationRequested)
        {
            // ignored
        }
        catch (Exception ex)
        {
            LastError = ex;
        }
        finally
        {
            ChangesInProgress                = false;
            Status.IsProgressAllIndetermined = false;
        }

        return true;
    }

    private async Task<bool> IsPackageHashMatch(
        FileInfo          fileInfo,
        Memory<byte>      hashToCheck,
        CancellationToken token)
    {
        byte[] hashLocal =
            await GetCryptoHashAsync<MD5>(fileInfo,
                                          null,
                                          true,
                                          true,
                                          token);

        if (hashLocal.SequenceEqual(hashToCheck.Span))
        {
            return true;
        }

        // Try reverse the hash result and retry the check
        Array.Reverse(hashLocal);
        return hashLocal.SequenceEqual(hashToCheck.Span);
    }

    private Task DownloadPackage(
        HttpClient        httpClient,
        string            url,
        string            localPath,
        long              downloadSize,
        CancellationToken token)
    {
        DownloadClient downloadClient = DownloadClient
           .CreateInstance(httpClient);

        return RunDownloadTask(downloadSize,
                               new FileInfo(localPath),
                               url,
                               downloadClient,
                               HttpClientDownloadProgressAdapter,
                               token: token);
    }

    private async Task<string> GetPackageStatsAndUrl(HttpClient client)
    {
        string? packageUrl = WpfPackageData?.Url;
        if (string.IsNullOrEmpty(packageUrl))
        {
            throw new NullReferenceException("WPF Package is available but the URL is undefined. Just a usual miHoYo move, great job! :)");
        }

        long totalSize = WpfPackageData?.PackageSize ?? 0;
        if (totalSize == 0)
        {
            UrlStatus packageUrlStatus = await client.GetURLStatusCode(packageUrl,
                                                                       _localCts.Token);
            packageUrlStatus.EnsureSuccessStatus();

            totalSize = packageUrlStatus.FileSize;
        }

        // Assign total size
        ProgressAllSizeTotal     = totalSize;
        ProgressPerFileSizeTotal = totalSize;

        return packageUrl;
    }

    private void ResetProgress()
    {
        ProgressAllSizeCurrent  = 0;
        ProgressAllCountTotal   = 1;
        ProgressAllCountCurrent = 1;

        ProgressPerFileSizeCurrent = 0;
    }
}
