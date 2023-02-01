using CollapseLauncher.Interfaces;
using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.IO;

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
        protected string GameIniVersionPath { get => Path.Combine(GameIniProfile[_defaultIniProfileSection]["game_install_path"].ToString(), "config.ini"); }
        protected string GameConfigDirPath { get; set; }
        public GameVersionBase AsVersionBase => this;
        public PresetConfigV2 GamePreset { get; set; }
        public RegionResourceProp GameAPIProp { get; set; }
        public string GameDirPath
        {
            get => Path.GetDirectoryName(GameIniVersionPath);
            set => UpdateGamePath(value);
        }
        protected UIElement ParentUIElement { get; init; }
        protected GameVersion GameVersionAPI => new GameVersion(GameAPIProp.data.game.latest.version);
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
            GameConfigDirPath = Path.Combine(LauncherConfig.AppGameFolder, GamePreset.ProfileName);

            // Initialize INIs
            InitializeIniProp(GameIniProfilePath, ref GameIniProfile, _defaultIniProfile, _defaultIniProfileSection);
            InitializeIniProp(GameIniVersionPath, ref GameIniVersion, _defaultIniVersion, _defaultIniVersionSection);
        }

        public GameVersion? GetGameExistingVersion() => GameVersionInstalled;

        public GameVersion GetGameVersionAPI() => GameVersionAPI;

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

        public virtual List<RegionResourceVersion> GetGameLatestZip() => new List<RegionResourceVersion> { GameAPIProp.data.game.latest };

        public virtual List<RegionResourceVersion> GetGamePreloadZip() => GameAPIProp.data.pre_download_game == null ? null : new List<RegionResourceVersion> { GameAPIProp.data.pre_download_game.latest };

        public virtual DeltaPatchProperty GetDeltaPatchInfo() => null;

        public virtual bool IsGameHasPreload() => IsGameVersionMatch() && !IsGameHasDeltaPatch() && GameAPIProp.data.pre_download_game != null;

        public virtual bool IsGameHasDeltaPatch() => false;

        public bool IsGameVersionMatch()
        {
            // Ensure if the GameVersionInstalled is available (this is coming from the Game Profile's Ini file.
            // If not, then return false to indicate that the game isn't installed.
            if (!GameVersionInstalled.HasValue) return false;

            // If the game is installed and the version doesn't match, then return to false.
            // But if the game version matches, then return to true.
            return GameVersionInstalled.Value.IsMatch(GameVersionAPI);
        }

        public bool IsGameInstalled()
        {
            // If the GameVersionInstalled doesn't have a value (not null), then return as false.
            if (!GameVersionInstalled.HasValue) return false;

            // Check if the executable file exist and has the size at least > 2 MiB. If not, then return as false.
            FileInfo execFileInfo = new FileInfo(Path.Combine(GameDirPath, $"{GamePreset.GameExecutableName}.exe"));
            if (execFileInfo.Exists) return execFileInfo.Length > 2 << 20;

            // Check if the identifier file exist. If not, then return as false.
            FileInfo identityFile = new FileInfo(Path.Combine(GameDirPath, string.Format(@"{0}_Data\app.info", Path.GetFileNameWithoutExtension(GamePreset.GameExecutableName))));
            if (!identityFile.Exists) return false;

            // Read the lines and check if one of the identifier matches.
            // Once it matches, then return as true.
            string[] infoLines = File.ReadAllLines(identityFile.FullName);
            foreach (string line in infoLines)
            {
                if (line == GamePreset.InternalGameNameInConfig) return true;
            }

            // If none of above matches, then return as false.
            return false;
        }

        public void UpdateGamePath(string path)
        {
            GameIniProfile[_defaultIniProfileSection]["game_install_path"] = path.Replace('\\', '/');
            SaveGameIni(GameIniProfilePath, GameIniProfile);
        }

        public void UpdateGameVersionToLatest()
        {
            GameIniVersion[_defaultIniVersionSection]["game_version"] = GameVersionAPI.VersionString;
            SaveGameIni(GameIniVersionPath, GameIniVersion);
        }

        public void UpdateGameVersion(GameVersion version)
        {
            GameIniVersion[_defaultIniVersionSection]["game_version"] = version.VersionString;
            SaveGameIni(GameIniVersionPath, GameIniVersion);
        }
            
        private void SaveGameIni(string filePath, in IniFile INI)
        {
            if (IsDiskPartitionExist(filePath))
            {
                INI.Save(filePath);
            }
        }

        private bool IsDiskPartitionExist(string path)
        {
            DriveInfo info = new DriveInfo(Path.GetPathRoot(path));
            return info.IsReady;
        }

        private void InitializeIniProp(string iniFilePath, ref IniFile ini, IniSection defaults, string section)
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
            InitializeIniDefaults(ref ini, defaults, section);
        }

        private void InitializeIniDefaults(ref IniFile ini, IniSection defaults, string section)
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
