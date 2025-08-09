using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using Hi3Helper.Data;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Update;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.SentryHelper;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Plugins;

public partial class PluginInfo
{
    public string[]?          UpdateCdnList { get; set; }
    public IPluginSelfUpdate? Updater       { get; set; }

    public bool IsUpdateSupported
    {
        get => (UpdateCdnList != null && UpdateCdnList.Length != 0) || Updater != null;
    }

    public bool IsPluginUpToDate
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public bool IsUpdateAvailable
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public bool IsUpdateCheckInProgress
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public bool IsUpdateInProgress
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public bool IsUpdateCompleted
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public double UpdateProgress
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public PluginManifest? NextUpdateManifestInfo
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Perform a background update check task.<br/>
    /// This method is supposed to be called only from MVVM-related methods or launcher's background check method.
    /// </summary>
    /// <returns>
    /// Returns <c>false</c> if update check isn't eligible to be performed due to plugin not supporting update or the same background task already being run or an unexpected error is occurred.<br/>
    /// Returns <c>true</c> if the operation has been completed successfully.
    /// </returns>
    public async ValueTask<bool> RunCheckUpdateTask(CancellationToken token = default)
    {
        // Invalidate if the plugin doesn't support update.
        if (UpdateCdnList == null || UpdateCdnList.Length == 0 || Updater == null || Instance == null)
        {
            return false;
        }

        // Invalidate if the plugin update is already being run or update check is in progress.
        if (IsUpdateInProgress || IsUpdateCheckInProgress || IsUpdateCompleted)
        {
            return false;
        }

        try
        {
            // Lock the operation to prevent multiple update checks being run at the same time.
            IsUpdateCheckInProgress = true;
            IsUpdateCompleted       = false;

            // Unassign previous properties.
            UpdateProgress         = 0d;
            NextUpdateManifestInfo = null;
            IsUpdateAvailable      = false;
            IsPluginUpToDate       = false;

            // Try to perform managed update check task. If unavailable, use plugin's own SelfUpdater.
            if (!await RunCheckUpdateTaskCoreManaged(this, token).ConfigureAwait(false))
            {
                await RunCheckUpdateTaskCoreUnmanaged(this, token).ConfigureAwait(false);
            }

            return true;
        }
        catch (Exception ex)
        {
            // Wrap it up
            UnknownPluginException wrappedEx =
                ex.WrapPluginException($"An unexpected error has occurred while performing update check task on plugin: {Name}");
            SentryHelper.ExceptionHandler(wrappedEx, SentryHelper.ExceptionType.PluginUnhandled);
            PluginLogger.LogError(wrappedEx, "[PluginInfo::RunCheckUpdateTask()]");
            
            return false;
        }
        finally
        {
            IsUpdateCheckInProgress = false;
        }
    }

