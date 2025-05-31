#nullable enable
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Helper.LauncherApiLoader.Legacy;
using CollapseLauncher.Helper.Loading;
using CollapseLauncher.Plugins;
using CollapseLauncher.Statics;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable StringLiteralTypo

namespace CollapseLauncher.Helper.Metadata
{
    internal static class LauncherMetadataHelper
    {
        private const string MetadataVersion = "v3";
        private const string LauncherMetadataStampPrefix = "stamp.json";

        #region Metadata and Stamp path/url prefixes

        internal static string CurrentLauncherChannel => LauncherConfig.IsPreview ? "preview" : "stable";
        internal static string LauncherMetadataFolder => Path.Combine(LauncherConfig.AppGameFolder, "_metadatav3");

        internal static string LauncherStampRemoteURLPath =>
            $"/metadata/{MetadataVersion}/{CurrentLauncherChannel}/".CombineURLFromString(LauncherMetadataStampPrefix);

        #endregion

        #region Metadata Stamp List and Config Dictionary

        internal static List<Stamp?>? LauncherMetadataStamp { get; private set; }
        internal static List<Stamp?>? NewUpdateMetadataStamp { get; private set; }
        private static Dictionary<string, Stamp>? LauncherMetadataStampDictionary { get; set; }

        internal static Dictionary<string, Dictionary<string, PresetConfig>?>? LauncherMetadataConfig
        {
            get;
            private set;
        }

        #endregion

        #region Game Name & Region Collection and Current Config

        internal static PresetConfig? CurrentMetadataConfig { get; set; }

        internal static string? CurrentMetadataConfigGameName;
        internal static string? CurrentMetadataConfigGameRegion;
        internal static Dictionary<string, List<string>?>? LauncherGameNameRegionCollection { get; private set; }

        #endregion

        #region Current Master Key config

        internal static MasterKeyConfig? CurrentMasterKey { get; private set; }

        internal static Dictionary<string, MasterKeyConfig>? OtherMasterKeyConfigVault { get; private set; }

        #endregion

        #region Current Game Name and Max Region Counts

        internal static int CurrentGameNameCount { get; private set; }
        internal static int CurrentGameRegionMaxCount { get; private set; }

        #endregion

        #region Internal State Lock Boolean

        private static bool _isUpdateCheckRunning;
        private static bool _isUpdateRoutineRunning;

        #endregion

        internal static async Task<PresetConfig?> GetMetadataConfig(string? gameName, string? gameRegion)
        {
            ArgumentException.ThrowIfNullOrEmpty(gameName);
            ArgumentException.ThrowIfNullOrEmpty(gameRegion);

            // Check the modification status
            int isConfigLocallyModified = await IsMetadataLocallyModified(gameName, gameRegion);

            switch (isConfigLocallyModified)
            {
                // Config is unmodified, ignore action.
                case 0:
                    break;
                // Local modification has been made and config needs to be reloaded
                case 1:
                    {
                        Logger.LogWriteLine($"Metadata config for {gameName} - {gameRegion} has been modified! Reloading the config!", LogType.Warning, true);

                        Dictionary<string, PresetConfig>? regionDict = null;
                        if (!LauncherMetadataConfig?.TryGetValue(gameName, out regionDict) ?? false)
                            throw new KeyNotFoundException("Game name is not found in the metadata collection!");

                        if (!regionDict?.ContainsKey(gameRegion) ?? false)
                            throw new KeyNotFoundException("Game region is not found in the metadata collection!");

                        // Get the stamp and remove the old config from metadata config dictionary
                        string stampKey = $"{gameName} - {gameRegion}";
                        Stamp? previousStamp = LauncherMetadataStampDictionary?[stampKey];
                        LauncherMetadataConfig?[gameName]?.Remove(gameRegion);

                        // If the previous stamp is found, then start reloading the config
                        if (previousStamp != null)
                        {
                            // Get the current channel
                            string currentChannel = CurrentLauncherChannel;
                            if (await LoadAndGetConfig(previousStamp, currentChannel) is PresetConfig presetConfig)
                            {
                                LauncherMetadataConfig?[gameName]?.Add(gameRegion, presetConfig);
                            }
                        }
                    }
                    break;
                // If the stamp is empty or metadata needs to be reinitialized, then reinit the config
                case -1:
                case -2:
                    Logger.LogWriteLine("Metadata config needs to be reinitialized! Reloading the config!", LogType.Warning, true);
                    await Initialize();
                    break;
            }

            var config = LauncherMetadataConfig?[gameName]?[gameRegion];
            if (config != null)
            {
                CurrentMetadataConfig = config;
                CurrentMetadataConfigGameName = gameName;
                CurrentMetadataConfigGameRegion = gameRegion;

                return config;
            }

            throw new AccessViolationException($"Config is not exist or null inside of the metadata! This should not be happening!\r\nGame: ({gameName} - {gameRegion})");
        }

