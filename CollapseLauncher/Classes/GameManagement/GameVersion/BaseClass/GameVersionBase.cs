using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Text;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Hi3Helper.SentryHelper;

// ReSharper disable CheckNamespace

namespace CollapseLauncher.GameVersioning
{
    internal class GameVersionBase : IGameVersionCheck
    {
        #region DefaultPresets

        private const string _defaultIniProfileSection = "launcher";
        private const string _defaultIniVersionSection = "General";

        private string _defaultGameDirPath =>
            Path.Combine(LauncherConfig.AppGameFolder, GamePreset.ProfileName ?? string.Empty, GamePreset.GameDirectoryName ?? string.Empty);

        private int gameChannelID    => GamePreset.ChannelID ?? 0;
        private int gameSubChannelID => GamePreset.SubChannelID ?? 0;
        private string gameCps => GamePreset.LauncherCPSType;

        private IniSection _defaultIniProfile =>
            new()
            {
                { "cps", new IniValue(GamePreset.LauncherCPSType) },
                { "channel", new IniValue(gameChannelID) },
                { "sub_channel", new IniValue(gameSubChannelID) },
                { "game_install_path", new IniValue(_defaultGameDirPath.Replace('\\', '/')) },
                { "game_start_name", new IniValue(GamePreset.GameExecutableName) },
                { "is_first_exit", new IniValue(false) },
                { "exit_type", new IniValue(2) }
            };

        private IniSection _defaultIniVersion =>
            new()
            {
                { "channel", new IniValue(gameChannelID) },
                { "cps", new IniValue(GamePreset.LauncherCPSType) },
                { "game_version", new IniValue() },
                { "sub_channel", new IniValue(gameSubChannelID) },
                { "sdk_version", new IniValue() },
                { "uapc", GenerateUAPCValue() }
            };

        private IniValue GenerateUAPCValue()
        {
            if (string.IsNullOrEmpty(GamePreset.LauncherBizName))
            {
                Logger.LogWriteLine($"Biz name in the game preset for {GamePreset.ProfileName} is empty! Cannot generate UAPC value", LogType.Warning, true);
                return new IniValue();
            }

            Dictionary<string, Dictionary<string, string>> uapc = new Dictionary<string, Dictionary<string, string>>
            {
                {
                    GamePreset.LauncherBizName, new Dictionary<string, string>
                    {
                        { "uapc", "" }
                    }
                },
                {
                    "hyp", new Dictionary<string, string>
                    {
                        { "uapc", "" }
                    }
                }
            };
            string uapcValue = uapc.Serialize(InternalAppJSONContext.Default.DictionaryStringDictionaryStringString, false);
            return new IniValue(uapcValue);
        }

        #endregion

        #region Properties

        protected readonly IniFile GameIniProfile = new();
        protected readonly IniFile GameIniVersion = new();
        protected          string  GameIniProfilePath => Path.Combine(GameConfigDirPath, "config.ini");

        protected string GameIniVersionPath
        {
            get
            {
                string path =
                    ConverterTool
                       .NormalizePath(Path.Combine(GameIniProfile[_defaultIniProfileSection]["game_install_path"].ToString(),
                                                   "config.ini"));
                return IsDiskPartitionExist(path)
                    ? path
                    : Path.Combine(GameConfigDirPath, GamePreset.GameDirectoryName ?? "Games", "config.ini");
            }
        }

#nullable enable
        public IniSection? GameIniVersionSection { get => GameIniVersion[_defaultIniVersionSection] ?? null; }
        public IniSection? GameIniProfileSection { get => GameIniVersion[_defaultIniProfileSection] ?? null; }
#nullable restore

        protected string             GameConfigDirPath { get; set; }
        public    GameVersionBase    AsVersionBase     => this;
        public    string             GameName          { get; set; }
        public    string             GameRegion        { get; set; }
        public    PresetConfig       GamePreset        { get => LauncherMetadataHelper.LauncherMetadataConfig?[GameName]?[GameRegion]; }
        public    RegionResourceProp GameAPIProp       { get; set; }
        public    GameNameType       GameType          => GamePreset.GameType;
        public    GameVendorProp     VendorTypeProp    { get; private set; }

