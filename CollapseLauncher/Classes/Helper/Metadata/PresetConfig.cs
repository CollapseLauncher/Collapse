using CollapseLauncher.Helper.Metadata.JsonConverter;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static Hi3Helper.Logger;

#nullable enable
namespace CollapseLauncher.Helper.Metadata
{
    [JsonConverter(typeof(JsonStringEnumConverter<GameChannel>))]
    public enum GameChannel
    {
        Stable,
        Beta,
        DevRelease
    }

    [JsonConverter(typeof(JsonStringEnumConverter<ServerRegionID>))]
    public enum ServerRegionID
    {
        os_usa = 0,
        os_euro = 1,
        os_asia = 2,
        os_cht = 3,
        cn_gf01 = 4,
        cn_qd01 = 5
    }

    [JsonConverter(typeof(JsonStringEnumConverter<GameNameType>))]
    public enum GameNameType
    {
        Honkai,
        Genshin,
        StarRail,
        Zenless,
        Unknown = int.MinValue
    }

    [JsonConverter(typeof(JsonStringEnumConverter<GameVendorType>))]
    public enum GameVendorType
    {
        miHoYo,
        Cognosphere
    }

    internal class PresetConfig
    {
        #region Constants
        private const string PrefixRegInstallLocation = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{0}";
        private const string PrefixRegGameConfig = "Software\\{0}\\{1}";
        private const string PrefixDefaultProgramFiles = "{1}Program Files\\{0}";
        #endregion

