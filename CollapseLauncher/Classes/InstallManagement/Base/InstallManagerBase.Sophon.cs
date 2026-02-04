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
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using Hi3Helper.Sophon;
using Hi3Helper.Sophon.Structs;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SophonLogger = Hi3Helper.Sophon.Helper.Logger;

#nullable enable
namespace CollapseLauncher.InstallManager.Base
{
    internal delegate ValueTask<TResult> SignedValueTaskSelectorAsync<in TFrom, TResult>(
        TFrom item, CancellationToken ctx)
        where TResult : struct, ISignedNumber<TResult>;

    internal partial class InstallManagerBase
    {
        #region Protected Virtual Properties
        protected virtual string _gameSophonChunkDir => Path.Combine(GamePath, "chunk_collapse");
        #endregion

        #region Protected Properties

        private HashSet<string> _sophonVOLanguageList      { get; } = [];
        private bool            _isSophonDownloadCompleted { get; set; }

        private bool         _isSophonPreloadCompleted
        {
            get => File.Exists(Path.Combine(_gameSophonChunkDir, PreloadVerifiedFileName));
            set
            {
                string verifiedFile =
                    EnsureCreationOfDirectory(Path.Combine(_gameSophonChunkDir, PreloadVerifiedFileName));
                try
                {
                    FileInfo fileInfo = new FileInfo(verifiedFile)
                        .EnsureCreationOfDirectory()
                        .EnsureNoReadOnly(out bool isExist);

                    if (value)
                    {
                        fileInfo.Create().Dispose();
                        return;
                    }

                    if (isExist)
                    {
                        fileInfo.Delete();
                    }
                }
                catch (Exception ex)
                {
                    SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                    Logger.LogWriteLine($"Error while deleting/creating sophon preload completion file! {ex}",
                                        LogType.Warning, true);
                }
            }
        }
        #endregion

        #region Public Virtual Properties
        public virtual bool IsUseSophon
        {
            get
            {
                if ((_canDeltaPatch && !_forceIgnoreDeltaPatch) || GameVersionManager?.GamePreset.LauncherResourceChunksURL == null)
                {
                    return false;
                }

                bool isForceRedirect = GameVersionManager.IsForceRedirectToSophon();
                if (!isForceRedirect && File.Exists(Path.Combine(GamePath, "@DisableSophon")))
                {
                    return false;
                }

                if (!isForceRedirect && !LauncherConfig.GetAppConfigValue("IsEnableSophon"))
                {
                    return false;
                }

                return true;
            }
        }

        protected virtual bool AskAdditionalSophonPkg
        {
            get => File.Exists(Path.Combine(GamePath, "@AskAdditionalSophonPackage"));
        }

        private readonly string[] CommonSophonPackageMatchingFields = ["game", "en-us", "zh-tw", "zh-cn", "ko-kr", "ja-jp"];
        #endregion

        #region Sophon Verification Methods
        protected virtual void CleanupTempSophonVerifiedFiles()
        {
            DirectoryInfo dirPath = new(_gameSophonChunkDir);
            try
            {
                if (!dirPath.Exists)
                {
                    return;
                }

                foreach (FileInfo file in dirPath.EnumerateFiles("*.verified", SearchOption.TopDirectoryOnly)
                                                 .EnumerateNoReadOnly())
                {
                    file.Delete();
                }
            }
            catch
            {
                // ignored
            }
        }
        #endregion

        #region Sophon Download and Install/Update/Preload Methods

