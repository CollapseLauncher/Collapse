using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace

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
                return GameApiProp.data?.pre_download_game != null;

            return GameApiProp.data?.pre_download_game?.latest != null || GameApiProp.data?.pre_download_game?.diffs != null;
        }

        public virtual bool IsGameHasDeltaPatch() => false;

        public virtual bool IsGameVersionMatch()
            // Ensure if the GameVersionInstalled is available (this is coming from the Game Profile's Ini file).
            // If not, then return false to indicate that the game isn't installed.
            => GameVersionInstalled.HasValue &&
                   // If the game is installed and the version doesn't match, then return to false.
                   // But if the game version matches, then return to true.
                   GameVersionInstalled.Value.IsMatch(GameVersionAPI);

        public virtual async ValueTask<bool> IsPluginVersionsMatch()
        {
#if !MHYPLUGINSUPPORT
            return true;
#else
            // Get the pluginVersions and installedPluginVersions
            Dictionary<string, GameVersion>  pluginVersions          = PluginVersionsAPI;
            Dictionary<string, GameVersion>? pluginVersionsInstalled = PluginVersionsInstalled;

            // If the plugin version installed is null, return true as it doesn't need to check
            if (pluginVersionsInstalled == null)
            {
                return true;
            }

            // Compare each entry in the dict
            if (pluginVersions.Count != pluginVersionsInstalled.Count)
            {
                return false;
            }

            MismatchPlugin = null;
            foreach (KeyValuePair<string, GameVersion> pluginVersion in pluginVersions)
            {
                if (pluginVersionsInstalled.TryGetValue(pluginVersion.Key, out GameVersion installedPluginVersion) &&
                    pluginVersion.Value.IsMatch(installedPluginVersion))
                {
                    continue;
                }

                // Uh-oh, we need to calculate the file hash.
                MismatchPlugin = await CheckPluginUpdate(pluginVersion.Key);
                if (MismatchPlugin.Count != 0)
                {
                    return false;
                }
            }

            // Update cached plugin versions
            PluginVersionsInstalled = PluginVersionsAPI;

            return true;
#endif
        }

        public virtual async ValueTask<bool> IsSdkVersionsMatch()
        {
#if !MHYPLUGINSUPPORT
            return true;
#else
            // Get the pluginVersions and installedPluginVersions
            GameVersion? sdkVersion = SdkVersionAPI;
            GameVersion? installedSdkVersion = SdkVersionInstalled;

            // If the SDK API has no value, return true
            if (!installedSdkVersion.HasValue && !sdkVersion.HasValue)
                return true;

            // If the SDK Resource is null, return true
            RegionResourcePlugin? sdkResource = GetGameSdkZip()?.FirstOrDefault();
            if (sdkResource == null)
                return true;

            // If the installed SDK returns empty (null), return false
            if (!installedSdkVersion.HasValue)
                return false;

            // Compare the version and the current SDK state if the indicator file is exist
            string validatePath = Path.Combine(GameDirPath, sdkResource.package?.pkg_version ?? string.Empty);
            bool isVersionEqual = installedSdkVersion.Equals(sdkVersion);
            bool isValidatePathExist = File.Exists(validatePath);
            bool isPkgVersionMatch = isValidatePathExist && await CheckSdkUpdate(validatePath);

            bool isSdkInstalled = isVersionEqual && isPkgVersionMatch;
            return isSdkInstalled;
#endif
        }

        public virtual bool IsGameInstalled()
        {
            // If the GameVersionInstalled doesn't have a value (not null), then return as false.
            if (!GameVersionInstalled.HasValue)
            {
                return false;
            }

            // Check if the executable file exist and has the size at least > 2 MiB. If not, then return as false.
            FileInfo execFileInfo = new FileInfo(Path.Combine(GameDirPath, GamePreset.GameExecutableName ?? string.Empty));

            // Check if the vendor type exist. If not, then return false
            if (VendorTypeProp.GameName == null || !VendorTypeProp.VendorType.HasValue)
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
                ReadOnlySpan<char> pathRoot = Path.GetPathRoot(path);
                if (pathRoot.IsEmpty)
                {
                    return false;
                }

                string pathRootStr = pathRoot.ToString();
                // Return from Directory.Exists() since the IsReady property use the same method.
                return Directory.Exists(pathRootStr);
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
            if (!await IsPluginVersionsMatch() || !await IsSdkVersionsMatch())
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

        public virtual async ValueTask<bool> CheckSdkUpdate(string validatePath)
        {
            try
            {
                using StreamReader reader = new StreamReader(validatePath);
                while (await reader.ReadLineAsync() is { } line)
                {
                    if (string.IsNullOrEmpty(line))
                        continue;

                    PkgVersionProperties? pkgVersion = line.Deserialize(CoreLibraryJsonContext.Default.PkgVersionProperties);

                    if (pkgVersion == null)
                    {
                        continue;
                    }

                    string filePath = Path.Combine(GameDirPath, pkgVersion.remoteName);
                    if (!File.Exists(filePath))
                        return false;

                    await using FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    byte[] hashArray = await Hash.GetCryptoHashAsync<MD5>(fs);
                    string md5 = Convert.ToHexStringLower(hashArray);
                    if (!md5.Equals(pkgVersion.md5, StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"Failed while checking the SDK file update\r\n{ex}", LogType.Error, true);
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                return false;
            }
        }

        public virtual async ValueTask<List<RegionResourcePlugin>> CheckPluginUpdate(string pluginKey)
        {
            List<RegionResourcePlugin> result = [];
            if (GameApiProp.data?.plugins == null)
            {
                return result;
            }

            RegionResourcePlugin? plugin = GameApiProp.data?.plugins?
                .FirstOrDefault(x => x.plugin_id != null && x.plugin_id.Equals(pluginKey, StringComparison.OrdinalIgnoreCase));
            if (plugin == null)
            {
                return result;
            }

            if (plugin.package?.validate == null)
            {
                return result;
            }

            foreach (RegionResourcePluginValidate validate in plugin.package?.validate!)
            {
                if (validate.path == null)
                {
                    continue;
                }

                string path = Path.Combine(GameDirPath, validate.path);
                try
                {
                    if (!File.Exists(path))
                    {
                        result.Add(plugin);
                        break;
                    }

                    await using FileStream fs  = new FileStream(path, FileMode.Open, FileAccess.Read);
                    string?                md5 = HexTool.BytesToHexUnsafe(await MD5.HashDataAsync(fs));
                    if (md5 == validate.md5)
                    {
                        continue;
                    }

                    result.Add(plugin);
                    break;
                }
                catch (FileNotFoundException)
                {
                    result.Add(plugin);
                }
            }

            return result;
        }

        public virtual DeltaPatchProperty? CheckDeltaPatchUpdate(string gamePath, string profileName,
                                                                 GameVersion gameVersion)
        {
            // If GameVersionInstalled doesn't have a value (null). then return null.
            if (!GameVersionInstalled.HasValue)
            {
                return null;
            }

            // Get the preload status
            bool isGameHasPreload = IsGameHasPreload() && GameVersionInstalled.Value.IsMatch(gameVersion);

            // If the game version doesn't match with the API's version, then go to the next check.
            if (GameVersionInstalled.Value.IsMatch(gameVersion) && !isGameHasPreload)
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
                if (GameVersionInstalled.Value.IsMatch(patchProperty.SourceVer)
                    && (GameVersionAPI?.IsMatch(patchProperty.TargetVer) ?? false)
                    && patchProperty.ProfileName == GamePreset.ProfileName)
                {
                    return patchProperty;
                }

                // If the state is on preload, then try check the preload delta patch
                if (GameVersionAPIPreload != null && isGameHasPreload && GameVersionInstalled.Value.IsMatch(patchProperty.SourceVer)
                    && GameVersionAPIPreload.Value.IsMatch(patchProperty.TargetVer)
                    && patchProperty.ProfileName == GamePreset.ProfileName)
                {
                    return patchProperty;
                }
            }

            // If all not passed, then return null.
            return null;
        }

        public virtual bool IsForceRedirectToSophon()
        {
            return GamePreset.GameLauncherApi?.IsForceRedirectToSophon ?? false;
        }
        #endregion
    }
}