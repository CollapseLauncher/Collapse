using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.Region;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

#nullable enable
namespace CollapseLauncher.GameManagement.Versioning
{
    internal partial class GameVersionBase
    {
        #region Config Constants
        private const string DefaultIniProfileSection = "launcher";
        private const string DefaultIniVersionSection = "General";
        #endregion

        #region Default Config Properties
        protected virtual int     DefaultGameChannelID    => GamePreset.ChannelID ?? 0;
        protected virtual int     DefaultGameSubChannelID => GamePreset.SubChannelID ?? 0;
        protected virtual string? DefaultGameCps          => GamePreset.LauncherCPSType;
        protected virtual string  DefaultGameDirPath      => Path.Combine(LauncherConfig.AppGameFolder,
                                                                          GamePreset.ProfileName ?? string.Empty,
                                                                          GamePreset.GameDirectoryName ?? string.Empty);

        protected virtual IniSection DefaultIniProfile => new()
            {
                { "cps", new IniValue(GamePreset.LauncherCPSType) },
                { "channel", new IniValue(DefaultGameChannelID) },
                { "sub_channel", new IniValue(DefaultGameSubChannelID) },
                { "game_install_path", new IniValue(DefaultGameDirPath.Replace('\\', '/')) },
                { "game_start_name", new IniValue(GamePreset.GameExecutableName) },
                { "is_first_exit", new IniValue(false) },
                { "exit_type", new IniValue(2) }
            };

        protected virtual IniSection DefaultIniVersion => new()
            {
                { "channel", new IniValue(DefaultGameChannelID) },
                { "cps", new IniValue(GamePreset.LauncherCPSType) },
                { "game_version", new IniValue() },
                { "sub_channel", new IniValue(DefaultGameSubChannelID) },
                { "sdk_version", new IniValue() },
                { "uapc", GenerateUapcValue() }
            };
        #endregion

        #region Game Config Properties
        protected virtual IniFile GameIniProfile     { get; } = new();
        protected virtual IniFile GameIniVersion     { get; } = new();

        [field: AllowNull, MaybeNull]
        public virtual IniSection GameIniVersionSection { get => field ??= GameIniVersion[DefaultIniVersionSection]; }

        [field: AllowNull, MaybeNull]
        public virtual IniSection GameIniProfileSection { get => field ??= GameIniVersion[DefaultIniProfileSection]; }
        #endregion

        #region Game Config Path Properties
        protected virtual string? GameConfigDirPath { get; set; }

        [field: AllowNull, MaybeNull]
        protected virtual string GameIniProfilePath => field ??= Path.Combine(GameConfigDirPath ?? "", "config.ini");

        [field: AllowNull, MaybeNull]
        protected virtual string GameIniVersionPath
        {
            get
            {
                if (field != null)
                {
                    return field;
                }

                var configPath = GameIniProfile[DefaultIniProfileSection]["game_install_path"].ToString();
                var defaultPath = Path.Combine(GameConfigDirPath ?? "", GamePreset.GameDirectoryName ?? "Games", "config.ini");

                if (string.IsNullOrEmpty(configPath)) return defaultPath;

                string path = ConverterTool.NormalizePath(Path.Combine(configPath, "config.ini"));
                return field = IsDiskPartitionExist(path) ? path : defaultPath;
            }
        }

