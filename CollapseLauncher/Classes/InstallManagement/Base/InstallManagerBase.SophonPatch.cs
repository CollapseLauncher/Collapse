// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
// ReSharper disable InconsistentNaming
// ReSharper disable GrammarMistakeInComment
// ReSharper disable LoopCanBeConvertedToQuery

using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.Region;
using Hi3Helper.Sophon;
using Hi3Helper.Sophon.Structs;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

            // Perform check on additional data package (in this case: Zenless Zone Zero has some cutscene files registered)
            await ConfirmAdditionalPatchDataPackageFiles(patchManifest, matchingFields, Token.Token);

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

        protected virtual async Task ConfirmAdditionalPatchDataPackageFiles(SophonChunkManifestInfoPair patchManifest,
                                                                            List<string> matchingFieldsList,
                                                                            CancellationToken token)
        {
            string currentVersion = GameVersion.ToString();

            List<SophonManifestPatchIdentity> otherManifestIdentity = patchManifest.OtherSophonPatchData.ManifestIdentityList
                                                                                   .Where(x => !CommonSophonPackageMatchingFields.Contains(x.MatchingField, StringComparer.OrdinalIgnoreCase))
                                                                                   .ToList();

            if (otherManifestIdentity.Count == 0)
            {
                return;
            }

            long sizeCurrentToDownload = patchManifest.OtherSophonPatchData.ManifestIdentityList
                                                      .Where(x => matchingFieldsList.Contains(x.MatchingField, StringComparer.OrdinalIgnoreCase))
                                                      .Sum(x =>
                                                           {
                                                               var firstTag = x.DiffTaggedInfo.FirstOrDefault(y => y.Key == currentVersion).Value;
                                                               return firstTag?.CompressedSize ?? 0;
                                                           });
            long sizeAdditionalToDownload = otherManifestIdentity
                                           .Sum(x =>
                                                {
                                                    var firstTag = x.DiffTaggedInfo.FirstOrDefault(y => y.Key == currentVersion).Value;
                                                    return firstTag?.CompressedSize ?? 0;
                                                });

            if (AskAdditionalSophonPkg)
            {
                bool isDownloadAdditionalData = await SpawnAdditionalPackageDownloadDialog(sizeCurrentToDownload,
                                                                                           sizeAdditionalToDownload,
                                                                                           true,
                                                                                           GetFileDetails);

                if (!isDownloadAdditionalData)
                {
                    return;
                }
            }

            matchingFieldsList.AddRange(otherManifestIdentity.Select(identity => identity.MatchingField));
            return;

            string GetFileDetails()
            {
                string filePath = Path.GetTempFileName();
                filePath = Path.Combine(Path.GetDirectoryName(filePath) ?? "", Path.GetFileNameWithoutExtension(filePath) + ".log");
                
                long sizeUncompressed = 0;
                long sizeCompressed   = 0;
                long fileCount        = 0;
                long chunkCount       = 0;

                // ReSharper disable once ConvertToUsingDeclaration
                using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    using StreamWriter writer = new StreamWriter(fileStream);
                    foreach (var field in otherManifestIdentity)
                    {
                        var fieldInfo = field.DiffTaggedInfo.FirstOrDefault(x => x.Key == currentVersion).Value;
                        if (fieldInfo == null)
                        {
                            continue;
                        }

                        writer.WriteLine($"Additional MatchingField ID: {field.MatchingField} ({field.CategoryName})");
                        writer.WriteLine($"    Patch Size to Download (Compressed): {ConverterTool.SummarizeSizeSimple(fieldInfo.CompressedSize)} ({fieldInfo.CompressedSize} bytes)");
                        writer.WriteLine($"    Patch Size to Download (Uncompressed): {ConverterTool.SummarizeSizeSimple(fieldInfo.UncompressedSize)} ({fieldInfo.UncompressedSize} bytes)");
                        writer.WriteLine($"    Update Chunk Count: {fieldInfo.ChunkCount}");
                        writer.WriteLine($"    File Count: {fieldInfo.FileCount}");
                        writer.WriteLine();

                        sizeCompressed   += fieldInfo.CompressedSize;
                        sizeUncompressed += fieldInfo.UncompressedSize;
                        fileCount        += fieldInfo.FileCount;
                        chunkCount       += fieldInfo.ChunkCount;
                    }

                    writer.WriteLine($"Total Patch Size to Download (Compressed): {ConverterTool.SummarizeSizeSimple(sizeCompressed)} ({sizeCompressed} bytes)");
                    writer.WriteLine($"Total Patch Size to Download (Uncompressed): {ConverterTool.SummarizeSizeSimple(sizeUncompressed)} ({sizeUncompressed} bytes)");
                    writer.WriteLine($"Total Update Chunk Count: {chunkCount}");
                    writer.WriteLine($"Total File Count: {fileCount}");
                }

                return filePath;
            }
        }

        protected async Task<bool> SpawnAdditionalPackageDownloadDialog(long baseDownloadSize,
                                                                        long additionalDownloadSize,
                                                                        bool isUpdate,
                                                                        Func<string>? getFileDetailPath)
        {
            Grid grid = UIElementExtensions.CreateGrid()
                .WithRows(GridLength.Auto, new GridLength(1, GridUnitType.Star));

            grid.AddElementToGridRow(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap
            }
           .AddTextBlockLine(Locale.Lang._Dialogs.SophonAdditionalPkgAvailableSubtitle1, true)
           .AddTextBlockLine(ConverterTool.SummarizeSizeSimple(additionalDownloadSize), Microsoft.UI.Text.FontWeights.Bold)
           .AddTextBlockLine(Locale.Lang._Dialogs.SophonAdditionalPkgAvailableSubtitle2, true)
           .AddTextBlockLine(ConverterTool.SummarizeSizeSimple(baseDownloadSize + additionalDownloadSize), true, Microsoft.UI.Text.FontWeights.Bold)
           .AddTextBlockLine(Locale.Lang._Dialogs.SophonAdditionalPkgAvailableSubtitle3)
           .AddTextBlockNewLine(2)
           .AddTextBlockLine(Locale.Lang._Dialogs.SophonAdditionalPkgAvailableSubtitle4, true)
           .AddTextBlockLine($"\"{Locale.Lang._Dialogs.SophonAdditionalConfirmYesBtn}\"", true, Microsoft.UI.Text.FontWeights.Bold)
           .AddTextBlockLine(Locale.Lang._Dialogs.SophonAdditionalPkgAvailableSubtitle5, true)
           .AddTextBlockLine($"\"{Locale.Lang._Dialogs.SophonAdditionalConfirmNoBtn}\"", true, Microsoft.UI.Text.FontWeights.Bold)
           .AddTextBlockLine(Locale.Lang._Dialogs.SophonAdditionalPkgAvailableSubtitle6, true)
           .AddTextBlockLine(ConverterTool.SummarizeSizeSimple(baseDownloadSize), true, Microsoft.UI.Text.FontWeights.Bold)
           .AddTextBlockLine(Locale.Lang._Dialogs.SophonAdditionalPkgAvailableSubtitle7)
           .AddTextBlockNewLine(2)
           .AddTextBlockLine(Locale.Lang._Dialogs.SophonAdditionalPkgAvailableFootnote1, true, Microsoft.UI.Text.FontWeights.Bold, size: 12, opacity: 0.75d)
           .AddTextBlockLine(Locale.Lang._Dialogs.SophonAdditionalPkgAvailableFootnote2, size: 12, opacity: 0.75d),
           0);

            if (getFileDetailPath != null)
            {
                Button showFileDetails = UIElementExtensions.CreateButtonWithIcon<Button>(Locale.Lang._Dialogs.SophonAdditionalPkgSeeDetailsBtn,
                                                                                          iconGlyph: "",
                                                                                          iconFontFamily: "FontAwesomeSolid",
                                                                                          buttonStyle: "AccentButtonStyle",
                                                                                          cornerRadius: new CornerRadius(14));
                showFileDetails.WithMargin(new Thickness(0, 16d, 0, 0));
                showFileDetails.Click += async (sender, _) =>
                                         {
                                             if (sender is not ButtonBase button)
                                             {
                                                 return;
                                             }

                                             button.IsEnabled = false;

                                             string filePath = getFileDetailPath.Invoke();
                                             if (!string.IsNullOrEmpty(filePath))
                                             {
                                                 Process process = new Process
                                                 {
                                                     StartInfo = new ProcessStartInfo
                                                     {
                                                         FileName = filePath,
                                                         UseShellExecute = true
                                                     }
                                                 };
                                                 process.Start();
                                             }

                                             await Task.Delay(TimeSpan.FromSeconds(2));
                                             button.IsEnabled = true;
                                         };

                grid.AddElementToGridRow(showFileDetails, 1);
            }

            ContentDialogResult confirmAdditionalTag = await SimpleDialogs.SpawnDialog(
             isUpdate ? Locale.Lang._Dialogs.SophonAdditionalPkgAvailableUpdateTitle : Locale.Lang._Dialogs.SophonAdditionalPkgAvailableDownloadTitle,
             grid,
             ParentUI,
             Locale.Lang._Misc.Cancel,
             Locale.Lang._Dialogs.SophonAdditionalConfirmYesBtn,
             Locale.Lang._Dialogs.SophonAdditionalConfirmNoBtn,
             defaultButton: ContentDialogButton.Secondary,
             dialogTheme: CustomControls.ContentDialogTheme.Warning);

            if (confirmAdditionalTag == ContentDialogResult.None)
                throw new OperationCanceledException("Cancelling the download/update");

            return confirmAdditionalTag == ContentDialogResult.Primary;
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
            List<(SophonChunkManifestInfoPair Patch, SophonChunkManifestInfoPair Main, bool IsCommon)> patchManifestList = [];

            // Iterate matching fields and get the patch metadata
            foreach (string matchingField in matchingFields)
            {
                bool isCommonPackage = CommonSophonPackageMatchingFields.Contains(matchingField, StringComparer.OrdinalIgnoreCase);

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

                Logger.LogWriteLine($"Getting diff for matching field: {matchingField}", LogType.Debug, true);

                // Get the manifest pair based on the matching field
                if (!rootPatchManifest
                       .TryGetOtherPatchInfoPair(matchingField, updateVersionfrom, out var patchManifest))
                {
                    Logger.LogWriteLine($"[InstallManagerBase::GetAlterSophonPatchAssets] Cannot find past-version patch manifest for matching field: {matchingField}, Skipping!",
                                        LogType.Warning,
                                        true);
                    continue;
                }

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
                patchManifestList.Add((patchManifest, mainManifest, isCommonPackage));
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

            List<Tuple<SophonPatchAsset, Dictionary<string, int>>> pipelineDownloadEnumerable = patchAssets
               .EnsureOnlyGetDedupPatchAssets()
               .Select(x => new Tuple<SophonPatchAsset, Dictionary<string, int>>(x, downloadedPatchHashSet))
               .ToList();

            List<Tuple<SophonPatchAsset, Dictionary<string, int>>> pipelinePatchEnumerable = patchAssets
               .Select(x => new Tuple<SophonPatchAsset, Dictionary<string, int>>(x, downloadedPatchHashSet))
               .ToList();

            ParallelOptions parallelOptions = new()
            {
                MaxDegreeOfParallelism = threadNum,
                CancellationToken      = token,
                TaskScheduler          = TaskScheduler.Default
            };

            string patchOutputDir = _gameSophonChunkDir;

            // Get download sizes
            long downloadSizeTotalAssetRemote = patchAssets.Where(x => x.PatchMethod != SophonPatchMethod.Remove).Sum(x => x.TargetFileSize);
            long downloadSizePatchOnlyRemote = patchAssets.Where(x => x.PatchMethod is SophonPatchMethod.CopyOver or SophonPatchMethod.Patch).Sum(x => x.PatchChunkLength);

            // Get download counts
            int downloadCountTotalAssetRemote = patchAssets.Count;

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
            ProgressAllCountCurrent          = 0;
            ProgressAllCountTotal            = pipelineDownloadEnumerable.Count;
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
            ProgressAllCountCurrent          = 0;
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
                                                    GamePath,
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