        public string GameDirPath
        {
            get => Path.GetDirectoryName(GameIniVersionPath);
            set
            {
                UpdateGamePath(value, false);
                UpdateGameChannels();
            }
        }

        public string GameDirAppDataPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow",
                         $"{VendorTypeProp.VendorType}", GamePreset.InternalGameNameInConfig ?? string.Empty);

        public string GameOutputLogName =>
            GameType switch
            {
                GameNameType.Genshin => "output_log.txt",
                GameNameType.Honkai => "output_log.txt",
                _ => "Player.log"
            };

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        protected UIElement   ParentUIElement { get; init; }

        protected GameVersion? GameVersionAPI
        {
            get
            {
                if (GameVersion.TryParse(GameAPIProp.data?.game?.latest?.version, out GameVersion? version))
                    return version;

                return null;
            }
        }

        protected Dictionary<string, GameVersion> PluginVersionsAPI
        {
            get
            {
                var value = new Dictionary<string, GameVersion>();

                // Return empty if the plugin is not exist
                if (GameAPIProp.data?.plugins == null || GameAPIProp.data?.plugins?.Count == 0)
                {
                    return value;
                }

                // Get the version and convert it into GameVersion
                foreach (var plugin in GameAPIProp.data?.plugins!)
                {
                    if (plugin.plugin_id != null)
                    {
                        value.Add(plugin.plugin_id, new GameVersion(plugin.version));
                    }
                }

                return value;
            }
        }

        protected GameVersion? SdkVersionAPI
        {
            get
            {
                // Return null if the plugin is not exist
                if (GameAPIProp.data?.sdk == null)
                    return null;

                // If the version provided by the SDK API, return the result
                if (GameVersion.TryParse(GameAPIProp.data?.sdk?.version, out GameVersion? result))
                    return result;

                // Otherwise, return null
                return null;
            }
        }

        protected GameVersion? GameVersionAPIPreload
        {
            get
            {
                GameVersion? currentInstalled = GameVersionInstalled;

                // If no installation installed, then return null
                if (currentInstalled == null)
                    return null;

                // Check if the pre_download_game property has value. If not, then return null
                if (string.IsNullOrEmpty(GameAPIProp?.data?.pre_download_game?.latest?.version))
                    return null;

                return new GameVersion(GameAPIProp.data.pre_download_game.latest.version);
            }
        }

        protected GameVersion? GameVersionInstalled
        {
            get
            {
                // Check if the INI has game_version key...
                if (GameIniVersion[_defaultIniVersionSection].ContainsKey("game_version"))
                {
                    string val = GameIniVersion[_defaultIniVersionSection]["game_version"].ToString();
                    if (string.IsNullOrEmpty(val))
                    {
                        return null;
                    }

                    return new GameVersion(val);
                }

                // If not, then return as null
                return null;
            }
            set
            {
                UpdateGameVersion(value ?? GameVersionAPI, false);
                UpdateGameChannels(false);
            }
        }

        protected Dictionary<string, GameVersion> PluginVersionsInstalled
        {
            get
            {
                var value = new Dictionary<string, GameVersion>();

                // Return empty if the plugin is not exist
                if (GameAPIProp.data?.plugins == null || GameAPIProp.data?.plugins?.Count == 0)
                {
                    return value;
                }

                // Get the version and convert it into GameVersion
                foreach (var plugin in GameAPIProp.data?.plugins!)
                {
                    // Check if the INI has plugin_ID_version key...
                    string keyName = $"plugin_{plugin.plugin_id}_version";
                    if (GameIniVersion[_defaultIniVersionSection].ContainsKey(keyName))
                    {
                        string val = GameIniVersion[_defaultIniVersionSection][keyName].ToString();
                        if (string.IsNullOrEmpty(val))
                        {
                            continue;
                        }

                        if (plugin.plugin_id != null)
                        {
                            value.Add(plugin.plugin_id, new GameVersion(val));
                        }
                    }
                }

                return value;
            }
            set => UpdatePluginVersions(value ?? PluginVersionsAPI);
        }

