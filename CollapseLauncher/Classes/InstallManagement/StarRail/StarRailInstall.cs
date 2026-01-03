using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using Hi3Helper.Data;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable GrammarMistakeInComment
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace
// ReSharper disable ArrangeObjectCreationWhenTypeEvident
// ReSharper disable EmptyRegion
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor

namespace CollapseLauncher.InstallManager.StarRail
{
    internal sealed partial class StarRailInstall : InstallManagerBase
    {
        #region Override Properties

        protected override int _gameVoiceLanguageID => GameVersionManager.GamePreset.GetVoiceLanguageID();
        protected override bool _canDeltaPatch => GameVersionManager.IsGameHasDeltaPatch();
        protected override DeltaPatchProperty _gameDeltaPatchProperty => GameVersionManager.GetDeltaPatchInfo();

        #endregion

        #region Properties

        private string _execName => Path.GetFileNameWithoutExtension(GameVersionManager.GamePreset.GameExecutableName);
        protected override string _gameDataPersistentPath => Path.Combine(GamePath, $"{_execName}_Data", "Persistent");

        protected override string _gameAudioLangListPath
        {
            get
            {
                // If the persistent folder is not exist, then return null
                if (!Directory.Exists(_gameDataPersistentPath))
                {
                    return null;
                }

                // Set the file list path
                string audioRecordPath = Path.Combine(_gameDataPersistentPath, "AudioLaucherRecord.txt");

                // Check if the file exist. If not, return null
                return !File.Exists(audioRecordPath) ? null :
                    // If it exists, then return the path
                    audioRecordPath;
            }
        }

        protected override string _gameAudioLangListPathStatic =>
            Path.Combine(_gameDataPersistentPath, "AudioLaucherRecord.txt");

        private StarRailRepairV2 _gameRepairManager { get; set; }

        #endregion

        public StarRailInstall(UIElement parentUI, IGameVersion gameVersionManager, IGameSettings gameSettings)
            : base(parentUI, gameVersionManager, gameSettings)
        {
        }

        #region Public Methods

        public override async ValueTask<int> StartPackageVerification(List<GameInstallPackage> gamePackage)
        {
            IsRunning = true;

            // Get the delta patch confirmation if the property is not null
            if (_gameDeltaPatchProperty == null)
                return await base.StartPackageVerification(gamePackage);

            // If the confirm is 1 (verified) or -1 (cancelled), then return the code
            int deltaPatchConfirm = await ConfirmDeltaPatchDialog(_gameDeltaPatchProperty,
                                                                  _gameRepairManager = GetGameRepairInstance(_gameDeltaPatchProperty.SourceVer));
            if (deltaPatchConfirm is -1 or 1)
            {
                return deltaPatchConfirm;
            }

            // If no delta patch is happening as deltaPatchConfirm returns 0 (normal update), then do the base verification
            return await base.StartPackageVerification(gamePackage);
        }

#nullable enable
        protected override StarRailRepairV2 GetGameRepairInstance(string? versionString) =>
            new StarRailRepairV2(ParentUI,
                    GameVersionManager,
                    GameSettings,
                    true,
                    versionString);
#nullable restore

        protected override async Task StartPackageInstallationInner(List<GameInstallPackage> gamePackage = null,
                                                                    bool isOnlyInstallPackage = false,
                                                                    bool doNotDeleteZipExplicit = false)
        {
            // If the delta patch is performed, then return
            if (!isOnlyInstallPackage && await StartDeltaPatch(_gameRepairManager, false, true))
            {
                // Assign the game package to delta-patch requirement list
                // and start the additional patching process (like Audio patch, etc)
                gamePackage ??= _gameDeltaPatchPreReqList;
            }

            // Run the base installation process
            await base.StartPackageInstallationInner(gamePackage, isOnlyInstallPackage, doNotDeleteZipExplicit);

            // Then start on processing hdifffiles list and deletefiles list
            await ApplyHdiffListPatch();
            await ApplyDeleteFileActionAsync(Token!.Token);

            // Update the audio lang list if not in isOnlyInstallPackage mode
            if (!isOnlyInstallPackage)
            {
                WriteAudioLangList(AssetIndex);
            }
        }

        protected override string GetLanguageStringByID(int id)
        {
            return id switch
                   {
                       0 => "Chinese",
                       1 => "Chinese",
                       2 => "English(US)",
                       3 => "Japanese",
                       4 => "Korean",
                       _ => throw new KeyNotFoundException($"ID: {id} is not supported!")
                   };
        }

        #endregion

        #region Override Methods - UninstallGame

