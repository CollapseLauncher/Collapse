using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;
using Hi3Helper.Data;

using Hi3Helper.Data;
using static Hi3Helper.Logger;

namespace Hi3Helper.Preset
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

        public void SetGameVersion()
        {
            try
            {
                IniFile ini = new IniFile();
                ini.Load(Path.Combine(ActualGameDataLocation, "config.ini"));

                GameVersion = ini["General"]["game_version"].ToString().Replace('.', '_');
            }
            catch (Exception e)
            {
                throw new Exception($"There's something wrong while reading config.ini file.\r\nTraceback: {e}");
            }
        }

        public string GetUsedLanguage(string RegLocation, string RegValueWildCard, string FallbackValue)
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
                IniFile ini = new IniFile();
                ini.Load(Path.Combine(a, "config.ini"));

                string path = ConverterTool.NormalizePath(ini["game_install_path"]["launcher"].ToString());
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

        public bool CheckExistingGame()
        {
            string RegValue = "InstallPath";
            bool ret = true;

            string a;
            try
            {
                a = (string)Registry.GetValue(InstallRegistryLocation, RegValue, null);
                if (a == null)
                {
                    ret = false;
                    throw new NullReferenceException($"Registry for \"{ZoneName}\" version doesn't exist, probably the version isn't installed.");
                }

                if (!Directory.Exists(a))
                {
                    if (Directory.Exists(DefaultGameLocation))
                    {
                        ActualGameLocation = DefaultGameLocation;
                        LogWriteLine($"Registered path for {ZoneName} version doesn't exist. But the default path does exist!", LogType.Warning);
                        return true;
                    }
                    ret = false;
                    throw new DirectoryNotFoundException($"Registry does exist but the registered directory for \"{ZoneName}\" version seems to be missing!");
                }
                else
                {
                    ActualGameLocation = a;
                    LogWriteLine($"\u001b[34;1m{ZoneName}\u001b[0m (\u001b[32;1m{Path.GetFileName(ConfigRegistryLocation)}\u001b[0m) version is detected!");
                }

                CheckExistingGameConfig(ref ret);
            }
            catch (DirectoryNotFoundException e)
            {
                LogWriteLine(e.ToString(), LogType.Warning, true);
            }
            catch (NullReferenceException e)
            {
                LogWriteLine(e.ToString(), LogType.Warning, true);
            }
            catch (Exception e)
            {
                LogWriteLine(e.ToString(), LogType.Error, true);
            }

            return ret;
        }

        void CheckExistingGameConfig(ref bool ret)
        {
            string iniPath = Path.Combine(ActualGameLocation, "config.ini");
            if (File.Exists(iniPath))
            {
                IniFile iniFile = new IniFile();
                iniFile.Load(iniPath);

                try
                {
                    ActualGameDataLocation = ConverterTool.NormalizePath(iniFile["launcher"]["game_install_path"].ToString());

                    if (File.Exists(Path.Combine(ActualGameDataLocation, "config.ini"))
                        || File.Exists(Path.Combine(ActualGameDataLocation, "BH3.exe")))
                        ret = true;
                    else
                        ret = false;
                }
                catch { ret = false; }
            }
            else
                ret = false;
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
        public bool IsSteamVersion { get; set; }
        public string LauncherSpriteURL { get; set; }
        public string LauncherResourceURL { get; set; }
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
