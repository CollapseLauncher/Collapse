using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static Hi3Helper.Logger;

namespace Hi3Helper.Preset
{
#nullable enable
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ServerRegionID
    {
        os_usa = 0,
        os_euro = 1,
        os_asia = 2,
        os_cht = 3
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum GameChannel
    {
        Beta = 0,
        Stable = 1,
        DevRelease = 2
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum GameType
    {
        Honkai = 0,
        Genshin = 1,
        StarRail = 2,
        Zenless = 3
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum GameVendorType
    {
        miHoYo,
        Cognosphere
    }

    public class Stamp
    {
        public long LastUpdated { get; set; }
    }

    public class Metadata
    {
        public Dictionary<string, Dictionary<string, PresetConfigV2>>? MetadataV2 { get; set; }
        public string? MasterKey { get; set; }
        public int MasterKeyBitLength { get; set; }

#nullable disable
        public void DecryptStrings()
        {
            int gameCount = MetadataV2?.Count ?? 0;
            mhyEncTool Decryptor = new mhyEncTool();
            Decryptor.InitMasterKey(MasterKey, MasterKeyBitLength, RSAEncryptionPadding.Pkcs1);

            string[] gameKeys = MetadataV2.Keys.ToArray();
            for (int i = 0; i < gameCount; i++)
            {
                string[] regionKeys = MetadataV2[gameKeys[i]].Keys.ToArray();
                int segmentCount = MetadataV2[gameKeys[i]].Count;

                for (int j = 0; j < segmentCount; j++)
                {
                    // Dec GameDispatchArrayURL
                    string[] GameDispatchArrayURL = MetadataV2[gameKeys[i]][regionKeys[j]].GameDispatchArrayURL ?? null;
                    Decryptor.DecryptStringWithMasterKey(ref GameDispatchArrayURL);
                    MetadataV2[gameKeys[i]][regionKeys[j]].GameDispatchArrayURL = GameDispatchArrayURL;

                    // Dec GameDispatchChannelName
                    string GameDispatchChannelName = MetadataV2[gameKeys[i]][regionKeys[j]].GameDispatchChannelName ?? null;
                    Decryptor.DecryptStringWithMasterKey(ref GameDispatchChannelName);
                    MetadataV2[gameKeys[i]][regionKeys[j]].GameDispatchChannelName = GameDispatchChannelName;

                    // Dec GameDispatchURLTemplate
                    string GameDispatchURLTemplate = MetadataV2[gameKeys[i]][regionKeys[j]].GameDispatchURLTemplate ?? null;
                    Decryptor.DecryptStringWithMasterKey(ref GameDispatchURLTemplate);
                    MetadataV2[gameKeys[i]][regionKeys[j]].GameDispatchURLTemplate = GameDispatchURLTemplate;

                    // Dec GameGatewayURLTemplate
                    string GameGatewayURLTemplate = MetadataV2[gameKeys[i]][regionKeys[j]].GameGatewayURLTemplate ?? null;
                    Decryptor.DecryptStringWithMasterKey(ref GameGatewayURLTemplate);
                    MetadataV2[gameKeys[i]][regionKeys[j]].GameGatewayURLTemplate = GameGatewayURLTemplate;
                }
            }
        }
#nullable enable
    }

    public class PresetConfigV2
    {
        private const string PrefixRegInstallLocation = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{0}";
        private const string PrefixRegGameConfig = "Software\\miHoYo\\{0}";
        private const string PrefixDefaultProgramFiles = "{1}Program Files\\{0}";

        public string? GetSteamInstallationPath()
        {
            try
            {
                List<SteamTool.AppInfo> AppList = SteamTool.GetSteamApps(SteamTool.GetSteamLibs());
                string? ret = AppList.Where(x => x.Id == SteamGameID).Select(y => y.GameRoot).FirstOrDefault();
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
            string RegPath = Path.GetFileName(ConfigRegistryLocation);
            try
            {
                ReadOnlySpan<char> regValue;
                RegistryKey? keys = Registry.CurrentUser.OpenSubKey(ConfigRegistryLocation);
                byte[]? value = (byte[]?)keys?.GetValue("GENERAL_DATA_h2389025596");

                if (keys is null || value is null || value.Length is 0)
                {
                    LogWriteLine($"Voice Language ID registry on {RegPath} doesn't exist. Fallback value will be used (2 / ja-jp).", LogType.Warning, true);
                    return 2;
                }

                regValue = Encoding.UTF8.GetString(value).AsSpan().Trim('\0');
                GeneralDataProp? RegValues = (GeneralDataProp?)JsonSerializer.Deserialize(new string(regValue), typeof(GeneralDataProp), GeneralDataPropContext.Default);
                return RegValues?.deviceVoiceLanguageType ?? 2;
            }
            catch (JsonException ex)
            {
                LogWriteLine($"System.Text.Json cannot deserialize language ID registry in this path: {RegPath}\r\nFallback value will be used (2 / ja-jp).\r\n{ex}", LogType.Warning, true);
                return 2;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Launcher cannot evaluate an existing language ID registry on {RegPath}\r\nFallback value will be used (2 / ja-jp).\r\n{ex}", LogType.Warning, true);
                return 2;
            }
        }

        // WARNING!!!
        // This feature is only available for Genshin.
        public void SetVoiceLanguageID(int LangID)
        {
            try
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
                    if (result is null) return;
                    regValue = Encoding.UTF8.GetString(result).AsSpan().Trim('\0');
                    initValue = (GeneralDataProp?)JsonSerializer.Deserialize(new string(regValue), typeof(GeneralDataProp), GeneralDataPropContext.Default) ?? initValue;
                }

                initValue.deviceVoiceLanguageType = LangID;
                keys.SetValue("GENERAL_DATA_h2389025596",
                    Encoding.UTF8.GetBytes(
                        JsonSerializer.Serialize(initValue, typeof(GeneralDataProp), GeneralDataPropContext.Default) + '\0'));
            }
            catch (Exception ex)
            {
                LogWriteLine($"Cannot save voice language ID: {LangID} to the registry!\r\n{ex}", LogType.Error, true);
            }
        }

        // WARNING!!!
        // This feature is only available for Genshin.
        public int GetRegServerNameID()
        {
            if (!IsGenshin ?? true) return 0;
            RegistryKey? keys = Registry.CurrentUser.OpenSubKey(ConfigRegistryLocation);
            byte[]? value = (byte[]?)keys?.GetValue("GENERAL_DATA_h2389025596", new byte[] { }, RegistryValueOptions.None);

            if (keys is null || value is null || value.Length is 0)
            {
                LogWriteLine($"Server name ID registry on \u001b[32;1m{Path.GetFileName(ConfigRegistryLocation)}\u001b[0m doesn't exist. Fallback value will be used (0 / USA).", LogType.Warning, true);
                return 0;
            }

            string regValue = new string(Encoding.UTF8.GetString(value).AsSpan().Trim('\0'));

            try
            {
                return (int?)(((GeneralDataProp?)JsonSerializer.Deserialize(regValue, typeof(GeneralDataProp), GeneralDataPropContext.Default))?.selectedServerName) ?? 0;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error while getting existing GENERAL_DATA_h2389025596 value on {ZoneFullname}! Returning value 0 as fallback!\r\nValue: {regValue}\r\n{ex}", LogType.Warning, true);
                return 0;
            }
        }

        // WARNING!!!
        // This feature is only available for Genshin.
        public class GeneralDataProp
        {
            public string deviceUUID { get; set; } = "";
            public string userLocalDataVersionId { get; set; } = "0.0.1";
            public int deviceLanguageType { get; set; } = 1;
            public int deviceVoiceLanguageType { get; set; } = 2;
            public ServerRegionID selectedServerName { get; set; } = ServerRegionID.os_asia;
            public int localLevelIndex { get; set; } = 0;
            public string deviceID { get; set; } = "";
            public string targetUID { get; set; } = "";
            public string curAccountName { get; set; } = "";
            public string uiSaveData { get; set; } = "";
            public string inputData { get; set; } = "";
            public string graphicsData { get; set; } = "";
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
            public double gammaValue { get; set; } = 2.200000047683716;
            public List<string> _overrideControllerMapKeyList { get; set; } = new List<string>();
            public List<string> _overrideControllerMapValueList { get; set; } = new List<string>();
            public bool rewiredDisableKeyboard { get; set; } = false;
            public bool rewiredEnableKeyboard { get; set; } = false;
            public bool rewiredEnableEDS { get; set; } = false;
            public bool disableRewiredDelayInit { get; set; } = false;
            public bool disableRewiredInitProtection { get; set; } = false;
            public int lastSeenPreDownloadTime { get; set; } = 0;
            public bool enableEffectAssembleInEditor { get; set; } = true;
            public bool forceDisableQuestResourceManagement { get; set; } = false;
            public bool needReportQuestResourceDeleteStatusFiles { get; set; } = false;
            public bool mtrCached { get; set; } = true;
            public bool mtrIsOpen { get; set; } = true;
            public int mtrMaxTTL { get; set; } = 32;
            public int mtrTimeOut { get; set; } = 5000;
            public int mtrTraceCount { get; set; } = 5;
            public int mtrAbortTimeOutCount { get; set; } = 3;
            public int mtrAutoTraceInterval { get; set; } = 3600;
            public int mtrTraceCDEachReason { get; set; } = 600;
            public int mtrTimeInterval { get; set; } = 1000;
            public List<string> mtrBanReasons { get; set; } = new List<string>();
            public List<string> _customDataKeyList { get; set; } = new List<string>();
            public List<string> _customDataValueList { get; set; } = new List<string>();
            public List<int> _serializedCodeSwitches { get; set; } = new List<int>();
            public bool urlCheckCached { get; set; } = false;
            public bool urlCheckIsOpen { get; set; } = false;
            public bool urlCheckAllIP { get; set; } = false;
            public int urlCheckTimeOut { get; set; } = 5000;
            public int urlCheckSueecssTraceCount { get; set; } = 5;
            public int urlCheckErrorTraceCount { get; set; } = 30;
            public int urlCheckAbortTimeOutCount { get; set; } = 3;
            public int urlCheckTimeInterval { get; set; } = 1000;
            public int urlCheckCDEachReason { get; set; } = 600;
            public List<string> urlCheckBanReasons { get; set; } = new List<string>();
            public bool mtrUseOldWinVersion { get; set; } = false;
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
                BetterHi3LauncherConfig = (BHI3LInfo?)JsonSerializer.Deserialize(Result, typeof(BHI3LInfo), BHI3LInfoContext.Default);
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

            string? path1 = Ini["launcher"]["game_install_path"].ToString();

            if (path1 == null) return false;

            ActualGameDataLocation = ConverterTool.NormalizePath(path1);

            return File.Exists(Path.Combine(ActualGameDataLocation, "config.ini")) || File.Exists(Path.Combine(ActualGameDataLocation, GameExecutableName));
        }

        private string? SystemDriveLetter { get => Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)); }
        public string? ProfileName { get; set; }
        public GameChannel GameChannel { get; set; } = GameChannel.Stable;
        public bool IsExperimental { get; set; } = false;
        public string? ZoneName { get; set; }
        public string? ZoneFullname { get; set; }
        public string? ZoneDescription { get; set; }
        public string? ZoneURL { get; set; }
        public string? ZoneLogoURL { get; set; }
        public string? ZonePosterURL { get; set; }
#nullable disable
        private string InstallRegistryLocation { get => string.Format(PrefixRegInstallLocation, InternalGameNameInConfig); }
        public string DefaultGameLocation { get => string.Format(PrefixDefaultProgramFiles, InternalGameNameFolder, SystemDriveLetter); }
        public string ConfigRegistryLocation { get => string.Format(PrefixRegGameConfig, InternalGameNameInConfig); }
#nullable enable
        public string? BetterHi3LauncherVerInfoReg { get; set; }
        public BHI3LInfo? BetterHi3LauncherConfig { get; private set; }
        public string? ActualGameLocation { get; set; }
        public string? ActualGameDataLocation { get; set; }
        public bool MigrateFromBetterHi3Launcher { get; set; } = false;
        public string? FallbackLanguage { get; set; }
        public string? SteamInstallRegistryLocation { get; set; }
        public int? SteamGameID { get; set; }
        public string? GameDirectoryName { get; set; }
#nullable disable
        public string GameExecutableName { get; set; }
#nullable enable
        public string? ZipFileURL { get; set; }
        public string? GameDispatchURL { get; set; }
        public string[]? GameSupportedLanguages { get; set; }
        public string[]? GameDispatchArrayURL { get; set; }
        public string? GameDispatchChannelName { get; set; }
        public string? GameDispatchURLTemplate { get; set; }
        public string? GameGatewayURLTemplate { get; set; }
        public string? GameGatewayDefault { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AssetLanguage GameDefaultCVLanguage { get; set; }
        public string? ProtoDispatchKey { get; set; }
        public string? CachesListAPIURL { get; set; }
        public byte? CachesListGameVerID { get; set; }
        public string? CachesEndpointURL { get; set; }
        public bool? IsGenshin { get; set; }
        public bool? IsConvertible { get; set; }
        public bool IsHideSocMedDesc { get; set; } = true;
        public List<string>? ConvertibleTo { get; set; }
        public GameType GameType { get; set; }
        public GameVendorType VendorType { get; set; } = GameVendorType.miHoYo;
        public string? ConvertibleCookbookURL { get; set; }
        public bool? UseRightSideProgress { get; set; }
        public bool LauncherSpriteURLMultiLang { get; set; }
        public string? LauncherSpriteURLMultiLangFallback { get; set; }
        public string? LauncherSpriteURL { get; set; }
        public string? LauncherResourceURL { get; set; }
        public string? DispatcherKey { get; set; }
        public int? DispatcherKeyBitLength { get; set; }
        public bool? IsRepairEnabled { get; set; }
        public bool? IsCacheUpdateEnabled { get; set; }
        public string? InternalGameNameFolder { get; set; }
        public string? InternalGameNameInConfig { get; set; }
    }
}
