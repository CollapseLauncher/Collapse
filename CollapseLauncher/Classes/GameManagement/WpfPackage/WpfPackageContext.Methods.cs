using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.Http;
using Hi3Helper.Shared.Region;
using Hi3Helper.SimpleZipArchiveReader;
using Hi3Helper.Win32.WinRT.ToastCOM.Notification;
using System;
using System.Buffers;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Notifications;
// ReSharper disable CheckNamespace
#pragma warning disable IDE0290
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.GameManagement.WpfPackage;

internal partial class WpfPackageContext
{
    private async ValueTask<bool> StartUpdateCheckAsyncCore(bool isForce = false)
    {
        try
        {
            ArgumentException.ThrowIfNullOrEmpty(GamePath);

            // Resets token
            ResetCancelToken();
            ResetStatusAndProgress();

            ChangesInProgress                = true;
            IsCheckAlreadyPerformed          = true;
            Status.IsProgressAllIndetermined = true;
            ProgressAllSizeTotal             = 0;
            ProgressPerFileSizeTotal         = 0;

            // Cancel if no update is available
            if (WpfPackageData == null ||
                (!isForce && !IsUpdateAvailable))
            {
                return true;
            }

            // Get client and URL
            HttpClient client = FallbackCDNUtil.GetGlobalHttpClient(false);
            string?    url    = WpfPackageData.Url;

            if (string.IsNullOrEmpty(url))
            {
                throw new InvalidOperationException("WpfPackageData is available but the URL returns null!");
            }

            if (url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                await PerformDownloadFromZipOnTheFly(client, url);
            }
            else
            {
                await PerformDownloadAndExtractRoutine(client, WpfPackageData.PackageMD5Hash ?? []);
            }

            SpawnUpdateFinishedNotification();
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

    private async ValueTask PerformDownloadFromZipOnTheFly(
        HttpClient client,
        string     url)
    {
        ResetProgress();

        string gamePath = GamePath;

        ZipArchiveReader reader =
            await ZipArchiveReader.CreateFromAsync(url, _localCts.Token);

        long totalSizeUncompressed = reader.Sum(x => x.Size);
        ProgressAllSizeTotal     = totalSizeUncompressed;
        ProgressPerFileSizeTotal = totalSizeUncompressed;

        int downloadThread = ThreadForDownloadNormalized;

        await Parallel.ForEachAsync(reader,
                                    new ParallelOptions
                                    {
                                        MaxDegreeOfParallelism = downloadThread,
                                        CancellationToken      = _localCts.Token
                                    },
                                    Impl);

        return;

        async ValueTask Impl(ZipArchiveEntry entry, CancellationToken token)
        {
            string filePath = Path.Combine(gamePath, entry.Filename);
            if (entry.IsDirectory)
            {
                Directory.CreateDirectory(filePath);
                return;
            }

            Status.IsProgressAllIndetermined = false;
            FileInfo fileInfo = new FileInfo(filePath)
                               .EnsureCreationOfDirectory()
                               .StripAlternateDataStream()
                               .EnsureNoReadOnly();

            int bufferSize = entry.Size.GetFileStreamBufferSize();

            await using FileStream stream = fileInfo.Open(FileMode.OpenOrCreate,
                                                          FileAccess.ReadWrite,
                                                          FileShare.ReadWrite,
                                                          bufferSize);

            if (fileInfo.Exists &&
                await IsPackageHashMatchWithCrc32(stream, entry.Crc32, token))
            {
                return;
            }

            stream.Position = 0;
            stream.SetLength(0);

            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                await using Stream deflateStream = await entry.OpenStreamFromAsync(CreateStreamFromUrl, token);

                int read;
                while ((read = await deflateStream.ReadAsync(buffer, token)) > 0)
                {
                    stream.Write(buffer.AsSpan(0, read));
                    Interlocked.Add(ref ProgressAllSizeCurrent, read);
                    Interlocked.Add(ref ProgressPerFileSizeCurrent, read);

                    UpdateProgressCrc(read);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        async Task<Stream> CreateStreamFromUrl(long? from, long? to, CancellationToken token)
        {
            HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.Range = new RangeHeaderValue(from, to);

            HttpResponseMessage response =
                await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStreamAsync(token);
        }
    }

    private async ValueTask PerformDownloadAndExtractRoutine(
        HttpClient client,
        byte[]     packageMd5Hash)
    {
        string   url          = await GetPackageStatsAndUrl(client);
        string   localPath    = Path.Combine(GamePath, Path.GetFileName(url));
        long     downloadSize = ProgressAllSizeTotal;
        FileInfo fileInfo     = new(localPath);

        while (true)
        {
            ResetProgress();

            // Try to verify the package first.
            // If fails, (re)download the package.
            if (IsSkipPackageVerification ||
                (fileInfo.Exists &&
                 fileInfo.Length == downloadSize &&
                 await IsPackageHashMatch(fileInfo, packageMd5Hash, _localCts.Token)))
            {
                goto StartExtract;
            }

            // Download the package
            await DownloadPackage(client,
                                  url,
                                  localPath,
                                  downloadSize,
                                  _localCts.Token);

            fileInfo.Refresh();
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

        if (IsDeletePackageAfterInstall)
        {
            fileInfo.TryDeleteFile();
        }
    }

    private async void SpawnUpdateFinishedNotification()
    {
        try
        {
            string gameName   = GameVersionManager.GameName;
            string regionName = GameVersionManager.GameRegion;

            string gameNameTranslated = InnerLauncherConfig.GetGameTitleRegionTranslationString(gameName, Locale.Lang._GameClientTitles) ?? gameName;
            string gameRegionTranslated = InnerLauncherConfig.GetGameTitleRegionTranslationString(regionName, Locale.Lang._GameClientRegions) ?? regionName;

            string icon = await ImageLoaderHelper
                             .GetCachedSpritesAsync(WpfPackageIconUrl,
                                                    false,
                                                    CancellationToken.None)
                          ?? "";

            // Create Toast Notification Content
            NotificationContent toastContent =
                NotificationContent
                   .Create()
                   .SetTitle(string.Format(Locale.Lang._WpfPackageContext.NotifUpdateCompletedTitle, WpfPackageNameLocalized))
                   .SetContent(string.Format(Locale.Lang._WpfPackageContext.NotifUpdateCompletedSubtitle,
                                             WpfPackageNameLocalized,
                                             gameNameTranslated,
                                             gameRegionTranslated,
                                             CurrentAvailableVersion))
                   .SetAppLogoPath(icon, true);

            // Create Toast Notification Service
            ToastNotification? toastService = WindowUtility.CurrentToastNotificationService?.CreateToastNotification(toastContent);

            // Create Toast Notifier
            ToastNotifier? toastNotifier = WindowUtility.CurrentToastNotificationService?.CreateToastNotifier();
            toastNotifier?.Show(toastService);
        }
        catch
        {
            // Ignored
        }
    }

    private async Task<bool> IsPackageHashMatchWithCrc32(
        FileStream        fileStream,
        uint              crc32,
        CancellationToken token)
    {
        byte[] hashLocal =
            await GetHashAsync<Crc32>(fileStream,
                                      true,
                                      true,
                                      token);

        byte[] crc32AsBytes = BitConverter.GetBytes(crc32);
        if (hashLocal.SequenceEqual(crc32AsBytes))
        {
            return true;
        }

        // Try reverse the hash result and retry the check
        Array.Reverse(hashLocal);
        if (hashLocal.SequenceEqual(crc32AsBytes))
        {
            return true;
        }

        Interlocked.Add(ref ProgressPerFileSizeCurrent, -fileStream.Length);
        Interlocked.Add(ref ProgressAllSizeCurrent,     -fileStream.Length);
        return false;
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
            packageUrlStatus.EnsureSuccessStatusCode();

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