        protected virtual async Task StartPackageInstallSophon(GameInstallStateEnum gameState, bool fallbackFromUpdate = false)
        {
            // Set the flag to false
            _isSophonDownloadCompleted = false;

            // Set the max thread and httpHandler based on settings
            int maxThread       = SophonGetThreadNum();
            int maxChunksThread = Math.Clamp(maxThread / 2, 2, 32);
            int maxHttpHandler  = Math.Max(maxThread, SophonGetHttpHandler());

            Logger.LogWriteLine($"Initializing Sophon Chunk download method with Main Thread: {maxThread}, Chunks Thread: {maxChunksThread} and Max HTTP handle: {maxHttpHandler}",
                                LogType.Default, true);

            // Initialize the HTTP client
            HttpClient httpClient = new HttpClientBuilder()
                                   .UseLauncherConfig(maxHttpHandler)
                                   .Create();

            using (ThreadPoolThrottle.Start())
            {
                // Create a sophon download speed limiter instance
                SophonDownloadSpeedLimiter downloadSpeedLimiter =
                    SophonDownloadSpeedLimiter.CreateInstance(LauncherConfig.DownloadSpeedLimitCached);

                // Reset status and progress properties
                ResetStatusAndProgress();

                // Set the progress bar to indetermined
                IsSophonInUpdateMode                 = false;
                Status.IsIncludePerFileIndicator     = false;
                Status.IsProgressPerFileIndetermined = true;
                Status.IsProgressAllIndetermined     = true;
                UpdateStatus();

                // Clear the VO language list
                _sophonVOLanguageList.Clear();

                // Subscribe the logger event if fallback is not used
                if (!fallbackFromUpdate)
                {
                    SophonLogger.LogHandler += UpdateSophonLogHandler;
                }

                // Get the requested URL and version based on current state.
                if (GameVersionManager?
                   .GamePreset
                   .LauncherResourceChunksURL != null)
                {
                    // Reassociate the URL if branch url exist
                    string? branchUrl = GameVersionManager
                                       .GamePreset
                                       .LauncherResourceChunksURL
                                       .BranchUrl;

                    if (!string.IsNullOrEmpty(branchUrl)
                        && !string.IsNullOrEmpty(GameVersionManager.GamePreset.LauncherBizName))
                    {
                        await GameVersionManager.GamePreset
                                                 .LauncherResourceChunksURL
                                                 .EnsureReassociated(httpClient,
                                                                     branchUrl,
                                                                     GameVersionManager.GamePreset.LauncherBizName,
                                                                     false,
                                                                     Token!.Token);
                    }

                #if SIMULATEAPPLYPRELOAD
                    string? requestedUrl = gameState switch
                    {
                        GameInstallStateEnum.InstalledHavePreload => GameVersionManager.GamePreset
                           .LauncherResourceChunksURL.PreloadUrl,
                        _ => GameVersionManager.GamePreset.LauncherResourceChunksURL.MainUrl
                    };
                    GameVersion? requestedVersion = gameState switch
                    {
                        GameInstallStateEnum.InstalledHavePreload => GameVersionManager!
                           .GetGameVersionApiPreload(),
                        _ => GameVersionManager!.GetGameVersionApiPreload()
                    } ?? GameVersionManager!.GetGameVersionApi();
                #else
                    string? requestedUrl = gameState switch
                                           {
                                               GameInstallStateEnum.InstalledHavePreload => GameVersionManager
                                                  .GamePreset
                                                  .LauncherResourceChunksURL.PreloadUrl,
                                               _ => GameVersionManager.GamePreset.LauncherResourceChunksURL.MainUrl
                                           };
                    GameVersion? requestedVersion = gameState switch
                                                    {
                                                        GameInstallStateEnum.InstalledHavePreload =>
                                                            GameVersionManager!
                                                               .GetGameVersionApiPreload(),
                                                        _ => GameVersionManager!.GetGameVersionApi()
                                                    } ?? GameVersionManager!.GetGameVersionApi();

                    // Add the tag query to the Url
                    requestedUrl += $"&tag={requestedVersion}";
#endif

                    try
                    {
                        // Initialize the info pair list
                        List<SophonChunkManifestInfoPair> sophonInfoPairList = [];

                        // Get the info pair based on info provided above (for main game file)
                        SophonChunkManifestInfoPair? sophonMainInfoPair = await
                            SophonManifest.CreateSophonChunkManifestInfoPair(httpClient,
                                                                             requestedUrl,
                                                                             GameVersionManager.GamePreset.LauncherResourceChunksURL.MainBranchMatchingField,
                                                                             Token!.Token);

                        // Ensure that the manifest is ordered based on _gameVoiceLanguageLocaleIdOrdered
                        RearrangeDataListLocaleOrder(sophonMainInfoPair.OtherSophonBuildData!.ManifestIdentityList,
                                                     x => x.MatchingField);

                        // Add the manifest to the pair list
                        sophonInfoPairList.Add(sophonMainInfoPair);

                        Dictionary<string, string> voLanguageDict =
                            GetSophonLanguageDisplayDictFromVoicePackList(sophonMainInfoPair.OtherSophonBuildData);

                        const string VOLanguageDefaultId = "ja-jp";

                        if (voLanguageDict.Count != 0)
                        {
                            // Now we only add VO list if the file actually exist.
                            // We won't bother any misconfiguration on user side anymore.
                            if (fallbackFromUpdate && File.Exists(_gameAudioLangListPathStatic))
                            {
                                string[] voLangList = await File.ReadAllLinesAsync(_gameAudioLangListPathStatic);
                                foreach (string voLang in voLangList)
                                {
                                    string? voLocaleId = GetLanguageLocaleCodeByLanguageString(
                                         voLang
                                     #if !DEBUG
                                        , false
                                     #endif
                                        );

                                    if (string.IsNullOrEmpty(voLocaleId))
                                    {
                                        continue;
                                    }

                                    _sophonVOLanguageList.Add(voLocaleId);
                                }

                                if (_sophonVOLanguageList.Count == 0)
                                {
                                    _sophonVOLanguageList.Add(VOLanguageDefaultId);
                                }
                            }
                            else
                            {
                                (HashSet<string>? addedVos, string? setAsDefaultVo) =
                                    await SimpleDialogs.Dialog_ChooseAudioLanguageChoice(voLanguageDict);

                                if (addedVos == null || setAsDefaultVo == null)
                                {
                                    throw new TaskCanceledException(); // Cancel entire operation
                                }

                                foreach (string addedVo in addedVos)
                                {
                                    _sophonVOLanguageList.Add(addedVo);
                                }

                                // Set the voice language ID to value given
                                GameVersionManager.GamePreset.SetVoiceLanguageID(setAsDefaultVo);
                            }
                        }

                        foreach (string sophonVoLangLocaleId in _sophonVOLanguageList)
                        {
                            // Get the info pair based on info provided above (for the selected VO audio file)
                            SophonChunkManifestInfoPair sophonSelectedVoLang =
                                sophonMainInfoPair.GetOtherManifestInfoPair(sophonVoLangLocaleId);
                            sophonInfoPairList.Add(sophonSelectedVoLang);
                        }

                        // If the fallback is used from update, use the same display as All Size for Per File progress.
                        if (fallbackFromUpdate)
                        {
                            ProgressPerFileSizeTotal = ProgressAllSizeTotal;
                        }

                        // Set the display to Install Mode
                        UpdateStatus();

                        // Get game install path and create directory if not exist
                        string gameInstallPath = GamePath;
                        if (!string.IsNullOrEmpty(gameInstallPath))
                        {
                            Directory.CreateDirectory(gameInstallPath);
                        }

                        // Get the list of the Sophon Assets first
                        List<SophonAsset> sophonAssetList = await GetSophonAssetListFromPair(
                            httpClient,
                            sophonInfoPairList,
                            downloadSpeedLimiter,
                            GameVersionManager.GamePreset.LauncherResourceChunksURL?.ExcludeMatchingFieldMain ?? [],
                            Token.Token);

                        // Get the remote total size and current total size
                        ProgressAllCountTotal  = sophonInfoPairList.Sum(x => x.ChunksInfo!.FilesCount);
                        ProgressAllSizeTotal   = sophonInfoPairList.Sum(x => x.ChunksInfo!.TotalSize);
                        ProgressAllSizeCurrent = 0;

                        // Check for the disk space requirement first and ensure that the space is sufficient
                        await EnsureDiskSpaceSufficiencyAsync(
                                                              ProgressAllSizeTotal,
                                                              gameInstallPath,
                                                              sophonAssetList,
                                                              async (sophonAsset, ctx) =>
                                                              {
                                                                  return await Task<long>.Factory.StartNew(() =>
                                                                      {
                                                                          // Get the file path and start the write process
                                                                          string   assetName      = sophonAsset.AssetName;
                                                                          string   assetFullPath  = Path.Combine(gameInstallPath, assetName);
                                                                          long     sophonAssetLen = sophonAsset.AssetSize;
                                                                          FileInfo filePath       = new FileInfo(assetFullPath + "_tempSophon");
                                                                          FileInfo origFilePath   = new FileInfo(assetFullPath);

                                                                          // If the original file path exist and the length is the same as the asset size
                                                                          // or if the temp file path exist and the length is the same as the asset size
                                                                          // (means the file has already been downloaded, then return sophonAssetLen)
                                                                          if ((origFilePath.Exists && origFilePath.Length == sophonAssetLen)
                                                                              || (filePath.Exists     && filePath.Length     == sophonAssetLen))
                                                                          {
                                                                              return sophonAssetLen;
                                                                          }
                            
                                                                          // If both orig and temp file don't exist or has different size, then return 0 as it doesn't exist
                                                                          return 0L;
                                                                      }, ctx,
                                                                      TaskCreationOptions.DenyChildAttach,
                                                                      TaskScheduler.Default);
                                                              }, Token.Token);

                        // Get the parallel options
                        var parallelOptions = new ParallelOptions
                        {
                            MaxDegreeOfParallelism = maxThread,
                            CancellationToken      = Token.Token
                        };
                        var parallelChunksOptions = new ParallelOptions
                        {
                            MaxDegreeOfParallelism = maxChunksThread,
                            CancellationToken      = Token.Token
                        };

                        // Set the progress bar to indetermined
                        Status.IsIncludePerFileIndicator     = false;
                        Status.IsProgressPerFileIndetermined = false;
                        Status.IsProgressAllIndetermined     = false;
                        UpdateStatus();

                        // Enumerate the asset in parallel and start the download process
                        await Parallel.ForEachAsync(sophonAssetList, parallelOptions, DelegateAssetDownload)
                                      .ConfigureAwait(false);

                        // Rename temporary files
                        await Parallel.ForEachAsync(sophonAssetList, parallelOptions, DelegateAssetRenameTempFile)
                                      .ConfigureAwait(false);

                        // Remove sophon verified files
                        CleanupTempSophonVerifiedFiles();

                        // ReSharper disable AccessToDisposedClosure
                        // Declare the download delegate
                        ValueTask DelegateAssetDownload(SophonAsset asset, CancellationToken _) =>
                            gameState == GameInstallStateEnum.NeedsUpdate
                                ? RunSophonAssetUpdateThread(httpClient, asset, parallelChunksOptions)
                                : RunSophonAssetDownloadThread(httpClient, asset, parallelChunksOptions);
                        // ReSharper enable AccessToDisposedClosure

                        // Declare the rename temp file delegate
                        async ValueTask DelegateAssetRenameTempFile(SophonAsset asset, CancellationToken token)
                        {
                            await Task.Run(() =>
                            {
                                // If the asset is a dictionary, then return
                                if (asset.IsDirectory)
                                {
                                    return;
                                }

                                // Throw if the token cancellation requested
                                token.ThrowIfCancellationRequested();

                                // Get the file path and start the write process
                                var assetName     = asset.AssetName;
                                var assetFullPath = Path.Combine(gameInstallPath, assetName);
                                var tempFilePath  = new FileInfo(assetFullPath + "_tempSophon")
                                                   .EnsureCreationOfDirectory()
                                                   .EnsureNoReadOnly(out bool isExistTemp);

                                // If the temp file is not exist, then return (ignore)
                                if (!isExistTemp)
                                {
                                    return;
                                }

                                // Get the original file path, ensure the existing file is not read only,
                                // then move the temp file to the original file path
                                var origFilePath  = new FileInfo(assetFullPath)
                                                   .EnsureCreationOfDirectory()
                                                   .StripAlternateDataStream()
                                                   .EnsureNoReadOnly();

                                // Move the thing
                                tempFilePath.MoveTo(origFilePath.FullName, true);
                                origFilePath.Refresh();
                            }, token);
                        }

                        _isSophonDownloadCompleted = true;
                    }
                    finally
                    {
                        // Unsubscribe the logger event if fallback is not used
                        if (!fallbackFromUpdate)
                        {
                            SophonLogger.LogHandler -= UpdateSophonLogHandler;
                        }
                        httpClient.Dispose();

                        // Unsubscribe download limiter
                        LauncherConfig.DownloadSpeedLimitChanged -= downloadSpeedLimiter.GetListener();
                    }
                }
            }
        }