        protected GameVersion? SdkVersionInstalled
        {
            get
            {
                // Check if the game config has SDK version. If not, return null
                string keyName = $"plugin_sdk_version";
                if (!GameIniVersion[_defaultIniVersionSection].ContainsKey(keyName))
                    return null;

                // Check if it has no value, then return null
                string versionName = GameIniVersion[_defaultIniVersionSection][keyName].ToString();
                if (string.IsNullOrEmpty(versionName))
                    return null;

                // Try parse the version. If it's not valid, then return null
                if (!GameVersion.TryParse(versionName, out GameVersion? result))
                    return null;

                // Return the result
                return result;
            }
            set => UpdateSdkVersion(value ?? SdkVersionAPI);
        }

        protected List<RegionResourcePlugin> MismatchPlugin { get; set; }

        // Assign for the Game Delta-Patch properties (if any).
        // If there's no Delta-Patch, then set it to null.
        protected DeltaPatchProperty GameDeltaPatchProp =>
            CheckDeltaPatchUpdate(GameDirPath, GamePreset.ProfileName, GameVersionAPI ?? throw new NullReferenceException("GameVersionAPI returns a null"));

        #endregion

        protected GameVersionBase(UIElement parentUIElement, RegionResourceProp gameRegionProp, string gameName, string gameRegion)
        {
            ParentUIElement = parentUIElement;
            GameAPIProp     = gameRegionProp;
            GameName        = gameName;
            GameRegion      = gameRegion;

            // Initialize INIs props
            InitializeIniProp();
        }

        public GameVersion? GetGameExistingVersion()
        {
            return GameVersionInstalled;
        }

        public GameVersion? GetGameVersionAPI()
        {
            return GameVersionAPI;
        }

        public GameVersion? GetGameVersionAPIPreload()
        {
            return GameVersionAPIPreload;
        }

        public async ValueTask<GameInstallStateEnum> GetGameState()
        {
            // Check if the game installed first
            // If the game is installed, then move to another step.
            if (IsGameInstalled())
            {
                // Check for the game/plugin version and preload availability.
                if (!IsGameVersionMatch())
                {
                    return GameInstallStateEnum.NeedsUpdate;
                }

                if (IsGameHasPreload())
                {
                    return GameInstallStateEnum.InstalledHavePreload;
                }

                if (!await IsPluginVersionsMatch() || !await IsSdkVersionsMatch())
                {
                    return GameInstallStateEnum.InstalledHavePlugin;
                }

                // If passes, then return as Installed.
                return GameInstallStateEnum.Installed;
            }

            // If none of above passes, then return as NotInstalled.
            return GameInstallStateEnum.NotInstalled;
        }

        public virtual List<RegionResourceVersion> GetGameLatestZip(GameInstallStateEnum gameState)
        {
            // Initialize the return list
            List<RegionResourceVersion> returnList = new();

            // If the GameVersion is not installed, then return the latest one
            if (gameState == GameInstallStateEnum.NotInstalled || gameState == GameInstallStateEnum.GameBroken)
            {
                // Add the latest prop to the return list
                returnList.Add(GameAPIProp.data?.game?.latest);

                return returnList;
            }

            // Try to get the diff file  by the first or default (null)
            if (GameAPIProp.data?.game?.diffs != null)
            {
                RegionResourceVersion diff = GameAPIProp.data?.game?.diffs
                                                        .FirstOrDefault(x => x.version == GameVersionInstalled?.VersionString);

                // Return if the diff is null, then get the latest. If found, then return the diff one.
                returnList.Add(diff ?? GameAPIProp.data?.game?.latest);
            }

            return returnList;
        }

#nullable enable
        public virtual List<RegionResourceVersion>? GetGamePreloadZip()
        {
            // Initialize the return list
            List<RegionResourceVersion> returnList = new();

            // If the preload is not exist, then return null
            if (GameAPIProp.data?.pre_download_game == null
           || ((GameAPIProp.data?.pre_download_game?.diffs?.Count ?? 0) == 0
             && GameAPIProp.data?.pre_download_game?.latest == null))
            {
                return null;
            }

            // Try to get the diff file  by the first or default (null)
            RegionResourceVersion? diff = GameAPIProp.data?.pre_download_game?.diffs?
                                                    .FirstOrDefault(x => x.version == GameVersionInstalled?.VersionString);

            // If the single entry of the diff is null, then return null
            // If the diff is null, then get the latest.
            // If diff is found, then add the diff one.
            returnList.Add(diff ?? GameAPIProp.data?.pre_download_game?.latest ?? throw new NullReferenceException($"Preload neither have diff or latest package!"));

            // Return the list
            return returnList;
        }

