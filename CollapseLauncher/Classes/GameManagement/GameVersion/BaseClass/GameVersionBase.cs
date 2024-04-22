using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Interfaces;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

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

        private IniSection _defaultIniProfile =>
            new()
            {
                { "cps", new IniValue() },
                { "channel", new IniValue(gameChannelID) },
                { "sub_channel", new IniValue(gameSubChannelID) },
                { "game_install_path", new IniValue(_defaultGameDirPath.Replace('\\', '/')) },
                { "game_start_name", new IniValue(GamePreset.GameExecutableName) },
                { "is_first_exit", new IniValue(false) },
                { "exit_type", new IniValue(2) }
            };

        private IniSection _defaultIniProfileBilibili =>
            new()
            {
                { "cps", new IniValue("bilibili") },
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
                { "cps", new IniValue() },
                { "game_version", new IniValue() },
                { "sub_channel", new IniValue(gameSubChannelID) },
                { "sdk_version", new IniValue() }
            };

        private IniSection _defaultIniVersionBilibili =>
            new()
            {
                { "channel", new IniValue(gameChannelID) },
                { "cps", new IniValue("bilibili") },
                { "game_version", new IniValue() },
                { "sub_channel", new IniValue(gameSubChannelID) },
                { "sdk_version", new IniValue() }
            };

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

        protected UIElement   ParentUIElement { get; init; }
        protected GameVersion GameVersionAPI  => new(GameAPIProp.data.game.latest.version);

        protected Dictionary<int, GameVersion> PluginVersionsAPI
        {
            get
            {
                var value = new Dictionary<int, GameVersion>();

                // Return empty if the plugin is not exist
                if (GameAPIProp.data?.plugins == null || GameAPIProp.data?.plugins?.Count == 0)
                {
                    return value;
                }

                // Get the version and convert it into GameVersion
                foreach (var plugin in GameAPIProp.data?.plugins!)
                {
                    value.Add(plugin.plugin_id, new GameVersion(plugin.version));
                }

                return value;
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
                UpdateGameVersion(value ?? GameVersionAPI);
                UpdateGameChannels();
            }
        }

        protected Dictionary<int, GameVersion> PluginVersionsInstalled
        {
            get
            {
                var value = new Dictionary<int, GameVersion>();

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

                        value.Add(plugin.plugin_id, new GameVersion(val));
                    }
                }

                return value;
            }
            set => UpdatePluginVersions(value ?? PluginVersionsAPI);
        }

        protected List<RegionResourcePlugin> MismatchPlugin { get; set; }

        // Assign for the Game Delta-Patch properties (if any).
        // If there's no Delta-Patch, then set it to null.
        protected DeltaPatchProperty GameDeltaPatchProp =>
            CheckDeltaPatchUpdate(GameDirPath, GamePreset.ProfileName, GameVersionAPI);

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

        public GameVersion GetGameVersionAPI()
        {
            return GameVersionAPI;
        }

        public GameVersion? GetGameVersionAPIPreload()
        {
            return GameVersionAPIPreload;
        }

        public GameInstallStateEnum GetGameState()
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

                if (!IsPluginVersionsMatch())
                {
                    return GameInstallStateEnum.InstalledHavePlugin;
                }

                if (IsGameHasPreload())
                {
                    return GameInstallStateEnum.InstalledHavePreload;
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

            // Update the plugins only
            if (gameState == GameInstallStateEnum.InstalledHavePlugin)
            {
                // Add the plugins to the return list
                MismatchPlugin?.ForEach(plugin => returnList.Add(plugin.package));
                return returnList;
            }

            // If the GameVersion is not installed, then return the latest one
            if (gameState == GameInstallStateEnum.NotInstalled || gameState == GameInstallStateEnum.GameBroken)
            {
                // Add the latest prop to the return list
                returnList.Add(GameAPIProp.data.game.latest);
                // If the game SDK is not null (Bilibili SDK zip), then add it to the return list
                if (GameAPIProp.data.sdk != null)
                {
                    returnList.Add(GameAPIProp.data.sdk);
                }

                return returnList;
            }

            // Try get the diff file  by the first or default (null)
            RegionResourceVersion diff = GameAPIProp.data.game.diffs
                                                    .FirstOrDefault(x => x.version == GameVersionInstalled?.VersionString);

            // Return if the diff is null, then get the latest. If found, then return the diff one.
            // If the game SDK is not null (Bilibili SDK zip), then add it to the return list
            returnList.Add(diff == null ? GameAPIProp.data.game.latest : diff);
            if (GameAPIProp.data.sdk != null)
            {
                returnList.Add(GameAPIProp.data.sdk);
            }

            return returnList;
        }

        public virtual List<RegionResourceVersion> GetGamePreloadZip()
        {
            // Initialize the return list
            List<RegionResourceVersion> returnList = new();

            // If the preload is not exist, then return null
            if (GameAPIProp.data.pre_download_game == null)
            {
                return null;
            }

            // Try get the diff file  by the first or default (null)
            RegionResourceVersion diff = GameAPIProp.data.pre_download_game?.diffs?
                                                    .FirstOrDefault(x => x.version == GameVersionInstalled?.VersionString);

            // Return if the diff is null, then get the latest. If found, then return the diff one.
            returnList.Add(diff ?? GameAPIProp.data.pre_download_game?.latest);
            if (GameAPIProp.data.sdk != null)
            {
                returnList.Add(GameAPIProp.data.sdk);
            }

            return returnList;
        }

        public virtual DeltaPatchProperty GetDeltaPatchInfo()
        {
            return null;
        }

        public virtual bool IsGameHasPreload()
        {
            return GameAPIProp.data.pre_download_game != null;
        }

        public virtual bool IsGameHasDeltaPatch()
        {
            return false;
        }

        public virtual bool IsGameVersionMatch()
        {
            // Ensure if the GameVersionInstalled is available (this is coming from the Game Profile's Ini file.
            // If not, then return false to indicate that the game isn't installed.
            if (!GameVersionInstalled.HasValue)
            {
                return false;
            }

            // If the game is installed and the version doesn't match, then return to false.
            // But if the game version matches, then return to true.
            return GameVersionInstalled.Value.IsMatch(GameVersionAPI);
        }

        public virtual bool IsPluginVersionsMatch()
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
                    MismatchPlugin = CheckPluginUpdate();
                    if (MismatchPlugin.Count == 0)
                    {
                        // Update cached plugin versions
                        PluginVersionsInstalled = PluginVersionsAPI;
                        return true;
                    }

                    return false;
                }
            }

            return true;
        #endif
        }

        public virtual List<RegionResourcePlugin> CheckPluginUpdate()
        {
            List<RegionResourcePlugin> result = [];
            if (GameAPIProp.data?.plugins == null)
            {
                return result;
            }

            foreach (var plugin in GameAPIProp.data?.plugins!)
            {
                foreach (var validate in plugin.package.validate)
                {
                    var path = Path.Combine(GameDirPath, validate.path);
                    try
                    {
                        using var fs  = new FileStream(path, FileMode.Open, FileAccess.Read);
                        var       md5 = HexTool.BytesToHexUnsafe(MD5.HashData(fs));
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
            // Try find the base game path from the executable location.
            string basePath = TryFindGamePathFromExecutableAndConfig(path);

            // If the executable file and version config doesn't exist (null), then return null.
            if (basePath == null)
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
    #nullable disable

        public virtual DeltaPatchProperty CheckDeltaPatchUpdate(string      gamePath, string profileName,
                                                                GameVersion gameVersion)
        {
            // If GameVersionInstalled doesn't have a value (null). then return null.
            if (!GameVersionInstalled.HasValue)
            {
                return null;
            }

            // Get the pre-load status
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
                        && GameVersionAPI.IsMatch(patchProperty.TargetVer)
                        && patchProperty.ProfileName == GamePreset.ProfileName)
                    {
                        return patchProperty;
                    }

                    // If the state is on pre-load, then try check the pre-load delta patch
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
            GameIniVersion[_defaultIniVersionSection]["game_version"] = GameVersionAPI.VersionString;
            if (saveValue)
            {
                SaveGameIni(GameIniVersionPath, GameIniVersion);
                UpdateGameChannels();
                UpdatePluginVersions(PluginVersionsAPI);
            }
        }

        public void UpdateGameVersion(GameVersion version, bool saveValue = true)
        {
            GameIniVersion[_defaultIniVersionSection]["game_version"] = version.VersionString;
            if (saveValue)
            {
                SaveGameIni(GameIniVersionPath, GameIniVersion);
            }
        }

        public void UpdateGameChannels(bool saveValue = true)
        {
            bool isBilibili = GamePreset.ZoneName == "Bilibili";
            GameIniVersion[_defaultIniVersionSection]["channel"]     = gameChannelID;
            GameIniVersion[_defaultIniVersionSection]["sub_channel"] = gameSubChannelID;

            if (isBilibili)
            {
                GameIniVersion[_defaultIniVersionSection]["cps"] = "bilibili";
            }
            // Remove the contains section if the client is not Bilibili and it does have the value.
            // This to avoid an issue with HSR config.ini detection
            else if (GameIniVersion.ContainsSection(_defaultIniVersionSection)
                          && GameIniVersion[_defaultIniVersionSection].ContainsKey("cps")
                          && GameIniVersion[_defaultIniVersionSection]["cps"].ToString() == "bilibili")
            {
                GameIniVersion[_defaultIniVersionSection].Remove("cps");
            }

            if (saveValue)
            {
                SaveGameIni(GameIniVersionPath, GameIniVersion);
            }
        }

        public void UpdatePluginVersions(Dictionary<int, GameVersion> versions, bool saveValue = true)
        {
            // If the plugin is empty, ignore it
            if (GameAPIProp.data?.plugins == null || GameAPIProp.data?.plugins?.Count == 0)
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

        protected virtual void TryReinitializeGameVersion()
        {
            // Check if the GameVersionInstalled == null (version config doesn't exist),
            // Reinitialize the version config and save the version config by assigning GameVersionInstalled.
            if (GameVersionInstalled == null)
            {
                GameVersionInstalled = GameVersionAPI;
            }
        }

        private void InitializeIniProp()
        {
            GameConfigDirPath = Path.Combine(LauncherConfig.AppGameFolder, GamePreset.ProfileName ?? string.Empty);

            // Initialize INIs
            if (GamePreset.ZoneName == "Bilibili")
            {
                InitializeIniProp(GameIniProfilePath, GameIniProfile, _defaultIniProfileBilibili,
                                  _defaultIniProfileSection);
                InitializeIniProp(GameIniVersionPath, GameIniVersion, _defaultIniVersionBilibili,
                                  _defaultIniVersionSection);
            }
            else
            {
                InitializeIniProp(GameIniProfilePath, GameIniProfile, _defaultIniProfile, _defaultIniProfileSection);
                InitializeIniProp(GameIniVersionPath, GameIniVersion, _defaultIniVersion, _defaultIniVersionSection);
            }

            // Initialize the GameVendorType
            VendorTypeProp = new GameVendorProp(GameDirPath,
                                                Path.GetFileNameWithoutExtension(GamePreset.GameExecutableName) ?? string.Empty,
                                                GamePreset.VendorType);
        }

        private string TryFindGamePathFromExecutableAndConfig(string path)
        {
            // Phase 1: Check on the root directory
            string   targetPath = Path.Combine(path, GamePreset.GameExecutableName ?? string.Empty);
            string   configPath = Path.Combine(path, "config.ini");
            FileInfo targetInfo = new FileInfo(targetPath);
            if (targetInfo.Exists && targetInfo.Length > 1 << 16 && File.Exists(configPath))
            {
                return Path.GetDirectoryName(targetPath);
            }

            // Phase 2: Check on the launcher directory + GamePreset.GameDirectoryName
            targetPath = Path.Combine(path, GamePreset.GameDirectoryName ?? "Games", GamePreset.GameExecutableName ?? string.Empty);
            configPath = Path.Combine(path, GamePreset.GameDirectoryName ?? "Games", "config.ini");
            targetInfo = new FileInfo(targetPath);
            if (targetInfo.Exists && targetInfo.Length > 1 << 16 && File.Exists(configPath))
            {
                return Path.GetDirectoryName(targetPath);
            }

            // If none of them passes, then return null.
            return null;
        }

        private bool IsTryParseIniVersionExist(string iniPath)
        {
            // Load version config file.
            IniFile iniFile = new IniFile();
            iniFile.Load(iniPath);

            // Check whether the config has game_version value and it must be a non-null value.
            if (iniFile[_defaultIniVersionSection].ContainsKey("game_version"))
            {
                string val = iniFile[_defaultIniVersionSection]["game_version"].ToString();
                if (val != null)
                {
                    return true;
                }
            }

            // If above doesn't passes, then return false.
            return false;
        }

        private bool IsDiskPartitionExist(string path) => !string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(Path.GetPathRoot(path)) && new DriveInfo(Path.GetPathRoot(path) ?? string.Empty).IsReady;

        private void SaveGameIni(string filePath, in IniFile INI)
        {
            // Check if the disk partition exist. If it's exist, then save the INI.
            if (IsDiskPartitionExist(filePath))
            {
                INI.Save(filePath);
            }
        }

        private void InitializeIniProp(string iniFilePath, in IniFile ini, IniSection defaults, string section)
        {
            // Get the file path of the INI file and normalize it
            iniFilePath = ConverterTool.NormalizePath(iniFilePath);
            string iniDirPath = Path.GetDirectoryName(iniFilePath);

            // Check if the disk partition is ready (exist)
            bool IsDiskReady = IsDiskPartitionExist(iniDirPath);

            // Create the directory of the gile if doesn't exist
            if (!Directory.Exists(iniDirPath) && IsDiskReady)
            {
                Directory.CreateDirectory(iniDirPath);
            }

            // Load the INI file.
            if (IsDiskReady)
            {
                ini.Load(iniFilePath, false, true);
            }

            // Initialize and ensure the non-existed values to their defaults.
            InitializeIniDefaults(ini, defaults, section);

            // Always save the file to ensure file existency
            SaveGameIni(iniFilePath, ini);
        }

        private void InitializeIniDefaults(in IniFile ini, IniSection defaults, string section)
        {
            // If the section doesn't exist, then add the section.
            if (!ini.ContainsSection(section))
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
            }

            UpdateGameChannels(false);
        }
    }
}