        internal static string GetTranslatedCurrentGameTitleRegionString()
        {
            string? curGameName = CurrentMetadataConfigGameName;
            string? curGameRegion = CurrentMetadataConfigGameRegion;

            string? curGameNameTranslate =
                InnerLauncherConfig.GetGameTitleRegionTranslationString(curGameName, Locale.Lang._GameClientTitles);
            string? curGameRegionTranslate =
                InnerLauncherConfig.GetGameTitleRegionTranslationString(curGameRegion, Locale.Lang._GameClientRegions);

            return $"{curGameNameTranslate} - {curGameRegionTranslate}";
        }

        /// <summary>
        /// Checks for the local modification of the metadata config
        /// </summary>
        /// <param name="gameName">Name of the game</param>
        /// <param name="gameRegion">Region of the game</param>
        /// <returns>
        /// - <c>1</c>   = The file has been modified<br/>
        /// - <c>0</c>   = The file is not modified<br/>
        /// - <c>-1</c>   = The file does not exist and need to be reinitialized<br/>
        /// - <c>-2</c>   = The stamp is not exist in the stamp dictionary or the stamp inside dictionary is null
        /// </returns>
        private static Task<int> IsMetadataLocallyModified(string gameName, string gameRegion)
        {
            return Task.Factory.StartNew(() =>
            {
                string stampKey = $"{gameName} - {gameRegion}";

                // If the stamp key does not exist in the stamp dictionary, return -2
                Stamp? stamp = null;
                if (!LauncherMetadataStampDictionary?.TryGetValue(stampKey, out stamp) ?? false)
                    return -2;

                // Load the stamp from dictionary and if it's null, return -2
                if (stamp == null)
                    return -2;

                // Ignore the stamp generated by the plugin. Return as 0 (unmodified)
                if (stamp.MetadataType == MetadataType.PresetConfigPlugin)
                    return 0;

                // SANITIZE: MetadataPath cannot be empty or null
                if (string.IsNullOrEmpty(stamp.MetadataPath))
                    throw new NullReferenceException($"MetadataPath property inside of the stamp from: {stampKey} cannot be empty or null!");

                // Get the config file info
                string configLocalFilePath = Path.Combine(LauncherMetadataFolder, stamp.MetadataPath);
                FileInfo configLocalFileInfo = new FileInfo(configLocalFilePath);

                // Compare the last modified time. If it doesn't match, return 1 (modified)
                return configLocalFileInfo.LastWriteTimeUtc != stamp.LastModifiedTimeUtc ? 1 :
                    // Otherwise, return 0 (unmodified)
                    0;
            }, TaskCreationOptions.DenyChildAttach);
        }

        internal static async Task Initialize(bool isCacheUpdateModeOnly = false, bool isShowLoadingMessage = true)
        {
            if (isShowLoadingMessage)
            {
                LoadingMessageHelper.ShowLoadingFrame();
                LoadingMessageHelper.SetMessage(Locale.Lang._MainPage.Initializing, Locale.Lang._MainPage.LoadingLauncherMetadata);
            }

            // Initialize the variable and create the metadata folder if it doesn't exist
            string metadataFolder = LauncherMetadataFolder;
            if (!Directory.Exists(metadataFolder))
            {
                Directory.CreateDirectory(metadataFolder);
            }

            // Get the current channel
            string currentChannel = CurrentLauncherChannel;

            // Initialize the stamp and config file
            await InitializeStamp(currentChannel);
            await InitializeConfig(currentChannel, isCacheUpdateModeOnly, isShowLoadingMessage);

            // Load preset config from plugins
            await PluginManager.LoadPlugins(LauncherMetadataConfig!, LauncherGameNameRegionCollection!, LauncherMetadataStampDictionary!);
        }