        public virtual List<RegionResourcePlugin>? GetGamePluginZip()
        {
            // Check if the plugin is not empty, then add it
            if ((GameAPIProp?.data?.plugins?.Count ?? 0) != 0)
                return new List<RegionResourcePlugin>(GameAPIProp?.data?.plugins!);

            // Return null if plugin is unavailable
            return null;
        }

        public virtual List<RegionResourcePlugin>? GetGameSdkZip()
        {
            // Check if the sdk is not empty, then add it
            if (GameAPIProp?.data?.sdk != null)
            {
                // Convert the value
                RegionResourcePlugin sdkPlugin = new RegionResourcePlugin
                {
                    plugin_id = "sdk",
                    release_id = "sdk",
                    version = GameAPIProp?.data?.sdk.version,
                    package = GameAPIProp?.data?.sdk
                };

                // If the package is not null, then add the validation
                if (sdkPlugin.package != null)
                {
                    sdkPlugin.package.pkg_version = GameAPIProp?.data?.sdk?.pkg_version;
                }

                // Return a single list
                return new List<RegionResourcePlugin>
                {
                    sdkPlugin
                };
            }

            // Return null if sdk is unavailable
            return null;
        }
#nullable restore

        public virtual DeltaPatchProperty GetDeltaPatchInfo()
        {
            return null;
        }

        public virtual bool IsGameHasPreload()
        {
            if (GamePreset.LauncherType == LauncherType.Sophon)
                return GameAPIProp.data?.pre_download_game != null;

            return GameAPIProp.data?.pre_download_game?.latest != null || GameAPIProp.data?.pre_download_game?.diffs != null;
        }

        public virtual bool IsGameHasDeltaPatch()
        {
            return false;
        }

        public virtual bool IsGameVersionMatch()
        {
            // Ensure if the GameVersionInstalled is available (this is coming from the Game Profile's Ini file).
            // If not, then return false to indicate that the game isn't installed.
            if (!GameVersionInstalled.HasValue)
            {
                return false;
            }

            // If the game is installed and the version doesn't match, then return to false.
            // But if the game version matches, then return to true.
            return GameVersionInstalled.Value.IsMatch(GameVersionAPI);
        }

