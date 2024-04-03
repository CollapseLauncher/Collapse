using CollapseLauncher.Helper.Loading;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.Region;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Helper.Metadata
{
    internal static class LauncherMetadataHelper
    {
        private const string MetadataVersion = "v3";
        private const string LauncherMetadataStampPrefix = "stamp.json";

        #region Metadata and Stamp path/url prefixes
        internal static string CurrentLauncherChannel => LauncherConfig.IsPreview ? "preview" : "stable";
        internal static string LauncherMetadataFolder => Path.Combine(LauncherConfig.AppGameFolder, "_metadatav3");
        internal static string LauncherStampRemoteURLPath => ConverterTool.CombineURLFromString($"/metadata/{MetadataVersion}/{CurrentLauncherChannel}/", LauncherMetadataStampPrefix);
        #endregion

        #region Metadata Stamp List and Config Dictionary
        internal static List<Stamp>? LauncherMetadataStamp { get; private set; }
        internal static List<Stamp>? NewUpdateMetadataStamp { get; private set; }
        internal static List<string>? LauncherGameNameCollection => LauncherGameNameRegionCollection?.Keys.ToList();
        internal static Dictionary<string, Dictionary<string, PresetConfig>?>? LauncherMetadataConfig { get; private set; }
        #endregion

        /* TODO: add real-time change support
        private static TimeSpan _lastModifiedCurrentConfig;
        private static PresetConfig? _currentConfig;
        private static long _currentConfigHash;
        */

        #region Game Name & Region Collection and Current Config
        internal static PresetConfig? CurrentMetadataConfig;
        internal static string? CurrentMetadataConfigGameName;
        internal static string? CurrentMetadataConfigGameRegion;
        internal static Dictionary<string, List<string>?>? LauncherGameNameRegionCollection { get; private set; }
        #endregion

        #region Current Master Key config
        internal static MasterKeyConfig? CurrentMasterKey { get; private set; }
        #endregion

        #region Current Game Name and Max Region Counts
        internal static int CurrentGameNameCount { get; private set; }
        internal static int CurrentGameRegionMaxCount { get; private set; }
        #endregion

        #region Internal State Lock Boolean
        private static bool IsUpdateCheckRunning = false;
        private static bool IsUpdateRoutineRunning = false;
        #endregion

        internal static PresetConfig? GetMetadataConfig(string gameName, string gameRegion)
        {
            PresetConfig? config = LauncherMetadataConfig?[gameName]?[gameRegion];
            if (config != null)
            {
                CurrentMetadataConfig = config;
                CurrentMetadataConfigGameName = gameName;
                CurrentMetadataConfigGameRegion = gameRegion;
            }

            return LauncherMetadataConfig?[gameName]?[gameRegion];
            // TODO: add real-time change support
            /*
            Stamp? stamp = LauncherMetadataStamp?.FirstOrDefault(x => x.GameName == gameName && x.GameRegion == gameRegion);
            if (stamp == null)
                throw new KeyNotFoundException($"Metadata stamp with Game Name: {gameName} and Game Region: {gameRegion} is not found!");

            if (string.IsNullOrEmpty(stamp.MetadataPath))
                throw new NullReferenceException($"The current stamp for Game Name: {gameName} and Game Region: {gameRegion} has empty or undefined MetadataPath!");

            string configLocalFilePath = Path.Combine(LauncherMetadataFolder, stamp.MetadataPath);
            FileInfo configLocalFileInfo = new FileInfo(configLocalFilePath);
            */
        }

        internal static async ValueTask Initialize(bool isCacheUpdateModeOnly = false, bool isShowLoadingMessage = true)
        {
            if (isShowLoadingMessage)
            {
                LoadingMessageHelper.ShowLoadingFrame();
                LoadingMessageHelper.SetMessage("Initializing", "Loading Launcher Metadata");
            }

            // Initialize the variable and create the metadata folder if it doesn't exist
            string metadataFolder = LauncherMetadataFolder;
            if (!Directory.Exists(metadataFolder))
                Directory.CreateDirectory(metadataFolder);

            // Get the current channel
            string currentChannel = CurrentLauncherChannel;

            // Initialize the stamp and config file
            await InitializeStamp(currentChannel);
            await InitializeConfig(currentChannel, isCacheUpdateModeOnly, isShowLoadingMessage);
        }

        internal static async ValueTask InitializeStamp(string currentChannel, bool throwAfterRetry = false)
        {
            string stampLocalFilePath = Path.Combine(LauncherMetadataFolder, LauncherMetadataStampPrefix);
            string stampRemoteFilePath = LauncherStampRemoteURLPath;

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
                    await using BridgedNetworkStream stampRemoteStream = await FallbackCDNUtil.TryGetCDNFallbackStream(stampRemoteFilePath, default);
                    await stampRemoteStream.CopyToAsync(stampLocalStream);

                    // Reset the position to 0
                    stampLocalStream.Position = 0;
                }

                // Deserialize the stream
                LauncherMetadataStamp = await stampLocalStream.DeserializeAsync<List<Stamp>>(InternalAppJSONContext.Default);
            }
            catch (Exception ex)
            {
                // Throw if it's allowed
                if (throwAfterRetry)
                    throw new TypeLoadException($"Failed while trying to load Metadata Stamp!", ex);

                // Or, retry the method recursively one more time.
                Logger.LogWriteLine($"An error has occurred while initializing Metadata Stamp! Retrying...\r\n{ex}", LogType.Warning, true);

                // Try dispose and delete the old file first, then retry to initialize the stamp once again.
                if (stampLocalStream != null) await stampLocalStream.DisposeAsync();
                if (File.Exists(stampLocalFilePath)) File.Delete(stampLocalFilePath);
                await InitializeStamp(currentChannel, true);
            }
            finally
            {
                // Dispose the local stream
                if (stampLocalStream != null) await stampLocalStream.DisposeAsync();
            }
        }

        internal static async ValueTask InitializeConfig(string currentChannel, bool isCacheUpdateModeOnly, bool isShowLoadingMessage)
        {
            if (LauncherMetadataStamp == null) throw new NullReferenceException($"The Metadata Stamp list is not initialized!");
            if (LauncherMetadataStamp.Count == 0) throw new InvalidOperationException($"The Metadata Stamp list is empty!");

            // Initialize the dictionary of the config
            if (LauncherMetadataConfig == null)
                LauncherMetadataConfig = new Dictionary<string, Dictionary<string, PresetConfig>?>();
            LauncherMetadataConfig.Clear();

            // Initialize the game name region collection if it's null
            if (LauncherGameNameRegionCollection == null)
                LauncherGameNameRegionCollection = new Dictionary<string, List<string>?>();
            LauncherGameNameRegionCollection.Clear();

            // Find and iterate the master key first
            Stamp? masterKeyStamp = LauncherMetadataStamp?.FirstOrDefault(x => x.MetadataType == MetadataType.MasterKey);
            if (masterKeyStamp == null) throw new KeyNotFoundException($"Master key information is not found in the stamp!");
            await LoadConfigInner(masterKeyStamp, currentChannel, false, true);

            // Iterate the stamp and try to load the configs
            int index = 1;
            List<Stamp> stampList = LauncherMetadataStamp!.Where(x => x.MetadataType == MetadataType.PresetConfigV2).ToList();
            foreach (Stamp stamp in stampList)
            {
                if (isShowLoadingMessage)
                    LoadingMessageHelper.SetMessage("Initializing", $"Loading Game Configuration [{index++}/{stampList?.Count}]: {InnerLauncherConfig.GetGameTitleRegionTranslationString(stamp.GameName, Locale.Lang._GameClientTitles)} - {InnerLauncherConfig.GetGameTitleRegionTranslationString(stamp.GameRegion, Locale.Lang._GameClientRegions)}");

                await LoadConfigInner(stamp, currentChannel, false, false, isCacheUpdateModeOnly);
            }

            // Save the current count of game name and game regions
            CurrentGameNameCount = LauncherMetadataConfig.Keys.Count;
            CurrentGameRegionMaxCount = LauncherMetadataConfig.Max(x => x.Value?.Count ?? 0);
        }

        internal static async ValueTask LoadConfigInner(Stamp stamp, string currentChannel, bool throwAfterRetry = false, bool allowDeserializeKey = false, bool isCacheUpdateModeOnly = false)
        {
            if (string.IsNullOrEmpty(stamp.MetadataPath)) throw new NullReferenceException($"The metadata stamp for this {stamp.MetadataType} type is empty!");

            string configLocalFilePath = Path.Combine(LauncherMetadataFolder, stamp.MetadataPath);
            string configRemoteFilePath = ConverterTool.CombineURLFromString($"/metadata/{MetadataVersion}/{currentChannel}/", stamp.MetadataPath);

            FileStream? configLocalStream = null;
            try
            {
                // Get the local stream
                configLocalStream = new FileStream(configLocalFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);

                // Check if the file doesn't exist, then download the file
                if (configLocalStream.Length == 0)
                {
                    // Get the stream and download the file
                    await using BridgedNetworkStream stampRemoteStream = await FallbackCDNUtil.TryGetCDNFallbackStream(configRemoteFilePath, default);
                    await stampRemoteStream.CopyToAsync(configLocalStream);

                    // Reset the position to 0
                    configLocalStream.Position = 0;
                }

                if (stamp.MetadataType == MetadataType.MasterKey && allowDeserializeKey)
                {
                    // Deserialize the key config
                    MasterKeyConfig? keyConfig = await configLocalStream.DeserializeAsync<MasterKeyConfig>(InternalAppJSONContext.Default);
                    if (keyConfig == null) throw new InvalidDataException($"Master key config seems to be empty!");

                    // Assign the key to instance property
                    CurrentMasterKey = keyConfig;
                }

                if (stamp.MetadataType == MetadataType.PresetConfigV2)
                {
                    if (string.IsNullOrEmpty(stamp.GameName) || string.IsNullOrEmpty(stamp.GameRegion)) throw new NullReferenceException($"Game name or region property inside the stamp is empty!");

                    // Deserialize the config
                    PresetConfig? presetConfig = await configLocalStream.DeserializeAsync<PresetConfig>(InternalAppJSONContext.Default);
                    if (presetConfig == null) throw new InvalidDataException($"Config seems to be empty!");

                    // Ignore if the isCacheUpdateModeOnly is true and the config doesn't support cache update
                    if (isCacheUpdateModeOnly && (!presetConfig.IsCacheUpdateEnabled ?? false))
                        return;

                    // Generate HashID and GameName
                    string HashComposition = $"{stamp.LastUpdated} - {stamp.GameName} - {stamp.GameRegion}";
                    int HashID = ConverterTool.BytesToCRC32Int(HashComposition);
                    presetConfig.HashID = HashID;
                    presetConfig.GameName = stamp.GameName;

                    // Dispose the file first
                    await configLocalStream.DisposeAsync();

                    // Get the file timestamp and set it to the variable
                    FileInfo configFileInfo = new FileInfo(configLocalFilePath);
                    DateTime configFileLastModified = configFileInfo.LastWriteTimeUtc;

                    // If the dictionary doesn't contains the dictionary of the game, then initialize it
                    if (!LauncherMetadataConfig?.ContainsKey(stamp.GameName) ?? false)
                    {
                        // Initialize and add the game preset config dictionary
                        Dictionary<string, PresetConfig>? presetConfigDict = new Dictionary<string, PresetConfig>();
                        LauncherMetadataConfig?.Add(stamp.GameName, presetConfigDict);
                    }

                    // If the game name region collection is not exist, create a new one
                    if (!LauncherGameNameRegionCollection?.ContainsKey(stamp.GameName) ?? false)
                        LauncherGameNameRegionCollection?.Add(stamp.GameName, new List<string>());

                    // Add the game region name into collection
                    LauncherGameNameRegionCollection?[stamp.GameName]?.Add(stamp.GameRegion);

                    // If the game preset config dictionary doesn't have the game region, then add it.
                    if (!LauncherMetadataConfig?[stamp.GameName]?.ContainsKey(stamp.GameRegion) ?? false)
                        LauncherMetadataConfig?[stamp.GameName]?.Add(stamp.GameRegion, presetConfig);
                }
            }
            catch (Exception ex)
            {
                // Throw if it's allowed
                if (throwAfterRetry)
                    throw new TypeLoadException($"Failed while trying to load Metadata Config for: {stamp.GameName} - {stamp.GameRegion}!", ex);

                // Or, retry the method recursively one more time.
                Logger.LogWriteLine($"An error has occurred while initializing Metadata Stamp! Retrying...\r\n{ex}", LogType.Warning, true);

                // Try dispose and delete the old file first, then retry to initialize the config once again.
                if (configLocalStream != null) await configLocalStream.DisposeAsync();
                if (File.Exists(configLocalFilePath)) File.Delete(configLocalFilePath);
                await LoadConfigInner(stamp, currentChannel, true, allowDeserializeKey, isCacheUpdateModeOnly);
            }
            finally
            {
                // Dispose the local stream
                if (configLocalStream != null) await configLocalStream.DisposeAsync();
            }
        }

        internal static async ValueTask<bool> IsMetadataHasUpdate()
        {
            // Delay the routine if the update check or routine is running
            while (IsUpdateCheckRunning || IsUpdateRoutineRunning) await Task.Delay(1000);

            try
            {
                IsUpdateCheckRunning = true;

                // Get the remote stream
                string stampRemoteFilePath = LauncherStampRemoteURLPath;
                await using BridgedNetworkStream stampRemoteStream = await FallbackCDNUtil.TryGetCDNFallbackStream(stampRemoteFilePath, default);

                // Check and throw if the stream returns null or empty
                if (stampRemoteStream == null)
                    throw new NullReferenceException("MetadataV3 stamp check stream returns a null or empty, which means there might be an issue while retrieving stream of the stamp!");

                // Deserialize the stream
                List<Stamp>? remoteMetadataStampList = await stampRemoteStream.DeserializeAsync<List<Stamp>>(InternalAppJSONContext.Default);

                // Check and throw if the metadata stamp returns null or empty
                if (remoteMetadataStampList == null || remoteMetadataStampList.Count == 0)
                    throw new NullReferenceException($"MetadataV3 stamp list is returns a null or empty after deserialization!");

                if (NewUpdateMetadataStamp == null)
                    NewUpdateMetadataStamp = new List<Stamp>();

                // Make sure to clear the new update list first
                NewUpdateMetadataStamp?.Clear();

                // Do iteration and check if the stamp is outdated
                bool isOutdatedStampDetected = false;
                foreach (Stamp? remoteMetadataStamp in remoteMetadataStampList)
                {
                    // Check if the local stamp does not have one, then add it to new update stamp list
                    Stamp? localStamp = LauncherMetadataStamp?.FirstOrDefault(x => remoteMetadataStamp?.GameRegion == x.GameRegion
                    && remoteMetadataStamp?.GameName == x.GameName
                    && remoteMetadataStamp?.LastUpdated == x.LastUpdated
                    && remoteMetadataStamp?.MetadataPath == x.MetadataPath
                    && remoteMetadataStamp?.MetadataType == x.MetadataType);

                    // If null, then add it to new update list
                    if (localStamp == null && remoteMetadataStamp != null)
                    {
                        Logger.LogWriteLine($"A new metadata config was found! [Name: {remoteMetadataStamp.GameName} | Region: {remoteMetadataStamp.GameRegion} | Type: {remoteMetadataStamp.MetadataType}] at {remoteMetadataStamp.LastUpdated}", LogType.Default, true);
                        isOutdatedStampDetected = true;
                        NewUpdateMetadataStamp?.Add(remoteMetadataStamp);
                    }
                }

                // Return the status
                return isOutdatedStampDetected;
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"An error has occurred while checking MetadataV3 update!\r\n{ex}", LogType.Error, true);
                return false;
            }
            finally
            {
                // Clear the new update stamp list and set the lock state
                IsUpdateCheckRunning = false;
            }
        }

        internal static async ValueTask RunMetadataUpdate()
        {
            // Delay the routine if the update check or routine is running
            while (IsUpdateCheckRunning || IsUpdateRoutineRunning) await Task.Delay(1000);

            try
            {
                IsUpdateRoutineRunning = true;

                // If the new update list is null or empty, then return
                if (NewUpdateMetadataStamp == null || NewUpdateMetadataStamp.Count == 0)
                {
                    Logger.LogWriteLine($"The new update stamp is empty! Please make sure that IsMetadataHasUpdate() has been executed and returns true.");
                    return;
                }

                // Remove the old metadata config file first
                foreach (Stamp newUpdateStamp in NewUpdateMetadataStamp)
                {
                    // Ensure if the MetadataPath is not empty
                    if (string.IsNullOrEmpty(newUpdateStamp.MetadataPath))
                        throw new NullReferenceException($"MetadataPath defined inside of the stamp is empty or null!");

                    // Get the local config file path and remove it if it exist
                    string configLocalFilePath = Path.Combine(LauncherMetadataFolder, newUpdateStamp.MetadataPath);
                    if (File.Exists(configLocalFilePath))
                        File.Delete(configLocalFilePath);

                    Logger.LogWriteLine($"Removed old metadata config file! [Name: {newUpdateStamp.GameName} | Region: {newUpdateStamp.GameRegion} | Type: {newUpdateStamp.MetadataType}]\r\nLocation: {configLocalFilePath}", LogType.Default, true);
                }

                // Then remove the stamp file
                string stampLocalFilePath = Path.Combine(LauncherMetadataFolder, LauncherMetadataStampPrefix);
                if (File.Exists(stampLocalFilePath))
                    File.Delete(stampLocalFilePath);
                
                Logger.LogWriteLine($"Removed old metadata stamp file!\r\nLocation: {stampLocalFilePath}", LogType.Default, true);

                // Then reinitialize the metadata
                await Initialize(false, true);
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"An error has occurred while updating MetadataV3!\r\n{ex}", LogType.Error, true);
            }
            finally
            {
                IsUpdateRoutineRunning = false;
            }
        }

        internal static List<string>? GetGameNameCollection() => LauncherGameNameRegionCollection?.Keys.ToList();

        internal static List<string>? GetGameRegionCollection(string gameName)
        {
            if (!LauncherGameNameRegionCollection?.ContainsKey(gameName) ?? false)
            {
                Logger.LogWriteLine($"Game region collection for name: \"{gameName}\" isn't exist!", LogType.Error, true);
                return null;
            }

            return LauncherGameNameRegionCollection?[gameName];
        }

        internal static int GetPreviousGameRegion(string gameName)
        {
            // Get the config key name
            string iniKeyName = $"LastRegion_{gameName!.Replace(" ", string.Empty)}";
            string? gameRegion;

            // Get the region collection
            List<string>? gameRegionCollection = GetGameRegionCollection(gameName);
            gameRegionCollection ??= LauncherGameNameRegionCollection?.FirstOrDefault().Value;

            // Throw if the collection is empty or null
            if (gameRegionCollection == null || gameRegionCollection.Count == 0)
                throw new NullReferenceException($"Game region collection is null or empty!");

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
            int indexOfGameRegion = gameRegionCollection?.IndexOf(gameRegion) ?? -1;
            return indexOfGameRegion < 1 ? 0 : indexOfGameRegion;
        }

        public static void SetPreviousGameRegion(string GameCategoryName, string RegionName, bool isSave = true)
        {
            string iniKeyName = $"LastRegion_{GameCategoryName!.Replace(" ", string.Empty)}";

            if (isSave)
            {
                LauncherConfig.SetAndSaveConfigValue(iniKeyName, RegionName);
            }
            else
            {
                LauncherConfig.SetAppConfigValue(iniKeyName, RegionName);
            }
        }
    }
}