        private async Task<List<SophonAsset>> GetSophonAssetListFromPair(
            HttpClient                        client,
            List<SophonChunkManifestInfoPair> sophonInfoPairs,
            SophonDownloadSpeedLimiter        downloadSpeedLimiter,
            string[]                          excludeMatchingFieldPatterns,
            CancellationToken                 token)
        {
            List<SophonAsset> sophonAssetList = [];

            await ConfirmAdditionalInstallDataPackageFiles(sophonInfoPairs, token);

            // Avoid duplicates by using HashSet of the url
            HashSet<string> currentlyProcessedPair = [];
            foreach (SophonChunkManifestInfoPair sophonDownloadInfoPair in sophonInfoPairs
                        .WhereMatchPattern(x => x.MatchingField,
                                           true,
                                           excludeMatchingFieldPatterns))
            {
                // Try add and if the hashset already contains the same Manifest ID registered, then skip
                if (!currentlyProcessedPair.Add(sophonDownloadInfoPair.ManifestInfo!.ManifestId))
                {
                    Logger.LogWriteLine($"Found duplicate operation for {sophonDownloadInfoPair.ManifestInfo.ManifestId}! Skipping...",
                                        LogType.Warning, true);
                    continue;
                }

                // Register to hashset to avoid duplication
                // Enumerate the pair to get the SophonAsset
                await foreach (SophonAsset sophonAsset in SophonManifest.EnumerateAsync(
                                client,
                                sophonDownloadInfoPair,
                                downloadSpeedLimiter,
                                token))
                {
                    // If the asset is a directory, skip
                    if (sophonAsset.IsDirectory)
                    {
                        continue;
                    }

                    sophonAssetList.Add(sophonAsset);
                }
            }

            // Return the list
            return sophonAssetList;
        }