        [field: AllowNull, MaybeNull]
        public virtual string GameDirPath
        {
            get => field ??= Path.GetDirectoryName(GameIniVersionPath) ?? string.Empty;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    value = string.Empty;
                }
                UpdateGamePath(field = value, false);
                UpdateGameChannels();
            }
        }

        [field: AllowNull, MaybeNull]
        public virtual string GameDirAppDataPath
            => field ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                      "AppData",
                                      "LocalLow",
                                      $"{VendorTypeProp.VendorType}",
                                      GamePreset.InternalGameNameInConfig ?? string.Empty);

        public virtual string GameOutputLogName
            => GameType switch
            {
                GameNameType.Genshin => "output_log.txt",
                GameNameType.Honkai => "output_log.txt",
                _ => "Player.log"
            };
        #endregion

        #region Default Overridable Value Methods
        protected virtual string GenerateUapcValue()
        {
            if (string.IsNullOrEmpty(GamePreset.LauncherBizName))
            {
                Logger.LogWriteLine($"Biz name in the game preset for {GamePreset.ProfileName} is empty! Cannot generate UAPC value", LogType.Warning, true);
                return string.Empty;
            }

            Dictionary<string, Dictionary<string, string>> uapc = new()
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
            string uapcValue = uapc.Serialize(GenericJsonContext.Default.DictionaryStringDictionaryStringString, false);
            return uapcValue;
        }
        #endregion

        #region Fix Game Config Methods
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
            const string channelIdKeyName = "channel";
            const string subChannelIdKeyName = "sub_channel";
            const string cpsKeyName = "cps";

            string gameIniVersionPath = Path.Combine(GameDirPath, "config.ini");

            GameIniVersion[DefaultIniVersionSection][channelIdKeyName] = GamePreset.ChannelID ?? 0;
            GameIniVersion[DefaultIniVersionSection][subChannelIdKeyName] = GamePreset.SubChannelID ?? 0;
            GameIniVersion[DefaultIniVersionSection][cpsKeyName] = GamePreset.LauncherCPSType;

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
        #endregion

        #region Update Game Config Methods
        public void UpdateGamePath(string path, bool saveValue = true)
        {
            GameIniProfile[DefaultIniProfileSection]["game_install_path"] = path.Replace('\\', '/');
            if (saveValue)
            {
                SaveGameIni(GameIniProfilePath, GameIniProfile);
            }
        }

        public void UpdateGameVersionToLatest(bool saveValue = true)
        {
            GameIniVersion[DefaultIniVersionSection]["game_version"] = GameVersionAPI?.VersionString;
            if (!saveValue)
            {
                return;
            }

            SaveGameIni(GameIniVersionPath, GameIniVersion);
            UpdateGameChannels();
            UpdatePluginVersions(PluginVersionsAPI);
        }

        public void UpdateGameVersion(GameVersion? version, bool saveValue = true)
        {
            GameIniVersion[DefaultIniVersionSection]["game_version"] = version?.VersionString;
            if (saveValue)
            {
                SaveGameIni(GameIniVersionPath, GameIniVersion);
            }
        }

        public void UpdateGameChannels(bool saveValue = true)
        {
            GameIniVersion[DefaultIniVersionSection]["channel"] = DefaultGameChannelID;
            GameIniVersion[DefaultIniVersionSection]["sub_channel"] = DefaultGameSubChannelID;
            GameIniVersion[DefaultIniVersionSection]["cps"] = DefaultGameCps;

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
            if (versions.Count == 0)
            {
                return;
            }

            // Get the plugin property and its key name
            foreach (KeyValuePair<string, GameVersion> version in versions)
            {
                string keyName = $"plugin_{version.Key}_version";

                // Set the value
                GameIniVersion[DefaultIniVersionSection][keyName] = version.Value.VersionString;
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
            if (GameApiProp.data?.sdk == null)
                return;

            // Set the value
            const string keyName = "plugin_sdk_version";
            GameIniVersion[DefaultIniVersionSection][keyName] = version.ToString();

            if (saveValue)
            {
                SaveGameIni(GameIniVersionPath, GameIniVersion);
            }
        }
        #endregion

        #region Find Game Config Methods
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
            return IsTryParseIniVersionExist(iniPath) ? basePath :
                // If the file doesn't exist, return as null.
                null;
        }

        protected virtual string? TryFindGamePathFromExecutableAndConfig(string path, string? executableName)
        {
            // Phase 1: Check on the root directory
            string   targetPath = Path.Combine(path, executableName ?? string.Empty);
            string   configPath = Path.Combine(path, "config.ini");
            FileInfo targetInfo = new FileInfo(targetPath);
            if (targetInfo is { Exists: true, Length: > 1 << 16 } && File.Exists(configPath))
            {
                return Path.GetDirectoryName(targetPath);
            }

            // Phase 2: Check on the launcher directory + GamePreset.GameDirectoryName
            targetPath = Path.Combine(path, GamePreset.GameDirectoryName ?? "Games", executableName ?? string.Empty);
            configPath = Path.Combine(path, GamePreset.GameDirectoryName ?? "Games", "config.ini");
            targetInfo = new FileInfo(targetPath);
            if (targetInfo is { Exists: true, Length: > 1 << 16 } && File.Exists(configPath))
            {
                return Path.GetDirectoryName(targetPath);
            }

            // If none of them passes, then return null.
            Logger.LogWriteLine("[TryFindGamePathFromExecutableAndConfig] Fail!");
            return null;
        }
        #endregion

        #region Load / Save Game Config
        public virtual void Reinitialize() => InitializeIniProp();

        public virtual void InitializeIniProp()
        {
            GameConfigDirPath = Path.Combine(LauncherConfig.AppGameFolder, GamePreset.ProfileName ?? string.Empty);

            // Initialize INIs
            InitializeIniProp(GameIniProfilePath, GameIniProfile, DefaultIniProfile,
                              DefaultIniProfileSection);
            InitializeIniProp(GameIniVersionPath, GameIniVersion, DefaultIniVersion,
                              DefaultIniVersionSection, true);

            // Initialize the GameVendorType
            VendorTypeProp = new GameVendorProp(GameDirPath,
                                                Path.GetFileNameWithoutExtension(GamePreset.GameExecutableName) ?? string.Empty,
                                                GamePreset.VendorType);
        }

        private void InitializeIniProp(string iniFilePath, IniFile ini, IniSection defaults, string section, bool allowOverwriteUnmatchedValues = false)
        {
            // Get the file path of the INI file and normalize it
            iniFilePath = ConverterTool.NormalizePath(iniFilePath);
            string? iniDirPath = Path.GetDirectoryName(iniFilePath);

            // Check if the disk partition is ready (exist)
            bool isDiskReady = IsDiskPartitionExist(iniDirPath);

            // Load the existing INI file if only the file exist.
            if (isDiskReady && File.Exists(iniFilePath))
            {
                ini.Load(iniFilePath);
            }

            // Initialize and ensure the non-existed values to their defaults.
            InitializeIniDefaults(ini, defaults, section, allowOverwriteUnmatchedValues);
        }

        private void InitializeIniDefaults(IniFile ini, IniSection defaults, string section, bool allowOverwriteUnmatchedValues)
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
                else if (allowOverwriteUnmatchedValues
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

        private void SaveGameIni(string filePath, IniFile ini)
        {
            // Check if the disk partition exist. If it's exist, then save the INI.
            if (IsDiskPartitionExist(filePath))
            {
                ini.Save(filePath);
            }
        }
        #endregion
    }
}
