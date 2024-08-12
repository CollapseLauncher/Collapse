using CollapseLauncher.GameSettings.Zenless;
using CollapseLauncher.GameSettings.Zenless.Enums;
using CollapseLauncher.GameVersioning;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

// ReSharper disable GrammarMistakeInComment
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace
// ReSharper disable ArrangeObjectCreationWhenTypeEvident
// ReSharper disable EmptyRegion
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor

#nullable enable
namespace CollapseLauncher.InstallManager.Zenless
{
    internal class ZenlessInstall : InstallManagerBase<GameTypeZenlessVersion>
    {
        #region Private Properties
        private ZenlessSettings? ZenlessSettings { get; set; }
        #endregion

        #region Override Properties
        protected override int _gameVoiceLanguageID
        {
            get => ZenlessSettings?.GeneralData?.DeviceLanguageVoiceType switch
            {
                LanguageVoice.zh_cn => 0,
                LanguageVoice.en_us => 1,
                LanguageVoice.ja_jp => 2,
                LanguageVoice.ko_kr => 3,
                LanguageVoice.Unset => 2,  // Default to ja_jp
                _ => throw new NotSupportedException("Type of the language voice type is not valid!")
            };
        }

        protected override string? _gameAudioLangListPath
        {
            get
            {
                // If the persistent folder is not exist, then return null
                if (!Directory.Exists(_gameDataPersistentPath))
                    return null;

                // Get the audio lang path index
                string audioLangPath = _gameAudioLangListPathStatic;
                return File.Exists(audioLangPath) ? audioLangPath : null;
            }
        }

        private            string? _gameAudioLangListPathAlternate
        {
            get
            {
                // If the persistent folder is not exist, then return null
                if (!Directory.Exists(_gameDataPersistentPath))
                    return null;

                // Get the audio lang path index
                string audioLangPath = _gameAudioLangListPathAlternateStatic;
                return File.Exists(audioLangPath) ? audioLangPath : null;
            }
        }

        protected override string _gameAudioLangListPathStatic =>
            Path.Combine(_gameDataPersistentPath, "audio_lang_launcher");
        private            string _gameAudioLangListPathAlternateStatic =>
            Path.Combine(_gameDataPersistentPath, "audio_lang");
        #endregion

        public ZenlessInstall(UIElement parentUI, IGameVersionCheck GameVersionManager, ZenlessSettings zenlessSettings)
            : base(parentUI, GameVersionManager)
        {
            ZenlessSettings = zenlessSettings;
        }

        #region Override Methods - StartPackageInstallationInner
        protected override async Task StartPackageInstallationInner(List<GameInstallPackage>? gamePackage = null,
                                                                    bool isOnlyInstallPackage = false,
                                                                    bool doNotDeleteZipExplicit = false)
        {
            // Run the base installation process
            await base.StartPackageInstallationInner(gamePackage, isOnlyInstallPackage, doNotDeleteZipExplicit);

            // Then start on processing hdifffiles list and deletefiles list
            await ApplyHdiffListPatch();
            ApplyDeleteFileAction();

            // Update the audio lang list if not in isOnlyInstallPackage mode
            if (!isOnlyInstallPackage)
            {
                WriteAudioLangList(_assetIndex);
            }
        }
        #endregion

        #region Override Methods - Audio Lang List
        protected override void WriteAudioLangList(List<GameInstallPackage> gamePackage)
        {
            // Run the writing method from the base first
            base.WriteAudioLangList(gamePackage);

            // Then create the one from the alternate one
            // Read all the existing list
            List<string> langList = File.Exists(_gameAudioLangListPathAlternateStatic)
                ? File.ReadAllLines(_gameAudioLangListPathAlternateStatic).ToList()
                : [];

            // Try lookup if there is a new language list, then add it to the list
            foreach (GameInstallPackage package in
                     _assetIndex.Where(x => x.PackageType == GameInstallPackageType.Audio))
            {
                string langString = GetLanguageStringByLocaleCodeAlternate(package.LanguageID);
                if (!langList.Contains(langString, StringComparer.OrdinalIgnoreCase))
                {
                    langList.Add(langString);
                }
            }

            // Create the audio lang list file
            using StreamWriter sw = new StreamWriter(_gameAudioLangListPathAlternateStatic,
                                                     new FileStreamOptions
                                                     { Mode = FileMode.Create, Access = FileAccess.Write });
            // Iterate the package list
            foreach (string langString in langList)
            {
                // Write the language string as per ID
                sw.WriteLine(langString);
            }
        }

        protected override void WriteAudioLangListSophon(List<string> sophonVOList)
        {
            // Run the writing method from the base first
            base.WriteAudioLangListSophon(sophonVOList);

            // Then create the one from the alternate one
            // Read all the existing list
            List<string> langList = File.Exists(_gameAudioLangListPathAlternateStatic)
                ? File.ReadAllLines(_gameAudioLangListPathAlternateStatic).ToList()
                : [];

            // Try lookup if there is a new language list, then add it to the list
            for (int index = 0; index < sophonVOList.Count; index++)
            {
                var packageLocaleCodeString = sophonVOList[index];
                string langString = GetLanguageStringByLocaleCodeAlternate(packageLocaleCodeString);
                if (!langList.Contains(langString, StringComparer.OrdinalIgnoreCase))
                {
                    langList.Add(langString);
                }
            }

            // Create the audio lang list file
            using var sw = new StreamWriter(_gameAudioLangListPathAlternateStatic,
                                            new FileStreamOptions
                                            { Mode = FileMode.Create, Access = FileAccess.Write });
            // Iterate the package list
            foreach (var voIds in langList)
            // Write the language string as per ID
            {
                sw.WriteLine(voIds);
            }
        }

        private string GetLanguageStringByLocaleCodeAlternate(string localeCode)
        {
            return localeCode switch
            {
                "zh-cn" => "Cn",
                "en-us" => "En",
                "ja-jp" => "Jp",
                "ko-kr" => "Kr",
                _ => throw new KeyNotFoundException($"Alternate locale code: {localeCode} is not supported!")
            };
        }
        #endregion

        #region Override Methods - UninstallGame
        protected override UninstallGameProperty AssignUninstallFolders() => new UninstallGameProperty()
        {
            gameDataFolderName = "ZenlessZoneZero_Data",
            foldersToDelete = new string[] { "APMCrashReporter" },
            filesToDelete = new string[] { "mhypbase.dll", "HoYoKProtect.sys", "GameAssembly.dll", "pkg_version", "config.ini", "^ZZZ.*", "^Unity.*" },
            foldersToKeepInData = new string[] { "ScreenShots" }
        };
        #endregion
    }
}
#nullable restore