        public virtual async ValueTask<bool> IsPluginVersionsMatch()
        {
        #if !MHYPLUGINSUPPORT
            return true;
        #else
            // Get the pluginVersions and installedPluginVersions
            var pluginVersions          = PluginVersionsAPI;
            var installedPluginVersions = PluginVersionsInstalled;

            // Compare each entry in the dict
            if (pluginVersions.Count != installedPluginVersions.Count)
            {
                return false;
            }

            MismatchPlugin = null;
            foreach (var pluginVersion in pluginVersions)
            {
                if (!installedPluginVersions.TryGetValue(pluginVersion.Key, out var installedPluginVersion) ||
                    !pluginVersion.Value.IsMatch(installedPluginVersion))
                {
                    // Uh-oh, we need to calculate the file hash.
                    MismatchPlugin = await CheckPluginUpdate(pluginVersion.Key);
                    if (MismatchPlugin.Count != 0)
                    {
                        return false;
                    }
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
            var sdkVersion = SdkVersionAPI;
            var installedSdkVersion = SdkVersionInstalled;

            // If the SDK API has no value, return true
            if (!installedSdkVersion.HasValue && !sdkVersion.HasValue)
                return true;

            // If the SDK Resource is null, return true
            RegionResourcePlugin sdkResource = GetGameSdkZip()?.FirstOrDefault();
            if (sdkResource == null)
                return true;

            // If the installed SDK returns empty (null), return false
            if (!installedSdkVersion.HasValue)
                return false;

            // Compare the version and the current SDK state if the indicator file is exist
            string validatePath        = Path.Combine(GameDirPath, sdkResource.package?.pkg_version ?? string.Empty);
            bool   isVersionEqual      = installedSdkVersion.Equals(sdkVersion);
            bool   isValidatePathExist = File.Exists(validatePath);
            bool   isPkgVersionMatch   = isValidatePathExist && await CheckSdkUpdate(validatePath);

            bool isSdkInstalled = isVersionEqual && isPkgVersionMatch;
            return isSdkInstalled;
#endif
        }

        public virtual async ValueTask<bool> CheckSdkUpdate(string validatePath)
        {
            try
            {
                using (StreamReader reader = new StreamReader(validatePath))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = await reader.ReadLineAsync();
                        if (string.IsNullOrEmpty(line))
                            continue;

                        PkgVersionProperties pkgVersion = line.Deserialize(CoreLibraryJSONContext.Default.PkgVersionProperties);

                        if (pkgVersion != null)
                        {
                            string filePath = Path.Combine(GameDirPath, pkgVersion.remoteName);
                            if (!File.Exists(filePath))
                                return false;

                            using FileStream fs  = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                            string           md5 = HexTool.BytesToHexUnsafe(await MD5.HashDataAsync(fs));
                            if (md5 == null || !md5.Equals(pkgVersion.md5, StringComparison.OrdinalIgnoreCase))
                                return false;
                        }
                    }
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
            if (GameAPIProp.data?.plugins == null)
            {
                return result;
            }

            RegionResourcePlugin plugin = GameAPIProp.data?.plugins?
                .FirstOrDefault(x => x.plugin_id != null && x.plugin_id.Equals(pluginKey, StringComparison.OrdinalIgnoreCase));
            if (plugin == null)
            {
                return result;
            }

            if (plugin.package?.validate != null)
            {
                foreach (var validate in plugin.package?.validate!)
                {
                    if (validate.path != null)
                    {
                        var path = Path.Combine(GameDirPath, validate.path);
                        try
                        {
                            if (!File.Exists(path))
                            {
                                result.Add(plugin);
                                break;
                            }

                            using var fs  = new FileStream(path, FileMode.Open, FileAccess.Read);
                            var       md5 = HexTool.BytesToHexUnsafe(await MD5.HashDataAsync(fs));
                            if (md5 != validate.md5)
                            {
                                result.Add(plugin);
                                break;
                            }
                        }
                        catch (FileNotFoundException)
                        {
                            result.Add(plugin);
                        }
                    }
                }
            }

            return result;
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
            return VendorTypeProp.GameName == GamePreset.InternalGameNameInConfig && execFileInfo.Exists &&
                   execFileInfo.Length > 1 << 16;
        }

    #nullable enable
        public virtual string? FindGameInstallationPath(string path)
        {
            // Try to find the base game path from the executable location.
            string? basePath = TryFindGamePathFromExecutableAndConfig(path, GamePreset.GameExecutableName);

            // If the executable file and version config doesn't exist (null), then return null.
            if (string.IsNullOrEmpty(basePath))
            {
                return null;
            }

            // Check if the ini file does have the "game_version" value.
            string iniPath = Path.Combine(basePath, "config.ini");
            if (IsTryParseIniVersionExist(iniPath))
            {
                return basePath;
            }

            // If the file doesn't exist, return as null.
            return null;
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
            if ((isExecExist && isGameDataDirExist && !isGameVendorValid)
             || !isGameExecDataDirValid
             || !isGameConfigIdValid
             || !isGameHasBilibiliStatus)
            {
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

            return true;
        }

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
            const string ChannelIdKeyName = "channel";
            const string SubChannelIdKeyName = "sub_channel";
            const string CpsKeyName = "cps";

            if (string.IsNullOrEmpty(GamePreset.LauncherCPSType)
            || GamePreset.ChannelID == null
            || GamePreset.SubChannelID == null)
                return true;

            bool isContainsChannelId = GameIniVersion[_defaultIniVersionSection].ContainsKey(ChannelIdKeyName);
            bool isContainsSubChannelId = GameIniVersion[_defaultIniVersionSection].ContainsKey(SubChannelIdKeyName);
            bool isCps = GameIniVersion[_defaultIniVersionSection].ContainsKey(CpsKeyName);
            if (!isContainsChannelId
             || !isContainsSubChannelId
             || !isCps)
                return false;

            string? channelIdCurrent = GameIniVersion[_defaultIniVersionSection][ChannelIdKeyName];
            string? subChannelIdCurrent = GameIniVersion[_defaultIniVersionSection][SubChannelIdKeyName];
            string? cpsCurrent = GameIniVersion[_defaultIniVersionSection][CpsKeyName];

            if (!int.TryParse(channelIdCurrent, null, out int channelIdCurrentInt)
             || !int.TryParse(subChannelIdCurrent, null, out int subChannelIdCurrentInt)
             || string.IsNullOrEmpty(cpsCurrent))
                return false;

            return !(channelIdCurrentInt != GamePreset.ChannelID
             || subChannelIdCurrentInt != GamePreset.SubChannelID
             || !cpsCurrent.Equals(GamePreset.LauncherCPSType));
        }

        protected virtual bool IsGameExecDataDirValid(string? executableName)
        {
            return true; // Always true for games other than Genshin Impact
        }

        protected virtual bool IsGameHasBilibiliStatus(string? executableName)
            {
            bool isBilibili = GamePreset.LauncherCPSType?
                .IndexOf("bilibili", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isBilibili)
                return true;

            executableName = Path.GetFileNameWithoutExtension(executableName);
            string sdkDllPath = Path.Combine(GameDirPath, $"{executableName}_Data", "Plugins", "PCGameSDK.dll");

            return !(!isBilibili && File.Exists(sdkDllPath));
        }

        protected virtual void FixInvalidGameVendor(string? executableName)
        {
            executableName = Path.GetFileNameWithoutExtension(executableName);
            string appInfoFilePath = Path.Combine(GameDirPath, $"{executableName}_Data", "app.info");
            string? appInfoFileDir = Path.GetDirectoryName(appInfoFilePath);

            if (!string.IsNullOrEmpty(appInfoFileDir) && !Directory.Exists(appInfoFileDir))
                Directory.CreateDirectory(appInfoFileDir);

            string appInfoString = $"{GamePreset.VendorType.ToString()}\n{GamePreset.InternalGameNameInConfig!}";
            byte[] buffer = Encoding.UTF8.GetBytes(appInfoString);
            File.WriteAllBytes(appInfoFilePath, buffer);
        }

        protected virtual void FixInvalidGameExecDataDir(string? executableName)
        {
            // Always return for games other than Genshin Impact
        }

        protected virtual void FixInvalidGameConfigId()
        {
            const string ChannelIdKeyName = "channel";
            const string SubChannelIdKeyName = "sub_channel";
            const string CpsKeyName = "cps";

            string gameIniVersionPath = Path.Combine(GameDirPath, "config.ini");

            GameIniVersion[_defaultIniVersionSection][ChannelIdKeyName] = GamePreset.ChannelID ?? 0;
            GameIniVersion[_defaultIniVersionSection][SubChannelIdKeyName] = GamePreset.SubChannelID ?? 0;
            GameIniVersion[_defaultIniVersionSection][CpsKeyName] = GamePreset.LauncherCPSType;

            SaveGameIni(gameIniVersionPath, GameIniVersion);
        }

        protected virtual void FixInvalidGameBilibiliStatus(string? executableName)
        {
            bool isBilibili = GamePreset.LauncherCPSType?
               .IndexOf("bilibili", StringComparison.OrdinalIgnoreCase) >= 0;

            executableName = Path.GetFileNameWithoutExtension(executableName);
            string sdkDllPath = Path.Combine(GameDirPath, $"{executableName}_Data", "Plugins", "PCGameSDK.dll");

            if (!isBilibili && File.Exists(sdkDllPath))
            {
                new FileInfo(sdkDllPath) { IsReadOnly = false }.Delete();
            }
        }
#nullable disable

        public virtual DeltaPatchProperty CheckDeltaPatchUpdate(string      gamePath, string profileName,
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
            if (!GameVersionInstalled.Value.IsMatch(gameVersion) || isGameHasPreload)
            {
                // Sanitation check if the directory doesn't exist, then return null.
                if (!Directory.Exists(gamePath))
                {
                    return null;
                }

                // Iterate the possible path
                IEnumerable PossiblePaths =
                    Directory.EnumerateFiles(gamePath, $"{profileName}*.patch", SearchOption.TopDirectoryOnly);
                foreach (string path in PossiblePaths)
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
            }

            // If all not passed, then return null.
            return null;
        }

        public virtual void Reinitialize()
        {
            InitializeIniProp();
        }

        public void UpdateGamePath(string path, bool saveValue = true)
        {
            GameIniProfile[_defaultIniProfileSection]["game_install_path"] = path.Replace('\\', '/');
            if (saveValue)
            {
                SaveGameIni(GameIniProfilePath, GameIniProfile);
            }
        }

        public void UpdateGameVersionToLatest(bool saveValue = true)
        {
            GameIniVersion[_defaultIniVersionSection]["game_version"] = GameVersionAPI?.VersionString;
            if (saveValue)
            {
                SaveGameIni(GameIniVersionPath, GameIniVersion);
                UpdateGameChannels();
                UpdatePluginVersions(PluginVersionsAPI);
            }
        }

        public void UpdateGameVersion(GameVersion? version, bool saveValue = true)
        {
            GameIniVersion[_defaultIniVersionSection]["game_version"] = version?.VersionString;
            if (saveValue)
            {
                SaveGameIni(GameIniVersionPath, GameIniVersion);
            }
        }

        public void UpdateGameChannels(bool saveValue = true)
        {
            GameIniVersion[_defaultIniVersionSection]["channel"]     = gameChannelID;
            GameIniVersion[_defaultIniVersionSection]["sub_channel"] = gameSubChannelID;
            GameIniVersion[_defaultIniVersionSection]["cps"]         = gameCps;

            /* Disable these lines as these will trigger some bugs (like Endless "Broken config.ini" dialog)
             * and causes the cps field to be missing for other non-Bilibili games
             * 
            // Remove the contains section if the client is not Bilibili, and it does have the value.
            // This to avoid an issue with HSR config.ini detection
            bool isBilibili = GamePreset.ZoneName == "Bilibili";
            if ( !isBilibili
                && GameIniVersion.ContainsSection(_defaultIniVersionSection)
                && GameIniVersion[_defaultIniVersionSection].ContainsKey("cps")
                && GameIniVersion[_defaultIniVersionSection]["cps"].ToString().IndexOf("bilibili", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                GameIniVersion[_defaultIniVersionSection].Remove("cps");
            }
            */

            if (saveValue)
            {
                SaveGameIni(GameIniVersionPath, GameIniVersion);
            }
        }

        public void UpdatePluginVersions(Dictionary<string, GameVersion> versions, bool saveValue = true)
        {
            // If the plugin is empty, ignore it
            if ((versions?.Count ?? 0) == 0)
            {
                return;
            }

            // Get the plugin property and its key name
            foreach (var version in versions)
            {
                string keyName = $"plugin_{version.Key}_version";

                // Set the value
                GameIniVersion[_defaultIniVersionSection][keyName] = version.Value.VersionString;
            }

            if (saveValue)
            {
                SaveGameIni(GameIniVersionPath, GameIniVersion);
            }
        }

        public void UpdateSdkVersion(GameVersion? version, bool saveValue = true)
        {
            // If the version is null, return
            if (!version.HasValue)
                return;

            // If the sdk is empty, ignore it
            if (GameAPIProp.data?.sdk == null)
                return;

            // Set the value
            string keyName = $"plugin_sdk_version";
            GameIniVersion[_defaultIniVersionSection][keyName] = version.ToString();

            if (saveValue)
            {
                SaveGameIni(GameIniVersionPath, GameIniVersion);
            }
        }

        protected virtual void TryReinitializeGameVersion()
        {
            // Check if the GameVersionInstalled == null (version config doesn't exist),
            // Reinitialize the version config and save the version config by assigning GameVersionInstalled.
            if (!GameVersionInstalled.HasValue && !string.IsNullOrEmpty(GameAPIProp.data?.game?.latest?.version))
            {
                GameVersionInstalled = GameVersionAPI;
            }
        }

        private void InitializeIniProp()
        {
            GameConfigDirPath = Path.Combine(LauncherConfig.AppGameFolder, GamePreset.ProfileName ?? string.Empty);

            // Initialize INIs
            InitializeIniProp(GameIniProfilePath, GameIniProfile, _defaultIniProfile,
                              _defaultIniProfileSection);
            InitializeIniProp(GameIniVersionPath, GameIniVersion, _defaultIniVersion,
                              _defaultIniVersionSection, true);

            // Initialize the GameVendorType
            VendorTypeProp = new GameVendorProp(GameDirPath,
                                                Path.GetFileNameWithoutExtension(GamePreset.GameExecutableName) ?? string.Empty,
                                                GamePreset.VendorType);
        }

#nullable enable
        protected virtual string? TryFindGamePathFromExecutableAndConfig(string path, string? executableName)
        {
            // Phase 1: Check on the root directory
            string   targetPath = Path.Combine(path, executableName ?? string.Empty);
            string   configPath = Path.Combine(path, "config.ini");
            FileInfo targetInfo = new FileInfo(targetPath);
            if (targetInfo.Exists && targetInfo.Length > 1 << 16 && File.Exists(configPath))
            {
                return Path.GetDirectoryName(targetPath);
            }

            // Phase 2: Check on the launcher directory + GamePreset.GameDirectoryName
            targetPath = Path.Combine(path, GamePreset.GameDirectoryName ?? "Games", executableName ?? string.Empty);
            configPath = Path.Combine(path, GamePreset.GameDirectoryName ?? "Games", "config.ini");
            targetInfo = new FileInfo(targetPath);
            if (targetInfo.Exists && targetInfo.Length > 1 << 16 && File.Exists(configPath))
            {
                return Path.GetDirectoryName(targetPath);
            }

            // If none of them passes, then return null.
            Logger.LogWriteLine("[TryFindGamePathFromExecutableAndConfig] Fail!");
            return null;
        }
#nullable restore

        private bool IsTryParseIniVersionExist(string iniPath)
        {
            // If the file doesn't exist, return false by default
            if (!File.Exists(iniPath))
            {
                return false;
            }

            // Load version config file.
            IniFile iniFile = new IniFile();
            iniFile.Load(iniPath);

            // Check whether the config has game_version value, and it must be a non-null value.
            if (iniFile[_defaultIniVersionSection].ContainsKey("game_version"))
            {
                string val = iniFile[_defaultIniVersionSection]["game_version"].ToString();
                if (val != null)
                {
                    return true;
                }
            }

            // If above doesn't pass, then return false.
            return false;
        }

        private bool IsDiskPartitionExist(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(Path.GetPathRoot(path)))
                {
                    return false;
                }
                return new DriveInfo(Path.GetPathRoot(path) ?? string.Empty).IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void SaveGameIni(string filePath, IniFile INI)
        {
            // Check if the disk partition exist. If it's exist, then save the INI.
            if (IsDiskPartitionExist(filePath))
            {
                INI.Save(filePath);
            }
        }

        private void InitializeIniProp(string iniFilePath, IniFile ini, IniSection defaults, string section, bool allowOverwriteUnmatchValues = false)
        {
            // Get the file path of the INI file and normalize it
            iniFilePath = ConverterTool.NormalizePath(iniFilePath);
            string iniDirPath = Path.GetDirectoryName(iniFilePath);

            // Check if the disk partition is ready (exist)
            bool IsDiskReady = IsDiskPartitionExist(iniDirPath);

            // Load the existing INI file if only the file exist.
            if (IsDiskReady && File.Exists(iniFilePath))
            {
                ini.Load(iniFilePath);
            }

            // Initialize and ensure the non-existed values to their defaults.
            InitializeIniDefaults(ini, defaults, section, allowOverwriteUnmatchValues);
        }

        private void InitializeIniDefaults(IniFile ini, IniSection defaults, string section, bool allowOverwriteUnmatchValues)
        {
            // If the section doesn't exist, then add the section.
            if (!ini.ContainsKey(section))
            {
                ini.Add(section);
            }

            // Iterate the defaults and start checking values.
            foreach (KeyValuePair<string, IniValue> value in defaults)
            {
                // If the key doesn't exist, then add default value.
                if (!ini[section].ContainsKey(value.Key))
                {
                    ini[section].Add(value.Key, value.Value);
                }
                else if (allowOverwriteUnmatchValues
                    && ini[section].ContainsKey(value.Key)
                    && !string.IsNullOrEmpty(value.Value.ToString())
                    && !string.IsNullOrEmpty(ini[section][value.Key].ToString())
                    && ini[section][value.Key].ToString() != value.Value.ToString())
                {
                    ini[section][value.Key] = value.Value;
                }
            }

            UpdateGameChannels(false);
        }
    }
}