    private static async ValueTask RunCheckUpdateTaskCoreUnmanaged(PluginInfo pluginInfo, CancellationToken token)
    {
        if (pluginInfo.Updater == null || pluginInfo.Instance == null)
        {
            throw new NullReferenceException("Updater and Instance property cannot be null!");
        }

        string updateOutputDir = Path.Combine(pluginInfo.PluginDirPath, MarkPendingUpdateApplyFileName);
        pluginInfo.Updater.TryPerformUpdateAsync(updateOutputDir,
                                                 true,
                                                 null,
                                                 pluginInfo.Instance.RegisterCancelToken(token),
                                                 out nint asyncResult);

        using SelfUpdateReturnInfo updateRoutineStatus = ReplicateFromPtr(await asyncResult.AsTask<nint>());
                
        // Update manifest if update is available.
        if (updateRoutineStatus.ReturnCode.HasFlag(SelfUpdateReturnCode.UpdateIsAvailable) &&
            updateRoutineStatus.PluginVersion != GameVersion.Empty &&
            updateRoutineStatus.PluginVersion != pluginInfo.Version)
        {
            pluginInfo.IsUpdateAvailable = true;
            pluginInfo.NextUpdateManifestInfo = new PluginManifest
            {
                PluginVersion         = updateRoutineStatus.PluginVersion ?? default,
                PluginStandardVersion = updateRoutineStatus.StandardVersion ?? default,
                MainPluginAuthor      = updateRoutineStatus.Author ?? string.Empty,
                MainPluginName        = updateRoutineStatus.Name ?? string.Empty,
                MainPluginDescription = updateRoutineStatus.Description ?? string.Empty,
                PluginCreationDate    = updateRoutineStatus.CreationDate ?? default,
                ManifestDate          = updateRoutineStatus.CompiledDate ?? default,
                MainLibraryName       = pluginInfo.PluginManifest.MainLibraryName,
                Assets                = [] // Leave the asset list empty as the update will be handled by the plugin itself.
            };
        }

        if (updateRoutineStatus.PluginVersion == pluginInfo.Version)
        {
            pluginInfo.IsPluginUpToDate = true;
        }

        // Throw if an error is occurred
        if (updateRoutineStatus.ReturnCode.HasFlag(SelfUpdateReturnCode.Error))
        {
            throw new InvalidOperationException($"Plugin has thrown an error while performing update check with return code: {updateRoutineStatus.ReturnCode} ({(int)updateRoutineStatus.ReturnCode})");
        }
    }

    private static async Task<bool> RunCheckUpdateTaskCoreManaged(PluginInfo pluginInfo, CancellationToken token = default)
    {
        if (pluginInfo.UpdateCdnList?.Length == 0)
        {
            return false;
        }

        using HttpClient httpClient = HttpClientBuilder.CreateDefaultClient();
        (_, PluginManifest? manifest) = await TryGetCDNFromList(httpClient, pluginInfo.UpdateCdnList!, token);

        // If the manifest is null or
        // If the manifest version is empty or
        // If the manifest version is the same as the plugin version, then ignore while return true as successful operation.
        if (manifest == null ||
            manifest.PluginVersion == GameVersion.Empty ||
            (pluginInfo.IsPluginUpToDate = manifest.PluginVersion == pluginInfo.Version))
        {
            return true;
        }

        // Otherwise, update the plugin infos.
        pluginInfo.NextUpdateManifestInfo = manifest;
        pluginInfo.IsUpdateAvailable      = true;
        return true;
    }

    /// <summary>
    /// Perform a background update task. Make sure to run <see cref="RunCheckUpdateTask"/> first and check if <see cref="IsUpdateAvailable"/> set to true before calling this method.<br/>
    /// This method is supposed to be called only from MVVM-related methods or launcher's background check method.
    /// </summary>
    public async ValueTask RunUpdateTask(CancellationToken token = default)
    {
        if (!IsUpdateAvailable ||
            !IsUpdateSupported ||
            IsUpdateInProgress ||
            IsUpdateCheckInProgress ||
            IsUpdateCompleted ||
            IsPluginUpToDate)
        {
            return;
        }

        string updateOutputDir = Path.Combine(PluginDirPath, MarkPendingUpdateFileName);
        try
        {
            UpdateProgress     = 0;
            IsUpdateInProgress = true;

            if (await RunUpdateTaskCoreManaged(updateOutputDir, this, token) ||
                await RunUpdateTaskCoreUnmanaged(updateOutputDir, this, token))
            {
                IsUpdateCompleted = true;

                string updateCompletedStampPath = Path.Combine(updateOutputDir, MarkPendingUpdateApplyFileName);
                await File.WriteAllTextAsync(updateCompletedStampPath, "Update Completed! - Managed", token);
            }
        }
        catch (Exception ex)
        {
            UnknownPluginException wrappedEx =
                ex.WrapPluginException($"An unexpected error has occurred while performing update check task on plugin: {Name}");
            SentryHelper.ExceptionHandler(wrappedEx, SentryHelper.ExceptionType.PluginUnhandled);
            PluginLogger.LogError(wrappedEx, "[PluginInfo::RunCheckUpdateTask()]");
        }
        finally
        {
            IsUpdateInProgress = false;
            IsUpdateAvailable  = false;
        }
    }

