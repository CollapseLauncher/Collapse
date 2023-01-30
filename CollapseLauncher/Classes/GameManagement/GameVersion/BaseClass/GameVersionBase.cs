using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.IO;

namespace CollapseLauncher.GameVersioning
{
    internal class GameVersionBase
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

        public PresetConfigV2 GamePreset { get; set; }
        public RegionResourceProp GameAPIProp { get; set; }

        protected readonly IniFile GameIniProfile = new IniFile();
        protected readonly IniFile GameIniVersion = new IniFile();
        protected string GameIniProfilePath { get => Path.Combine(GameConfigDirPath, "config.ini"); }
        protected string GameIniVersionPath { get => Path.Combine(GameIniProfile[_defaultIniProfileSection]["game_install_path"].ToString(), "config.ini"); }
        protected string GameConfigDirPath { get; set; }
        public string GameDirPath
        {
            get => Path.GetDirectoryName(GameIniVersionPath);
            set => UpdateGamePath(value);
        }
        protected GameVersion GameVersionAPI
        {
            get
            {
                return new GameVersion(GameAPIProp.data.game.latest.version);
            }
        }
        protected GameVersion? GameVersionInstalled
        {
            get
            {
                if (GameIniVersion[_defaultIniVersionSection].ContainsKey("game_version"))
                {
                    string val = GameIniVersion[_defaultIniVersionSection]["game_version"].ToString();
                    if (string.IsNullOrEmpty(val)) return null;
                    return new GameVersion(val);
                }

                return null;
            }
            set => UpdateGameVersion(value ?? GameVersionAPI);
        }
        protected UIElement ParentUIElement { get; init; }

        protected GameVersionBase(UIElement parentUIElement, RegionResourceProp gameRegionProp, PresetConfigV2 gamePreset)
        {
            GamePreset = gamePreset;
            ParentUIElement = parentUIElement;
            GameAPIProp = gameRegionProp;
            GameConfigDirPath = Path.Combine(LauncherConfig.AppGameFolder, GamePreset.ProfileName);

            InitializeIniProp(GameIniProfilePath, ref GameIniProfile, _defaultIniProfile, _defaultIniProfileSection);
            InitializeIniProp(GameIniVersionPath, ref GameIniVersion, _defaultIniVersion, _defaultIniVersionSection);
        }

        private void InitializeIniProp(string iniFilePath, ref IniFile ini, IniSection defaults, string section)
        {
            iniFilePath = ConverterTool.NormalizePath(iniFilePath);
            string iniDirPath = Path.GetDirectoryName(iniFilePath);

            if (!Directory.Exists(iniDirPath))
            {
                Directory.CreateDirectory(iniDirPath);
            }

            ini.Load(iniFilePath, false, true);

            InitializeIniDefaults(ref ini, defaults, section);
        }

        private void InitializeIniDefaults(ref IniFile ini, IniSection defaults, string section)
        {
            if (!ini.ContainsSection(section))
            {
                ini.Add(section);
            }

            foreach (KeyValuePair<string, IniValue> value in defaults)
            {
                if (!ini[section].ContainsKey(value.Key))
                {
                    ini[section].Add(value.Key, value.Value);
                }
            }
        }

        public GameVersion? GetGameExistingVersion() => GameVersionInstalled;
        public GameVersion GetGameVersionAPI() => GameVersionAPI;

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
            if (!GameVersionInstalled.HasValue) return false;
            FileInfo execFileInfo = new FileInfo(Path.Combine(GameDirPath, $"{GamePreset.GameExecutableName}.exe"));

            if (execFileInfo.Exists) return execFileInfo.Length > 2 << 20;

            FileInfo identityFile = new FileInfo(Path.Combine(GameDirPath, string.Format(@"{0}_Data\app.info", Path.GetFileNameWithoutExtension(GamePreset.GameExecutableName))));
            if (!identityFile.Exists) return false;

            string[] infoLines = File.ReadAllLines(identityFile.FullName);
            foreach (string line in infoLines)
            {
                if (line == GamePreset.InternalGameNameInConfig) return true;
            }

            return false;
        }

        public void UpdateGamePath(string path)
        {
            GameIniProfile[_defaultIniProfileSection]["game_install_path"] = path;
            GameIniProfile.Save(GameIniProfilePath);
        }

        public void UpdateGameVersionToLatest()
        {
            GameIniVersion[_defaultIniVersionSection]["game_version"] = GameVersionAPI.VersionString;
            GameIniVersion.Save(GameIniVersionPath);
        }

        public void UpdateGameVersion(GameVersion version)
        {
            GameIniVersion[_defaultIniVersionSection]["game_version"] = version.VersionString;
            GameIniVersion.Save(GameIniVersionPath);
        }
    }
}
