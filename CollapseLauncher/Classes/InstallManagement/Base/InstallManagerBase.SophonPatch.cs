// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
// ReSharper disable InconsistentNaming
// ReSharper disable GrammarMistakeInComment
// ReSharper disable LoopCanBeConvertedToQuery

using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Shared.Region;
using Hi3Helper.Sophon;
using Hi3Helper.Sophon.Structs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.InstallManager.Base
{
    internal partial class InstallManagerBase
    {
        protected virtual async Task<bool> AlterStartPatchUpdateSophon(HttpClient httpClient,
                                                                       bool isPreloadMode,
                                                                       int maxThread,
                                                                       int maxChunksThread,
                                                                       int maxHttpHandler)
        {
            // Sanity check
            ArgumentNullException.ThrowIfNull(GameVersionManager,
                                              nameof(GameVersionManager));
            ArgumentNullException.ThrowIfNull(GameVersionManager.GamePreset.LauncherResourceChunksURL,
                                              nameof(GameVersionManager.GamePreset.LauncherResourceChunksURL));
            ArgumentNullException.ThrowIfNull(GameVersionManager.GamePreset.LauncherResourceChunksURL.BranchUrl,
                                              nameof(GameVersionManager.GamePreset.LauncherResourceChunksURL.BranchUrl));
            ArgumentNullException.ThrowIfNull(GameVersionManager.GamePreset.LauncherBizName,
                                              nameof(GameVersionManager.GamePreset.LauncherBizName));

            // Get GameVersionManager and GamePreset
            IGameVersion gameVersion = GameVersionManager;
            PresetConfig gamePreset = gameVersion.GamePreset;

            // Gt current and future version
            GameVersion? requestedVersionFrom = gameVersion.GetGameExistingVersion() ??
                                                throw new NullReferenceException("Cannot get previous/current version of the game");
            GameVersion? requestedVersionTo = (isPreloadMode ?
                                                  gameVersion.GetGameVersionApiPreload() :
                                                  gameVersion.GetGameVersionApi()) ??
                                              throw new NullReferenceException("Cannot get next/future version of the game");

            // Assign branch properties
            SophonChunkUrls branchResources = gamePreset.LauncherResourceChunksURL;
            string branchUrl = branchResources.BranchUrl;
            string bizName = gamePreset.LauncherBizName;

            // If patch URL is null, then return false and back to old compare method
            if (string.IsNullOrEmpty(branchResources.PatchUrl))
            {
                return false;
            }

            // Reassociate URL's queries
            await branchResources.EnsureReassociated(httpClient,
                                                     branchUrl,
                                                     bizName,
                                                     isPreloadMode,
                                                     Token.Token);

            // Fetch manifest info and get patch metadata
            SophonChunkManifestInfoPair patchManifest =
                await SophonPatch.CreateSophonChunkManifestInfoPair(httpClient,
                                                                    url: branchResources.PatchUrl,
                                                                    versionUpdateFrom: requestedVersionFrom.Value.VersionString,
                                                                    matchingField: branchResources.MainBranchMatchingField,
                                                                    token: Token.Token);

            // If the patch metadata is not found, then return false and back to old compare method
            if (!patchManifest.IsFound)
            {
                Logger.LogWriteLine($"[InstallManagerBase::AlterStartPatchUpdateSophon] Cannot find alternative patch method for version: {requestedVersionFrom} -> {requestedVersionTo}",
                                    LogType.Error,
                                    true);
                return false;
            }

            // Get matching fields from main game and the VO
            List<string> matchingFields = [GameVersionManager.GamePreset.LauncherResourceChunksURL.MainBranchMatchingField];
            matchingFields.AddRange(await GetAlterSophonPatchVOMatchingFields(Token.Token));

            // Create a sophon download speed limiter instance
            SophonDownloadSpeedLimiter downloadSpeedLimiter =
                SophonDownloadSpeedLimiter.CreateInstance(LauncherConfig.DownloadSpeedLimitCached);

            // Get the patch assets to download
            (List<SophonPatchAsset>, List<SophonChunkManifestInfoPair>) patchAssets =
                await GetAlterSophonPatchAssets(httpClient,
                                                branchResources.PatchUrl,
                                                (isPreloadMode ? branchResources.PreloadUrl : branchResources.MainUrl) ?? "",
                                                matchingFields,
                                                requestedVersionFrom.Value.VersionString,
                                                downloadSpeedLimiter,
                                                Token.Token);

            // Start the patch pipeline
            await StartAlterSophonPatch(httpClient,
                                        isPreloadMode,
                                        patchAssets.Item1,
                                        patchAssets.Item2,
                                        downloadSpeedLimiter,
                                        maxThread,
                                        Token.Token);

            return true;
        }

        protected virtual async Task<(List<SophonPatchAsset>, List<SophonChunkManifestInfoPair>)>
            GetAlterSophonPatchAssets(HttpClient httpClient,
                                      string manifestUrl,
                                      string downloadOverUrl,
                                      List<string> matchingFields,
                                      string updateVersionfrom,
                                      SophonDownloadSpeedLimiter downloadLimiter,
                                      CancellationToken token)
        {
            SophonChunkManifestInfoPair?      rootPatchManifest = null;
            SophonChunkManifestInfoPair?      rootMainManifest  = null;
            List<(SophonChunkManifestInfoPair Patch, SophonChunkManifestInfoPair Main)> patchManifestList = [];

            // Iterate matching fields and get the patch metadata
            foreach (string matchingField in matchingFields)
            {
                // Initialize root manifest if it's null
                rootPatchManifest ??= await SophonPatch
                    .CreateSophonChunkManifestInfoPair(httpClient,
                                                       url: manifestUrl,
                                                       versionUpdateFrom: updateVersionfrom,
                                                       matchingField: matchingField,
                                                       token: token);

                rootMainManifest ??= await SophonManifest
                    .CreateSophonChunkManifestInfoPair(httpClient,
                                                       url: downloadOverUrl,
                                                       matchingField: matchingField,
                                                       token: token);

                // Get the manifest pair based on the matching field
                SophonChunkManifestInfoPair patchManifest = rootPatchManifest
                    .GetOtherPatchInfoPair(matchingField, updateVersionfrom);

                // Get the main manifest pair based on the matching field
                SophonChunkManifestInfoPair mainManifest = rootMainManifest
                    .GetOtherManifestInfoPair(matchingField);

                // If the patch metadata is not found, continue to other manifest pair
                if (!patchManifest.IsFound)
                {
                    Logger.LogWriteLine($"[InstallManagerBase::GetAlterSophonPatchAssets] Cannot find patch manifest for matching field: {matchingField}",
                                        LogType.Error,
                                        true);
                    continue;
                }

                // Otherwise, add the manifest to the list
                patchManifestList.Add((patchManifest, mainManifest));
            }

            // Initialize the return list and iterate the manifests
            List<SophonPatchAsset> patchAssets = [];
            foreach (var manifestPair in patchManifestList)
            {
                // Get the asset and add it to the list
                await foreach (SophonPatchAsset patchAsset in SophonPatch
                    .EnumerateUpdateAsync(httpClient,
                                          manifestPair.Patch,
                                          manifestPair.Main,
                                          updateVersionfrom,
                                          downloadLimiter,
                                          token))
                {
                    patchAssets.Add(patchAsset);
                }
            }

            return (patchAssets, patchManifestList.Select(x => x.Patch).ToList());
        }

        protected virtual async Task<List<string>> GetAlterSophonPatchVOMatchingFields(CancellationToken token)
        {
            FileInfo voAudioLangFileInfo = new(_gameAudioLangListPathStatic);
            List<string> voAudioMatchingFields = [];

            if (!voAudioLangFileInfo.Exists)
            {
                return voAudioMatchingFields;
            }

            using StreamReader voAudioLangReader = voAudioLangFileInfo.OpenText();
            while (await voAudioLangReader.ReadLineAsync(token) is { } line)
            {
                string? matchingField = GetLanguageLocaleCodeByLanguageString(line);
                if (string.IsNullOrEmpty(matchingField))
                {
                    continue;
                }

                voAudioMatchingFields.Add(matchingField);
            }

            return voAudioMatchingFields;
        }

        protected virtual async Task StartAlterSophonPatch(HttpClient httpClient,
                                                           bool isPreloadMode,
                                                           List<SophonPatchAsset> patchAssets,
                                                           List<SophonChunkManifestInfoPair> patchManifestInfoPairs,
                                                           SophonDownloadSpeedLimiter downloadLimiter,
                                                           int threadNum,
                                                           CancellationToken token)
        {
            Dictionary<string, int> downloadedPatchHashSet = new();
            Lock dictionaryLock = new();

            IEnumerable<Tuple<SophonPatchAsset, Dictionary<string, int>>> pipelineDownloadEnumerable = patchAssets
               .EnsureOnlyGetDedupPatchAssets()
               .Select(x => new Tuple<SophonPatchAsset, Dictionary<string, int>>(x, downloadedPatchHashSet));

            IEnumerable<Tuple<SophonPatchAsset, Dictionary<string, int>>> pipelinePatchEnumerable = patchAssets
               .Select(x => new Tuple<SophonPatchAsset, Dictionary<string, int>>(x, downloadedPatchHashSet));

            ParallelOptions parallelOptions = new()
            {
                MaxDegreeOfParallelism = threadNum,
                CancellationToken      = token,
                TaskScheduler          = TaskScheduler.Default
            };
            
            if (LauncherConfig.GetAppConfigValue("SophonPreloadApplyPerfMode"))
            {
                parallelOptions.MaxDegreeOfParallelism = Environment.ProcessorCount;
            }

            string patchOutputDir = _gameSophonChunkDir;

            // Get download sizes
            long downloadSizeTotalAssetRemote = patchAssets.Where(x => x.PatchMethod != SophonPatchMethod.Remove).Sum(x => x.TargetFileSize);
            long downloadSizePatchOnlyRemote = patchManifestInfoPairs.Sum(x => x.ChunksInfo.TotalSize);

            // Get download counts
            int downloadCountTotalAssetRemote = patchAssets.Count;
            int downloadCountPatchOnlyRemote = patchManifestInfoPairs.Sum(x => x.ChunksInfo.ChunksCount);

            // Ensure disk space sufficiency
            await EnsureDiskSpaceSufficiencyAsync(downloadSizePatchOnlyRemote,
                                                  GamePath,
                                                  patchAssets.EnsureOnlyGetDedupPatchAssets().ToList(),
                                                  (asset, _) =>
                                                  {
                                                      if (asset.PatchMethod is not (SophonPatchMethod.Patch or SophonPatchMethod.CopyOver))
                                                      {
                                                          return ValueTask.FromResult(0L);
                                                      }

                                                      string patchFilePath = Path.Combine(patchOutputDir, asset.PatchHash);
                                                      FileInfo patchFileInfo = new FileInfo(patchFilePath);

                                                      if (!patchFileInfo.Exists)
                                                      {
                                                          return ValueTask.FromResult(asset.PatchSize);
                                                      }

                                                      long patchFileCurrentSize = patchFileInfo.Length;
                                                      long patchFileRemainedSize = asset.PatchSize - patchFileCurrentSize;

                                                      return ValueTask.FromResult(patchFileRemainedSize != 0 ? asset.PatchSize : 0L);
                                                  },
                                                  token);

            // Assign local download progress
            ProgressAllCountCurrent          = 1;
            ProgressAllCountTotal            = downloadCountPatchOnlyRemote;
            ProgressPerFileSizeCurrent       = 0;
            ProgressPerFileSizeTotal         = downloadSizePatchOnlyRemote;
            ProgressAllSizeCurrent           = 0;
            ProgressAllSizeTotal             = downloadSizePatchOnlyRemote;
            Status.IsIncludePerFileIndicator = false;

            // Run parallel pipeline for download
            await Parallel.ForEachAsync(pipelineDownloadEnumerable, parallelOptions, ImplDownload);

            // If it's on preload mode, then return as we only need to perform patch download.
            if (isPreloadMode)
            {
                return;
            }

            // If it's not a preload mode (patch mode), then execute the patch pipeline as well
            ProgressAllCountCurrent          = 1;
            ProgressAllCountTotal            = downloadCountTotalAssetRemote;
            ProgressPerFileSizeCurrent       = 0;
            ProgressPerFileSizeTotal         = downloadSizePatchOnlyRemote;
            ProgressAllSizeCurrent           = 0;
            ProgressAllSizeTotal             = downloadSizeTotalAssetRemote;
            Status.IsIncludePerFileIndicator = true;

            // Run parallel pipeline for patch
            await Parallel.ForEachAsync(pipelinePatchEnumerable, parallelOptions, ImplPatchUpdate);

            if (_canDeleteZip)
            {
                patchAssets.RemovePatches(patchOutputDir);
            }

            return;

            async ValueTask ImplDownload(Tuple<SophonPatchAsset, Dictionary<string, int>> ctx, CancellationToken innerToken)
            {
                SophonPatchAsset        patchAsset     = ctx.Item1;
                Dictionary<string, int> downloadedDict = ctx.Item2;

                using (dictionaryLock.EnterScope())
                {
                    _ = downloadedDict.TryAdd(patchAsset.PatchNameSource, 0);
                    downloadedDict[patchAsset.PatchNameSource]++;
                }

                UpdateCurrentDownloadStatus();
                await patchAsset.DownloadPatchAsync(httpClient,
                                                    patchOutputDir,
                                                    true,
                                                    read =>
                                                    {
                                                        UpdateSophonFileTotalProgress(read);
                                                        UpdateSophonFileDownloadProgress(read, read);
                                                    },
                                                    downloadLimiter,
                                                    innerToken);

                Logger.LogWriteLine($"Downloaded patch file for: {patchAsset.TargetFilePath}",
                                    LogType.Debug,
                                    true);
                Interlocked.Increment(ref ProgressAllCountCurrent);
            }

            async ValueTask ImplPatchUpdate(Tuple<SophonPatchAsset, Dictionary<string, int>> ctx, CancellationToken innerToken)
            {
                SophonPatchAsset patchAsset = ctx.Item1;
                Dictionary<string, int> downloadedDict = ctx.Item2;

                using (dictionaryLock.EnterScope())
                {
                    if (!string.IsNullOrEmpty(patchAsset.PatchNameSource) &&
                        downloadedDict.Remove(patchAsset.PatchNameSource))
                    {
                        Interlocked.Add(ref ProgressPerFileSizeCurrent, patchAsset.PatchSize);
                    }
                }

                UpdateCurrentPatchStatus();
                await patchAsset.ApplyPatchUpdateAsync(httpClient,
                                                       GamePath,
                                                       patchOutputDir,
                                                       true,
                                                       read =>
                                                       {
                                                           UpdateSophonFileDownloadProgress(0, read);
                                                       },
                                                       UpdateSophonFileTotalProgress,
                                                       downloadLimiter,
                                                       innerToken);

                (string status, string fileToLog) =
                    patchAsset.PatchMethod switch
                    {
                        SophonPatchMethod.Remove => ("removed", patchAsset.OriginalFilePath),
                        SophonPatchMethod.CopyOver => ("created", patchAsset.TargetFilePath),
                        SophonPatchMethod.DownloadOver => ("downloaded", patchAsset.TargetFilePath),
                        _ => ("patched", patchAsset.TargetFilePath)
                    };
                Logger.LogWriteLine($"File has been {status} [{patchAsset.PatchMethod}]: {fileToLog}",
                                    LogType.Debug,
                                    true);
                Interlocked.Increment(ref ProgressAllCountCurrent);
            }

            void UpdateCurrentDownloadStatus()
            {
                string perFromToLocale = string.Format(Locale.Lang._Misc.PerFromTo,
                                                       ProgressAllCountCurrent,
                                                       ProgressAllCountTotal);
                Status.ActivityStatus = $"{Locale.Lang._Misc.Downloading}: {perFromToLocale}";
                UpdateStatus();
            }

            void UpdateCurrentPatchStatus()
            {
                string perFromToLocale = string.Format(Locale.Lang._Misc.PerFromTo,
                                                       ProgressAllCountCurrent,
                                                       ProgressAllCountTotal);
                Status.ActivityStatus = $"{Locale.Lang._Misc.ApplyingPatch}: {perFromToLocale}";
                UpdateStatus();
            }
        }
    }
}
