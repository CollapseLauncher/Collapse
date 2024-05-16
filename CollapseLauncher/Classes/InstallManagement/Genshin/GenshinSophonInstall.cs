using CollapseLauncher.Dialogs;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Sophon;
using Hi3Helper.Sophon.Infos;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Logger = Hi3Helper.Logger;
using SophonLogger = Hi3Helper.Sophon.Logger;

namespace CollapseLauncher.InstallManager.Genshin
{
    internal class GenshinSophonInstall : GenshinInstall
    {
        #region Properties
        private List<string> SophonVOLanguageList { get; set; } = new List<string>();
        private bool IsDownloadCompleted = false;
        #endregion

        public GenshinSophonInstall(UIElement parentUI, IGameVersionCheck GameVersionManager)
            : base(parentUI, GameVersionManager)
        {

        }

        public override async Task StartPackageDownload(bool skipDialog)
        {
            // Set the flag to false
            IsDownloadCompleted = false;

            // Set the max thread and httpHandler based on settings
            int maxThread = Math.Max((int)_threadCount, 8);
            int maxHttpHandler = Math.Min(_downloadThreadCount * 16, 128);

            Logger.LogWriteLine($"Initializing Sophon Chunk download method with Thread: {maxThread} and Max HTTP handle: {maxHttpHandler}", LogType.Default, true);

            // Initialize the HTTP client
            HttpClientHandler httpClientHandler = new HttpClientHandler
            {
                MaxConnectionsPerServer = maxHttpHandler
            };
            HttpClient httpClient = new HttpClient(httpClientHandler)
            {
                DefaultRequestVersion = HttpVersion.Version30,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
            };

            try
            {
                // Reset status and progress properties
                ResetStatusAndProgressProperty();

                // Clear the VO language list
                SophonVOLanguageList?.Clear();

                // Subscribe the logger event
                SophonLogger.LogHandler += SophonLogger_LogHandler;

                // Get the requested URL and version based on current state.
                GameInstallStateEnum gameState = _gameVersionManager!.GetGameState();
                string requestedUrl = gameState switch
                {
                    GameInstallStateEnum.InstalledHavePreload => _gameVersionManager.GamePreset.LauncherResourceChunksURL.PreloadUrl,
                    _ => _gameVersionManager.GamePreset.LauncherResourceChunksURL.MainUrl
                };
                GameVersion requestedVersion = gameState switch
                {
                    GameInstallStateEnum.InstalledHavePreload => _gameVersionManager!.GetGameVersionAPIPreload(),
                    _ => _gameVersionManager!.GetGameVersionAPI()
                } ?? _gameVersionManager!.GetGameVersionAPI();

                // Add the tag query to the Url
                requestedUrl += $"&tag={requestedVersion.ToString()}";

                // Set the progress bar to indetermined
                _status!.IsIncludePerFileIndicator = false;
                _status!.IsProgressPerFileIndetermined = false;
                _status!.IsProgressTotalIndetermined = true;
                UpdateStatus();

                // Initialize the info pair list
                List<SophonChunkManifestInfoPair> sophonInfoPairList = new List<SophonChunkManifestInfoPair>();

                // Get the info pair based on info provided above (for main game file)
                SophonChunkManifestInfoPair sophonMainInfoPair = await SophonManifest
                    .CreateSophonChunkManifestInfoPair(httpClient, requestedUrl, "game", _token.Token);
                sophonInfoPairList.Add(sophonMainInfoPair);

                // Run the audio dialog question
                List<string> voLanguageList = GetAudioLanguageStringList(sophonMainInfoPair.OtherSophonData);
                int voLangIndex = await SimpleDialogs.Dialog_ChooseAudioLanguage(_parentUI, voLanguageList);
                string voLangLocaleCode = GetLanguageLocaleCodeByID(voLangIndex);
                SophonVOLanguageList.Add(voLangLocaleCode);

                // Get the info pair based on info provided above (for the selected VO audio file)
                SophonChunkManifestInfoPair sophonSelectedVoLang = sophonMainInfoPair.GetOtherManifestInfoPair(voLangLocaleCode);
                sophonInfoPairList.Add(sophonSelectedVoLang);

                // Get the remote total size and current total size
                _progressTotalCount = sophonInfoPairList.Sum(x => x.ChunksInfo.FilesCount);
                _progressTotalSize = sophonInfoPairList.Sum(x => x.ChunksInfo.TotalSize);
                _progressTotalSizeCurrent = 0;

                // Get the parallel options
                ParallelOptions parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxThread,
                    CancellationToken = _token.Token
                };

                // Start over the stopwatch
                RestartStopwatch();

                // Set the progress bar to indetermined
                _status!.IsIncludePerFileIndicator = false;
                _status!.IsProgressPerFileIndetermined = false;
                _status!.IsProgressTotalIndetermined = false;
                UpdateStatus();

                // Enumerate the asset in parallel and start the download process
                foreach (SophonChunkManifestInfoPair sophonDownloadInfoPair in sophonInfoPairList)
                {
                    await Parallel.ForEachAsync(
                        SophonManifest.EnumerateAsync(httpClient, sophonDownloadInfoPair),
                        parallelOptions,
                        async (asset, threadToken) => await RunSophonAssetDownloadThread(httpClient, asset, parallelOptions));
                }

                IsDownloadCompleted = true;
            }
            finally
            {
                // Unsubscribe the logger event
                SophonLogger.LogHandler -= SophonLogger_LogHandler;

                httpClientHandler.Dispose();
                httpClient.Dispose();
            }
        }