        internal static async Task InitializeStamp(string currentChannel, bool throwAfterRetry = false)
        {
            string stampLocalFilePath = Path.Combine(LauncherMetadataFolder, LauncherMetadataStampPrefix);
            string stampRemoteFilePath = LauncherStampRemoteURLPath;

            // Initialize and clear the stamp dictionary
            LauncherMetadataStampDictionary ??= new Dictionary<string, Stamp>();
            LauncherMetadataStampDictionary.Clear();

            FileStream? stampLocalStream = null;

            // Load the stamp file
            try
            {
                // Get the local stream
                stampLocalStream = new FileStream(stampLocalFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);

                // Check if the file is empty, then download the file
                if (stampLocalStream.Length == 0)
                {
                    // Get the stream and download the file
                    await using BridgedNetworkStream stampRemoteStream =
                        await FallbackCDNUtil.TryGetCDNFallbackStream(stampRemoteFilePath);
                    await stampRemoteStream.CopyToAsync(stampLocalStream);

                    // Reset the position to 0
                    stampLocalStream.Position = 0;
                }

                // Deserialize the stream
                LauncherMetadataStamp =
                    await stampLocalStream.DeserializeAsListAsync(StampJsonContext.Default.Stamp);

                // SANITIZE: Check if the stamp is empty, then throw
                if (LauncherMetadataStamp == null || LauncherMetadataStamp.Count == 0)
                    throw new FormatException("JSON response of the stamp is empty or null!");

                // Get master key information first
                AddItemBasedOnType(LauncherMetadataStamp, LauncherMetadataStampDictionary, MetadataType.MasterKey);

                // Then Community Tools
                AddItemBasedOnType(LauncherMetadataStamp, LauncherMetadataStampDictionary, MetadataType.CommunityTools);

                // Then Game Preset Configs
                AddItemBasedOnType(LauncherMetadataStamp, LauncherMetadataStampDictionary, MetadataType.PresetConfigV2);

                // Last one, Unknown metadata
                AddItemBasedOnType(LauncherMetadataStamp, LauncherMetadataStampDictionary, MetadataType.Unknown);
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                // Throw if it's allowed
                if (throwAfterRetry)
                {
                    throw new TypeLoadException("Failed while trying to load Metadata Stamp!", ex);
                }

                // Or, retry the method recursively one more time.
                Logger.LogWriteLine($"An error has occurred while initializing Metadata Stamp! Retrying...\r\n{ex}",
                                    LogType.Warning, true);

                // Try to dispose and delete the old file first, then retry to initialize the stamp once again.
                if (stampLocalStream != null)
                {
                    await stampLocalStream.DisposeAsync();
                }

                if (File.Exists(stampLocalFilePath))
                {
                    File.Delete(stampLocalFilePath);
                }

                await InitializeStamp(currentChannel, true);
            }
            finally
            {
                // Dispose the local stream
                if (stampLocalStream != null)
                {
                    await stampLocalStream.DisposeAsync();
                }
            }
            return;

            static void AddItemBasedOnType(IEnumerable<Stamp?> stamps, Dictionary<string, Stamp> dict, MetadataType metadataType)
            {
                // Load and add stamp into stamp dictionary
                foreach (Stamp? stamp in stamps.Where(x => x?.MetadataType == metadataType))
                {
                    if (stamp == null)
                        continue;

                    string? gameName = string.IsNullOrEmpty(stamp.GameName) ? stamp.MetadataType.ToString() : stamp.GameName;
                    string? gameRegion = string.IsNullOrEmpty(stamp.GameRegion) ? stamp.MetadataPath : stamp.GameRegion;
                    dict?.Add($"{gameName} - {gameRegion}", stamp);
                }
            }
        }

