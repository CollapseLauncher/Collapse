using Hi3Helper.Data;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static Hi3Helper.Logger;

namespace Hi3Helper.Preset
{
    public class PresetConfigClasses
    {
        public enum ServerRegionID
        {
            os_usa = 0,
            os_euro = 1,
            os_asia = 2,
            os_cht = 3
        }
        public string GetSteamInstallationPath()
        {
            try
            {
                List<SteamTool.AppInfo> AppList = SteamTool.GetSteamApps(SteamTool.GetSteamLibs());
                string ret = AppList.Where(x => x.Id == SteamGameID).Select(y => y.GameRoot).FirstOrDefault();
                if (ret == null) return null;
                return ConverterTool.NormalizePath(ret);
                // returnval = (string)Registry.GetValue(SteamInstallRegistryLocation, "InstallLocation", null);
            }
            catch
            {
                return null;
            }
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

        public string GetGameLanguage()
        {
            string value = "";
            try
            {
                RegistryKey keys = Registry.CurrentUser.OpenSubKey(ConfigRegistryLocation);

                if (keys == null) return FallbackLanguage;

                foreach (string valueName in keys.GetValueNames())
                    if (valueName.Contains("MIHOYOSDK_NOTICE_LANGUAGE_"))
                        value = valueName;

                return Encoding.UTF8.GetString((byte[])Registry.GetValue($"HKEY_CURRENT_USER\\{ConfigRegistryLocation}", value, FallbackLanguage)).Replace("\0", string.Empty);
            }
            catch
            {
                LogWriteLine($"Language registry on \u001b[32;1m{Path.GetFileName(ConfigRegistryLocation)}\u001b[0m version doesn't exist. Fallback value will be used.", LogType.Warning);
                return FallbackLanguage;
            }
        }

        // WARNING!!!
        // This feature is only available for Genshin.
        public int GetVoiceLanguageID()
        {
            try
            {
                string regValue, value = string.Empty;
                RegistryKey keys = Registry.CurrentUser.OpenSubKey(ConfigRegistryLocation);
                value = keys.GetValueNames().Where(x => x.Contains("GENERAL_DATA")).First();
                regValue = Encoding.UTF8.GetString((byte[])keys.GetValue(value, "{}", RegistryValueOptions.None)).Replace("\0", string.Empty);

                return JsonConvert.DeserializeObject<GeneralDataProp>(regValue).deviceVoiceLanguageType;
            }
            catch
            {
                LogWriteLine($"Voice Language ID registry on \u001b[32;1m{Path.GetFileName(ConfigRegistryLocation)}\u001b[0m doesn't exist. Fallback value will be used (2 / ja-jp).", LogType.Warning);
                return 2;
            }
        }

        // WARNING!!!
        // This feature is only available for Genshin.
        public void SetVoiceLanguageID(int LangID)
        {
            string regValue;
            RegistryKey keys;
            GeneralDataProp initValue = new GeneralDataProp();
            try
            {
                keys = Registry.CurrentUser.OpenSubKey(ConfigRegistryLocation, true);
                regValue = Encoding.UTF8.GetString((byte[])keys.GetValue("GENERAL_DATA_h2389025596", "{}", RegistryValueOptions.None)).Replace("\0", string.Empty);
                initValue = JsonConvert.DeserializeObject<GeneralDataProp>(regValue);
            }
            catch
            {
                keys = Registry.CurrentUser.CreateSubKey(ConfigRegistryLocation);
            }
            initValue.deviceVoiceLanguageType = LangID;
            keys.SetValue("GENERAL_DATA_h2389025596", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(initValue, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include }) + '\0'));
        }

        // WARNING!!!
        // This feature is only available for Genshin.
        public int GetRegServerNameID()
        {
            try
            {
                string regValue, value = string.Empty;
                RegistryKey keys = Registry.CurrentUser.OpenSubKey(ConfigRegistryLocation);
                if (keys == null) return 0;
                value = keys.GetValueNames().Where(x => x.Contains("GENERAL_DATA")).First();
                regValue = Encoding.UTF8.GetString((byte[])keys.GetValue(value, "{}", RegistryValueOptions.None)).Replace("\0", string.Empty);

                return (int)JsonConvert.DeserializeObject<GeneralDataProp>(regValue).selectedServerName;
            }
            catch
            {
                LogWriteLine($"Server name ID registry on \u001b[32;1m{Path.GetFileName(ConfigRegistryLocation)}\u001b[0m doesn't exist. Fallback value will be used (0 / USA).", LogType.Warning);
                return 0;
            }
        }