    private static async ValueTask<bool> RunUpdateTaskCoreManaged(string outputDir, PluginInfo pluginInfo, CancellationToken token)
    {
        if (pluginInfo.UpdateCdnList == null || pluginInfo.UpdateCdnList.Length == 0)
        {
            return false;
        }

        using HttpClient httpClient = HttpClientBuilder.CreateDefaultClient();
        (string? cdnBaseUrl, PluginManifest? manifest) =
            await TryGetCDNFromList(httpClient, pluginInfo.UpdateCdnList, token);

        if (manifest == null || cdnBaseUrl == null)
        {
            return false;
        }

        bool hasManifestIncluded =
            manifest
               .Assets
               .FirstOrDefault(x => x.FilePath.Equals(PluginManager.ManifestPrefix, StringComparison.OrdinalIgnoreCase)) != null;

        if (!hasManifestIncluded)
        {
            manifest.Assets.Add(new PluginManifestAssetInfo
            {
                FileHash = [],
                FilePath = PluginManager.ManifestPrefix,
                Size     = 0
            });
        }

        long downloadedAll  = 0;
        long totalAssetSize = manifest.Assets.Sum(x => x.Size);
        await Parallel.ForEachAsync(manifest.Assets, token, Impl);

        return true;

        void UpdateProgress(long read)
        {
            Interlocked.Add(ref downloadedAll, read);
            lock (pluginInfo)
            {
                pluginInfo.UpdateProgress = ConverterTool.ToPercentage(totalAssetSize, downloadedAll);
            }
        }

        async ValueTask Impl(PluginManifestAssetInfo asset, CancellationToken innerToken)
        {
            string  filePath = Path.Combine(outputDir, asset.FilePath);
            string? fileDir  = Path.GetDirectoryName(filePath);
            string  fileUrl  = cdnBaseUrl.CombineUrlFromString(asset.FilePath);

            if (!string.IsNullOrEmpty(fileDir))
            {
                Directory.CreateDirectory(fileDir);
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(256 << 10);

            int  retry  = 5;
            MD5? hasher = null;

            long   downloadedCurrent = 0;
            byte[] hash              = [];

        Download:
            try
            {
                downloadedCurrent = 0;
                hasher            = MD5.Create();

                using HttpResponseMessage response =
                    await httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, innerToken);
                await using Stream     responseStream = await response.Content.ReadAsStreamAsync(innerToken);
                await using FileStream fileStream     = File.Create(filePath);

                int read;
                while ((read = await responseStream.ReadAtLeastAsync(buffer, buffer.Length, false, innerToken)) > 0)
                {
                    Interlocked.Add(ref downloadedCurrent, read);
                    UpdateProgress(read);
                    hasher.TransformBlock(buffer, 0, read, buffer, 0);
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), innerToken);
                }

                hasher.TransformFinalBlock(buffer, 0, read);
                hash = hasher.Hash!;
            }
            catch when (retry > 0)
            {
                --retry;
                UpdateProgress(-downloadedCurrent);

                goto Download;
            }
            finally
            {
                hasher?.Dispose();
            }

            if (filePath.EndsWith(PluginManager.ManifestPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (asset.FileHash.SequenceEqual(hash))
            {
                return;
            }

            Array.Reverse(hash);
            if (asset.FileHash.SequenceEqual(hash))
            {
                return;
            }

            Array.Reverse(hash);
            throw new InvalidDataException($"!Cannot update as the data hash isn't matching! {fileUrl} ({HexTool.BytesToHexUnsafe(hash)} Local != {HexTool.BytesToHexUnsafe(asset.FileHash)} Remote)");
        }
    }