        private List<string> GetAudioLanguageStringList(SophonData sophonData)
        {
            List<string> value = new List<string>();
            foreach (SophonManifestIdentity Entry in sophonData.ManifestIdentityList)
            {
                // Check the lang ID and add the translation of the language to the list
                switch (Entry.MatchingField.ToLower())
                {
                    case "en-us":
                        value.Add(Locale.Lang._Misc.LangNameENUS);
                        break;
                    case "ja-jp":
                        value.Add(Locale.Lang._Misc.LangNameJP);
                        break;
                    case "zh-cn":
                        value.Add(Locale.Lang._Misc.LangNameCN);
                        break;
                    case "ko-kr":
                        value.Add(Locale.Lang._Misc.LangNameKR);
                        break;
                }
            }
            return value;
        }

        private void SophonLogger_LogHandler(object sender, LogStruct e)
        {
            if (e.LogLevel == LogLevel.Debug) return;
            (bool isNeedWriteLog, LogType logType) logPair = e.LogLevel switch
            {
                LogLevel.Warning => (true, LogType.Warning),
                LogLevel.Debug => (false, LogType.Debug),
                LogLevel.Error => (true, LogType.Error),
                _ => (true, LogType.Default)
            };
            Logger.LogWriteLine(e.Message, logPair.logType, logPair.isNeedWriteLog);
        }

        private async ValueTask RunSophonAssetDownloadThread(HttpClient client, SophonAsset asset, ParallelOptions parallelOptions)
        {
            // If the asset is a dictionary, then return
            if (asset.IsDirectory) return;

            // Get the file path and start the write process
            string filePath = EnsureCreationOfDirectory(Path.Combine(_gamePath, asset.AssetName));
            await asset.WriteToStreamAsync(
                client,
                () => new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite),
                parallelOptions,
                UpdateSophonDownloadProgress,
                UpdateSophonDownloadStatus
                );
        }

        private async void UpdateSophonDownloadProgress(long read)
        {
            Interlocked.Add(ref _progressTotalSizeCurrent, read);
            _progressTotalReadCurrent += read;

            if (await base.CheckIfNeedRefreshStopwatch())
            {
                // Assign local sizes to progress
                _progress.ProgressTotalDownload = _progressTotalSizeCurrent;
                _progress.ProgressTotalSizeToDownload = _progressTotalSize;

                // Calculate the speed
                _progress.ProgressTotalSpeed = _progressTotalSizeCurrent / _stopwatch.Elapsed.TotalSeconds;

                // Calculate percentage
                _progress.ProgressTotalPercentage = Math.Round(((double)_progressTotalSizeCurrent / _progressTotalSize) * 100, 2);
                // Calculate the timelapse
                _progress.ProgressTotalTimeLeft = ((_progressTotalSize - _progressTotalSizeCurrent) / ConverterTool.Unzeroed(_progress.ProgressTotalSpeed)).ToTimeSpanNormalized();

                UpdateProgress();
            }
        }

        private void UpdateSophonDownloadStatus(SophonAsset asset)
        {
            Interlocked.Add(ref _progressTotalCountCurrent, 1);
            _status.ActivityStatus = string.Format("{0}: {1}", Locale.Lang!._Misc!.Downloading, string.Format(Locale.Lang._Misc.PerFromTo!, _progressTotalCountCurrent, _progressTotalCount));
            UpdateStatus();
        }

        protected override async Task StartPackageInstallationInner(List<GameInstallPackage> gamePackage = null, bool isOnlyInstallPackage = false, bool doNotDeleteZipExplicit = false)
        {
            // await base.StartPackageInstallationInner(gamePackage, isOnlyInstallPackage, doNotDeleteZipExplicit);
            return;
        }

        public override async ValueTask<int> StartPackageVerification(List<GameInstallPackage> gamePackage)
        {
            // We are going to override the verification method from base class. So in order to bypass the loop,
            // we need to ensure if the IsDownloadCompleted is true. If so, set it to false and return 1 as successful.
            // Otherwise, return 0 as continue the installation.
            if (IsDownloadCompleted)
            {
                IsDownloadCompleted = false;
                return 1;
            }
            return 0;
        }

        protected override void WriteAudioLangList()
        {
            // Create persistent directory if not exist
            if (!Directory.Exists(_gameDataPersistentPath))
            {
                Directory.CreateDirectory(_gameDataPersistentPath);
            }

            // Create the audio lang list file
            using (StreamWriter sw = new StreamWriter(_gameAudioLangListPathStatic,
                new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write }))
            {
                // Iterate the package list
                foreach (string voIds in SophonVOLanguageList)
                {
                    // Write the language string as per ID
                    sw.WriteLine(GetLanguageStringByLocaleCode(voIds));
                }
            }
        }
    }
}