        // WARNING!!!
        // This feature is only available for Genshin.
        class GeneralDataProp
        {
            public string deviceUUID { get; set; } = "";
            public string userLocalDataVersionId { get; set; } = "0.0.1";
            public int deviceLanguageType { get; set; } = 1;
            public int deviceVoiceLanguageType { get; set; } = 2;
            public ServerRegionID? selectedServerName { get; set; }
            public int localLevelIndex { get; set; } = 0;
            public string deviceID { get; set; } = "";
            public string targetUID { get; set; } = "";
            public string curAccountName { get; set; } = "";
            public string uiSaveData { get; set; } = "";
            public string inputData { get; set; } = "";
            // Initialize 60 fps limit if it's blank
            public string graphicsData { get; set; } = "{\"customVolatileGrades\":[{\"key\":1,\"value\":2}]";
            public string globalPerfData { get; set; } = "";
            public int miniMapConfig { get; set; } = 1;
            public bool enableCameraSlope { get; set; } = true;
            public bool enableCameraCombatLock { get; set; } = true;
            public bool completionPkg { get; set; } = false;
            public bool completionPlayGoPkg { get; set; } = false;
            public bool onlyPlayWithPSPlayer { get; set; } = false;
            public bool needPlayGoFullPkgPatch { get; set; } = false;
            public bool resinNotification { get; set; } = true;
            public bool exploreNotification { get; set; } = true;
            public int volumeGlobal { get; set; } = 10;
            public int volumeSFX { get; set; } = 10;
            public int volumeMusic { get; set; } = 10;
            public int volumeVoice { get; set; } = 10;
            public int audioAPI { get; set; } = -1;
            public int audioDynamicRange { get; set; } = 0;
            // Use Surround by default if it's blank
            public int audioOutput { get; set; } = 1;
            public bool _audioSuccessInit { get; set; } = true;
            public bool enableAudioChangeAndroidMinimumBufferCapacity { get; set; } = true;
            public int audioAndroidMiniumBufferCapacity { get; set; } = 2 << 10;
            public bool motionBlur { get; set; } = true;
            public bool gyroAiming { get; set; } = false;
            public bool firstHDRSetting { get; set; } = true;
            public double maxLuminosity { get; set; } = 0.0f;
            public double uiPaperWhite { get; set; } = 0.0f;
            public double scenePaperWhite { get; set; } = 0.0f;
            public double gammaValue { get; set; } = 2.200000047683716f;
            public IEnumerable<object> _overrideControllerMapKeyList { get; set; } = new List<object>();
            public IEnumerable<object> _overrideControllerMapValueList { get; set; } = new List<object>();
            public int lastSeenPreDownloadTime { get; set; } = 0;
            public bool mtrCached { get; set; } = false;
            public bool mtrIsOpen { get; set; } = false;
            public int mtrMaxTTL { get; set; } = 0x20;
            public int mtrTimeOut { get; set; } = 0x1388;
            public int mtrTraceCount { get; set; } = 5;
            public int mtrAbortTimeOutCount { get; set; } = 3;
            public int mtrAutoTraceInterval { get; set; } = 0;
            public int mtrTraceCDEachReason { get; set; } = 0x258;
            public IEnumerable<object> _customDataKeyList { get; set; } = new List<object>();
            public IEnumerable<object> _customDataValueList { get; set; } = new List<object>();
        }

        void SetFallbackGameFolder(string a, bool tryFallback = false)
        {
            if (File.Exists(Path.Combine(a, GameDirectoryName, GameDirectoryName)))
            {
                ActualGameDataLocation = Path.Combine(a, GameDirectoryName);
            }
            else if (tryFallback)
            {
                IniFile ini = new IniFile();
                ini.Load(Path.Combine(a, "config.ini"));

                string path = ConverterTool.NormalizePath(ini["game_install_path"]["launcher"].ToString());
                if (!Directory.Exists(path))
                {
                    if (Directory.Exists(Path.Combine(DefaultGameLocation, GameDirectoryName)))
                        ActualGameDataLocation = Path.Combine(DefaultGameLocation, GameDirectoryName);
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

        public bool CheckExistingGameBetterLauncher()
        {
            if (BetterHi3LauncherVerInfoReg == null) return false;
            try
            {
                BetterHi3LauncherConfig = JsonConvert.DeserializeObject<BHI3LInfo>(
                    Encoding.UTF8.GetString((byte[])Registry.CurrentUser.OpenSubKey("Software\\Bp\\Better HI3 Launcher").GetValue(BetterHi3LauncherVerInfoReg))
                    );
                if (!File.Exists(Path.Combine(BetterHi3LauncherConfig.game_info.install_path)) && !BetterHi3LauncherConfig.game_info.installed) return false;
                return true;
            }
            catch
            {
                return false;
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
                        || File.Exists(Path.Combine(ActualGameDataLocation, GameExecutableName)))
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
        public string BetterHi3LauncherVerInfoReg { get; set; }
        public BHI3LInfo BetterHi3LauncherConfig { get; private set; }
        public bool MigrateFromBetterHi3Launcher { get; set; } = false;
        public string FallbackLanguage { get; set; }
        public bool IsSteamVersion { get; set; }
        public string SteamInstallRegistryLocation { get; set; }
        public int SteamGameID { get; set; }
        public string GameDirectoryName { get; set; }
        public string GameExecutableName { get; set; }
        public string ZipFileURL { get; set; }
#nullable enable
        public string? GameDispatchURL { get; set; }
        public string? ProtoDispatchKey { get; set; }
        public string? CachesListAPIURL { get; set; }
        public byte? CachesListGameVerID { get; set; }
        public string? CachesEndpointURL { get; set; }
#nullable disable
        public Dictionary<string, MirrorUrlMember> MirrorList { get; set; }
        public List<string> LanguageAvailable { get; set; }
        public bool? IsGenshin { get; set; }
        public bool? IsConvertible { get; set; }
        public bool IsHideSocMedDesc { get; set; } = true;
        public List<string> ConvertibleTo { get; set; }
        public string ConvertibleCookbookURL { get; set; }
        public bool? UseRightSideProgress { get; set; }
        public bool LauncherSpriteURLMultiLang { get; set; } = false;
        public string LauncherSpriteURLMultiLangFallback { get; set; } = "en-us";
        public string LauncherSpriteURL { get; set; }
        public string LauncherResourceURL { get; set; }
        public string LauncherInfoURL { get; set; }
    }

    public class BHI3LInfo
    {
        public BHI3LInfo_GameInfo game_info { get; set; }
    }

    public class BHI3LInfo_GameInfo
    {
        public string version { get; set; }
        public string install_path { get; set; }
        public bool installed { get; set; }
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

    public class PkgVersionProperties
    {
        public string localName { get; set; }
        public string remoteURL { get; set; }
        public string remoteName { get; set; }
        public string md5 { get; set; }
        public long fileSize { get; set; }
        public bool isPatch { get; set; } = false;
    }
}
