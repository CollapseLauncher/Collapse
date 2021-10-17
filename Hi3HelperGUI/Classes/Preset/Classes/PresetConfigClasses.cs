using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;
using Hi3HelperGUI.Data;

using static Hi3HelperGUI.Logger;

namespace Hi3HelperGUI.Preset
{
    public class PresetConfigClasses
    {
        public void SetGameLocationParameters(string gameLocation)
        {
            ActualGameLocation = gameLocation;
            UsedLanguage = GetUsedLanguage(ConfigRegistryLocation, "MIHOYOSDK_NOTICE_LANGUAGE_", FallbackLanguage);
            SetFallbackGameFolder(gameLocation);
            SetGameVersion();
        }

        void SetGameVersion()
        {
            try
            {
                IniParser Ini = new(Path.Combine(ActualGameDataLocation, "config.ini"));
                GameVersion = Ini.Read("game_version", "General").Replace('.', '_');
            }
            catch (Exception e)
            {
                throw new Exception($"There's something wrong while reading config.ini file.\r\nTraceback: {e}");
            }
        }

        string GetUsedLanguage(string RegLocation, string RegValueWildCard, string FallbackValue)
        {
            string value = "";
            try
            {
                RegistryKey keys = Registry.CurrentUser.OpenSubKey(RegLocation);
                foreach (string valueName in keys.GetValueNames())
                    if (valueName.Contains(RegValueWildCard))
                        value = valueName;

                return Encoding.UTF8.GetString((byte[])Registry.GetValue($"HKEY_CURRENT_USER\\{RegLocation}", value, FallbackValue)).Replace("\0", string.Empty);
            }
            catch
            {
                LogWriteLine($"Language registry on \u001b[32;1m{Path.GetFileName(RegLocation)}\u001b[0m version doesn't exist. Fallback value will be used.", LogType.Warning);
                return FallbackValue;
            }
        }

        void SetFallbackGameFolder(string a, bool tryFallback = false)
        {
            if (File.Exists(Path.Combine(a, "Games", "bh3.exe")))
            {
                ActualGameDataLocation = Path.Combine(a, "Games");
            }
            else if (tryFallback)
            {
                IniParser Ini = new(Path.Combine(a, "config.ini"));
                string path = ConverterTool.NormalizePath(Ini.Read("game_install_path", "launcher"));
                if (!Directory.Exists(path))
                {
                    if (Directory.Exists(Path.Combine(DefaultGameLocation, "Games")))
                        ActualGameDataLocation = Path.Combine(DefaultGameLocation, "Games");
                    else
                        throw new DirectoryNotFoundException($"\"{a}\" directory doesn't exist");
                }

                return;
            }
            else
            {
                SetFallbackGameFolder(a, true);
            }
        }

        public string ProfileName { get; set; }
        public string ZoneName { get; set; }
        public string InstallRegistryLocation { get; set; }
        public string ConfigRegistryLocation { get; set; }
        public string ActualGameLocation { get; set; }
        public string ActualGameDataLocation { get; set; }
        public string DefaultGameLocation { get; set; }
        public string DictionaryHost { get; set; }
        public string UpdateDictionaryAddress { get; set; }
        public string BlockDictionaryAddress { get; set; }
        public string GameVersion { get; set; }
        public string UsedLanguage { get; set; }
        public string FallbackLanguage { get; set; }
        public Dictionary<string, MirrorUrlMember> MirrorList { get; set; }
        public List<string> LanguageAvailable { get; set; }
    }

    public class AppSettings
    {
        public bool ShowConsole { get; set; }
        public ushort SupportedGameVersion { get; set; }
        public ushort PreviousGameVersion { get; set; }
        public byte MirrorSelection { get; set; }
        public List<string> AvailableMirror { get; set; }
    }
    public class MirrorUrlMember
    {
        public string AssetBundle { get; set; }
        public string Bigfile { get; set; }
    }

    public class UpdateDataProperties
    {
        public string N { get; set; }
        public long CS { get; set; }
        public long ECS { get; set; }
        public string CRC { get; set; }
        public string ActualPath { get; set; }
        public string HumanizeSize { get; set; }
        public string RemotePath { get; set; }
        public string ZoneName { get; set; }
        public string DataType { get; set; }
        public string DownloadStatus { get; set; } = "Not yet downloaded";
    }
}