        internal static async Task InitializeConfig(string currentChannel,
                                                    bool   isCacheUpdateModeOnly,
                                                    bool   isShowLoadingMessage)
        {
            if (LauncherMetadataStamp == null)
            {
                throw new NullReferenceException("The Metadata Stamp list is not initialized!");
            }

            if (LauncherMetadataStamp.Count == 0)
            {
                throw new InvalidOperationException("The Metadata Stamp list is empty!");
            }

            // Initialize the dictionary of the config
            LauncherMetadataConfig ??= [];
            LauncherMetadataConfig.Clear();

            // Initialize the game name region collection if it's null
            LauncherGameNameRegionCollection ??= [];
            LauncherGameNameRegionCollection.Clear();

            // Initialize the game master key config vault
            OtherMasterKeyConfigVault ??= [];
            OtherMasterKeyConfigVault.Clear();

            // Initialize the community tools properties
            PageStatics.CommunityToolsProperty ??= new CommunityToolsProperty();
            PageStatics.CommunityToolsProperty.Clear();
            PageStatics.CommunityToolsProperty.OfficialToolsDictionary?.Clear();
            PageStatics.CommunityToolsProperty.CommunityToolsDictionary?.Clear();

            #region Master Key Loading
            // Load master key config
            bool isMasterKeyLoaded = false;
            foreach (Stamp? stamp in LauncherMetadataStamp.Where(x => x?.MetadataType == MetadataType.MasterKey))
            {
                if (stamp == null)
                    continue;

                object? masterKey = await LoadAndGetConfig(stamp, currentChannel, false, true);
                if (masterKey is not MasterKeyConfig keyConfig)
                    continue;

                if (!isMasterKeyLoaded)
                {
                    CurrentMasterKey  = keyConfig;
                    isMasterKeyLoaded = true;
                }

                _ = OtherMasterKeyConfigVault.TryAdd(stamp.MetadataPath ?? "", keyConfig);
            }

            // Throw if none of the master key is loaded
            if (!isMasterKeyLoaded)
            {
                throw new InvalidOperationException("No master key load operation has been executed!");
            }
            #endregion

            #region Region Metadata Config Loading
            ConcurrentDictionary<string, PresetConfig> loadedRegionMetadata = [];
            List<Stamp?> regionMetadataStamps = LauncherMetadataStamp
                                               .Where(x => x?.MetadataType == MetadataType.PresetConfigV2)
                                               .ToList();

            // Load metadata in parallel
            int regionMetadataStampsCount = regionMetadataStamps.Count;
            await Parallel.ForEachAsync(regionMetadataStamps
                .Select(x => (x, loadedRegionMetadata))
                .Index(),
                LoadRegionMetadataConfig);

            // Sort the metadata based on stamps order
            foreach (Stamp? stamp in regionMetadataStamps)
            {
                if (stamp == null)
                    continue;

                string filePath = stamp.MetadataPath ?? "";
                if (!loadedRegionMetadata.TryGetValue(filePath, out PresetConfig? presetConfig))
                {
                    continue;
                }

                string gameName   = stamp.GameName ?? "";
                string gameRegion = stamp.GameRegion ?? "";

                // Re-add the region dict into the game name map
                if (!LauncherMetadataConfig.TryGetValue(gameName, out Dictionary<string, PresetConfig>? regionDict))
                {
                    LauncherMetadataConfig.Add(gameName, regionDict = []);
                }

                // Re-add the region preset config into its region dict
                if (!(regionDict?.TryAdd(gameRegion, presetConfig) ?? false))
                {
                    continue;
                }

                // Re-add the region list into the game name map
                if (!LauncherGameNameRegionCollection.TryGetValue(gameName, out List<string>? regionList))
                {
                    LauncherGameNameRegionCollection.Add(gameName, regionList = []);
                }

                // Re-add the region name into its region dict
                regionList?.Add(gameRegion);
            }

            async ValueTask LoadRegionMetadataConfig((int, (Stamp?, ConcurrentDictionary<string, PresetConfig>)) state, CancellationToken token)
            {
                int                                        index            = state.Item1;
                Stamp?                                     stamp            = state.Item2.Item1;
                ConcurrentDictionary<string, PresetConfig> regionDictionary = state.Item2.Item2;

                if (stamp == null || string.IsNullOrEmpty(stamp.GameName) || string.IsNullOrEmpty(stamp.GameRegion))
                    return;

                if (isShowLoadingMessage)
                {
                    LoadingMessageHelper.SetMessage(Locale.Lang._MainPage.Initializing,
                                                    $"{Locale.Lang._MainPage.LoadingGameConfiguration} [{index}/{regionMetadataStampsCount}]: " +
                                                    $"{InnerLauncherConfig.GetGameTitleRegionTranslationString(stamp.GameName, Locale.Lang._GameClientTitles)} - " +
                                                    $"{InnerLauncherConfig.GetGameTitleRegionTranslationString(stamp.GameRegion, Locale.Lang._GameClientRegions)}");
                }

                object? regionMetadataObject = await LoadAndGetConfig(stamp, currentChannel);
                if (regionMetadataObject is PresetConfig presetConfig)
                {
                    // If the cache update mode is enabled and the config is not enabled, then skip
                    if (isCacheUpdateModeOnly && (!presetConfig.IsCacheUpdateEnabled ?? false)) return;

                    // Try to add the preset config map into the dictionary
                    regionDictionary.TryAdd(stamp.MetadataPath ?? "", presetConfig);
                }
            }
            #endregion

            #region Community Tools Config Loading
            foreach (Stamp? stamp in LauncherMetadataStamp.Where(x => x?.MetadataType == MetadataType.CommunityTools))
            {
                if (stamp == null)
                    continue;

                object? communityTools = await LoadAndGetConfig(stamp, currentChannel);
                if (communityTools is not CommunityToolsProperty communityToolsProperty)
                    continue;

                foreach (KeyValuePair<GameNameType, List<CommunityToolsEntry>> entry in communityToolsProperty.OfficialToolsDictionary ?? [])
                {
                    PageStatics.CommunityToolsProperty.OfficialToolsDictionary?.TryAdd(entry.Key, entry.Value);
                }

                foreach (KeyValuePair<GameNameType, List<CommunityToolsEntry>> entry in communityToolsProperty.CommunityToolsDictionary ?? [])
                {
                    PageStatics.CommunityToolsProperty.CommunityToolsDictionary?.TryAdd(entry.Key, entry.Value);
                }
            }
            #endregion

            // Save the current count of game name and game regions
            CurrentGameNameCount      = LauncherMetadataConfig.Keys.Count;
            CurrentGameRegionMaxCount = LauncherMetadataConfig.Max(x => x.Value?.Count ?? 0);
        }

