using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.EncTool.Parser.CacheParser;
using Hi3Helper.EncTool.Parser.KianaDispatch;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

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
    internal const string MetadataFilename  = "107438912";

    internal static void RemoveUnlistedVideoAssetFromList(this List<FilePropertiesRemote> originList,
                                                          List<FilePropertiesRemote>      assetListFromVideo)
    {
        List<FilePropertiesRemote> originOthersListOnly = originList.Where(x => x.FT != FileType.Video).ToList();
        List<FilePropertiesRemote> originVideoListOnly = originList.Where(x => x.FT == FileType.Video).ToList();
        originList.Clear();
        originList.AddRange(originOthersListOnly);

        HashSet<string> assetListVideoDict =
            assetListFromVideo.Select(x => x.N).ToHashSet(StringComparer.OrdinalIgnoreCase);

        originList.AddRange(originVideoListOnly.Where(originVideoAsset => assetListVideoDict.Contains(originVideoAsset.N)));
    }

    internal static async Task<List<FilePropertiesRemote>>
        GetVideoAssetListAsync<T>(
            this HttpClient   assetBundleHttpClient,
            PresetConfig      presetConfig,
            KianaDispatch     gameServerInfo,
            ProgressBase<T>   progressibleInstance,
            int[]?            ignoredCgIds = null,
            CancellationToken token        = default)
        where T : IAssetIndexSummary
    {
        AudioLanguageType gameLanguageType = GetCurrentGameAudioLanguage(presetConfig);
        int               parallelThread   = Math.Clamp(progressibleInstance.ThreadForIONormalized * 4, 16, 64);

        HashSet<int> ignoredCgHashset = new(ignoredCgIds ?? []);
        List<CacheAssetInfo> assetInfoList =
            await assetBundleHttpClient
               .GetCacheAssetBundleListAsync(presetConfig,
                                             gameServerInfo,
                                             CacheAssetType.Data,
                                             progressibleInstance,
                                             token);

        CacheAssetInfo? cgMetadataFile = assetInfoList
           .FirstOrDefault(x => x.Asset.N.EndsWith(MetadataFilename));

        if (cgMetadataFile == null)
        {
            Logger.LogWriteLine($"[AssetBundleExtension::GetVideoAssetListAsync] Cannot find CG Metadata file with Asset ID: {MetadataFilename}",
                                LogType.Error,
                                true);
            return [];
        }

        // Update Progress
        progressibleInstance.Status.ActivityStatus =
            string.Format(Locale.Current.Lang?._CachesPage?.CachesStatusFetchingType ?? "", "Video");
        progressibleInstance.Status.IsProgressAllIndetermined = true;
        progressibleInstance.Status.IsIncludePerFileIndicator = false;

        progressibleInstance.UpdateStatus();

        await using Stream cgFileStream =
            (await assetBundleHttpClient.TryGetCachedStreamFrom(cgMetadataFile.AssetUrl, token: token))
           .Stream;
        await using MemoryStream cgFileStreamMemory = new();
        await progressibleInstance.DoCopyStreamProgress(cgFileStream, cgFileStreamMemory, token: token);
        cgFileStreamMemory.Position = 0;

        await using CacheStream dechipheredCgStream =
            new(cgFileStreamMemory, preSeed: cgMetadataFile.MhyMersenneTwisterSeed);

        List<FilePropertiesRemote> cgEntries = [];
        await Parallel
           .ForEachAsync(KianaCgMetadata.Parse(dechipheredCgStream),
                         new ParallelOptions
                         {
                             CancellationToken      = token,
                             MaxDegreeOfParallelism = parallelThread
                         },
                         ImplCheckAndAdd);

        return cgEntries;

        async ValueTask ImplCheckAndAdd(KeyValuePair<int, KianaCgMetadata> entry, CancellationToken innerToken)
        {
            if (ignoredCgHashset.Contains(entry.Value.SubCategoryId))
            {
                return;
            }

            string assetName = (gameLanguageType == AudioLanguageType.Japanese
                ? entry.Value.PathJp
                : entry.Value.PathCn) ?? throw new NullReferenceException();
            assetName += ".usm";

            long assetFilesize = gameLanguageType == AudioLanguageType.Japanese
                ? entry.Value.SizeJp
                : entry.Value.SizeCn;

            string baseUrl  = progressibleInstance.GetRandomAsbBaseUrl(gameServerInfo);
            string assetUrl = baseUrl.CombineURLFromString("Video", assetName);

            // Update status
            progressibleInstance.Status.ActivityStatus = string.Format(Locale.Current.Lang?._GameRepairPage?.Status14 ?? "", assetName);
            progressibleInstance.Status.IsProgressAllIndetermined = true;
            progressibleInstance.Status.IsProgressPerFileIndetermined = true;
            progressibleInstance.UpdateStatus();

            if (entry.Value.Category is CGCategory.Birthday or CGCategory.Activity or CGCategory.VersionPV)
            {
                UrlStatus urlStatus = await assetBundleHttpClient.GetURLStatusCode(assetUrl, innerToken);
                Logger.LogWriteLine($"The CG asset: {assetName} " +
                                    (urlStatus.IsSuccessStatusCode ? "is" : "is not") + $" available (Status code: {urlStatus.StatusCode})", LogType.Default, true);
                if (!urlStatus.IsSuccessStatusCode)
                {
                    throw new HttpRequestException("No Asset bundle URLs were reachable");
                }

                if (urlStatus.FileSize > 0)
                {
                    assetFilesize = urlStatus.FileSize;
                }
            }

            lock (cgEntries)
            {
                cgEntries.Add(new FilePropertiesRemote
                {
                    FT               = FileType.Video,
                    N                = assetName,
                    RN               = assetUrl,
                    S                = assetFilesize,
                    AssociatedObject = entry.Value
                });
            }
        }
    }
}