        private async Task ConfirmAdditionalInstallDataPackageFiles(
            List<SophonChunkManifestInfoPair> installManifest,
            CancellationToken                 token)
        {
            string[] commonPackageMatchingFields = ["game", "en-us", "zh-tw", "zh-cn", "ko-kr", "ja-jp"];

            SophonChunkManifestInfoPair? installManifestFirst = installManifest.FirstOrDefault();
            if (installManifestFirst == null)
            {
                return;
            }

            List<string> matchingFieldsList = installManifest.Select(x => x.MatchingField).ToList()!;

            List<SophonManifestBuildIdentity> otherManifestIdentity = installManifestFirst.OtherSophonBuildData!.ManifestIdentityList
               .Where(x => !commonPackageMatchingFields.Contains(x.MatchingField, StringComparer.OrdinalIgnoreCase))
               .ToList();

            if (otherManifestIdentity.Count == 0)
            {
                return;
            }

            long sizeCurrentToDownload = installManifestFirst.OtherSophonBuildData.ManifestIdentityList
                                                             .Where(x => matchingFieldsList.Contains(x.MatchingField, StringComparer.OrdinalIgnoreCase))
                                                             .Sum(x =>
                                                                  {
                                                                      SophonManifestChunkInfo? firstTag = x.ChunkInfo;
                                                                      return firstTag?.CompressedSize ?? 0;
                                                                  });

            long sizeAdditionalToDownload = otherManifestIdentity
                                           .Sum(x => x.ChunkInfo.CompressedSize);

            if (AskAdditionalSophonPkg)
            {
                bool isDownloadAdditionalData = await SpawnAdditionalPackageDownloadDialog(sizeCurrentToDownload,
                                                                                           sizeAdditionalToDownload,
                                                                                           false,
                                                                                           GetFileDetails);

                if (!isDownloadAdditionalData)
                {
                    return;
                }
            }

            List<string> additionalMatchingFields = otherManifestIdentity.Select(x => x.MatchingField).ToList();

            installManifest.AddRange(additionalMatchingFields.Select(matchingField => installManifestFirst.GetOtherManifestInfoPair(matchingField)));
            return;

            string GetFileDetails()
            {
                string filePath = Path.GetTempFileName();
                filePath = Path.Combine(Path.GetDirectoryName(filePath) ?? "", Path.GetFileNameWithoutExtension(filePath) + ".log");

                long sizeUncompressed = 0;
                long sizeCompressed = 0;
                long fileCount = 0;
                long chunkCount = 0;

                // ReSharper disable once ConvertToUsingDeclaration
                using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    using StreamWriter writer = new StreamWriter(fileStream);
                    foreach (var field in otherManifestIdentity)
                    {
                        var fieldInfo = field.ChunkInfo;
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

                        sizeCompressed += fieldInfo.CompressedSize;
                        sizeUncompressed += fieldInfo.UncompressedSize;
                        fileCount += fieldInfo.FileCount;
                        chunkCount += fieldInfo.ChunkCount;
                    }

                    writer.WriteLine($"Total Patch Size to Download (Compressed): {ConverterTool.SummarizeSizeSimple(sizeCompressed)} ({sizeCompressed} bytes)");
                    writer.WriteLine($"Total Patch Size to Download (Uncompressed): {ConverterTool.SummarizeSizeSimple(sizeUncompressed)} ({sizeUncompressed} bytes)");
                    writer.WriteLine($"Total Update Chunk Count: {chunkCount}");
                    writer.WriteLine($"Total File Count: {fileCount}");
                }

                return filePath;
            }
        }

