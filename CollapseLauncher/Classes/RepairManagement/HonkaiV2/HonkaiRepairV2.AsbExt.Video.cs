using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.EncTool.Parser.Cache;
using Hi3Helper.EncTool.Parser.KianaDispatch;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CGMetadataHashId = Hi3Helper.EncTool.Parser.Cache.HashID;

// ReSharper disable CheckNamespace
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.RepairManagement;

internal static partial class AssetBundleExtension
{
    internal const string RelativePathVideo = @"BH3_Data\StreamingAssets\Video";

    internal static async Task<(List<FilePropertiesRemote> AssetList, KianaDispatch GameServerInfo)>
        GetVideoAssetListAsync<T>(
            this HttpClient   assetBundleHttpClient,
            PresetConfig      presetConfig,
            GameVersion       gameVersion,
            int[]?            ignoredCgIds         = null,
            ProgressBase<T>?  progressibleInstance = null,
            CancellationToken token                = default)
        where T : IAssetIndexSummary
    {
        bool isUseHttpRepairOverride = LauncherConfig.GetAppConfigValue("EnableHTTPRepairOverride");
        AudioLanguageType gameLanguageType = GetCurrentGameAudioLanguage(presetConfig);
        int parallelThread = LauncherConfig.AppCurrentDownloadThread;
        if (parallelThread <= 0)
        {
            parallelThread = Environment.ProcessorCount;
        }

        ignoredCgIds ??= [];
        (List<CacheAssetInfo> assetInfoList, KianaDispatch gameServerInfo) =
            await GetCacheAssetBundleListAsync(assetBundleHttpClient,
                                               presetConfig,
                                               gameVersion,
                                               CacheAssetType.Data,
                                               progressibleInstance,
                                               token);

        CacheAssetInfo? cgMetadataFile = assetInfoList
           .FirstOrDefault(x => x.Asset.N.EndsWith(CGMetadataHashId.CgMetadataFilename));

        if (cgMetadataFile == null)
        {
            Logger.LogWriteLine($"[AssetBundleExtension::GetVideoAssetListAsync] Cannot find CG Metadata file with Asset ID: {CGMetadataHashId.CgMetadataFilename}",
                                LogType.Error,
                                true);
            return ([], gameServerInfo);
        }

        // Update Progress
        if (progressibleInstance != null)
        {
            progressibleInstance.Status.ActivityStatus =
                string.Format(Locale.Lang._CachesPage.CachesStatusFetchingType, "Video");
            progressibleInstance.Status.IsProgressAllIndetermined = true;
            progressibleInstance.Status.IsIncludePerFileIndicator = false;

            progressibleInstance.UpdateStatus();
        }

        await using Stream cgFileStream =
            (await assetBundleHttpClient.TryGetCachedStreamFrom(cgMetadataFile.AssetUrl, token: token))
           .Stream;
        await using MemoryStream cgFileStreamMemory = new MemoryStream();
        await cgFileStream.CopyToAsync(cgFileStreamMemory, token);
        cgFileStreamMemory.Position = 0;

        await using CacheStream dechipheredCgStream =
            new CacheStream(cgFileStreamMemory, preSeed: cgMetadataFile.MhyMersenneTwisterSeed);

        List<FilePropertiesRemote> cgEntries = [];
        await Parallel
           .ForEachAsync(CGMetadata.Enumerate(dechipheredCgStream, Encoding.UTF8),
                         new ParallelOptions
                         {
                             CancellationToken      = token,
                             MaxDegreeOfParallelism = parallelThread
                         },
                         ImplCheckAndAdd);

        return (cgEntries, gameServerInfo);

        async ValueTask ImplCheckAndAdd(CGMetadata entry, CancellationToken innerToken)
        {
            if (entry.InStreamingAssets ||
                ignoredCgIds.Contains(entry.CgSubCategory))
            {
                return;
            }

            string assetName = gameLanguageType == AudioLanguageType.Japanese
                ? entry.CgPathHighBitrateJP
                : entry.CgPathHighBitrateCN;
            assetName += ".usm";

            long assetFilesize = gameLanguageType == AudioLanguageType.Japanese
                ? entry.FileSizeHighBitrateJP
                : entry.FileSizeHighBitrateCN;

            foreach (string baseUrl in gameServerInfo.ExternalAssetUrls)
            {
                string assetUrl = (isUseHttpRepairOverride ? "http://" : "https://") + baseUrl;
                assetUrl = assetUrl.CombineURLFromString("Video", assetName);

                // If the file has no appoinment schedule (like non-birthday CG), then return true
                if (entry.AppointmentDownloadScheduleID == 0)
                {
                    goto AddCgEntry;
                }

                // Update status
                if (progressibleInstance != null)
                {
                    progressibleInstance.Status.ActivityStatus = string.Format(Locale.Lang._GameRepairPage.Status14, entry.CgExtraKey);
                    progressibleInstance.Status.IsProgressAllIndetermined = true;
                    progressibleInstance.Status.IsProgressPerFileIndetermined = true;
                    progressibleInstance.UpdateStatus();
                }

                UrlStatus urlStatus = await assetBundleHttpClient.GetURLStatusCode(assetUrl, innerToken);
                Logger.LogWriteLine($"The CG asset: {assetName} " +
                                    (urlStatus.IsSuccessStatusCode ? "is" : "is not") + $" available (Status code: {urlStatus.StatusCode})", LogType.Default, true);
                if (!urlStatus.IsSuccessStatusCode)
                {
                    continue;
                }

            AddCgEntry:
                lock (cgEntries)
                {
                    cgEntries.Add(new FilePropertiesRemote
                    {
                        FT               = FileType.Video,
                        N                = assetName,
                        RN               = assetUrl,
                        S                = assetFilesize,
                        AssociatedObject = entry
                    });
                }
                return;
            }

            throw new HttpRequestException("No Asset bundle URLs were reachable");
        }
    }
}
