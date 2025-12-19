using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable AccessToDisposedClosure
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace
// ReSharper disable MemberCanBeProtected.Global

#nullable enable
namespace CollapseLauncher.GameManagement.Versioning
{
    internal partial class GameVersionBase
    {
        #region Protected Methods
        protected virtual bool IsExecutableFileExist(string? executableName)
        {
            string fullPath = Path.Combine(GameDirPath, executableName ?? "");
            return File.Exists(fullPath);
        }

        protected virtual bool IsGameDataDirExist(string? executableName)
        {
            executableName = Path.GetFileNameWithoutExtension(executableName);
            string fullPath = Path.Combine(GameDirPath, $"{executableName}_Data");
            return Directory.Exists(fullPath);
        }

        protected virtual bool IsGameVendorValid(string? executableName)
        {
            executableName = Path.GetFileNameWithoutExtension(executableName);
            string appInfoFilePath = Path.Combine(GameDirPath, $"{executableName}_Data", "app.info");
            if (!File.Exists(appInfoFilePath)) return false;

            string[] strings = File.ReadAllLines(appInfoFilePath);
            if (strings.Length != 2) return false;

            string metaGameVendor = GamePreset.VendorType.ToString();
            string? metaGameName = GamePreset.InternalGameNameInConfig;

            return metaGameVendor.Equals(strings[0])
                   && (metaGameName?.Equals(strings[1]) ?? false);
        }

        protected virtual bool IsGameConfigIdValid()
        {
            const string channelIdKeyName = "channel";
            const string subChannelIdKeyName = "sub_channel";
            const string cpsKeyName = "cps";

            if (string.IsNullOrEmpty(GamePreset.LauncherCPSType)
            || GamePreset.ChannelID == null
            || GamePreset.SubChannelID == null)
                return true;

            bool isContainsChannelId = GameIniVersion[DefaultIniVersionSection].ContainsKey(channelIdKeyName);
            bool isContainsSubChannelId = GameIniVersion[DefaultIniVersionSection].ContainsKey(subChannelIdKeyName);
            bool isCps = GameIniVersion[DefaultIniVersionSection].ContainsKey(cpsKeyName);
            if (!isContainsChannelId
             || !isContainsSubChannelId
             || !isCps)
                return false;

            string? channelIdCurrent = GameIniVersion[DefaultIniVersionSection][channelIdKeyName];
            string? subChannelIdCurrent = GameIniVersion[DefaultIniVersionSection][subChannelIdKeyName];
            string? cpsCurrent = GameIniVersion[DefaultIniVersionSection][cpsKeyName];

            if (!int.TryParse(channelIdCurrent, null, out int channelIdCurrentInt)
             || !int.TryParse(subChannelIdCurrent, null, out int subChannelIdCurrentInt)
             || string.IsNullOrEmpty(cpsCurrent))
                return false;

            return !(channelIdCurrentInt != GamePreset.ChannelID
             || subChannelIdCurrentInt != GamePreset.SubChannelID
             || !cpsCurrent.Equals(GamePreset.LauncherCPSType));
        }

        protected virtual bool IsGameExecDataDirValid(string? executableName) => true; // Always true for games other than Genshin Impact

        protected virtual bool IsGameHasBilibiliStatus(string? executableName)
        {
            bool isBilibili = GamePreset.LauncherCPSType?
                .IndexOf("bilibili", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isBilibili)
                return true;

            executableName = Path.GetFileNameWithoutExtension(executableName);
            string sdkDllPath = Path.Combine(GameDirPath, $"{executableName}_Data", "Plugins", "PCGameSDK.dll");

            return !File.Exists(sdkDllPath);
        }
        #endregion

        #region Check Game "Has" State
        public virtual bool IsGameHasPreload()
        {
            if (GamePreset.LauncherType == LauncherType.Sophon)
                return GameDataSophonBranchPreload != null;

            return GameDataPackagePreload is { CurrentVersion: not null };
        }

        public virtual bool IsGameHasDeltaPatch() => false;

        public virtual bool IsGameVersionMatch()
            // Ensure if the GameVersionInstalled is available (this is coming from the Game Profile's Ini file).
            // If not, then return false to indicate that the game isn't installed.
            // If the game is installed and the version doesn't match, then return to false.
            // But if the game version matches, then return to true.
            => GameVersionInstalled == GameVersionAPI;