        internal static async Task<object?> LoadAndGetConfig(Stamp  stamp,
                                                             string currentChannel,
                                                             bool   throwAfterRetry     = false,
                                                             bool   allowDeserializeKey = false)
        {
            if (string.IsNullOrEmpty(stamp.MetadataPath))
            {
                throw new NullReferenceException($"The metadata stamp for this {stamp.MetadataType} type is empty!");
            }

            string configLocalFilePath = Path.Combine(LauncherMetadataFolder, stamp.MetadataPath);
            string configRemoteFilePath =
                $"/metadata/{MetadataVersion}/{currentChannel}/".CombineURLFromString(stamp.MetadataPath);

            FileStream? configLocalStream = null;
            try
            {
                // Get the local stream
                configLocalStream = new FileStream(configLocalFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);

                // Check if the file doesn't exist, then download the file
                if (configLocalStream.Length == 0)
                {
                    // Get the stream and download the file
                    await using BridgedNetworkStream stampRemoteStream =
                        await FallbackCDNUtil.TryGetCDNFallbackStream(configRemoteFilePath);
                    await stampRemoteStream.CopyToAsync(configLocalStream);

                    // Reset the position to 0
                    configLocalStream.Position = 0;
                }

                switch (stamp.MetadataType)
                {
                    case MetadataType.MasterKey when allowDeserializeKey:
                        {
                            // Deserialize the key config
                            MasterKeyConfig? keyConfig =
                                await configLocalStream.DeserializeAsync(MesterKeyConfigJsonContext.Default.MasterKeyConfig);

                            // Return the key to instance property
                            return keyConfig;
                        }
                    case MetadataType.CommunityTools:
                        {
                            // Deserialize the community tools
                            return await CommunityToolsProperty.LoadCommunityTools(configLocalStream);
                        }
                    case MetadataType.PresetConfigV2 when string.IsNullOrEmpty(stamp.GameName) || string.IsNullOrEmpty(stamp.GameRegion):
                        throw new NullReferenceException("Game name or region property inside the stamp is empty!");
                    // Deserialize the config
                    case MetadataType.PresetConfigV2:
                        {
                            PresetConfig? presetConfig =
                                await configLocalStream.DeserializeAsync(PresetConfigJsonContext.Default.PresetConfig);

                            if (presetConfig == null)
                            {
                                throw new InvalidDataException("Config seems to be empty!");
                            }

                            // Generate HashID and GameName
                            string hashComposition = $"{stamp.LastUpdated} - {stamp.GameName} - {stamp.GameRegion}";
                            byte[] hashBytes = Hash.GetHashFromString<Crc32>(hashComposition);
                            int hashID = BitConverter.ToInt32(hashBytes);

                            presetConfig.HashID = hashID;
                            presetConfig.GameName = stamp.GameName;
                            presetConfig.GameLauncherApi ??= presetConfig.LauncherType switch
                            {
                                LauncherType.Sophon => LegacyLauncherApiLoader.CreateApiInstance(presetConfig, stamp.GameName, stamp.GameRegion),
                                LauncherType.HoYoPlay => HoYoPlayLauncherApiLoader.CreateApiInstance(presetConfig, stamp.GameName, stamp.GameRegion),
                                _ => throw new NotSupportedException($"Launcher type: {presetConfig.LauncherType} is not supported!")
                            };

                            // Dispose the file first
                            await configLocalStream.DisposeAsync();

                            return presetConfig;
                        }
                    case MetadataType.Unknown:
                        throw new NotSupportedException($"Metadata type: {MetadataType.Unknown} is not supported!");
                    default:
                        throw new ArgumentOutOfRangeException(nameof(stamp), $"Cannot deserialize metadata as the type: {stamp.MetadataType} is unknown");
                }
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                // Throw if it's allowed
                if (throwAfterRetry)
                {
                    throw new
                        TypeLoadException($"Failed while trying to load Metadata Config for: {stamp.GameName} - {stamp.GameRegion}!",
                                          ex);
                }

                // Or, retry the method recursively one more time.
                Logger.LogWriteLine($"An error has occurred while initializing Metadata Stamp! Retrying...\r\n{ex}",
                                    LogType.Warning, true);

                // Try to dispose and delete the old file first, then retry to initialize the config once again.
                if (configLocalStream != null)
                    await configLocalStream.DisposeAsync();

                if (File.Exists(configLocalFilePath))
                    File.Delete(configLocalFilePath);

                return await LoadAndGetConfig(stamp, currentChannel, true, allowDeserializeKey);
            }
            finally
            {
                // Dispose the local stream
                if (configLocalStream != null)
                {
                    await configLocalStream.DisposeAsync();

                    // Register last write timestamp into Stamp
                    stamp.LastModifiedTimeUtc = GetFileLastModifiedStampUtc(configLocalFilePath);
                }
            }
        }