        protected override GameInstallFileInfo GetGameInstallFileInfo()
        {
            if (GameVersionManager.GamePreset.GameInstallFileInfo is { } gameInstallFileInfo)
            {
                return gameInstallFileInfo;
            }

            return new GameInstallFileInfo
            {
                GameDataFolderName = "StarRail_Data",
                FoldersToDelete    = ["AntiCheatExpert"],
                FilesToDelete =
                [
                    "ACE-BASE.sys", "GameAssembly.dll", "pkg_version", "config.ini", "^StarRail.*", "^Unity.*"
                ],
                FoldersToKeepInData = ["ScreenShots"]
            };
        }

        #endregion

        #region Override Method - InnerParsePkgVersion2FileInfo
#nullable enable
        protected override async ValueTask InnerParsePkgVersion2FileInfo(string gamePath, string path,
                                                                         List<LocalFileInfo> pkgFileInfo,
                                                                         HashSet<string> pkgFileInfoHashSet,
                                                                         CancellationToken token)
        {
            // Assign path to reader
            using StreamReader reader = new StreamReader(path, true);
            // Do loop until EOF
            while (await reader.ReadLineAsync(token) is {} line)
            {
                // Read line and deserialize
                LocalFileInfo? localFileInfo = line.Deserialize(LocalFileInfoJsonContext.Default.LocalFileInfo);

                // Assign the values
                if (localFileInfo == null)
                    continue;

                localFileInfo.FullPath = Path.Combine(gamePath, localFileInfo.RelativePath);
                localFileInfo.FileName = Path.GetFileName(localFileInfo.RelativePath);
                localFileInfo.IsFileExist = File.Exists(localFileInfo.FullPath);

                // Add it to the list and hashset
                pkgFileInfo.Add(localFileInfo);
                pkgFileInfoHashSet.Add(localFileInfo.RelativePath);

                // If it's an audio file, then add the mark file into the entry as well.
                // This to avoid the mark file to be tagged as "Unused" (as per issue #672)
                if (localFileInfo.RelativePath.EndsWith(".pck"))
                {
                    // Get the string of the mark hex hash
                    string? markHashString = HexTool.BytesToHexUnsafe(localFileInfo.MD5Hash);
                    if (string.IsNullOrEmpty(markHashString))
                    {
                        continue;
                    }

                    // Get the mark file's relative path
                    string relativePathDir = Path.GetDirectoryName(localFileInfo.RelativePath) ?? "";
                    string relativePathNameNoExt = Path.GetFileNameWithoutExtension(localFileInfo.RelativePath);
                    string relativePathMarkMerged = Path.Combine(relativePathDir, relativePathNameNoExt + $"_{markHashString}.hash");

                    // Create the LocalFileInfo instance of the mark file
                    LocalFileInfo localFileInfoMark = new LocalFileInfo
                    {
                        FullPath = Path.Combine(gamePath, relativePathMarkMerged),
                        RelativePath = relativePathMarkMerged,
                        FileName = Path.GetFileName(relativePathMarkMerged)
                    };
                    localFileInfoMark.IsFileExist = File.Exists(localFileInfoMark.FullPath);

                    // Add the mark file entry into the list and hashset
                    pkgFileInfo.Add(localFileInfoMark);
                    pkgFileInfoHashSet.Add(localFileInfoMark.RelativePath);
                }
            }
        }
#nullable restore
        #endregion

        #region Override Methods - Others

        protected override string GetLanguageLocaleCodeByID(int id)
        {
            return id switch
                   {
                       0 => "zh-cn",
                       1 => "zh-cn",
                       2 => "en-us",
                       3 => "ja-jp",
                       4 => "ko-kr",
                       _ => base.GetLanguageLocaleCodeByID(id)
                   };
        }

        protected override int GetIDByLanguageLocaleCode(string localeCode)
        {
            return localeCode switch
                   {
                       "zh-cn" => 0,
                       "zh-tw" => 1,
                       "en-us" => 2,
                       "ja-jp" => 3,
                       "ko-kr" => 4,
                       _ => base.GetIDByLanguageLocaleCode(localeCode)
                   };
        }

        protected override string GetLanguageDisplayByLocaleCode(string localeCode, bool throwIfInvalid = true)
        {
            return localeCode switch
                   {
                       "zh-cn" => Lang._Misc.LangNameCN,
                       "zh-tw" => null, // ^-> use the same one as per referred by the API
                       "en-us" => Lang._Misc.LangNameENUS,
                       "ko-kr" => Lang._Misc.LangNameKR,
                       "ja-jp" => Lang._Misc.LangNameJP,
                       _ => base.GetLanguageDisplayByLocaleCode(localeCode, throwIfInvalid)
                   };
        }

        #endregion
    }
}