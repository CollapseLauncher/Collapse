using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CollapseLauncher.GameVersioning
{
    internal class GameVersionBase : IGameVersionCheck
    {
        #region DefaultPresets
        private const string _defaultIniProfileSection = "launcher";
        private const string _defaultIniVersionSection = "General";
        private string _defaultGameDirPath { get => Path.Combine(LauncherConfig.AppGameFolder, GamePreset.ProfileName, GamePreset.GameDirectoryName); }

        private IniSection _defaultIniProfile
        {
            get => new IniSection()
            {
                { "cps", new IniValue() },
                { "channel", new IniValue("1") },
                { "sub_channel", new IniValue("1") },
                { "game_install_path", new IniValue(_defaultGameDirPath.Replace('\\', '/')) },
                { "game_start_name", new IniValue(GamePreset.GameExecutableName) },
                { "is_first_exit", new IniValue(false) },
                { "exit_type", new IniValue(2) }
            };
        }

        private IniSection _defaultIniVersion
        {
            get => new IniSection()
            {
                { "channel", new IniValue(1) },
                { "cps", new IniValue() },
                { "game_version", new IniValue() },
                { "sub_channel", new IniValue(1) },
                { "sdk_version", new IniValue() }
            };
        }
        #endregion

        #region Properties
        protected readonly IniFile GameIniProfile = new IniFile();
        protected readonly IniFile GameIniVersion = new IniFile();
        protected string GameIniProfilePath { get => Path.Combine(GameConfigDirPath, "config.ini"); }
        protected string GameIniVersionPath
        {
            get
            {
                string path = ConverterTool.NormalizePath(Path.Combine(GameIniProfile[_defaultIniProfileSection]["game_install_path"].ToString(), "config.ini"));
                return IsDiskPartitionExist(path) ? path : Path.Combine(GameConfigDirPath, GamePreset.GameDirectoryName ?? "Games", "config.ini");
            }
        }
        protected string GameConfigDirPath { get; set; }
        public GameVersionBase AsVersionBase => this;
        public PresetConfigV2 GamePreset { get; set; }
        public RegionResourceProp GameAPIProp { get; set; }
        public GameType GameType => GamePreset.GameType;
        public GameVendorProp VendorTypeProp { get; private set; }
        public string GameDirPath
        {
            get => Path.GetDirectoryName(GameIniVersionPath);
            set => UpdateGamePath(value, false);
        }
        public string GameDirAppDataPath
        {
            get => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", $"{VendorTypeProp.VendorType}", GamePreset.InternalGameNameInConfig);
        }
        public string GameOutputLogName
        {
            get => GameType switch
            {
                GameType.Genshin => "output_log.txt",
                GameType.Honkai => "output_log.txt",
                _ => "Player.log"
            };
        }
        protected UIElement ParentUIElement { get; init; }
        protected GameVersion GameVersionAPI => new GameVersion(GameAPIProp.data.game.latest.version);
        protected GameVersion? GameVersionAPIPreload
        {
            get
            {
                GameVersion? currentInstalled = GameVersionInstalled;

                // If no installation installed, then return null
                if (currentInstalled == null) return null;
                // Check if the pre_download_game property has value. If not, then return null
                if (GameAPIProp.data.pre_download_game == null) return null;

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
                    if (string.IsNullOrEmpty(val)) return null;
                    return new GameVersion(val);
                }

                // If not, then return as null
                return null;
            }
            set => UpdateGameVersion(value ?? GameVersionAPI);
        }
        #endregion

        protected GameVersionBase(UIElement parentUIElement, RegionResourceProp gameRegionProp, PresetConfigV2 gamePreset)
        {
            GamePreset = gamePreset;
            ParentUIElement = parentUIElement;
            GameAPIProp = gameRegionProp;

            // Initialize INIs props
            InitializeIniProp();
        }

        public GameVersion? GetGameExistingVersion() => GameVersionInstalled;

        public GameVersion GetGameVersionAPI() => GameVersionAPI;
        public GameVersion? GetGameVersionAPIPreload() => GameVersionAPIPreload;

        public GameInstallStateEnum GetGameState()
        {
            // Check if the game installed first
            // If the game is installed, then move to another step.
            if (IsGameInstalled())
            {
                // Check for the game version and preload availability.
                if (!IsGameVersionMatch()) return GameInstallStateEnum.NeedsUpdate;
                if (IsGameHasPreload()) return GameInstallStateEnum.InstalledHavePreload;

                // If passes, then return as Installed.
                return GameInstallStateEnum.Installed;
            }

            // If none of above passes, then return as NotInstalled.
            return GameInstallStateEnum.NotInstalled;
        }

        public virtual List<RegionResourceVersion> GetGameLatestZip(GameInstallStateEnum gameState)
        {
            // If the GameVersion is not installed, then return the latest one
            if (gameState == GameInstallStateEnum.NotInstalled || gameState == GameInstallStateEnum.GameBroken)
            {
                return new List<RegionResourceVersion> { GameAPIProp.data.game.latest };
            }

            // Try get the diff file  by the first or default (null)
            RegionResourceVersion diff = GameAPIProp.data.game.diffs
                .Where(x => x.version == GameVersionInstalled?.VersionString)
                .FirstOrDefault();

            // Return if the diff is null, then get the latest. If found, then return the diff one.
            return new List<RegionResourceVersion> { diff == null ? GameAPIProp.data.game.latest : diff };
        }

        public virtual List<RegionResourceVersion> GetGamePreloadZip()
        {
            // If the preload is not exist, then return null
            if (GameAPIProp.data.pre_download_game == null) return null;

            // Try get the diff file  by the first or default (null)
            RegionResourceVersion diff = GameAPIProp.data.pre_download_game?.diffs?
                .Where(x => x.version == GameVersionInstalled?.VersionString)
                .FirstOrDefault();

            // Return if the diff is null, then get the latest. If found, then return the diff one.
            return new List<RegionResourceVersion> { diff == null ? GameAPIProp.data.pre_download_game.latest : diff };
        }

        public virtual DeltaPatchProperty GetDeltaPatchInfo() => null;

        public virtual bool IsGameHasPreload() => GameAPIProp.data.pre_download_game != null;

        public virtual bool IsGameHasDeltaPatch() => false;

        public virtual bool IsGameVersionMatch()
        {
            // Ensure if the GameVersionInstalled is available (this is coming from the Game Profile's Ini file.
            // If not, then return false to indicate that the game isn't installed.
            if (!GameVersionInstalled.HasValue) return false;

            // If the game is installed and the version doesn't match, then return to false.
            // But if the game version matches, then return to true.
            return GameVersionInstalled.Value.IsMatch(GameVersionAPI);
        }

        public virtual bool IsGameInstalled()
        {
            // If the GameVersionInstalled doesn't have a value (not null), then return as false.
            if (!GameVersionInstalled.HasValue) return false;

            // Check if the executable file exist and has the size at least > 2 MiB. If not, then return as false.
            FileInfo execFileInfo = new FileInfo(Path.Combine(GameDirPath, GamePreset.GameExecutableName));

            // Check if the vendor type exist. If not, then return false
            if (VendorTypeProp.GameName == null || !VendorTypeProp.VendorType.HasValue) return false;

            // Check all the pattern and return based on the condition
            return VendorTypeProp.GameName == GamePreset.InternalGameNameInConfig && execFileInfo.Exists && execFileInfo.Length > 1 << 16;
        }

#nullable enable
        public virtual string? FindGameInstallationPath(string path)
        {
            // Try find the base game path from the executable location.
            string basePath = TryFindGamePathFromExecutableAndConfig(path);

            // If the executable file and version config doesn't exist (null), then return null.
            if (basePath == null) return null;

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

        public virtual void Reinitialize() => InitializeIniProp();

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

        private void InitializeIniProp()
        {
            GameConfigDirPath = Path.Combine(LauncherConfig.AppGameFolder, GamePreset.ProfileName);

            // Initialize INIs
            InitializeIniProp(GameIniProfilePath, GameIniProfile, _defaultIniProfile, _defaultIniProfileSection);
            InitializeIniProp(GameIniVersionPath, GameIniVersion, _defaultIniVersion, _defaultIniVersionSection);

            // Initialize the GameVendorType
            VendorTypeProp = new GameVendorProp(GameDirPath, Path.GetFileNameWithoutExtension(GamePreset.GameExecutableName), GamePreset.VendorType);
        }

        private string TryFindGamePathFromExecutableAndConfig(string path)
        {
            // Phase 1: Check on the root directory
            string targetPath = Path.Combine(path, GamePreset.GameExecutableName);
            string configPath = Path.Combine(path, "config.ini");
            FileInfo targetInfo = new FileInfo(targetPath);
            if (targetInfo.Exists && targetInfo.Length > 1 << 16 && File.Exists(configPath)) return Path.GetDirectoryName(targetPath);

            // Phase 2: Check on the launcher directory + GamePreset.GameDirectoryName
            targetPath = Path.Combine(path, GamePreset.GameDirectoryName ?? "Games", GamePreset.GameExecutableName);
            configPath = Path.Combine(path, GamePreset.GameDirectoryName ?? "Games", "config.ini");
            targetInfo = new FileInfo(targetPath);
            if (targetInfo.Exists && targetInfo.Length > 1 << 16 && File.Exists(configPath)) return Path.GetDirectoryName(targetPath);

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
                if (val != null) return true;
            }

            // If above doesn't passes, then return false.
            return false;
        }

        private bool IsDiskPartitionExist(string path) =>
            string.IsNullOrEmpty(path) || string.IsNullOrEmpty(Path.GetPathRoot(path)) ? false : new DriveInfo(Path.GetPathRoot(path)).IsReady;

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
        }
    }
}