        private static DateTime GetFileLastModifiedStampUtc(string configLocalFilePath)
        {
            FileInfo fileInfo = new FileInfo(configLocalFilePath);
            return fileInfo.LastWriteTimeUtc;
        }

        internal static async ValueTask<bool> IsMetadataHasUpdate()
        {
            // Delay the routine if the update check or routine is running
            while (_isUpdateCheckRunning || _isUpdateRoutineRunning)
            {
                await Task.Delay(1000);
            }

            try
            {
                _isUpdateCheckRunning = true;

                // Get the remote stream
                string stampRemoteFilePath = LauncherStampRemoteURLPath;
                await using BridgedNetworkStream stampRemoteStream =
                    await FallbackCDNUtil.TryGetCDNFallbackStream(stampRemoteFilePath);

                // Check and throw if the stream returns null or empty
                if (stampRemoteStream != null)
                {
                    List<Stamp?>? remoteMetadataStampList =
                        await stampRemoteStream.DeserializeAsListAsync(StampJsonContext.Default.Stamp);

                    // Check and throw if the metadata stamp returns null or empty
                    if (remoteMetadataStampList == null || remoteMetadataStampList.Count == 0)
                    {
                        throw new
                            NullReferenceException("MetadataV3 stamp list is returns a null or empty after deserialization!");
                    }

                    NewUpdateMetadataStamp ??= [];

                    // Make sure to clear the new update list first
                    NewUpdateMetadataStamp.Clear();

                    // Do iteration and check if the stamp is outdated
                    bool isOutdatedStampDetected = false;
                    foreach (Stamp? remoteMetadataStamp in remoteMetadataStampList)
                    {
                        if (remoteMetadataStamp == null)
                            continue;

                        // Check if the local stamp does not have one, then add it to new update stamp list
                        Stamp? localStamp =
                            LauncherMetadataStamp?.FirstOrDefault(x => remoteMetadataStamp.GameRegion ==
                                                                       x?.GameRegion
                                                                       && remoteMetadataStamp.GameName ==
                                                                       x?.GameName
                                                                       && remoteMetadataStamp.LastUpdated ==
                                                                       x?.LastUpdated
                                                                       && remoteMetadataStamp.MetadataPath ==
                                                                       x.MetadataPath
                                                                       && remoteMetadataStamp.MetadataType ==
                                                                       x.MetadataType);
                        if (localStamp != null) continue;


                        // If null, then add it to new update list
                        Logger.LogWriteLine($"A new metadata config was found! [Name: {remoteMetadataStamp.GameName} | Region: {remoteMetadataStamp.GameRegion} | Type: {remoteMetadataStamp.MetadataType}] at {remoteMetadataStamp.LastUpdated}",
                                            LogType.Default, true);
                        isOutdatedStampDetected = true;
                        NewUpdateMetadataStamp?.Add(remoteMetadataStamp);
                    }

                    // Return the status
                    return isOutdatedStampDetected;
                }
                else
                {
                    throw new NullReferenceException("MetadataV3 stamp check stream returns a null or empty, which means there might be an issue while retrieving stream of the stamp!");
                }
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                Logger.LogWriteLine($"An error has occurred while checking MetadataV3 update!\r\n{ex}", LogType.Error,
                                    true);
                return false;
            }
            finally
            {
                // Clear the new update stamp list and set the lock state
                _isUpdateCheckRunning = false;
            }
        }