        #region Config Propeties
        [JsonPropertyName("GameChannel")]
        public GameChannel Channel { get; init; }
        public AudioLanguageType GameDefaultCVLanguage { get; init; }
        public GameNameType GameType { get; init; } = GameNameType.Unknown;
        public GameVendorType VendorType { get; init; }

        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? BetterHi3LauncherVerInfoReg { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? DispatcherKey { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? FallbackLanguage { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? InternalGameNameFolder { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? InternalGameNameInConfig { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? GameDirectoryName { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? GameDispatchURL { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? GameDispatchChannelName { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? GameDispatchDefaultName { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? GameDispatchURLTemplate { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? GameExecutableName { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? GameGatewayDefault { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? GameGatewayURLTemplate { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? GameName { get; set; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? LauncherSpriteURL { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? LauncherSpriteURLMultiLangFallback { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? LauncherPluginURL { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? LauncherResourceURL { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? ProfileName { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? ProtoDispatchKey { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? SteamInstallRegistryLocation { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? ZoneDescription { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? ZoneFullname { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? ZoneLogoURL { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? ZoneName { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? ZonePosterURL { get; init; }
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? ZoneURL { get; init; }

        [JsonConverter(typeof(ServeV3StringListConverter))]
        public List<string>? ConvertibleTo { get; init; }
        [JsonConverter(typeof(ServeV3StringListConverter))]
        public List<string>? GameSupportedLanguages { get; init; }
        [JsonConverter(typeof(ServeV3StringListConverter))]
        public List<string>? GameDispatchArrayURL { get; init; }

        public bool? IsCacheUpdateEnabled { get; init; }
        public bool? IsConvertible { get; init; }
        public bool? IsExperimental { get; init; }
        public bool? IsGenshin { get; init; }
        public bool? IsHideSocMedDesc { get; init; } = true;
        public bool? IsRepairEnabled { get; init; }
        public bool? LauncherSpriteURLMultiLang { get; init; }
        public bool? UseRightSideProgress { get; init; }

        public byte? CachesListGameVerID { get; init; }

        public int? ChannelID { get; init; }
        public int? DispatcherKeyBitLength { get; init; }
        public int? LauncherID { get; init; }
        public int? SubChannelID { get; init; }
        public int? SteamGameID { get; init; }

        public Dictionary<string, GameDataTemplate>? GameDataTemplates { get; init; } = new Dictionary<string, GameDataTemplate>();
        public Dictionary<string, SteamGameProp>? ZoneSteamAssets { get; init; } = new Dictionary<string, SteamGameProp>();

        public DateTime LastModifiedFile { get; set; }
        #endregion

        #region Dynamic Config Properties
        private string? SystemDriveLetter { get => Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)); }
        public string InstallRegistryLocation { get => string.Format(PrefixRegInstallLocation, InternalGameNameInConfig); }
        public string DefaultGameLocation { get => string.Format(PrefixDefaultProgramFiles, InternalGameNameFolder, SystemDriveLetter); }
        public string ConfigRegistryLocation { get => string.Format(PrefixRegGameConfig, VendorType, InternalGameNameInConfig); }
        public string? ActualGameDataLocation { get; set; }
        public int HashID { get; set; }
        #endregion

        #region Language Handler
        public string? GetGameLanguage()
        {
            ReadOnlySpan<char> value;
            RegistryKey? keys = Registry.CurrentUser.OpenSubKey(ConfigRegistryLocation!);

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
        // This feature is only available for Genshin and Star Rail.
        public int GetVoiceLanguageID()
        {
            string RegPath = Path.GetFileName(ConfigRegistryLocation);
            switch (GameType)
            {
                case GameNameType.Genshin:
                    return GetVoiceLanguageID_Genshin(RegPath);
                case GameNameType.StarRail:
                    return GetVoiceLanguageID_StarRail(RegPath);
                default:
                    return int.MinValue;
            }
        }

        private int GetVoiceLanguageID_StarRail(string RegPath)
        {
            try
            {
                string regValue;
                RegistryKey? keys = Registry.CurrentUser.OpenSubKey(ConfigRegistryLocation!);
                byte[]? value = (byte[]?)keys?.GetValue("LanguageSettings_LocalAudioLanguage_h882585060");

                if (keys is null || value is null || value.Length is 0)
                {
                    LogWriteLine($"Voice Language ID registry on {RegPath} doesn't exist. Fallback value will be used (2 / ja-jp).", LogType.Warning, true);
                    return 2;
                }

                regValue = Encoding.UTF8.GetString(value).AsSpan().Trim('\0').ToString();
                return GetStarRailVoiceLanguageByName(regValue);
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

        private int GetVoiceLanguageID_Genshin(string RegPath)
        {
            try
            {
                ReadOnlySpan<char> regValue;
                RegistryKey? keys = Registry.CurrentUser.OpenSubKey(ConfigRegistryLocation!);
                byte[]? value = (byte[]?)keys?.GetValue("GENERAL_DATA_h2389025596");

                if (keys is null || value is null || value.Length is 0)
                {
                    LogWriteLine($"Voice Language ID registry on {RegPath} doesn't exist. Fallback value will be used (2 / ja-jp).", LogType.Warning, true);
                    return 2;
                }

                regValue = Encoding.UTF8.GetString(value).AsSpan().Trim('\0');
                GeneralDataProp? RegValues = (GeneralDataProp?)JsonSerializer.Deserialize(new string(regValue), typeof(GeneralDataProp), InternalAppJSONContext.Default);
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
        // This feature is only available for Genshin and Star Rail.
        public void SetVoiceLanguageID(int LangID)
        {
            switch (GameType)
            {
                case GameNameType.Genshin:
                    SetVoiceLanguageID_Genshin(LangID);
                    break;
                case GameNameType.StarRail:
                    SetVoiceLanguageID_StarRail(LangID);
                    break;
            }
        }

        public int GetStarRailVoiceLanguageByName(string name) => name switch
        {
            "cn" => 0,
            "tw" => 1,
            "en" => 2,
            "jp" => 3,
            "kr" => 4,
            _ => 3 // Set to JP by default
        };

        public string GetStarRailVoiceLanguageByID(int id) => id switch
        {
            0 => "cn",
            1 => "cn", // Force Traditional Chinese value to use Simplified Chinese since they shared the same VO files (as provided by the API)
            2 => "en",
            3 => "jp",
            4 => "kr",
            _ => "jp" // Set to JP by default
        };

        public string GetStarRailVoiceLanguageFullNameByID(int id) => id switch
        {
            0 => "Chinese(PRC)",
            1 => "Chinese(PRC)", // Force Traditional Chinese value to use Simplified Chinese since they shared the same VO files (as provided by the API)
            2 => "English",
            3 => "Japanese",
            4 => "Korean",
            _ => "Japanese" // Set to JP by default
        };

        public string GetStarRailVoiceLanguageFullNameByName(string name) => name switch
        {
            "cn" => "Chinese(PRC)",
            "tw" => "Chinese(PRC)", // Force Traditional Chinese value to use Simplified Chinese since they shared the same VO files (as provided by the API)
            "en" => "English",
            "jp" => "Japanese",
            "kr" => "Korean",
            _ => "Japanese" // Set to JP by default
        };

        private void SetVoiceLanguageID_Genshin(int LangID)
        {
            try
            {
                RegistryKey? keys = Registry.CurrentUser.OpenSubKey(ConfigRegistryLocation!, true);
                if (keys is null) keys = Registry.CurrentUser.CreateSubKey(ConfigRegistryLocation!);
                var result = (byte[]?)keys.GetValue("GENERAL_DATA_h2389025596");
                var initValue = new GeneralDataProp();
                if (result != null)
                {
                    ReadOnlySpan<char> regValue = Encoding.UTF8.GetString(result).AsSpan().Trim('\0');
                    initValue = (GeneralDataProp?)JsonSerializer.Deserialize(new string(regValue), typeof(GeneralDataProp), InternalAppJSONContext.Default) ?? initValue;
                }

                initValue.deviceVoiceLanguageType = LangID;
                keys.SetValue("GENERAL_DATA_h2389025596",
                    Encoding.UTF8.GetBytes(
                        JsonSerializer.Serialize(initValue, typeof(GeneralDataProp), InternalAppJSONContext.Default) + '\0'));
            }
            catch (Exception ex)
            {
                LogWriteLine($"Cannot save voice language ID: {LangID} to the registry!\r\n{ex}", LogType.Error, true);
            }
        }

        private void SetVoiceLanguageID_StarRail(int LangID)
        {
            try
            {
                RegistryKey? keys = Registry.CurrentUser.OpenSubKey(ConfigRegistryLocation!, true);
                if (keys is null) keys = Registry.CurrentUser.CreateSubKey(ConfigRegistryLocation!);

                string initValue = GetStarRailVoiceLanguageByID(LangID);
                keys.SetValue("LanguageSettings_LocalAudioLanguage_h882585060", Encoding.UTF8.GetBytes(initValue + '\0'));
            }
            catch (Exception ex)
            {
                LogWriteLine($"Cannot save voice language ID: {LangID} to the registry!\r\n{ex}", LogType.Error, true);
            }
        }

        #endregion

        #region Game Data Template Handler
        public byte[]? GetGameDataTemplate(string key, byte[] gameVersion)
        {
            if (GameDataTemplates == null || !GameDataTemplates.ContainsKey(key)) return null;

            GameDataTemplate value = GameDataTemplates[key];
            if (value.DataVersion == null) return null;

            int verInt = GameDataVersion.GetBytesToIntVersion(gameVersion);
            if (!value.DataVersion.ContainsKey(verInt)) return null;

            GameDataVersion verData = value.DataVersion[verInt];

            if (!DataCooker.IsServeV3Data(verData.Data))
                return verData.Data;

            DataCooker.GetServeV3DataSize(verData.Data, out long compressedSize, out long decompressedSize);
            byte[] outData = new byte[decompressedSize];
            DataCooker.ServeV3Data(verData.Data, outData, (int)compressedSize, (int)decompressedSize, out int _);
            return outData;
        }
        #endregion

        #region Genshin Registry Handler
        // WARNING!!!
        // This feature is only available for Genshin.
        public int GetRegServerNameID()
        {
            if (!IsGenshin ?? true) return 0;
            RegistryKey? keys = Registry.CurrentUser.OpenSubKey(ConfigRegistryLocation!);
            byte[]? value = (byte[]?)keys?.GetValue("GENERAL_DATA_h2389025596", new byte[] { }, RegistryValueOptions.None);

            if (keys is null || value is null || value.Length is 0)
            {
                LogWriteLine($"Server name ID registry on \u001b[32;1m{Path.GetFileName(ConfigRegistryLocation)}\u001b[0m doesn't exist. Fallback value will be used (0 / USA).", LogType.Warning, true);
                return 0;
            }

            string regValue = new string(Encoding.UTF8.GetString(value).AsSpan().Trim('\0'));

            try
            {
                return (int?)(((GeneralDataProp?)JsonSerializer.Deserialize(regValue, typeof(GeneralDataProp), InternalAppJSONContext.Default))?.selectedServerName) ?? 0;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error while getting existing GENERAL_DATA_h2389025596 value on {ZoneFullname}! Returning value 0 as fallback!\r\nValue: {regValue}\r\n{ex}", LogType.Warning, true);
                return 0;
            }
        }
        #endregion

        #region Game Configs Check
        public bool CheckExistingGame()
        {
            try
            {
                string? Value = (string?)Registry.GetValue(InstallRegistryLocation!, "InstallPath", null);
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
            if (string.IsNullOrEmpty(Path)) return CheckInnerGameConfig(DefaultGameLocation!);
            if (Directory.Exists(Path)) return CheckInnerGameConfig(Path);

            return false;
        }

        private bool CheckInnerGameConfig(in string GamePath)
        {
            string ConfigPath = Path.Combine(GamePath, "config.ini");
            if (!File.Exists(ConfigPath)) return false;

            IniFile Ini = new IniFile();
            Ini.Load(ConfigPath);

            string? path1 = Ini["launcher"]!["game_install_path"].ToString();

            if (path1 == null) return false;

            ActualGameDataLocation = ConverterTool.NormalizePath(path1);

            return File.Exists(Path.Combine(ActualGameDataLocation!, "config.ini")) && File.Exists(Path.Combine(ActualGameDataLocation!, GameExecutableName!));
        }
        #endregion
    }
}
