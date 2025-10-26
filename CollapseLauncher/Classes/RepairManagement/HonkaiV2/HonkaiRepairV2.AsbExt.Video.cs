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
    internal const string RelativePathVideo = @"BH3_Data\StreamingAssets\Video\";

    internal static async Task<List<FilePropertiesRemote>>
        GetVideoAssetListAsync<T>(
            this HttpClient   assetBundleHttpClient,
            PresetConfig      presetConfig,
            GameVersion       gameVersion,
            KianaDispatch     gameServerInfo,
            ProgressBase<T>   progressibleInstance,
            int[]?            ignoredCgIds = null,
            CancellationToken token        = default)
        where T : IAssetIndexSummary
    {
        bool              isUseHttpRepairOverride = progressibleInstance.IsForceHttpOverride;
        AudioLanguageType gameLanguageType        = GetCurrentGameAudioLanguage(presetConfig);
        int               parallelThread          = Math.Clamp(progressibleInstance.ThreadForIONormalized * 4, 16, 64);

        HashSet<int> ignoredCgHashset = new(ignoredCgIds ?? []);
        List<CacheAssetInfo> assetInfoList =
            await GetCacheAssetBundleListAsync(assetBundleHttpClient,
                                               presetConfig,
                                               gameVersion,
                                               gameServerInfo,
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
            return [];
        }

        // Update Progress
        progressibleInstance.Status.ActivityStatus =
            string.Format(Locale.Lang._CachesPage.CachesStatusFetchingType, "Video");
        progressibleInstance.Status.IsProgressAllIndetermined = true;
        progressibleInstance.Status.IsIncludePerFileIndicator = false;

        progressibleInstance.UpdateStatus();

        await using Stream cgFileStream =
            (await assetBundleHttpClient.TryGetCachedStreamFrom(cgMetadataFile.AssetUrl, token: token))
           .Stream;
        await using MemoryStream cgFileStreamMemory = new MemoryStream();
        await progressibleInstance.DoCopyStreamProgress(cgFileStream, cgFileStreamMemory, token: token);
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

        return cgEntries;

        async ValueTask ImplCheckAndAdd(CGMetadata entry, CancellationToken innerToken)
        {
            if (entry.InStreamingAssets ||
                ignoredCgHashset.Contains(entry.CgSubCategory))
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
                /*
                if (entry.AppointmentDownloadScheduleID == 0)
                {
                    goto AddCgEntry; // I love goto. Dun ask me why :>
                }
                */

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

                if (urlStatus.FileSize > 0)
                {
                    assetFilesize = urlStatus.FileSize;
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
