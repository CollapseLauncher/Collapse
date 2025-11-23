using CollapseLauncher.Helper;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.EncTool.Parser.SimpleZipArchiveReader;
using Hi3Helper.SentryHelper;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Hi3Helper.Logger;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable BaseMethodCallWithDefaultParameter
// ReSharper disable GrammarMistakeInComment
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace
// ReSharper disable ArrangeObjectCreationWhenTypeEvident
// ReSharper disable EmptyRegion
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable IdentifierTypo

namespace CollapseLauncher.InstallManager.Genshin
{
    internal sealed partial class GenshinInstall : InstallManagerBase
    {
        #region Override Properties
        protected override int _gameVoiceLanguageID => GameVersionManager.GamePreset.GetVoiceLanguageID();
        #endregion

        #region Properties
        protected override string _gameAudioLangListPath
        {
            get
            {
                // If the persistent folder is not exist, then return null
                if (!Directory.Exists(_gameDataPersistentPath))
                {
                    return null;
                }

                // Try get the file list
                string[] audioPath =
                    Directory.GetFiles(_gameDataPersistentPath, "audio_lang_*", SearchOption.TopDirectoryOnly);
                // If the path is null or has no length, then return null
                return audioPath.Length == 0 ? null :
                    // If not, then return the first path
                    audioPath[0];
            }
        }

        protected override string _gameAudioLangListPathStatic =>
            Path.Combine(_gameDataPersistentPath, "audio_lang_14");

        private string _gameAudioNewPath => Path.Combine(_gameDataPath, "StreamingAssets", "AudioAssets");

        private string _gameAudioOldPath =>
            Path.Combine(_gameDataPath, "StreamingAssets", "Audio", "GeneratedSoundBanks", "Windows");

        private GenshinRepair Repair { get; set; }

        #endregion

        public GenshinInstall(
            UIElement parentUI,
            IGameVersion gameVersionManager,
            IGameSettings gameSettings,
            IRepair gameRepair)
            : base(parentUI,
                  gameVersionManager,
                  gameSettings)
        {
            Repair = gameRepair as GenshinRepair ?? throw new InvalidOperationException("The type of game repair must be GenshinRepair!");
        }

        #region Override Methods - StartPackageInstallationInner

        protected override async Task StartPackageInstallationInner(List<GameInstallPackage> gamePackage = null,
                                                                    bool isOnlyInstallPackage = false,
                                                                    bool doNotDeleteZipExplicit = false)
        {
            if (!isOnlyInstallPackage)
                // Starting from 3.6 update, the Audio files have been moved to "AudioAssets" folder
            {
                EnsureMoveOldToNewAudioDirectory();
            }

            // Run the base installation process
            await base.StartPackageInstallationInner(gamePackage);

            // Then start on processing hdifffiles list and deletefiles list
            await ApplyHdiffListPatch();
            await ApplyDeleteFileActionAsync(Token.Token);
        }

        private void EnsureMoveOldToNewAudioDirectory()
        {
            // Return if the old path doesn't exist
            if (!Directory.Exists(_gameAudioOldPath))
            {
                return;
            }

            // If it exists, then enumerate the content of it and do move operation
            int offset = _gameAudioOldPath.Length + 1;
            foreach (string oldPath in Directory.EnumerateFiles(_gameAudioOldPath, "*", SearchOption.AllDirectories))
            {
                string basePath  = oldPath.AsSpan()[offset..].ToString();
                string newPath   = EnsureCreationOfDirectory(Path.Combine(_gameAudioNewPath, basePath));

                FileInfo oldFileInfo = new FileInfo(oldPath)
                {
                    IsReadOnly = false
                };
                oldFileInfo.MoveTo(newPath, true);
            }

            try
            {
                // Then if all the files are already moved, delete the old path
                if (Directory.Exists(_gameAudioOldPath))
                {
                    Directory.CreateDirectory(_gameAudioOldPath);
                }
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Failed while deleting old audio folder: {_gameAudioOldPath}\r\n{ex}", LogType.Error,
                             true);
            }
        }

        #endregion

        #region Override Methods - UninstallGame

        protected override GameInstallFileInfo GetGameInstallFileInfo()
        {
            if (GameVersionManager.GamePreset.GameInstallFileInfo is { } gameInstallFileInfo)
            {
                return gameInstallFileInfo;
            }

            string execName = GameVersionManager.GamePreset.ZoneName switch
            {
                "Global" => "GenshinImpact",
                "Mainland China" => "YuanShen",
                "Bilibili" => "YuanShen",
                "Google Play" => "GenshinImpact",
                _ => throw new NotSupportedException($"Unknown GI Game Region!: {GameVersionManager.GamePreset.ZoneName}")
            };

            return new GameInstallFileInfo
            {
                GameDataFolderName = $"{execName}_Data",
                FoldersToDelete    = [$"{execName}_Data"],
                FilesToDelete =
                [
                    "HoYoKProtect.sys", "pkg_version", $"{execName}.exe", "UnityPlayer.dll", "config.ini", "^mhyp.*",
                    "^Audio.*"
                ],
                FoldersToKeepInData = ["ScreenShot"]
            };
        }