        protected virtual async Task StartPackageUpdateSophon(GameInstallStateEnum gameState, bool isPreloadMode)
        {
            if (!Enum.IsDefined(gameState))
            {
                throw new InvalidEnumArgumentException(nameof(gameState), (int)gameState, typeof(GameInstallStateEnum));
            }

            // Set the flag to false
            _isSophonDownloadCompleted = false;

            // Set the max thread and httpHandler based on settings
            int maxThread       = SophonGetThreadNum();
            int maxChunksThread = Math.Clamp(maxThread / 2, 2, 32);
            int maxHttpHandler  = Math.Max(maxThread, SophonGetHttpHandler());

            Logger.LogWriteLine($"Initializing Sophon Chunk update method with Main Thread: {maxThread}, Chunks Thread: {maxChunksThread} and Max HTTP handle: {maxHttpHandler}",
                                LogType.Default, true);

            // Initialize the HTTP client
            HttpClient httpClient = new HttpClientBuilder()
                                   .UseLauncherConfig(maxHttpHandler)
                                   .Create();

            try
            {
                // Reset status and progress properties
                ResetStatusAndProgress();

                // Set the progress bar to indetermined
                IsSophonInUpdateMode                 = !isPreloadMode;
                Status.IsIncludePerFileIndicator     = !isPreloadMode;
                Status.IsProgressPerFileIndetermined = true;
                Status.IsProgressAllIndetermined     = true;
                UpdateStatus();

                // Clear the VO language list
                _sophonVOLanguageList.Clear();

                // Subscribe the logger event
                SophonLogger.LogHandler += UpdateSophonLogHandler;

                // Try to alter method to use patch mode.
                // If it returns true, then return.
                if (await AlterStartPatchUpdateSophon(httpClient,
                                                      isPreloadMode,
                                                      maxThread,
                                                      maxChunksThread,
                                                      maxHttpHandler))
                {
                    // Indicate the download is completed (if it's a preload one)
                    _isSophonPreloadCompleted = isPreloadMode;
                    // Indicate the patch is completed
                    _isSophonDownloadCompleted = true;

                    return;
                }

                // Init asset list
                List<SophonAsset> sophonUpdateAssetList = [];

                // Get the previous version details of the preload or the recent update.
                GameVersion? requestedVersionFrom = GameVersionManager!.GetGameExistingVersion();
                if (GameVersionManager.GamePreset.LauncherResourceChunksURL != null)
                {
                    // Reassociate the URL if branch url exist
                    string? branchUrl = GameVersionManager.GamePreset
                                                           .LauncherResourceChunksURL
                                                           .BranchUrl;
                    if (!string.IsNullOrEmpty(branchUrl)
                        && !string.IsNullOrEmpty(GameVersionManager.GamePreset.LauncherBizName))
                    {
                        await GameVersionManager.GamePreset
                                                 .LauncherResourceChunksURL
                                                 .EnsureReassociated(httpClient,
                                                                     branchUrl,
                                                                     GameVersionManager.GamePreset.LauncherBizName,
                                                                     false,
                                                                     Token!.Token);
                    }

                    string? requestedBaseUrlFrom = isPreloadMode
                        ? GameVersionManager.GamePreset.LauncherResourceChunksURL.PreloadUrl
                        : GameVersionManager.GamePreset.LauncherResourceChunksURL.MainUrl;
#if SIMULATEAPPLYPRELOAD
                    string requestedBaseUrlTo = GameVersionManager.GamePreset.LauncherResourceChunksURL.PreloadUrl!;
#else
                    string requestedBaseUrlTo = requestedBaseUrlFrom!;
#endif
                    // Add the tag query to the previous version's Url
                    requestedBaseUrlFrom += $"&tag={requestedVersionFrom.ToString()}";

                    // Create a sophon download speed limiter instance
                    SophonDownloadSpeedLimiter downloadSpeedLimiter =
                        SophonDownloadSpeedLimiter.CreateInstance(LauncherConfig.DownloadSpeedLimitCached);

                    // Add base game diff data
                    bool isSuccess = await AddSophonDiffAssetsToList(
                        httpClient,
                        requestedBaseUrlFrom,
                        requestedBaseUrlTo,
                        sophonUpdateAssetList,
                        GameVersionManager.GamePreset.LauncherResourceChunksURL.MainBranchMatchingField,
                        false,
                        downloadSpeedLimiter);

                    // If it doesn't success to get the base diff, then fallback to actually download the whole game
                    if (!isSuccess)
                    {
                        Logger.LogWriteLine($"The current game version: {requestedVersionFrom.ToString()} is too obsolete and incremental update is unavailable. Falling back to full update", LogType.Warning, true);

                        // Spawn the confirmation dialog
                        ContentDialogResult fallbackResultConfirm = await SimpleDialogs.SpawnDialog(
                            Locale.Lang._Dialogs.SophonIncrementUpdateUnavailTitle,
                            new TextBlock
                            {
                                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
                            }
                            .AddTextBlockLine(string.Format(Locale.Lang._Dialogs.SophonIncrementUpdateUnavailSubtitle1, requestedVersionFrom.ToString()))
                            .AddTextBlockLine(Locale.Lang._Dialogs.SophonIncrementUpdateUnavailSubtitle2, Microsoft.UI.Text.FontWeights.Bold)
                            .AddTextBlockLine(Locale.Lang._Dialogs.SophonIncrementUpdateUnavailSubtitle3)
                            .AddTextBlockNewLine(2)
                            .AddTextBlockLine(
                                string.Format(Locale.Lang._Dialogs.SophonIncrementUpdateUnavailSubtitle4,
                                              Locale.Lang._Misc.YesContinue,
                                              Locale.Lang._Misc.NoCancel),
                                Microsoft.UI.Text.FontWeights.Bold,
                                10),
                            ParentUI,
                            Locale.Lang._Misc.NoCancel,
                            Locale.Lang._Misc.YesContinue,
                            defaultButton: ContentDialogButton.Primary,
                            dialogTheme: CustomControls.ContentDialogTheme.Warning);

                        // If cancelled, then throw
                        if (ContentDialogResult.Primary != fallbackResultConfirm)
                        {
                            throw new TaskCanceledException("The full update routine has been cancelled by the user");
                        }

                        // Otherwise, continue updating the entire game
                        gameState = GameInstallStateEnum.NotInstalled;
                        await StartPackageInstallSophon(gameState, true);
                        return;
                    }

                    string[] excludeMatchingFieldPatterns =
                        isPreloadMode
                            ? GameVersionManager.GamePreset.LauncherResourceChunksURL.ExcludeMatchingFieldPreload
                            : GameVersionManager.GamePreset.LauncherResourceChunksURL.ExcludeMatchingFieldUpdate;

                    // If the game has lang list path, then add it
                    if (_gameAudioLangListPath != null)
                    {
                        // Add existing voice-over diff data
                        await AddSophonAdditionalVODiffAssetsToList(httpClient,
                                                                    requestedBaseUrlFrom,
                                                                    requestedBaseUrlTo,
                                                                    sophonUpdateAssetList,
                                                                    excludeMatchingFieldPatterns,
                                                                    downloadSpeedLimiter);
                    }

                    await TryGetAdditionalPackageForSophonDiff(httpClient,
                                                               requestedBaseUrlFrom,
                                                               requestedBaseUrlTo,
                                                               GameVersionManager.GamePreset.LauncherResourceChunksURL.MainBranchMatchingField,
                                                               excludeMatchingFieldPatterns,
                                                               sophonUpdateAssetList,
                                                               downloadSpeedLimiter);
                }

                // Filter asset list
                await FilterSophonPatchAssetList(sophonUpdateAssetList, Token!.Token);

                // Get the remote chunk size
                ProgressPerFileSizeTotal   = sophonUpdateAssetList.GetCalculatedDiffSize(!isPreloadMode);
                ProgressPerFileSizeCurrent = 0;

                // Get the remote total size and current total size
                ProgressAllCountTotal = sophonUpdateAssetList.Count(x => !x.IsDirectory);
                ProgressAllSizeTotal = !isPreloadMode
                    ? sophonUpdateAssetList.Sum(x => x.AssetSize)
                    : ProgressPerFileSizeTotal;
                ProgressAllSizeCurrent = 0;

                // Get the parallel options
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxThread,
                    CancellationToken      = Token.Token
                };
                var parallelChunksOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxChunksThread,
                    CancellationToken      = Token.Token
                };

                // Get the update source and destination, also where the staging chunk files will be stored
                string chunkPath = _gameSophonChunkDir;
                string gamePath  = GamePath;

                // If the chunk directory is not exist, then create one.
                if (!string.IsNullOrEmpty(chunkPath))
                {
                    Directory.CreateDirectory(chunkPath);
                }

                bool canDeleteChunks          = _canDeleteZip;
                bool isSophonPreloadCompleted = _isSophonPreloadCompleted;