        public virtual Task<bool> IsPluginVersionsMatch()
        {
#if !MHYPLUGINSUPPORT
            return true;
#else
            // Get the pluginVersions and installedPluginVersions
            Dictionary<string, GameVersion> pluginVersions          = PluginVersionsAPI;
            Dictionary<string, GameVersion> pluginVersionsInstalled = PluginVersionsInstalled;

            // If the plugin version installed is null, return true as it doesn't need to check
            if (pluginVersions.Count == 0)
            {
                return Task.FromResult(true);
            }

            // Check each of the version
            MismatchPlugin.Clear();
            foreach (KeyValuePair<string, GameVersion> pluginVersion in pluginVersions)
            {
                if (pluginVersionsInstalled.TryGetValue(pluginVersion.Key, out GameVersion installedPluginVersion) &&
                    pluginVersion.Value == installedPluginVersion)
                {
                    continue;
                }

                // Uh-oh, we need to calculate the file hash.
                MismatchPlugin = CheckPluginUpdate(pluginVersion.Key);
            }

            // Return based on how many mismatched plugin found
            return Task.FromResult(MismatchPlugin.Count == 0);
#endif
        }

        public virtual async Task<bool> IsSdkVersionsMatch()
        {
#if !MHYPLUGINSUPPORT
            return true;
#else
            // Get the pluginVersions and installedPluginVersions
            GameVersion? sdkVersion          = SdkVersionAPI;
            GameVersion? installedSdkVersion = SdkVersionInstalled;

            // If the SDK API has no value, return true
            if (!installedSdkVersion.HasValue && !sdkVersion.HasValue)
                return true;

            // If the SDK Resource is null, return true
            if (GetGameSdkZip().FirstOrDefault() is not {} sdk)
                return true;

            // If the installed SDK returns empty (null), return false
            if (!installedSdkVersion.HasValue)
                return false;

            // Compare the version and the current SDK state if the indicator file is exist
            string validatePath        = Path.Combine(GameDirPath, sdk.PkgVersionFileName ?? string.Empty);
            bool   isVersionEqual      = installedSdkVersion.Equals(sdkVersion);
            bool   isValidatePathExist = File.Exists(validatePath);
            bool   isPkgVersionMatch   = isValidatePathExist && await CheckSdkUpdate(validatePath);
            bool   isSdkInstalled      = isVersionEqual && isPkgVersionMatch;

            return isSdkInstalled;
#endif
        }

        public virtual bool IsGameInstalled() => IsGameInstalledInner(GamePreset.GameExecutableName);

        protected virtual bool IsGameInstalledInner([NotNullWhen(true)] string? executableName)
        {
            // If the executable name is null or empty, return false
            if (string.IsNullOrEmpty(executableName))
            {
                return false;
            }

            // If the GameVersionInstalled doesn't have a value (not null), then return as false.
            if (!GameVersionInstalled.HasValue)
            {
                return false;
            }

            // Check if the executable file exist and has the size at least > 64 KiB. If not, then return as false.
            FileInfo execFileInfo = new(Path.Combine(GameDirPath, executableName));

            // Check if the vendor type exist. If not, then return false
            if (VendorTypeProp.GameName == null || string.IsNullOrEmpty(VendorTypeProp.VendorType))
            {
                return false;
            }

            // Check all the pattern and return based on the condition
            return VendorTypeProp.GameName == GamePreset.InternalGameNameInConfig && execFileInfo is { Exists: true, Length: > 1 << 16 };
        }

        protected virtual bool IsTryParseIniVersionExist(string iniPath)
        {
            // If the file doesn't exist, return false by default
            if (!File.Exists(iniPath))
            {
                return false;
            }

            // Load version config file.
            IniFile iniFile = IniFile.LoadFrom(iniPath);

            // Check whether the config has game_version value, and it must be a non-null value.
            if (!iniFile[DefaultIniVersionSection].TryGetValue("game_version", out IniValue gameVersion))
            {
                return false;
            }

            string? val = gameVersion;
            // If above doesn't pass, then return false.
            return !string.IsNullOrEmpty(val);
        }