    private static async ValueTask<bool> RunUpdateTaskCoreUnmanaged(string outputDir, PluginInfo pluginInfo, CancellationToken token)
    {
        if (pluginInfo.Instance == null ||
            pluginInfo.Updater == null)
        {
            return false;
        }

        pluginInfo.Updater.TryPerformUpdateAsync(outputDir,
                                                 false,
                                                 (in progress) =>
                                                 {
                                                     lock (pluginInfo)
                                                     {
                                                         pluginInfo.UpdateProgress = ConverterTool.ToPercentage(progress.TotalBytesToDownload, progress.DownloadedBytes);
                                                     }
                                                 },
                                                 pluginInfo.Instance.RegisterCancelToken(token),
                                                 out nint asyncResult);

        nint                       checkUpdateStatusP   = await asyncResult.AsTask<nint>();
        using SelfUpdateReturnInfo selfUpdateReturnInfo = ReplicateFromPtr(checkUpdateStatusP);
        SelfUpdateReturnCode       checkUpdateStatus    = selfUpdateReturnInfo.ReturnCode;

        // Throw if an error is occurred
        if (checkUpdateStatus.HasFlag(SelfUpdateReturnCode.Error))
        {
            throw new InvalidOperationException($"Plugin has thrown an error while performing update with return code: {checkUpdateStatus} ({(int)checkUpdateStatus})");
        }

        return checkUpdateStatus == SelfUpdateReturnCode.UpdateSuccess;
    }

    private static async Task<(string?, PluginManifest?)> TryGetCDNFromList(HttpClient client, string[] cdnList, CancellationToken token)
    {
        string[] randomizedUrls = GetRandomItems(cdnList);

        foreach (string currentCdnUrl in randomizedUrls)
        {
            string manifestUrl = currentCdnUrl.CombineUrlFromString(PluginManager.ManifestPrefix);

            try
            {
                using HttpResponseMessage response =
                    await client.GetAsync(manifestUrl, HttpCompletionOption.ResponseHeadersRead, token);

                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                PluginManifest? manifest =
                    await (await response.Content.ReadAsStreamAsync(token))
                       .DeserializeAsync(PluginManifestContext.Default.PluginManifest, token: token);

                return (currentCdnUrl, manifest);
            }
            catch
            {
                // ignored
            }
        }

        return (null, null);
    }

    private static unsafe SelfUpdateReturnInfo ReplicateFromPtr(nint ptr)
    {
        // Fallback if the result is actually the enum.
        SelfUpdateReturnCode asRetCode = (SelfUpdateReturnCode)(uint)ptr;
        if (IsValidReturnCode(asRetCode))
        {
            return new SelfUpdateReturnInfo((SelfUpdateReturnCode)(uint)ptr);
        }

        // Return the struct if it returns it.
        SelfUpdateReturnInfo* selfUpdateReturnInfo = ptr.AsPointer<SelfUpdateReturnInfo>();
        SelfUpdateReturnInfo  ret                  = *selfUpdateReturnInfo; // basically copy the fields.

        // Free the struct from native memory (but not the pointer inside of it since it's already copied above).
        Mem.Free(selfUpdateReturnInfo);
        return ret;
    }

    private static bool IsValidReturnCode(SelfUpdateReturnCode retCode)
    {
        uint asUint = (uint)retCode;
        if (retCode.HasFlag(SelfUpdateReturnCode.Ok) &&
            asUint is >= 0b_00000001_00000000_00000000_00000000 and <= 0b_10000000_00000000_00000000_00000000)
        {
            return true;
        }

        if (retCode.HasFlag(SelfUpdateReturnCode.Error) &&
            asUint is >= 0b_00000000_00000000_00000001_00000000 and <= 0b_00000000_10000000_00000000_00000000)
        {
            return true;
        }

        return false;
    }

    private static TItem[] GetRandomItems<TItem>(TItem[] items)
    {
        TItem[] itemsRandomized = new TItem[items.Length];
        Random.Shared.GetItems(items, itemsRandomized);

        return itemsRandomized;
    }
}