                // If preload completed and perf mode is on, use all CPU cores 
                if (isSophonPreloadCompleted && LauncherConfig.GetAppConfigValue("SophonPreloadApplyPerfMode").ToBool())
                {
                    maxThread       = Environment.ProcessorCount;
                    maxChunksThread = Math.Clamp(maxThread / 2, 2, 32);

                    parallelOptions = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = maxThread,
                        CancellationToken      = Token.Token
                    };
                    parallelChunksOptions = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = maxChunksThread,
                        CancellationToken      = Token.Token
                    };
                }

                // Test the disk space requirement first and ensure that the space is sufficient
                await EnsureDiskSpaceSufficiencyAsync(ProgressPerFileSizeTotal,
                                                      chunkPath,
                                                      sophonUpdateAssetList,
                                                      async (x, ctx) =>
                                                          await x.GetDownloadedPreloadSize(
                                                           chunkPath,
                                                           gamePath,
                                                           isPreloadMode,
                                                           ctx),
                                                      Token.Token);

                Status.IsProgressPerFileIndetermined = false;
                Status.IsProgressAllIndetermined     = false;
                Status.ActivityStatus = $"{(IsSophonInUpdateMode && !isPreloadMode
                    ? Locale.Lang._Misc.UpdatingAndApplying
                    : Locale.Lang._Misc.Downloading)}: {string.Format(Locale.Lang._Misc.PerFromTo, ProgressAllCountCurrent,
                                                                      ProgressAllCountTotal)}";
                UpdateStatus();

                // Set the delegate function for the download action
                async ValueTask Action(HttpClient localHttpClient, SophonAsset asset)
                {
                    if (isPreloadMode)
                    {
                        // If preload mode, then only download the chunks
                        await asset.DownloadDiffChunksAsync(localHttpClient, chunkPath, parallelChunksOptions,
                                                            UpdateSophonFileTotalProgress, null,
                                                            UpdateSophonDownloadStatus, isSophonPreloadCompleted);
                        return;
                    }

                    // Ensure to remove the read-only attribute
                    string currentAssetPath = Path.Combine(gamePath, asset.AssetName);
                    TryUnassignReadOnlyFileSingle(currentAssetPath);

                    // Otherwise, start the patching process
                    await asset.WriteUpdateAsync(localHttpClient, gamePath, gamePath, chunkPath, canDeleteChunks,
                                                 parallelChunksOptions, UpdateSophonFileTotalProgress,
                                                 UpdateSophonFileDownloadProgress, UpdateSophonDownloadStatus);
                }

                // Enumerate in parallel and process the assets
                await Parallel.ForEachAsync(sophonUpdateAssetList
                                           .Where(x => !x.IsDirectory)
                                           .ToHashSet(),
                                            parallelOptions,
                                            (asset, _) => Action(httpClient, asset));

                _isSophonPreloadCompleted = isPreloadMode;

                // If it's in update mode, then clean up the temp sophon verified files
                if (!isPreloadMode)
                {
                    CleanupTempSophonVerifiedFiles();
                }

                _isSophonDownloadCompleted = true;
            }
            finally
            {
                // Unsubscribe the logger event
                SophonLogger.LogHandler -= UpdateSophonLogHandler;
                httpClient.Dispose();

                // Check if the DXSETUP file is exist, then delete it.
                // The DXSETUP files causes some false positive detection of data modification
                // for some games (like Genshin, which causes 4302-x errors for some reason)
                string dxSetupDir = Path.Combine(GamePath, "DXSETUP");
                TryDeleteReadOnlyDir(dxSetupDir);
            }
        }

        protected virtual Task FilterSophonPatchAssetList(List<SophonAsset> itemList, CancellationToken token)
        {
            // NOP
            return Task.CompletedTask;
        }

        private ValueTask RunSophonAssetDownloadThread(HttpClient      client,
                                                       SophonAsset     asset,
                                                       ParallelOptions parallelOptions)
        {
            // If the asset is a dictionary, then return
            if (asset.IsDirectory)
            {
                return ValueTask.CompletedTask;
            }

            // Get the file path and start the write process
            string? assetName = asset.AssetName;
            string  filePath  = EnsureCreationOfDirectory(Path.Combine(GamePath, assetName));

            if (File.Exists(filePath + "_tempSophon")) // Fallback to legacy behaviour
            {
                return RunSophonAssetUpdateThread(client, asset, parallelOptions);
            }

            // HACK: To avoid user unable to continue the download due to executable being downloaded completely,
            //       append "_tempSophon" on it.
            string filename = Path.GetFileName(assetName);
            if (filename.StartsWith(GameVersionManager.GamePreset.GameExecutableName ?? "", StringComparison.OrdinalIgnoreCase))
            {
                filePath += "_tempSophon";
            }

            // Get the target and temp file info
            FileInfo existingFileInfo = new FileInfo(filePath)
                                       .EnsureCreationOfDirectory()
                                       .EnsureNoReadOnly();

            return asset.WriteToStreamAsync(client,
                                            assetSize => existingFileInfo.Open(new FileStreamOptions
                                            {
                                                Mode       = FileMode.OpenOrCreate,
                                                Access     = FileAccess.ReadWrite,
                                                Share      = FileShare.ReadWrite,
                                                BufferSize = assetSize.GetFileStreamBufferSize()
                                            }),
                                            parallelOptions,
                                            UpdateSophonFileTotalProgress,
                                            UpdateSophonFileDownloadProgress,
                                            UpdateSophonDownloadStatus
                                           );
        }

        private ValueTask RunSophonAssetUpdateThread(HttpClient      client,
                                                     SophonAsset     asset,
                                                     ParallelOptions parallelOptions)
        {
            // If the asset is a dictionary, then return
            if (asset.IsDirectory)
            {
                return ValueTask.CompletedTask;
            }

            // Get the file path and start the write process
            string? assetName = asset.AssetName;
            string  filePath  = EnsureCreationOfDirectory(Path.Combine(GamePath, assetName));

            // Get the target and temp file info
            FileInfo existingFileInfo =
                new FileInfo(filePath)
                   .EnsureCreationOfDirectory()
                   .EnsureNoReadOnly(out bool isExistingFileInfoExist);
            FileInfo sophonFileInfo =
                new FileInfo(filePath + "_tempSophon")
                   .EnsureCreationOfDirectory()
                   .EnsureNoReadOnly(out bool isSophonFileInfoExist);

            // Use "_tempSophon" if file is new or if "_tempSophon" file exist. Otherwise use original file if exist
            if (!isExistingFileInfoExist || isSophonFileInfoExist
                                         || (isExistingFileInfoExist && isSophonFileInfoExist))
            {
                filePath = sophonFileInfo.FullName;
            }

            // However if the file has already been existed and completely downloaded while _tempSophon is exist,
            // delete the _tempSophon one to avoid uncompleted files being applied instead.
            else if (isExistingFileInfoExist && existingFileInfo.Length == asset.AssetSize && isSophonFileInfoExist)
            {
                sophonFileInfo.Delete();
            }

            return asset.WriteToStreamAsync(client,
                                            assetSize => new FileStream(filePath,
                                                                        FileMode.OpenOrCreate,
                                                                        FileAccess.ReadWrite,
                                                                        FileShare.ReadWrite,
                                                                        assetSize.GetFileStreamBufferSize()),
                                            parallelOptions,
                                            UpdateSophonFileTotalProgress,
                                            UpdateSophonFileDownloadProgress,
                                            UpdateSophonDownloadStatus
                                           );
        }

        private static async Task EnsureDiskSpaceSufficiencyAsync<TFrom>(
            long                                      sizeToCompare,
            string                                    gamePath,
            List<TFrom>                               assetList,
            SignedValueTaskSelectorAsync<TFrom, long> sizeSelector,
            CancellationToken                         token)
        {
            // Get SIMD'ed total sizes
            long downloadedSize = await assetList.SumParallelAsync(async (x, ctx) => await sizeSelector(x, ctx),
                                                                   token);

            long sizeRemainedToDownload = sizeToCompare - downloadedSize;

            // Push log regarding size
            Logger.LogWriteLine($"Total free space required to download: {ConverterTool.SummarizeSizeSimple(sizeToCompare)}"
                                + $" and {ConverterTool.SummarizeSizeSimple(sizeRemainedToDownload)} remained to be downloaded.",
                                LogType.Default, true);

            // Get the information about the disk
            DriveInfo driveInfo = new DriveInfo(gamePath);

            // Push log regarding disk space
            Logger.LogWriteLine($"Total free space remained on disk: {driveInfo.Name}: {ConverterTool.SummarizeSizeSimple(driveInfo.TotalFreeSpace)}.",
                                LogType.Default, true);

            // If the space is insufficient, then show the dialog and throw
            if (sizeRemainedToDownload > driveInfo.TotalFreeSpace)
            {
                string errStr = $"Free Space on {driveInfo.Name} is not sufficient! " +
                                $"(Free space: {ConverterTool.SummarizeSizeSimple(driveInfo.TotalFreeSpace)}, Req. Space: {ConverterTool.SummarizeSizeSimple(sizeRemainedToDownload)} (Total: {ConverterTool.SummarizeSizeSimple(sizeToCompare)}), " +
                                $"Drive: {driveInfo.Name})";
                await SimpleDialogs.Dialog_InsufficientDriveSpace(driveInfo.TotalFreeSpace,
                                                                  sizeRemainedToDownload, driveInfo.Name);

                // Push log for the disk space error
                Logger.LogWriteLine(errStr, LogType.Error, true);
                throw new TaskCanceledException(errStr);
            }
        }

        #endregion

        #region Sophon Asset Package Methods
        private async Task TryGetAdditionalPackageForSophonDiff(HttpClient                 httpClient,
                                                                string                     requestedUrlFrom,
                                                                string                     requestedUrlTo,
                                                                string                     mainMatchingField,
                                                                string[]                   excludeMatchingFieldsPattern,
                                                                List<SophonAsset>          sophonPreloadAssetList,
                                                                SophonDownloadSpeedLimiter downloadSpeedLimiter)
        {
            SophonChunkManifestInfoPair manifestPair = await SophonManifest
               .CreateSophonChunkManifestInfoPair(httpClient, requestedUrlTo, mainMatchingField, false, Token!.Token);

            if (!manifestPair.IsFound)
            {
                return;
            }

            List<string> additionalPackageMatchingFields =
                manifestPair.OtherSophonBuildData!.ManifestIdentityList
                            .Where(x => !CommonSophonPackageMatchingFields.Contains(x.MatchingField, StringComparer.OrdinalIgnoreCase))
                            .Select(x => x.MatchingField)
                            .WhereMatchPattern(x => x, true, excludeMatchingFieldsPattern)
                            .ToList();

            if (additionalPackageMatchingFields.Count == 0)
            {
                return;
            }

            foreach (string matchingField in additionalPackageMatchingFields)
            {
                await AddSophonDiffAssetsToList(httpClient,
                                                requestedUrlFrom,
                                                requestedUrlTo,
                                                sophonPreloadAssetList,
                                                matchingField,
                                                true,
                                                downloadSpeedLimiter);
            }
        }

        private async Task<bool> AddSophonDiffAssetsToList(HttpClient                 httpClient,
                                                           string                     requestedUrlFrom,
                                                           string                     requestedUrlTo,
                                                           List<SophonAsset>          sophonPreloadAssetList,
                                                           string                     matchingField,
                                                           bool                       discardIfOldNotExist,
                                                           SophonDownloadSpeedLimiter downloadSpeedLimiter)
        {
            // Get the manifest pair for both previous (from) and next (to) version
            SophonChunkManifestInfoPair requestPairFrom = await SophonManifest
               .CreateSophonChunkManifestInfoPair(httpClient, requestedUrlFrom, matchingField, false, Token!.Token);
            SophonChunkManifestInfoPair requestPairTo = await SophonManifest
               .CreateSophonChunkManifestInfoPair(httpClient, requestedUrlTo, matchingField, false, Token.Token);

            // If the request pair source is not found, then return false
            if (!requestPairFrom.IsFound && !requestPairTo.IsFound)
            {
                Logger.LogWriteLine($"Sophon manifest for source via URL: {requestedUrlFrom} is not found! Return message: {requestPairFrom.ReturnMessage} ({requestPairFrom.ReturnCode})", LogType.Warning, true);
                if (!requestPairTo.IsFound)
                {
                    return false;
                }
                Logger.LogWriteLine($"The target Sophon manifest still exist. The assets from target's matching field: {matchingField} will be used instead!", LogType.Warning, true);
            }

            // If the request pair target is not found, then return false
            if (!requestPairTo.IsFound)
            {
                Logger.LogWriteLine($"Sophon manifest for target via URL: {requestedUrlTo} is not found! Return message: {requestPairTo.ReturnMessage} ({requestPairTo.ReturnCode})", LogType.Warning, true);
                return false;
            }

            RearrangeDataListLocaleOrder(requestPairTo.OtherSophonBuildData!.ManifestIdentityList,
                                         x => x.MatchingField);

            if (requestPairFrom.IsFound)
            {
                // Ensure that the manifest is ordered based on _gameVoiceLanguageLocaleIdOrdered
                RearrangeDataListLocaleOrder(requestPairFrom.OtherSophonBuildData!.ManifestIdentityList,
                                             x => x.MatchingField);

                // Add asset to the list
                await foreach (SophonAsset sophonAsset in SophonUpdate
                                                         .EnumerateUpdateAsync(httpClient, requestPairFrom, requestPairTo,
                                                                               false, downloadSpeedLimiter)
                                                         .WithCancellation(Token.Token))
                {
                    sophonPreloadAssetList.Add(sophonAsset);
                }
            }
            else
            {
                await foreach (SophonAsset sophonAsset in SophonManifest.EnumerateAsync(httpClient, requestPairTo, downloadSpeedLimiter, Token.Token))
                {
                    string filePath = Path.Combine(GamePath, sophonAsset.AssetName);
                    if (!File.Exists(filePath) && discardIfOldNotExist)
                    {
                        Logger.LogWriteLine($"Asset from matching field: {matchingField} is discarded: {sophonAsset.AssetName}", LogType.Warning, true);
                        continue;
                    }

                    sophonPreloadAssetList.Add(sophonAsset);
                }
            }

            // Return as success
            return true;
        }

        private async Task AddSophonAdditionalVODiffAssetsToList(
            HttpClient                 httpClient,
            string                     requestedUrlFrom,
            string                     requestedUrlTo,
            List<SophonAsset>          sophonPreloadAssetList,
            string[]                   excludeMatchingFieldsPattern,
            SophonDownloadSpeedLimiter downloadSpeedLimiter)
        {
            // Get the main VO language name from Id
            string mainLangId = GetLanguageLocaleCodeByID(_gameVoiceLanguageID);

            if (!excludeMatchingFieldsPattern.Any(x => Regex.IsMatch(mainLangId, x)))
            {
                // Get the manifest pair for both previous (from) and next (to) version for the main VO
                await AddSophonDiffAssetsToList(httpClient,
                                                requestedUrlFrom,
                                                requestedUrlTo,
                                                sophonPreloadAssetList,
                                                mainLangId,
                                                false,
                                                downloadSpeedLimiter);
            }

            // Check if the audio lang list file is exist, then try add others
            FileInfo fileInfo = new FileInfo(_gameAudioLangListPath).StripAlternateDataStream().EnsureNoReadOnly();
            if (fileInfo.Exists)
            {
                // Use stream reader to read the list one-by-one
                using StreamReader reader = new StreamReader(_gameAudioLangListPath);
                // Read until EOF
                while (await reader.ReadLineAsync() is { } line)
                {
                    // Get other lang Id, pass it and try add to the list
                    string? otherLangId = GetLanguageLocaleCodeByLanguageString(line
#if !DEBUG
                        , false
#endif
                        );

                    // Check if the voice pack is actually the same as default.
                    if (string.IsNullOrEmpty(otherLangId) ||
                        otherLangId.Equals(mainLangId, StringComparison.OrdinalIgnoreCase) ||
                        excludeMatchingFieldsPattern.Any(x => Regex.IsMatch(otherLangId, x)))
                    {
                        continue;
                    }

                    // Get the manifest pair for both previous (from) and next (to) version for other VOs
                    await AddSophonDiffAssetsToList(httpClient,
                                                    requestedUrlFrom,
                                                    requestedUrlTo,
                                                    sophonPreloadAssetList,
                                                    otherLangId,
                                                    false,
                                                    downloadSpeedLimiter);
                }
            }
        }

        #endregion

        #region Sophon Thread Related Methods

        protected virtual int SophonGetThreadNum()
        {
            // Get from config
            var n = LauncherConfig.GetAppConfigValue("SophonCpuThread").ToInt();
            if (n == 0) // If config is default "0", then use sqrt of thread number as safe number
            {
                n = (int)Math.Sqrt(Environment.ProcessorCount);
            }

            return Math.Clamp(n, 2, 64); // Clamp value to prevent errors
        }

        protected virtual int SophonGetHttpHandler()
        {
            var n = LauncherConfig.GetAppConfigValue("SophonHttpConnInt").ToInt();
            if (n == 0)
            {
                n = (int)Math.Sqrt(Environment.ProcessorCount) * 2;
            }

            return Math.Clamp(n, 4, 128);
        }

        #endregion

        #region Sophon Audio/Voice-Packs Locale Methods

        protected virtual int GetSophonLocaleCodeIndex(SophonManifestBuildData sophonData, string lookupName)
        {
            List<string> localeList = sophonData.ManifestIdentityList
                                                .Where(x => IsValidLocaleCode(x.MatchingField))
                                                .Select(x => x.MatchingField.ToLower())
                                                .ToList();

            int index = localeList.IndexOf(lookupName);
            return Math.Max(0, index);
        }

        protected virtual Dictionary<string, string> GetSophonLanguageDisplayDictFromVoicePackList(SophonManifestBuildData sophonData)
        {
            Dictionary<string, string> value = new(StringComparer.OrdinalIgnoreCase);
            foreach (SophonManifestBuildIdentity identity in sophonData.ManifestIdentityList)
            {
                // Check the lang ID and add the translation of the language to the list
                string localeCode = identity.MatchingField.ToLower();
                if (!IsValidLocaleCode(localeCode))
                {
                    continue;
                }

                string? languageDisplay = GetLanguageDisplayByLocaleCode(localeCode, false);
                if (string.IsNullOrEmpty(languageDisplay))
                {
                    continue;
                }

                value.Add(localeCode, languageDisplay);
            }

            return value;
        }

        protected virtual void WriteAudioLangListSophon(ICollection<string> sophonVOList)
        {
            // Create persistent directory if not exist
            if (!Directory.Exists(_gameDataPersistentPath))
            {
                Directory.CreateDirectory(_gameDataPersistentPath);
            }

            // If the game does not have audio lang list, then return
            if (string.IsNullOrEmpty(_gameAudioLangListPathStatic))
            {
                return;
            }

            // Read all the existing list
            List<string> langList = File.Exists(_gameAudioLangListPathStatic)
                ? File.ReadAllLines(_gameAudioLangListPathStatic).ToList()
                : [];

            // Try lookup if there is a new language list, then add it to the list
            foreach (string packageLocaleCodeString in sophonVOList)
            {
                string langString = GetLanguageStringByLocaleCode(packageLocaleCodeString);
                if (!langList.Contains(langString, StringComparer.OrdinalIgnoreCase))
                {
                    langList.Add(langString);
                }
            }

            // Create the audio lang list file
            using StreamWriter sw = new StreamWriter(_gameAudioLangListPathStatic,
                                                     new FileStreamOptions
                                                         { Mode = FileMode.Create, Access = FileAccess.Write });
            // Iterate the package list
            foreach (string voIds in langList)
                // Write the language string as per ID
            {
                sw.WriteLine(voIds);
            }
        }

        #endregion
    }
}