        protected virtual bool IsDiskPartitionExist(string? path)
        {
            try
            {
                var pathRoot = Path.GetPathRoot(path);
                return !string.IsNullOrEmpty(pathRoot) && Directory.Exists(pathRoot);
                       // Return from Directory.Exists() since the IsReady property use the same method.
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion

        #region Check Game State
        public async ValueTask<GameInstallStateEnum> GetGameState()
        {
            // Check if the game installed first
            // If the game is installed, then move to another step.
            if (!IsGameInstalled())
            {
                return GameInstallStateEnum.NotInstalled;
            }

            // If the game version is not match, return need update.
            // Otherwise, move to the next step.
            if (!IsGameVersionMatch())
            {
                return GameInstallStateEnum.NeedsUpdate;
            }

            // Check for the game/plugin version and preload availability.
            if (IsGameHasPreload())
            {
                return GameInstallStateEnum.InstalledHavePreload;
            }

            // If the plugin version is not match, return that it's installed but have plugin updates.
            Task<bool> pluginMatchTask      = IsPluginVersionsMatch();
            Task<bool> sdkMatchTask         = IsSdkVersionsMatch();
            bool[]     isBothDepTaskMatches = await Task.WhenAll(pluginMatchTask, sdkMatchTask);
            if (!isBothDepTaskMatches.All(x => x))
            {
                return GameInstallStateEnum.InstalledHavePlugin;
            }

            // If all passes, then return as Installed.
            return GameInstallStateEnum.Installed;
        }

        public virtual async ValueTask<bool> EnsureGameConfigIniCorrectiveness(UIElement uiParentElement)
        {
            string? execName = GamePreset.GameExecutableName;
            bool isExecExist = IsExecutableFileExist(execName);
            bool isGameDataDirExist = IsGameDataDirExist(execName);
            bool isGameVendorValid = IsGameVendorValid(execName);
            bool isGameConfigIdValid = IsGameConfigIdValid();
            bool isGameExecDataDirValid = IsGameExecDataDirValid(execName);
            bool isGameHasBilibiliStatus = IsGameHasBilibiliStatus(execName);

            // If the game exist but has invalid state, then ask for the dialog
            if ((!isExecExist || !isGameDataDirExist || isGameVendorValid)
                && isGameExecDataDirValid
                && isGameConfigIdValid
                && isGameHasBilibiliStatus)
            {
                return true;
            }

            string? translatedGameTitle =
                InnerLauncherConfig.GetGameTitleRegionTranslationString(GamePreset.GameName,
                                                                        Locale.Lang._GameClientTitles);
            string? translatedGameRegion =
                InnerLauncherConfig.GetGameTitleRegionTranslationString(GamePreset.ZoneName,
                                                                        Locale.Lang._GameClientRegions);
            string gameNameTranslated = $"{translatedGameTitle} - {translatedGameRegion}";

            TextBlock textBlock = new TextBlock { TextAlignment = TextAlignment.Left, TextWrapping = TextWrapping.WrapWholeWords }
                                 .AddTextBlockLine(Locale.Lang._HomePage.GameStateInvalid_Subtitle1, true)
                                 .AddTextBlockLine(gameNameTranslated, FontWeights.Bold).AddTextBlockNewLine(2)
                                 .AddTextBlockLine(Locale.Lang._HomePage.GameStateInvalid_Subtitle2).AddTextBlockNewLine(2)
                                 .AddTextBlockLine(Locale.Lang._HomePage.GameStateInvalid_Subtitle3)
                                 .AddTextBlockLine(Locale.Lang._Misc.YesContinue, FontWeights.SemiBold)
                                 .AddTextBlockLine(Locale.Lang._HomePage.GameStateInvalid_Subtitle4)
                                 .AddTextBlockLine(Locale.Lang._Misc.NoCancel, FontWeights.SemiBold)
                                 .AddTextBlockLine(Locale.Lang._HomePage.GameStateInvalid_Subtitle5);

            ContentDialogResult dialogResult = await SimpleDialogs.SpawnDialog(
                                                                               Locale.Lang._HomePage.GameStateInvalid_Title,
                                                                               textBlock,
                                                                               uiParentElement,
                                                                               Locale.Lang._Misc.NoCancel,
                                                                               Locale.Lang._Misc.YesContinue,
                                                                               null,
                                                                               ContentDialogButton.Primary,
                                                                               ContentDialogTheme.Error
                                                                              );

            if (dialogResult == ContentDialogResult.None) return true;

            // Perform the fix
            FixInvalidGameExecDataDir(execName);
            FixInvalidGameVendor(execName);
            FixInvalidGameConfigId();
            FixInvalidGameBilibiliStatus(execName);
            InitializeIniDefaults(GameIniVersion, DefaultIniVersion, DefaultIniVersionSection, false);
            Reinitialize();

            textBlock = new TextBlock { TextAlignment = TextAlignment.Left, TextWrapping = TextWrapping.WrapWholeWords }
                       .AddTextBlockLine(Locale.Lang._HomePage.GameStateInvalidFixed_Subtitle1, true)
                       .AddTextBlockLine(gameNameTranslated, FontWeights.Bold).AddTextBlockNewLine(2)
                       .AddTextBlockLine(Locale.Lang._HomePage.GameStateInvalidFixed_Subtitle2)
                       .AddTextBlockLine(Locale.Lang._GameRepairPage.RepairBtn2Full, FontWeights.Bold)
                       .AddTextBlockLine(Locale.Lang._HomePage.GameStateInvalidFixed_Subtitle3)
                       .AddTextBlockLine(Locale.Lang._GameRepairPage.PageTitle, FontWeights.Bold)
                       .AddTextBlockLine(Locale.Lang._HomePage.GameStateInvalidFixed_Subtitle4);

            _ = await SimpleDialogs.SpawnDialog(
                                                Locale.Lang._HomePage.GameStateInvalidFixed_Title,
                                                textBlock,
                                                uiParentElement,
                                                Locale.Lang._Misc.OkayHappy,
                                                null,
                                                null,
                                                ContentDialogButton.Close,
                                                ContentDialogTheme.Success
                                               );

            return false;
        }

        public virtual ValueTask<bool> CheckSdkUpdate(string validatePath)
        {
            try
            {
                if (!File.Exists(validatePath))
                {
                    return ValueTask.FromResult(false);
                }

                long totalSize    = 0;
                long existingSize = 0;

                string gamePath = GameDirPath;
                foreach (PkgVersionProperties pkg in EnumerateList())
                {
                    Interlocked.Add(ref totalSize, pkg.fileSize);

                    string   filePath = Path.Combine(gamePath, pkg.remoteName ?? "");
                    FileInfo fileInfo = new(filePath);
                    if (fileInfo.Exists)
                    {
                        Interlocked.Add(ref existingSize, fileInfo.Length);
                    }
                }

                return ValueTask.FromResult(totalSize == existingSize);

                IEnumerable<PkgVersionProperties> EnumerateList()
                {
                    using StreamReader reader = File.OpenText(validatePath);
                    while (reader.ReadLine() is { } line)
                    {
                        PkgVersionProperties? pkgVersion = line.Deserialize(CoreLibraryJsonContext.Default.PkgVersionProperties);
                        if (pkgVersion == null)
                        {
                            continue;
                        }

                        yield return pkgVersion;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return ValueTask.FromResult(false);
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"Failed while checking the SDK file update\r\n{ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                return ValueTask.FromResult(true);
            }
        }

        protected virtual List<HypPluginPackageInfo> CheckPluginUpdate(string pluginKey)
        {
            List<HypPluginPackageInfo> result = [];
            if (GameDataPlugin?.Plugins == null ||
                GameDataPlugin.Plugins.Count == 0)
            {
                return result;
            }

            List<HypPluginPackageInfo> pluginList = GameDataPlugin.Plugins;
            if (pluginList.FirstOrDefault(x => x.PluginId?.Equals(pluginKey, StringComparison.OrdinalIgnoreCase) ??
                                               false) is not { PluginPackage: { } packageData } packageInfo ||
                packageData.PackageAssetValidationList.Count == 0)
            {
                return result;
            }

            foreach (HypPackageData validate in packageData.PackageAssetValidationList)
            {
                if (validate.FilePath == null)
                {
                    continue;
                }

                string path = Path.Combine(GameDirPath, validate.FilePath);
                try
                {
                    FileInfo fileInfo = new FileInfo(path)
                                       .EnsureNoReadOnly()
                                       .StripAlternateDataStream();

                    if (fileInfo.Exists &&
                        fileInfo.Length == validate.PackageSize)
                    {
                        continue;
                    }

                    result.Add(packageInfo);
                    break;
                }
                catch (FileNotFoundException)
                {
                    result.Add(packageInfo);
                }
            }

            return result;
        }

        protected virtual DeltaPatchProperty? CheckDeltaPatchUpdate(string gamePath, string profileName,
                                                                    GameVersion gameVersion)
        {
            // If GameVersionInstalled doesn't have a value (null). then return null.
            if (!GameVersionInstalled.HasValue)
            {
                return null;
            }

            // Get the preload status
            bool isGameHasPreload = IsGameHasPreload() && GameVersionInstalled == gameVersion;

            // If the game version doesn't match with the API's version, then go to the next check.
            if (GameVersionInstalled == gameVersion && !isGameHasPreload)
            {
                return null;
            }

            // Sanitation check if the directory doesn't exist, then return null.
            if (!Directory.Exists(gamePath))
            {
                return null;
            }

            // Iterate the possible path
            IEnumerable possiblePaths =
                Directory.EnumerateFiles(gamePath, $"{profileName}*.patch", SearchOption.TopDirectoryOnly);
            foreach (string path in possiblePaths)
            {
                // Initialize patchProperty for versioning check.
                DeltaPatchProperty patchProperty = new DeltaPatchProperty(path);
                // If the version of the game is valid and the profile name matches, then return the property.
                if (GameVersionInstalled == patchProperty.SourceVer
                    && GameVersionAPI == patchProperty.TargetVer
                    && patchProperty.ProfileName == GamePreset.ProfileName)
                {
                    return patchProperty;
                }

                // If the state is on preload, then try check the preload delta patch
                if (GameVersionAPIPreload.HasValue && isGameHasPreload && GameVersionInstalled == patchProperty.SourceVer
                    && GameVersionAPIPreload == patchProperty.TargetVer
                    && patchProperty.ProfileName == GamePreset.ProfileName)
                {
                    return patchProperty;
                }
            }

            // If all not passed, then return null.
            return null;
        }

        public virtual bool IsForceRedirectToSophon() => GamePreset.GameLauncherApi?.IsForceRedirectToSophon ?? false;
        #endregion
    }
}