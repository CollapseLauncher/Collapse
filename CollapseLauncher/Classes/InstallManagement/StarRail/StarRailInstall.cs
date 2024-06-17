using CollapseLauncher.GameVersioning;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static Hi3Helper.Logger;
using static Hi3Helper.Locale;
using System.Reflection.Metadata;

namespace CollapseLauncher.InstallManager.StarRail
{
    internal class StarRailInstall : InstallManagerBase<GameTypeStarRailVersion>, IGameInstallManager
    {
        #region Override Properties
        protected override int _gameVoiceLanguageID { get => _gameVersionManager.GamePreset.GetVoiceLanguageID(); }
        protected override bool _canDeltaPatch { get => _gameVersionManager.IsGameHasDeltaPatch(); }
        protected override DeltaPatchProperty _gameDeltaPatchProperty { get => _gameVersionManager.GetDeltaPatchInfo(); }
        #endregion

        #region Properties
        private string _execName { get => Path.GetFileNameWithoutExtension(_gameVersionManager.GamePreset.GameExecutableName); }
        protected override string _gameDataPersistentPath { get => Path.Combine(_gamePath, $"{_execName}_Data", "Persistent"); }
        protected override string _gameAudioLangListPath
        {
            get
            {
                // If the persistent folder is not exist, then return null
                if (!Directory.Exists(_gameDataPersistentPath)) return null;

                // Set the file list path
                string audioRecordPath = Path.Combine(_gameDataPersistentPath, "AudioLaucherRecord.txt");

                // Check if the file exist. If not, return null
                if (!File.Exists(audioRecordPath)) return null;

                // If it exists, then return the path
                return audioRecordPath;
            }
        }
        protected override string _gameAudioLangListPathStatic { get => Path.Combine(_gameDataPersistentPath, "AudioLaucherRecord.txt"); }
        private StarRailRepair _gameRepairManager { get; set; }
        #endregion

        public StarRailInstall(UIElement parentUI, IGameVersionCheck GameVersionManager)
            : base(parentUI, GameVersionManager) { }

        #region Public Methods
        public override async ValueTask<int> StartPackageVerification(List<GameInstallPackage> gamePackage)
        {
            IsRunning = true;

            // Get the delta patch confirmation if the property is not null
            if (_gameDeltaPatchProperty != null)
            {
                // If the confirm is 1 (verified) or -1 (cancelled), then return the code
                int deltaPatchConfirm = await ConfirmDeltaPatchDialog(_gameDeltaPatchProperty, _gameRepairManager = new StarRailRepair(_parentUI, _gameVersionManager, true, _gameDeltaPatchProperty.SourceVer));
                if (deltaPatchConfirm == -1 || deltaPatchConfirm == 1) return deltaPatchConfirm;
            }

            // If no delta patch is happening as deltaPatchConfirm returns 0 (normal update), then do the base verification
            return await base.StartPackageVerification(gamePackage);
        }

        protected override async Task StartPackageInstallationInner(List<GameInstallPackage> gamePackage = null, bool isOnlyInstallPackage = false, bool doNotDeleteZipExplicit = false)
        {
            // If the delta patch is performed, then return
            if (!isOnlyInstallPackage && await StartDeltaPatch(_gameRepairManager, false, true))
            {
                // Update the audio package list after delta patch has been initiated
                WriteAudioLangList(_gameDeltaPatchPreReqList);
                return;
            }

            // Run the base installation process
            await base.StartPackageInstallationInner(gamePackage, isOnlyInstallPackage, doNotDeleteZipExplicit);

            // Then start on processing hdifffiles list and deletefiles list
            await ApplyHdiffListPatch();
            ApplyDeleteFileAction();

            // Update the audio lang list if not in isOnlyInstallPackage mode
            if (!isOnlyInstallPackage)
                WriteAudioLangList(_assetIndex);
        }

        protected override string GetLanguageStringByID(int id) => id switch
        {
            0 => "Chinese",
            1 => "Chinese",
            2 => "English(US)",
            3 => "Japanese",
            4 => "Korean",
            _ => throw new KeyNotFoundException($"ID: {id} is not supported!")
        };
        #endregion

        #region Override Methods - UninstallGame
        protected override UninstallGameProperty AssignUninstallFolders() => new()
        {
            gameDataFolderName = "StarRail_Data",
            foldersToDelete = new[] { "AntiCheatExpert" },
            filesToDelete = new[] { "ACE-BASE.sys", "GameAssembly.dll", "pkg_version", "config.ini", "^StarRail.*", "^Unity.*" },
            foldersToKeepInData = new[] { "ScreenShots" }
        };
        #endregion

        #region Override Methods - Others
        protected override string GetLanguageLocaleCodeByID(int id) => id switch
        {
            0 => "zh-cn",
            1 => "zh-cn",
            2 => "en-us",
            3 => "ja-jp",
            4 => "ko-kr",
            _ => base.GetLanguageLocaleCodeByID(id)
        };

        protected override int GetIDByLanguageLocaleCode(string localeCode) => localeCode switch
        {
            "zh-cn" => 0,
            "zh-tw" => 1,
            "en-us" => 2,
            "ja-jp" => 3,
            "ko-kr" => 4,
            _ => base.GetIDByLanguageLocaleCode(localeCode)
        };

        protected override string GetLanguageDisplayByLocaleCode(string localeCode) => localeCode switch
        {
            "zh-cn" => Lang._Misc.LangNameCN,
            "zh-tw" => null, // ^-> use the same one as per referred by the API
            "en-us" => Lang._Misc.LangNameENUS,
            "ko-kr" => Lang._Misc.LangNameKR,
            "ja-jp" => Lang._Misc.LangNameJP,
            _ => base.GetLanguageDisplayByLocaleCode(localeCode)
        };
        #endregion
    }
}
