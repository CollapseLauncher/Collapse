using CollapseLauncher.Extension;
using CollapseLauncher.Helper.JsonConverter;
using CollapseLauncher.Helper.LauncherApiLoader;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Microsoft.Win32;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
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
        os_usa  = 0,
        os_euro = 1,
        os_asia = 2,
        os_cht  = 3,
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

    [JsonConverter(typeof(JsonStringEnumConverter<LauncherType>))]
    public enum LauncherType
    {
        Sophon,
        HoYoPlay
    }

    public class SophonChunkUrls
    {
        private bool IsReassociated = false;
        private const string QueryPackageIdHead = "package_id";
        private const string QueryPasswordHead = "password";

        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? BranchUrl { get; set; }

        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? MainUrl { get; set; }

        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? PreloadUrl { get; set; }

        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? SdkUrl { get; set; }

        public async ValueTask EnsureReassociated(HttpClient client, string? branchUrl, string bizName, CancellationToken token)
        {
            if (IsReassociated)
                return;

            if (string.IsNullOrEmpty(branchUrl))
                return;

            // Fetch branch info
            ActionTimeoutValueTaskCallback<HoYoPlayLauncherGameInfo?> hypLauncherBranchCallback =
                async innerToken =>
                    await FallbackCDNUtil.DownloadAsJSONType<HoYoPlayLauncherGameInfo>(branchUrl, InternalAppJSONContext.Default, innerToken);

            HoYoPlayLauncherGameInfo? hypLauncherBranchInfo = await hypLauncherBranchCallback.WaitForRetryAsync(
                LauncherApiBase.ExecutionTimeout,
                LauncherApiBase.ExecutionTimeoutStep,
                LauncherApiBase.ExecutionTimeoutAttempt,
                null, token);

            // If branch info is null or empty, return
            HoYoPlayGameInfoBranch? branch = hypLauncherBranchInfo?
                .GameInfoData?
                .GameBranchesInfo?
                .FirstOrDefault(x => x.GameInfo?.BizName?.Equals(bizName) ?? false);
            if (branch == null)
                return;

            // Re-associate url if main field is exist
            if (branch.GameMainField != null)
            {
                ArgumentException.ThrowIfNullOrEmpty(branch.GameMainField.PackageId);
                ArgumentException.ThrowIfNullOrEmpty(branch.GameMainField.Password);

                string packageIdValue = branch.GameMainField.PackageId;
                string passwordValue = branch.GameMainField.Password;

                MainUrl = MainUrl.AssociateGameAndLauncherId(
                    QueryPasswordHead,
                    QueryPackageIdHead,
                    passwordValue,
                    packageIdValue);
            }

            // Re-associate url if preload field is exist
            if (branch.GamePreloadField != null)
            {
                ArgumentException.ThrowIfNullOrEmpty(branch.GamePreloadField.PackageId);
                ArgumentException.ThrowIfNullOrEmpty(branch.GamePreloadField.Password);

                string packageIdValue = branch.GamePreloadField.PackageId;
                string passwordValue = branch.GamePreloadField.Password;

                PreloadUrl = MainUrl.AssociateGameAndLauncherId(
                    QueryPasswordHead,
                    QueryPackageIdHead,
                    passwordValue,
                    packageIdValue);
            }

            // Mark as re-associated
            IsReassociated = true;
        }
    }

    internal static class PresetConfigExt
    {
        internal static string? AssociateGameAndLauncherId(this string? url,
            string launcherIdOrPasswordHead, string gameIdOrGamePackageIdHead,
            string? launcherIdOrPassword, string? gameIdOrGamePackageId)
        {
            if (string.IsNullOrEmpty(url))
                return url;

            if (string.IsNullOrEmpty(launcherIdOrPassword) || string.IsNullOrEmpty(gameIdOrGamePackageId))
                return url;

            int urlLen = url.Length + (1 << 10);
            bool isUseRent = urlLen > 4 << 10;
            char[] urlBuffer = isUseRent ? ArrayPool<char>.Shared.Rent(urlLen) : new char[urlLen];
            Span<char> urlSpanBuffer = urlBuffer;
            ReadOnlySpan<char> urlSpan = url;

            try
            {
                int urlSpanBufferLen = 0;
                Span<Range> splitRanges = stackalloc Range[32];
                int urlSplitRangesLen = urlSpan.Split(splitRanges, '?', StringSplitOptions.RemoveEmptyEntries);
                if (urlSplitRangesLen < 2)
                    return url;

                ReadOnlySpan<char> urlPathSpan = urlSpan[splitRanges[0]];
                ReadOnlySpan<char> querySpan = urlSpan[splitRanges[1]];

                if (!urlPathSpan.TryCopyTo(urlSpanBuffer))
                    throw new InvalidOperationException("Failed to copy url path string to buffer");
                urlSpanBufferLen += splitRanges[0].End.Value - splitRanges[0].Start.Value;
                urlSpanBuffer[urlSpanBufferLen++] = '?';

                #region Parse and split queries - Sanitize the GameId and LauncherId query
                int querySplitRangesLen = querySpan.Split(splitRanges, '&', StringSplitOptions.RemoveEmptyEntries);
                int queryWritten = 0;
                Span<char> querySpanBuffer = urlSpanBuffer.Slice(urlSpanBufferLen);
                for (int i = querySplitRangesLen - 1; i > -1; i--)
                {
                    Range segmentRange = splitRanges[i];
                    int segmentLen = segmentRange.End.Value - segmentRange.Start.Value;
                    ReadOnlySpan<char> querySegment = querySpan[segmentRange];

                    // Skip GameId or LauncherId query head
                    if (querySegment.StartsWith(launcherIdOrPasswordHead, StringComparison.OrdinalIgnoreCase)
                     || querySegment.StartsWith(gameIdOrGamePackageIdHead, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Otherwise, add others
                    if (!querySegment.TryCopyTo(querySpanBuffer.Slice(queryWritten)))
                        throw new InvalidOperationException("Failed to copy query string to buffer");
                    queryWritten += segmentLen;

                    // Add trailing
                    if (i > 0) querySpanBuffer[queryWritten++] = '&';
                }

                // Append '&'
                if (queryWritten > 0 && querySpanBuffer[queryWritten - 1] != '&'
                    && querySpanBuffer[queryWritten - 1] != '?')
                    querySpanBuffer[queryWritten++] = '&';

                urlSpanBufferLen += queryWritten;
                #endregion

                #region Append GameId and LauncherId query
                if (!launcherIdOrPasswordHead.TryCopyTo(urlSpanBuffer.Slice(urlSpanBufferLen)))
                    throw new InvalidOperationException("Failed to copy launcher id or password head string to buffer");
                urlSpanBufferLen += launcherIdOrPasswordHead.Length;
                urlSpanBuffer[urlSpanBufferLen++] = '=';
                if (!launcherIdOrPassword.TryCopyTo(urlSpanBuffer.Slice(urlSpanBufferLen)))
                    throw new InvalidOperationException("Failed to copy launcher id or password value string to buffer");
                urlSpanBufferLen += launcherIdOrPassword.Length;

                urlSpanBuffer[urlSpanBufferLen++] = '&';

                if (!gameIdOrGamePackageIdHead.TryCopyTo(urlSpanBuffer.Slice(urlSpanBufferLen)))
                    throw new InvalidOperationException("Failed to copy game id or package id head string to buffer");
                urlSpanBufferLen += gameIdOrGamePackageIdHead.Length;
                urlSpanBuffer[urlSpanBufferLen++] = '=';
                if (!gameIdOrGamePackageId.TryCopyTo(urlSpanBuffer.Slice(urlSpanBufferLen)))
                    throw new InvalidOperationException("Failed to copy game id or package id value string to buffer");
                urlSpanBufferLen += gameIdOrGamePackageId.Length;

                string returnString = new string(urlBuffer, 0, urlSpanBufferLen);
#if DEBUG
                LogWriteLine($"URL string's GameId or Password and LauncherId or PackageId association has been successfully replaced!\r\nSource: {url}\r\nResult: {returnString}", LogType.Debug, true);
#endif
                return returnString;
                #endregion
            }
            finally
            {
                if (isUseRent) ArrayPool<char>.Shared.Return(urlBuffer);
            }
        }
    }

    internal class PresetConfig
    {
        #region Constants
        // ReSharper disable once UnusedMember.Local
        private const string PrefixRegInstallLocation =
            "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{0}";

        private const string PrefixRegGameConfig       = "Software\\{0}\\{1}";
        private const string PrefixDefaultProgramFiles = "{1}Program Files\\{0}";
        private const string QueryLauncherIdHead       = "launcher_id";
        private const string QueryGameIdHead           = "game_ids[]";
        #endregion

        #region Config Propeties

        [JsonIgnore]
        public  ILauncherApi?    GameLauncherApi { get; set; }

        [JsonPropertyName("GameChannel")] public GameChannel       Channel { get; init; }
        public                                   AudioLanguageType GameDefaultCVLanguage { get; init; }
        public                                   GameNameType      GameType { get; init; } = GameNameType.Unknown;
        public                                   GameVendorType    VendorType { get; init; }
        public                                   LauncherType      LauncherType { get; init; }

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
        public string? GameDispatchURLTemplate { get; set; }

        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? GameExecutableName { get; init; }

        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? GameGatewayDefault { get; init; }

        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? GameGatewayURLTemplate { get; set; }

        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? GameName { get; set; }

        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? LauncherId { get; init; }

        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? LauncherGameId { get; init; }

        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? LauncherBizName { get; init; }

        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? LauncherSpriteURL { get; init; }

        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? LauncherSpriteURLMultiLangFallback { get; init; }

        private string? _launcherPluginURL;
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? LauncherPluginURL
        {
            get => _launcherPluginURL;
            init => _launcherPluginURL = value.AssociateGameAndLauncherId(QueryLauncherIdHead, QueryGameIdHead, LauncherId, LauncherGameId);
        }

        private string? _launcherResourceURL;
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? LauncherResourceURL
        {
            get => _launcherResourceURL;
            init => _launcherResourceURL = value.AssociateGameAndLauncherId(QueryLauncherIdHead, QueryGameIdHead, LauncherId, LauncherGameId);
        }

        private string? _launcherGameChannelSDKURL;
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? LauncherGameChannelSDKURL
        {
            get => _launcherGameChannelSDKURL;
            init => _launcherGameChannelSDKURL = value.AssociateGameAndLauncherId(QueryLauncherIdHead, QueryGameIdHead, LauncherId, LauncherGameId);
        }

        private string? _launcherGameInfoDisplayURL;
        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? LauncherGameInfoDisplayURL
        {
            get => _launcherGameInfoDisplayURL;
            init => _launcherGameInfoDisplayURL = value.AssociateGameAndLauncherId(QueryLauncherIdHead, QueryGameIdHead, LauncherId, LauncherGameId);
        }

        private SophonChunkUrls? _launcherResourceChunksURL;
        public SophonChunkUrls? LauncherResourceChunksURL
        {
            get => _launcherResourceChunksURL;
            init
            {
                _launcherResourceChunksURL = value;
                if (!string.IsNullOrEmpty(_launcherResourceChunksURL?.BranchUrl))
                {
                    string branchUrl = _launcherResourceChunksURL.BranchUrl;
                    _launcherResourceChunksURL.BranchUrl = branchUrl
                        .AssociateGameAndLauncherId(QueryLauncherIdHead, QueryGameIdHead, LauncherId, LauncherGameId);
                }
            }
        }

        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? LauncherNewsURL { get; init; }

        [JsonConverter(typeof(ServeV3StringConverter))]
        public string? LauncherCPSType { get; init; }

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

        public bool? IsPluginUpdateEnabled      { get; init; } = false;
        
        public bool? IsConvertible              { get; init; }
        public bool? IsExperimental             { get; init; }
        public bool? IsGenshin                  { get; init; }
        public bool? IsHideSocMedDesc           { get; init; } = true;
        
        #if !DEBUG
        public bool? IsRepairEnabled            { get; init; }
        public bool? IsCacheUpdateEnabled       { get; init; }
        #else
        public bool? IsRepairEnabled      = true;
        public bool? IsCacheUpdateEnabled = true;
        #endif
        public bool? LauncherSpriteURLMultiLang { get; init; }

        public byte? CachesListGameVerID { get; init; }

        public int? ChannelID              { get; init; }
        public int? DispatcherKeyBitLength { get; init; }
        public int? LauncherID             { get; init; }
        public int? SubChannelID           { get; init; }
        public int? SteamGameID            { get; init; }

        public Dictionary<string, GameDataTemplate>? GameDataTemplates { get; init; } = new();
        public Dictionary<string, SteamGameProp>?    ZoneSteamAssets   { get; init; } = new();

        public DateTime LastModifiedFile { get; set; }

        #endregion

        #region Dynamic Config Properties

        private static string? SystemDriveLetter =>
            Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System));

        public string? InstallRegistryLocation { get; set; }

        public string DefaultGameLocation =>
            string.Format(PrefixDefaultProgramFiles, InternalGameNameFolder, SystemDriveLetter);

        public string ConfigRegistryLocation =>
            string.Format(PrefixRegGameConfig, VendorType, InternalGameNameInConfig);

        public string? ActualGameDataLocation { get; set; }
        public int     HashID                 { get; set; }

        #endregion

        #region Language Handler

        public string? GetGameLanguage()
        {
            RegistryKey?       keys = Registry.CurrentUser.OpenSubKey(ConfigRegistryLocation);

            byte[]? result = (byte[]?)keys?.GetValue("MIHOYOSDK_CURRENT_LANGUAGE_h2559149783");

            if (keys is null || result is null || result.Length is 0)
            {
                LogWriteLine($"Language registry on \u001b[32;1m{Path.GetFileName(ConfigRegistryLocation)}\u001b[0m version doesn't exist. Fallback value will be used.",
                             LogType.Warning, true);
                return FallbackLanguage;
            }

            ReadOnlySpan<char> value = Encoding.UTF8.GetString(result).AsSpan().Trim('\0');
            return new string(value);
        }

        // WARNING!!!
        // This feature is only available for Genshin and Star Rail.
        public int GetVoiceLanguageID()
        {
            string regPath = Path.GetFileName(ConfigRegistryLocation);
            switch (GameType)
            {
                case GameNameType.Genshin:
                    return GetVoiceLanguageID_Genshin(regPath);
                case GameNameType.StarRail:
                    return GetVoiceLanguageID_StarRail(regPath);
                default:
                    return int.MinValue;
            }
        }

        private int GetVoiceLanguageID_StarRail(string regPath)
        {
            try
            {
                RegistryKey? keys  = Registry.CurrentUser.OpenSubKey(ConfigRegistryLocation);
                byte[]?      value = (byte[]?)keys?.GetValue("LanguageSettings_LocalAudioLanguage_h882585060");

                if (keys is null || value is null || value.Length is 0)
                {
                    LogWriteLine($"Voice Language ID registry on {regPath} doesn't exist. Fallback value will be used (2 / ja-jp).",
                                 LogType.Warning, true);
                    return 2;
                }

                var regValue = Encoding.UTF8.GetString(value).AsSpan().Trim('\0').ToString();
                return GetStarRailVoiceLanguageByName(regValue);
            }
            catch (JsonException ex)
            {
                LogWriteLine($"System.Text.Json cannot deserialize language ID registry in this path: {regPath}\r\nFallback value will be used (2 / ja-jp).\r\n{ex}",
                             LogType.Warning, true);
                return 2;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Launcher cannot evaluate an existing language ID registry on {regPath}\r\nFallback value will be used (2 / ja-jp).\r\n{ex}",
                             LogType.Warning, true);
                return 2;
            }
        }

        private int GetVoiceLanguageID_Genshin(string regPath)
        {
            try
            {
                RegistryKey?       keys  = Registry.CurrentUser.OpenSubKey(ConfigRegistryLocation);
                byte[]?            value = (byte[]?)keys?.GetValue("GENERAL_DATA_h2389025596");

                if (keys is null || value is null || value.Length is 0)
                {
                    LogWriteLine($"Voice Language ID registry on {regPath} doesn't exist. Fallback value will be used (2 / ja-jp).",
                                 LogType.Warning, true);
                    return 2;
                }

                ReadOnlySpan<char> regValue = Encoding.UTF8.GetString(value).AsSpan().Trim('\0');
                GeneralDataProp? regValues =
                    (GeneralDataProp?)JsonSerializer.Deserialize(new string(regValue), typeof(GeneralDataProp),
                                                                 InternalAppJSONContext.Default);
                return regValues?.deviceVoiceLanguageType ?? 2;
            }
            catch (JsonException ex)
            {
                LogWriteLine($"System.Text.Json cannot deserialize language ID registry in this path: {regPath}\r\nFallback value will be used (2 / ja-jp).\r\n{ex}",
                             LogType.Warning, true);
                return 2;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Launcher cannot evaluate an existing language ID registry on {regPath}\r\nFallback value will be used (2 / ja-jp).\r\n{ex}",
                             LogType.Warning, true);
                return 2;
            }
        }

        // WARNING!!!
        // This feature is only available for Genshin and Star Rail.
        public void SetVoiceLanguageID(int langID)
        {
            switch (GameType)
            {
                case GameNameType.Genshin:
                    SetVoiceLanguageID_Genshin(langID);
                    break;
                case GameNameType.StarRail:
                    SetVoiceLanguageID_StarRail(langID);
                    break;
            }
        }

        public int GetStarRailVoiceLanguageByName(string name)
        {
            return name switch
                   {
                       "cn" => 0,
                       "tw" => 1,
                       "en" => 2,
                       "jp" => 3,
                       "kr" => 4,
                       _ => 3 // Set to JP by default
                   };
        }

        public string GetStarRailVoiceLanguageByID(int id)
        {
            return id switch
                   {
                       0 => "cn",
                       1 => "cn", // Force Traditional Chinese value to use Simplified Chinese since they shared the same VO files (as provided by the API)
                       2 => "en",
                       3 => "jp",
                       4 => "kr",
                       _ => "jp" // Set to JP by default
                   };
        }

        public string GetStarRailVoiceLanguageFullNameByID(int id)
        {
            return id switch
                   {
                       0 => "Chinese(PRC)",
                       1 => "Chinese(PRC)", // Force Traditional Chinese value to use Simplified Chinese since they shared the same VO files (as provided by the API)
                       2 => "English",
                       3 => "Japanese",
                       4 => "Korean",
                       _ => "Japanese" // Set to JP by default
                   };
        }

        private void SetVoiceLanguageID_Genshin(int langID)
        {
            try
            {
                RegistryKey keys = Registry.CurrentUser.OpenSubKey(ConfigRegistryLocation, true) ?? Registry.CurrentUser.CreateSubKey(ConfigRegistryLocation);

                var result    = (byte[]?)keys.GetValue("GENERAL_DATA_h2389025596");
                var initValue = new GeneralDataProp();
                if (result != null)
                {
                    ReadOnlySpan<char> regValue = Encoding.UTF8.GetString(result).AsSpan().Trim('\0');
                    initValue = (GeneralDataProp?)JsonSerializer.Deserialize(new string(regValue),
                                                                             typeof(GeneralDataProp),
                                                                             InternalAppJSONContext.Default) ??
                                initValue;
                }

                initValue.deviceVoiceLanguageType = langID;
                keys.SetValue("GENERAL_DATA_h2389025596",
                              Encoding.UTF8.GetBytes(
                                                     JsonSerializer.Serialize(initValue, typeof(GeneralDataProp),
                                                                              InternalAppJSONContext.Default) + '\0'));
            }
            catch (Exception ex)
            {
                LogWriteLine($"Cannot save voice language ID: {langID} to the registry!\r\n{ex}", LogType.Error, true);
            }
        }

        private void SetVoiceLanguageID_StarRail(int langID)
        {
            try
            {
                RegistryKey keys = Registry.CurrentUser.OpenSubKey(ConfigRegistryLocation, true) ?? Registry.CurrentUser.CreateSubKey(ConfigRegistryLocation);

                string initValue = GetStarRailVoiceLanguageByID(langID);
                keys.SetValue("LanguageSettings_LocalAudioLanguage_h882585060",
                              Encoding.UTF8.GetBytes(initValue + '\0'));
            }
            catch (Exception ex)
            {
                LogWriteLine($"Cannot save voice language ID: {langID} to the registry!\r\n{ex}", LogType.Error, true);
            }
        }

        #endregion

        #region Game Data Template Handler

        public byte[]? GetGameDataTemplate(string key, byte[] gameVersion)
        {
            if (GameDataTemplates == null || !GameDataTemplates.TryGetValue(key, out GameDataTemplate? value))
            {
                return null;
            }

            if (value.DataVersion == null)
            {
                return null;
            }

            int verInt = GameDataVersion.GetBytesToIntVersion(gameVersion);
            if (!value.DataVersion.TryGetValue(verInt, out GameDataVersion? verData))
            {
                // Fallback to find the default value anyway
                KeyValuePair<int, GameDataVersion>? kvpTemp = value.DataVersion.FirstOrDefault();
                if (kvpTemp == null)
                    return null;

                // ReSharper disable once ConstantConditionalAccessQualifier
                verData = kvpTemp?.Value;
            }

            if (verData == null)
                return null;

            if (!DataCooker.IsServeV3Data(verData.Data))
            {
                return verData.Data;
            }

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
            if (!IsGenshin ?? true)
            {
                return 0;
            }

            RegistryKey? keys = Registry.CurrentUser.OpenSubKey(ConfigRegistryLocation);
            byte[]? value =
                (byte[]?)keys?.GetValue("GENERAL_DATA_h2389025596", new byte[] { }, RegistryValueOptions.None);

            if (keys is null || value is null || value.Length is 0)
            {
                LogWriteLine($"Server name ID registry on \u001b[32;1m{Path.GetFileName(ConfigRegistryLocation)}\u001b[0m doesn't exist. Fallback value will be used (0 / USA).",
                             LogType.Warning, true);
                return 0;
            }

            string regValue = new string(Encoding.UTF8.GetString(value).AsSpan().Trim('\0'));

            try
            {
                return (int?)((GeneralDataProp?)JsonSerializer.Deserialize(regValue, typeof(GeneralDataProp),
                                                                           InternalAppJSONContext.Default))
                  ?.selectedServerName ?? 0;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error while getting existing GENERAL_DATA_h2389025596 value on {ZoneFullname}! Returning value 0 as fallback!\r\nValue: {regValue}\r\n{ex}",
                             LogType.Warning, true);
                return 0;
            }
        }

        #endregion

        #region Game Configs Check

        public bool CheckExistingGame()
        {
            try
            {
                if (LauncherType == LauncherType.HoYoPlay)
                    return TryCheckGameLocationHYP();
                else
                {
                    if (string.IsNullOrEmpty(InstallRegistryLocation)) return false;
                    string? value = (string?)Registry.GetValue(InstallRegistryLocation, "InstallPath", null);
                    return TryCheckGameLocation(value);
                }
            }
            catch (Exception e)
            {
                LogWriteLine($"{e}", LogType.Error, true);
                return false;
            }
        }

        public bool TryCheckGameLocationHYP()
        {
            string rootRegistryKey = string.Format(@"Software\{0}\HYP", VendorType);
            RegistryKey? registryKey = Registry.CurrentUser.OpenSubKey(rootRegistryKey);
            if (registryKey == null) return false;

            string[] subKeyVersions = registryKey.GetSubKeyNames();
            foreach (string subKeyVersion in subKeyVersions)
            {
                RegistryKey? subKey = registryKey.OpenSubKey(subKeyVersion);
                if (subKey == null) continue;

                string[] subKeyKey = subKey.GetSubKeyNames();
                if (subKeyKey.Length == 0) continue;

                string? gameKeyName = subKeyKey.FirstOrDefault(x => x.Equals(LauncherBizName, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrEmpty(gameKeyName)) continue;

                RegistryKey? gameKey = subKey.OpenSubKey(gameKeyName);
                if (gameKey == null) continue;

                object? gamePathObj = gameKey.GetValue("GameInstallPath", null);
                if (gamePathObj is not null and string gamePath)
                {
                    return CheckInnerGameConfig(gamePath, LauncherType.HoYoPlay);
                }
            }

            return false;
        }

        public bool TryCheckGameLocation(string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return CheckInnerGameConfig(DefaultGameLocation, LauncherType.Sophon);
            }

            if (Directory.Exists(path))
            {
                return CheckInnerGameConfig(path, LauncherType.Sophon);
            }

            return false;
        }

        private bool CheckInnerGameConfig(string gamePath, LauncherType launcherType)
        {
            switch (launcherType)
            {
                case LauncherType.Sophon:
                    // Start: Sophon Check
                    string configPath = Path.Combine(gamePath, "config.ini");
                    if (!File.Exists(configPath))
                        return false;

                    IniFile ini = new IniFile();
                    ini.Load(configPath);
                    string? path1 = ini["launcher"]!["game_install_path"].ToString();
                    if (string.IsNullOrEmpty(path1))
                        return false;

                    ActualGameDataLocation = ConverterTool.NormalizePath(path1);
                    return File.Exists(Path.Combine(ActualGameDataLocation!, "config.ini")) &&
                           File.Exists(Path.Combine(ActualGameDataLocation!, GameExecutableName!));
                    // End: Sophon Check
                case LauncherType.HoYoPlay:
                    // Start: HYP Check
                    if (string.IsNullOrEmpty(GameDirectoryName)) return false;
                    if (string.IsNullOrEmpty(GameExecutableName)) return false;
                    string tryHypDirPath = ConverterTool.NormalizePath(gamePath);
                    string tryHypConfigPath = Path.Combine(tryHypDirPath, "config.ini");
                    string tryHypGameExePath = Path.Combine(tryHypDirPath, GameExecutableName);

                    // If both file doesn't exist, then return false
                    if (!(File.Exists(tryHypConfigPath) && File.Exists(tryHypGameExePath)))
                        return false;

                    // Otherwise, return true and set the ActualGameDataLocation property
                    ActualGameDataLocation = tryHypDirPath;
                    return true;
                    // End: HYP Check
                default:
                    return false;
            }
        }

        #endregion
    }
}