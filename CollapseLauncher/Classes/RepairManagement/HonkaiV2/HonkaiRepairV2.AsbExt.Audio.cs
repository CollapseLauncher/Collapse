using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.EncTool.Parser.KianaDispatch;
using Hi3Helper.EncTool.Parser.Senadina;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using System;
using System.Collections.Generic;
using System.IO;
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
    internal const string RelativePathAudio = @"BH3_Data\StreamingAssets\Audio\GeneratedSoundBanks\Windows";

    internal static async Task<List<FilePropertiesRemote>>
        GetAudioAssetListAsync<T>(
            this HttpClient         assetBundleHttpClient,
            PresetConfig            presetConfig,
            GameVersion             gameVersion,
            KianaDispatch           gameServerInfo,
            SenadinaFileIdentifier? audioFileIdentifier,
            AudioPCKType[]?         ignoredCgIds         = null,
            ProgressBase<T>?        progressibleInstance = null,
            CancellationToken       token                = default)
        where T : IAssetIndexSummary
    {
        ignoredCgIds ??= [];
        int[] ignoredCgIdInts = Array.ConvertAll(ignoredCgIds, x => (int)x);

        ArgumentNullException.ThrowIfNull(audioFileIdentifier);
        int parallelThread = LauncherConfig.AppCurrentDownloadThread;
        if (parallelThread <= 0)
        {
            parallelThread = Environment.ProcessorCount;
        }

        // Update Progress
        if (progressibleInstance != null)
        {
            progressibleInstance.Status.ActivityStatus =
                string.Format(Locale.Lang._CachesPage.CachesStatusFetchingType, "Audio Manifest");
            progressibleInstance.Status.IsProgressAllIndetermined = true;
            progressibleInstance.Status.IsIncludePerFileIndicator = false;

            progressibleInstance.UpdateStatus();
        }

        bool              isUseHttpRepairOverride = LauncherConfig.GetAppConfigValue("EnableHTTPRepairOverride");
        AudioLanguageType gameLanguageType        = GetCurrentGameAudioLanguage(presetConfig);

        Exception? lastException = null;
        foreach (string baseAsbUrl in gameServerInfo.ExternalAssetUrls)
        {
            try
            {
                string baseAudioAssetUrl = ((isUseHttpRepairOverride ? "http://" : "https://") + baseAsbUrl)
                   .CombineURLFromString($"Audio/Windows/{gameVersion.Major}_{gameVersion.Minor}/{gameServerInfo
                                            .Manifest
                                            .ManifestAudio
                                            .ManifestAudioRevision}");

                await using Stream manifestStream = audioFileIdentifier.fileStream ?? throw new NullReferenceException("Senadina Audio Identifier Stream cannot be null!");
                KianaAudioManifest manifestData =
                    new(manifestStream, gameVersion.VersionArrayManifest);

                List<FilePropertiesRemote> assetList = [];
                await Parallel.ForEachAsync(manifestData.AudioAssets,
                                            new ParallelOptions
                                            {
                                                CancellationToken      = token,
                                                MaxDegreeOfParallelism = parallelThread
                                            },
                                            ImplCheckAndAdd);

                return assetList;

                async ValueTask ImplCheckAndAdd(ManifestAssetInfo audioAsset, CancellationToken innerToken)
                {
                    // Eliminate removed audio assets or not matching language.
                    if ((audioAsset.Language != gameLanguageType &&
                        audioAsset.Language != AudioLanguageType.Common) ||
                        ignoredCgIdInts.Contains((int)audioAsset.PckType))
                    {
                        return;
                    }

                    if (audioAsset.NeedMap)
                    {
                        goto AddAsset;
                    }

                    if (progressibleInstance != null)
                    {
                        progressibleInstance.Status.ActivityStatus = string.Format(Locale.Lang._GameRepairPage.Status15, audioAsset.Path);
                        progressibleInstance.Status.IsProgressAllIndetermined = true;
                        progressibleInstance.Status.IsProgressPerFileIndetermined = true;
                        progressibleInstance.UpdateStatus();
                    }

                    string    assetUrl  = baseAudioAssetUrl.CombineURLFromString(audioAsset.Path);
                    UrlStatus urlStatus = await assetBundleHttpClient.GetURLStatusCode(assetUrl, innerToken);
                    Logger.LogWriteLine($"The audio asset: {audioAsset.Path} " + (urlStatus.IsSuccessStatusCode ? "is" : "is not") + $" available (Status code: {urlStatus.StatusCode})", LogType.Default, true);

                    if (!urlStatus.IsSuccessStatusCode)
                    {
                        return;
                    }

                AddAsset:
                    lock (assetList)
                    {
                        assetList.Add(new FilePropertiesRemote
                        {
                            AssociatedObject = audioAsset,
                            CRC              = audioAsset.HashString,
                            FT               = FileType.Audio,
                            RN               = baseAudioAssetUrl.CombineURLFromString(audioAsset.Path),
                            N                = audioAsset.Name + ".pck",
                            S                = audioAsset.Size
                        });
                    }
                }
            }
            catch (Exception e)
            {
                lastException = e;
            }
        }

        throw lastException ?? new HttpRequestException("No Asset bundle URLs were reachable");
    }
}