        internal static ConcurrentDictionary<Stamp, byte> MetadataUpdateEntry = new();
        internal static async ValueTask RunMetadataUpdate()
        {
            // Delay the routine if the update check or routine is running
            while (_isUpdateCheckRunning || _isUpdateRoutineRunning)
            {
                await Task.Delay(1000);
            }

            try
            {
                _isUpdateRoutineRunning = true;

                // If the new update list is null or empty, then return
                if (NewUpdateMetadataStamp == null || NewUpdateMetadataStamp.Count == 0)
                {
                    Logger.LogWriteLine("The new update stamp is empty! Please make sure that IsMetadataHasUpdate() has been executed and returns true.");
                    return;
                }

                // Remove the old metadata config file first
                foreach (Stamp? newUpdateStamp in NewUpdateMetadataStamp)
                {
                    if (newUpdateStamp == null) continue;
                    if (!MetadataUpdateEntry.TryAdd(newUpdateStamp, 0))
                    {
                        Logger.LogWriteLine($"[RunMetadataUpdate] Skipping duplicate assignment for stamp:\r\n\t" +
                                            $"N : {newUpdateStamp.GameName}\r\n\tT : {newUpdateStamp.MetadataType}",
                                            LogType.Error, true);
                        continue;
                    }

                    // Ensure if the MetadataPath is not empty
                    if (string.IsNullOrEmpty(newUpdateStamp.MetadataPath))
                    {
                        throw new NullReferenceException("MetadataPath defined inside of the stamp is empty or null!");
                    }

                    // Get the local config file path and remove it if it exists
                    string configLocalFilePath = Path.Combine(LauncherMetadataFolder, newUpdateStamp.MetadataPath);
                    if (File.Exists(configLocalFilePath))
                    {
                        File.Delete(configLocalFilePath);
                    }

                    Logger.LogWriteLine($"Removed old metadata config file! [Name: {newUpdateStamp.GameName} | Region: {newUpdateStamp.GameRegion} | Type: {newUpdateStamp.MetadataType}]\r\nLocation: {configLocalFilePath}",
                                        LogType.Default, true);
                    MetadataUpdateEntry.Remove(newUpdateStamp, out _);
                }

                // Then update the stamp file
                string stampLocalFilePath = Path.Combine(LauncherMetadataFolder, LauncherMetadataStampPrefix);
                await UpdateStampContent(stampLocalFilePath, NewUpdateMetadataStamp);

                // Then reinitialize the metadata
                await Initialize();
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                Logger.LogWriteLine($"An error has occurred while updating MetadataV3!\r\n{ex}", LogType.Error, true);
            }
            finally
            {
                _isUpdateRoutineRunning = false;
            }
        }