        #endregion

        #region Override Methods - GetUnusedFileInfoList
        protected override async Task<(List<LocalFileInfo>, long)> GetUnusedFileInfoList(bool includeZipCheck)
        {
            string gamePath = GamePath;
            string gameBiz  = GameVersionManager.GameBiz;
            string gameId   = GameVersionManager.GameId;

            // Preallocate list and start fetch from Repair
            List<LocalFileInfo> localFileInfos = new(16 << 10);
            List<PkgVersionProperties> assets = await Repair.ResetAndFetchAssets();

            // Convert
            localFileInfos.AddRange(assets.Select(ConvertPkgVersion));

            // Add assets from Plugin
            if (GameVersionManager.LauncherApi?.LauncherGameResourcePlugin is {} pluginApi &&
                (pluginApi.Data?.TryFindByBizOrId(gameBiz, gameId, out HypResourcePluginData pluginData) ?? false) &&
                pluginData.Plugins.Count > 0)
            {
                localFileInfos.AddRange(pluginData
                                       .Plugins
                                       .SelectMany(x => x.PluginPackage?.PackageAssetValidationList ?? [])
                                       .Select(ConvertHypData));
            }

            // Add assets from SDK
            if (GameVersionManager.LauncherApi?.LauncherGameResourceSdk is { } sdkApi &&
                (sdkApi.Data?.TryFindByBizOrId(gameBiz, gameId, out HypChannelSdkData sdkData) ?? false) &&
                sdkData.SdkPackageDetail?.Url is { } sdkZipUrl)
            {
                ZipArchiveReader reader = await ZipArchiveReader.CreateFromRemoteAsync(sdkZipUrl);
                localFileInfos.AddRange(reader
                                       .Entries
                                       .Where(x => !x.IsDirectory)
                                       .Select(ConvertZipEntry));
            }

            // Add assets from WPF
            if (GameVersionManager.LauncherApi?.LauncherGameResourceWpf is { } wpfApi &&
                (wpfApi.Data?.TryFindByBizOrId(gameBiz, gameId, out HypWpfPackageData wpfData) ?? false) &&
                wpfData.PackageInfo?.Url is { } wpfZipUrl)
            {
                ZipArchiveReader reader = await ZipArchiveReader.CreateFromRemoteAsync(wpfZipUrl);
                localFileInfos.AddRange(reader
                                       .Entries
                                       .Where(x => !x.IsDirectory)
                                       .Select(ConvertZipEntry));
            }

            // Convert to Hash Set and clear the list for unused files later
            Dictionary<string, LocalFileInfo> hashSet = localFileInfos.ToDictionary(x => x.FullPath);
            localFileInfos.Clear();

            // Get ignore list
            GameInstallFileInfo gameInstallFileInfo = GetGameInstallFileInfo();
            List<string>        regexMatchList      = gameInstallFileInfo.FilesCleanupIgnoreList.ToList();

            // Add ignore list for pkg_version files
            string audioInstalledList = _gameAudioLangListPathStatic;
            if (File.Exists(audioInstalledList))
            {
                using StreamReader audioInstalledListReader = File.OpenText(audioInstalledList);
                while (await audioInstalledListReader.ReadLineAsync() is { } audioInstalledEntry)
                {
                    string match = $"Audio_{audioInstalledEntry}_pkg_version";
                    regexMatchList.Add($"^{Regex.Escape(match)}$");
                }
            }

            // Compares with existing file
            foreach (LocalFileInfo fileInfo in new DirectoryInfo(gamePath)
                                              .EnumerateFiles("*", SearchOption.AllDirectories)
                                              .Select(x => new LocalFileInfo(x, gamePath))
                                              .WhereMatchPattern(x => x.RelativePath, true, regexMatchList))
            {
                if (hashSet.ContainsKey(fileInfo.FullPath))
                {
                    continue;
                }

                localFileInfos.Add(fileInfo);
            }

            long localFileSizes = localFileInfos.Sum(x => x.FileSize);
            return (localFileInfos, localFileSizes);

            LocalFileInfo ConvertZipEntry(SimpleZipArchiveEntry  asset) => new LocalFileInfo(asset, gamePath);
            LocalFileInfo ConvertPkgVersion(PkgVersionProperties asset) => new LocalFileInfo(asset, gamePath);
            LocalFileInfo ConvertHypData(HypPackageData          asset) => new LocalFileInfo(asset, gamePath);
        }
        #endregion
    }
}