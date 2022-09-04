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
#nullable enable
    public class PresetConfigClasses
    {
        public long LastUpdated { get; set; }
        public List<PresetConfigClasses>? Metadata { get; set; }
        public string? MasterKey { get; set; }
        public int MasterKeyBitLength { get; set; }

        public enum ServerRegionID
        {
            os_usa = 0,
            os_euro = 1,
            os_asia = 2,
            os_cht = 3
        }

        public string? GetSteamInstallationPath()
        {
            try
            {
                List<SteamTool.AppInfo> AppList = SteamTool.GetSteamApps(SteamTool.GetSteamLibs());
                string ret = AppList.Where(x => x.Id == SteamGameID).Select(y => y.GameRoot).FirstOrDefault();
                return ret == null ? null : ConverterTool.NormalizePath(ret);
            }
            catch
            {
                return null;
            }
        }

        public string? GetGameLanguage()
        {
            ReadOnlySpan<char> value;
            RegistryKey? keys = Registry.CurrentUser.OpenSubKey(ConfigRegistryLocation);

            byte[]? result = (byte[]?)keys?.GetValue("MIHOYOSDK_CURRENT_LANGUAGE_h2559149783");

            if (keys is null || result is null || result.Length is 0)
            {
                LogWriteLine($"Language registry on \u001b[32;1m{Path.GetFileName(ConfigRegistryLocation)}\u001b[0m version doesn't exist. Fallback value will be used.", LogType.Warning, true);
                return FallbackLanguage;
            }

            value = Encoding.UTF8.GetString(result).AsSpan().Trim('\0');
            return new string(value);
        }

        // WARNING!!!
        // This feature is only available for Genshin.
        public int GetVoiceLanguageID()
        {
            ReadOnlySpan<char> regValue;
            RegistryKey? keys = Registry.CurrentUser.OpenSubKey(ConfigRegistryLocation);
            byte[]? value = (byte[]?)keys?.GetValue("GENERAL_DATA_h2389025596");

            if (keys is null || value is null || value.Length is 0)
            {
                LogWriteLine($"Voice Language ID registry on \u001b[32;1m{Path.GetFileName(ConfigRegistryLocation)}\u001b[0m doesn't exist. Fallback value will be used (2 / ja-jp).", LogType.Warning, true);
                return 2;
            }

            regValue = Encoding.UTF8.GetString(value).AsSpan().Trim('\0');
            return JsonConvert.DeserializeObject<GeneralDataProp>(new string(regValue))?.deviceVoiceLanguageType ?? 2;
        }

        // WARNING!!!
        // This feature is only available for Genshin.
        public void SetVoiceLanguageID(int LangID)
        {
            ReadOnlySpan<char> regValue;
            RegistryKey? keys = Registry.CurrentUser.OpenSubKey(ConfigRegistryLocation, true);
            GeneralDataProp initValue = new GeneralDataProp();
            byte[]? result;

            if (keys is null)
                keys = Registry.CurrentUser.CreateSubKey(ConfigRegistryLocation);
            else
            {
                result = (byte[]?)keys.GetValue("GENERAL_DATA_h2389025596");
                regValue = Encoding.UTF8.GetString(result).AsSpan().Trim('\0');
                initValue = JsonConvert.DeserializeObject<GeneralDataProp>(new string(regValue)) ?? initValue;
            }

            initValue.deviceVoiceLanguageType = LangID;
            keys.SetValue("GENERAL_DATA_h2389025596", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(initValue, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include }) + '\0'));
        }

        // WARNING!!!
        // This feature is only available for Genshin.
        public int GetRegServerNameID()
        {
            if (!IsGenshin ?? true) return 0;
            RegistryKey? keys = Registry.CurrentUser.OpenSubKey(ConfigRegistryLocation);
            byte[]? value = (byte[]?)keys?.GetValue("GENERAL_DATA_h2389025596", "{}", RegistryValueOptions.None);

            if (keys is null || value is null || value.Length is 0)
            {
                LogWriteLine($"Server name ID registry on \u001b[32;1m{Path.GetFileName(ConfigRegistryLocation)}\u001b[0m doesn't exist. Fallback value will be used (0 / USA).", LogType.Warning, true);
                return 0;
            }

            string regValue = new string(Encoding.UTF8.GetString(value).AsSpan().Trim('\0'));

            return (int)(JsonConvert.DeserializeObject<GeneralDataProp>(regValue)?.selectedServerName ?? ServerRegionID.os_usa);
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

        public bool CheckExistingGameBetterLauncher()
        {
            if (BetterHi3LauncherVerInfoReg == null) return false;
            RegistryKey? Key = Registry.CurrentUser.OpenSubKey("Software\\Bp\\Better HI3 Launcher");
            byte[]? Value = (byte[]?)Key?.GetValue(BetterHi3LauncherVerInfoReg);
            
            if (Value is null) return false;

            string? Result = Encoding.UTF8.GetString(Value);
            string? GamwePath;

            try
            {
                BetterHi3LauncherConfig = JsonConvert.DeserializeObject<BHI3LInfo>(Result);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Registry Value {BetterHi3LauncherVerInfoReg}:\r\n{Result}\r\n\r\nException:\r\n{ex}", LogType.Error, true);
                return false;
            };

            if (Key is null || Value.Length is 0 || BetterHi3LauncherConfig is null) return false;

            GamwePath = ConverterTool.NormalizePath(BetterHi3LauncherConfig.game_info.install_path);

            return File.Exists(Path.Combine(GamwePath, GameExecutableName)) && File.Exists(Path.Combine(GamwePath, "config.ini")) && BetterHi3LauncherConfig.game_info.installed;
        }

        public bool CheckExistingGame()
        {
            try
            {
                string? Value = (string?)Registry.GetValue(InstallRegistryLocation, "InstallPath", null);
                return TryCheckGameLocation(Value);
            }
            catch (Exception e)
            {
                LogWriteLine($"{e}", LogType.Error, true);
                return false;
            }
        }

        public bool TryCheckGameLocation(in string? Path)
        {
            if (string.IsNullOrEmpty(Path)) return CheckInnerGameConfig(DefaultGameLocation);
            if (Directory.Exists(Path)) return CheckInnerGameConfig(Path);

            return false;
        }

        private bool CheckInnerGameConfig(in string GamePath)
        {
            string ConfigPath = Path.Combine(GamePath, "config.ini");
            if (!File.Exists(ConfigPath)) return false;

            IniFile Ini = new IniFile();
            Ini.Load(ConfigPath);

            ActualGameDataLocation = ConverterTool.NormalizePath(Ini["launcher"]["game_install_path"].ToString());

            return File.Exists(Path.Combine(ActualGameDataLocation, "config.ini")) || File.Exists(Path.Combine(ActualGameDataLocation, GameExecutableName));
        }

        public string? ProfileName { get; set; }
        public string? ZoneName { get; set; }
        public string? ZoneFullname { get; set; }
        public string? ZoneDescription { get; set; }
        public string? ZoneURL { get; set; }
#nullable disable
        public string InstallRegistryLocation { get; set; }
        public string DefaultGameLocation { get; set; }
        public string ConfigRegistryLocation { get; set; }
        public string BetterHi3LauncherVerInfoReg { get; set; }
        public BHI3LInfo BetterHi3LauncherConfig { get; private set; }
#nullable enable
        public string? ActualGameLocation { get; set; }
        public string? ActualGameDataLocation { get; set; }
        public string? DictionaryHost { get; set; }
        public string? UpdateDictionaryAddress { get; set; }
        public string? BlockDictionaryAddress { get; set; }
        public string? GameVersion { get; set; }
        public bool MigrateFromBetterHi3Launcher { get; set; } = false;
        public string? FallbackLanguage { get; set; }
        public string? SteamInstallRegistryLocation { get; set; }
        public int SteamGameID { get; set; }
        public string? GameDirectoryName { get; set; }
        public string? GameExecutableName { get; set; }
        public string? ZipFileURL { get; set; }
#nullable enable
        public string? GameDispatchURL { get; set; }
        public string? ProtoDispatchKey { get; set; }
        public string? CachesListAPIURL { get; set; }
        public byte? CachesListGameVerID { get; set; }
        public string? CachesEndpointURL { get; set; }
#nullable disable
        public Dictionary<string, MirrorUrlMember> MirrorList { get; set; }
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
        public string DispatcherKey { get; set; }
        public int? DispatcherKeyBitLength { get; set; }
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