        private static async Task UpdateStampContent(string stampPath, List<Stamp?> newStampList)
        {
            try
            {
                // Check the file existence
                if (!File.Exists(stampPath))
                    throw new FileNotFoundException($"Unable to update the stamp file because it is not exist! It should have been located here: {stampPath}");

                // Read the old stamp list stream
                List<Stamp?> oldStampList;
                await using (FileStream stampStream = File.OpenRead(stampPath))
                {
                    // Deserialize and do sanitize if the old stamp list is empty
                    oldStampList = await stampStream.DeserializeAsListAsync(StampJsonContext.Default.Stamp);
                    if (oldStampList == null || oldStampList.Count == 0)
                        throw new NullReferenceException("The old stamp list contains an empty/null content!");

                    // Try to iterate the new stamp list to replace the old ones or add a new entry
                    foreach (Stamp? newStamp in newStampList)
                    {
                        if (newStamp == null) continue;

                        // Find the old stamp reference from the old list
                        Stamp? oldStampRef = oldStampList?.FirstOrDefault(x => newStamp.GameRegion ==
                                                                          x?.GameRegion
                                                                          && newStamp.GameName ==
                                                                          x?.GameName
                                                                          && newStamp.MetadataPath ==
                                                                          x?.MetadataPath
                                                                          && newStamp.MetadataType ==
                                                                          x?.MetadataType);
                        // Check if the old stamp ref is null or index of old stamp reference returns < 0, then
                        // add it as a new entry.
                        int indexOfOldStamp;
                        if (oldStampRef == null || (indexOfOldStamp = oldStampList?.IndexOf(oldStampRef) ?? -1) < 0)
                            oldStampList?.Add(newStamp);
                        // Otherwise, overwrite with the new one
                        else
                            oldStampList![indexOfOldStamp] = newStamp;
                    }
                }

                // Now write the updated list to the stamp file
                await using (FileStream updatedStampStream = File.Create(stampPath))
                {
                    await oldStampList.SerializeAsync(updatedStampStream, StampJsonContext.Default.ListStamp);
                    return;
                }
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                Logger.LogWriteLine($"An error has occurred while updating stamp file content. Removing the stamp file instead!\r\n{ex}", LogType.Error, true);
            }

            // Remove the file instead if an error occurred
            if (File.Exists(stampPath))
            {
                File.Delete(stampPath);
                Logger.LogWriteLine($"Removed old metadata stamp file!\r\nLocation: {stampPath}",
                                    LogType.Default, true);
            }
        }

        internal static List<string>? GetGameNameCollection()
        {
            return LauncherGameNameRegionCollection?.Keys.ToList();
        }

        internal static List<string>? GetGameRegionCollection(string gameName)
        {
            if (LauncherGameNameRegionCollection?.TryGetValue(gameName, out List<string>? hashSetRegion) ?? false)
            {
                return hashSetRegion!;
            }

            string msg = $"Game region collection for name: \"{gameName}\" isn't exist!";
            Logger.LogWriteLine(msg, LogType.Error, true);
            return null;
        }

        internal static int GetPreviousGameRegion(string? gameName)
        {
            // Get the config key name
            string iniKeyName = $"LastRegion_{gameName!.Replace(" ", string.Empty)}";
            string? gameRegion;

            // Get the region collection
            List<string>? gameRegionCollection = GetGameRegionCollection(gameName);
            gameRegionCollection ??= LauncherGameNameRegionCollection?.FirstOrDefault().Value!;

            // Throw if the collection is empty or null
            if (gameRegionCollection == null || gameRegionCollection.Count == 0)
            {
                throw new NullReferenceException("Game region collection is null or empty!");
            }

            // If the config key name is not exist, then return the first region
            if (!LauncherConfig.IsConfigKeyExist(iniKeyName))
            {
                gameRegion = gameRegionCollection.FirstOrDefault();
                LauncherConfig.SetAndSaveConfigValue(iniKeyName, gameRegion);
                return 0;
            }

            // Get the last region and find the index inside the collection.
            // If not found, then set the region to the first.
            gameRegion = LauncherConfig.GetAppConfigValue(iniKeyName).ToString();
            int indexOfGameRegion = gameRegionCollection.IndexOf(gameRegion!);
            return indexOfGameRegion < 1 ? 0 : indexOfGameRegion;
        }

        public static void SetPreviousGameRegion(string? gameCategoryName, string? regionName, bool isSave = true)
        {
            string iniKeyName = $"LastRegion_{gameCategoryName?.Replace(" ", string.Empty)}";

            if (isSave)
            {
                LauncherConfig.SetAndSaveConfigValue(iniKeyName, regionName);
            }
            else
            {
                LauncherConfig.SetAppConfigValue(iniKeyName, regionName);
            }
        }
    }
}
