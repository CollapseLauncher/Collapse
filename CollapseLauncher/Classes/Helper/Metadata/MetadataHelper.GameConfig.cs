using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Helper.Loading;
using CollapseLauncher.Plugins;
using Hi3Helper;
using Hi3Helper.Shared.Region;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.Metadata;

internal static partial class MetadataHelper
{
    private static async Task InitializeAllConfigsAsync(bool showLoadingMessage)
    {
        // Clear existing dict
        ConfigGameDict.Clear();

        // Always read MasterKey config first before loading everything in parallel.
        await InitializeMasterKeyConfigAsync();

        // Load game and community tools config in parallel.
        InitializeGameConfigDictionary();
        await Task.WhenAll(InitializeGameConfigAsync(showLoadingMessage),
                           InitializeCommunityToolsConfigAsync());
    }

    private static async Task InitializeMasterKeyConfigAsync()
    {
        if (!StampMasterKeyDict.TryGetValue(" - ", out Stamp? stamp))
        {
            throw new KeyNotFoundException("Cannot find stamp for the primary MasterKey config!");
        }

        CurrentMasterKey = await Util.LoadFileAsync(stamp, MesterKeyConfigJsonContext.Default.MasterKeyConfig);
    }

    private static void InitializeGameConfigDictionary()
    {
        // Pre-allocate ConfigGameDict based on StampGameDict order (also to avoid duplicated game title and region keys)
        foreach (Stamp stamp in StampGameDict.Values)
        {
            ref Dictionary<string, PresetConfig>? regionDict =
                ref CollectionsMarshal.GetValueRefOrAddDefault(ConfigGameDict, stamp.GameName ?? UnknownString, out _);

            regionDict ??= new Dictionary<string, PresetConfig>(StringComparer.OrdinalIgnoreCase);
            CollectionsMarshal.GetValueRefOrAddDefault(regionDict, stamp.GameRegion ?? UnknownString, out _);
        }
    }

    private static async Task InitializeGameConfigAsync(bool showLoadingMessage)
    {
        int loaded = 0;
        int count  = StampGameDict.Values.Count;

        // Load built-in games first
        await Parallel.ForEachAsync(StampGameDict.Values,
                                    CancellationToken.None,
                                    Impl);

        // Then load plugin-based games
        await PluginManager.LoadPlugins(ConfigGameDict, StampGameDict);
        return;

        async ValueTask Impl(Stamp stamp, CancellationToken token)
        {
            Interlocked.Increment(ref loaded);

            if (!ConfigGameDict.TryGetValue(stamp.GameName ?? UnknownString, out Dictionary<string, PresetConfig>? regionDict))
            {
                Logger.LogWriteLine($"Region dictionary for game title: {stamp.GameName} is not initialized!",
                                    LogType.Warning,
                                    true);
                return;
            }

            if (showLoadingMessage)
            {
                LoadingMessageHelper.SetMessage(Locale.Current.Lang?._MainPage?.Initializing,
                                                $"{Locale.Current.Lang?._MainPage?.LoadingGameConfiguration} [{loaded}/{count}]: " +
                                                $"{Util.GetStringTranslation(Locale.Current.Lang?._GameClientTitles, stamp.GameName)} - " +
                                                $"{Util.GetStringTranslation(Locale.Current.Lang?._GameClientRegions, stamp.GameRegion)}");
            }

            PresetConfig regionConfig = await Util.LoadFileAsync(stamp, PresetConfigJsonContext.Default.PresetConfig);
            regionConfig.GameName ??= stamp.GameName;
            regionConfig.ZoneName ??= stamp.GameRegion;
            regionConfig.GameLauncherApi ??= regionConfig.LauncherType switch
            {
                LauncherType.HoYoPlay => HypApiLoader.CreateApiInstance(regionConfig, regionConfig.GameName ?? UnknownString, regionConfig.ZoneName ?? UnknownString),
                LauncherType.Legacy => throw new NotSupportedException("Legacy Launcher API is no longer supported!"),
                LauncherType.Plugin => throw new InvalidOperationException("You cannot set built-in game PresetConfig as Plugin-based game type!"),
                _ => throw new NotSupportedException($"Launcher type: {regionConfig.LauncherType} is not supported!")
            };

            string regionKey = stamp.GameRegion ?? UnknownString;
            if (!regionDict.TryAdd(regionKey, regionConfig))
            {
                regionDict[regionKey] = regionConfig;
            }
        }
    }

    private static async Task InitializeCommunityToolsConfigAsync()
    {
        await Parallel.ForEachAsync(StampCommunityToolDict.Values,
                                    CancellationToken.None,
                                    Impl);
        return;

        static async ValueTask Impl(Stamp stamp, CancellationToken token)
        {
            CommunityToolsProperty property =
                await Util.LoadFileAsync(stamp,
                                         CommunityToolsPropertyJsonContext.Default.CommunityToolsProperty);
            CommunityToolsProperty.CombineFrom(property);
        }
    }

    #region Public Methods

    public static bool TryGetGameConfig(string?                               gameTitle,
                                        string?                               gameRegion,
                                        [NotNullWhen(true)] out PresetConfig? presetConfig)
    {
        Unsafe.SkipInit(out presetConfig);

        if (string.IsNullOrEmpty(gameTitle) ||
            string.IsNullOrEmpty(gameRegion))
        {
            return false;
        }

        return ConfigGameDict.TryGetValue(gameTitle, out Dictionary<string, PresetConfig>? gameRegionDict) &&
               gameRegionDict.TryGetValue(gameRegion, out presetConfig);
    }

    public static PresetConfig GetAndSetCurrentConfig(string? gameTitle, string? gameRegion)
    {
        ArgumentException.ThrowIfNullOrEmpty(gameTitle);
        ArgumentException.ThrowIfNullOrEmpty(gameRegion);

        string key = $"{gameTitle} - {gameRegion}";

        if (!StampGameDict.TryGetValue(key, out Stamp? gameConfigStamp))
        {
            throw new KeyNotFoundException($"Cannot find game config stamp with key: {key}!");
        }

        ref Dictionary<string, PresetConfig> gameConfigDictRef =
            ref CollectionsMarshal.GetValueRefOrNullRef(ConfigGameDict, gameTitle);

        if (Unsafe.IsNullRef(ref gameConfigDictRef))
        {
            throw new KeyNotFoundException($"Game title: {gameTitle} was not found inside of config game dictionary!");
        }

        ref PresetConfig gamePresetConfig =
            ref CollectionsMarshal.GetValueRefOrNullRef(gameConfigDictRef, gameRegion);

        if (Unsafe.IsNullRef(ref gameConfigDictRef))
        {
            throw new KeyNotFoundException($"Game region: {gameRegion} was not found inside of config game dictionary!");
        }

        ConfigFileStatus status = Util.GetConfigFileStatus(gameConfigStamp);
        switch (status)
        {
            case ConfigFileStatus.FileModified:
                // Force reload the config file. This is useful to reload the config file for debugging.
                string modifiedFilePath = Path.Combine(LauncherMetadataDirectory, gameConfigStamp.MetadataPath);
                using (FileStream modifiedFileStream =
                       File.Open(modifiedFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    // Load and replace the config from ref in dictionary.
                    gamePresetConfig = modifiedFileStream.Deserialize(PresetConfigJsonContext.Default.PresetConfig)
                                    ?? gamePresetConfig; // Silently ignore deserialization failure and keep using the old config if deserialization fails.
                }
                break;
            case ConfigFileStatus.FileMissing:
                throw new FileNotFoundException($"FATAL: Game config file for stamp: {key} is not found! ({gameConfigStamp.MetadataPath}). Please restart the launcher to reinitialize the metadata.");
            case ConfigFileStatus.Ok:
                break; // Do nothing
            default:
                throw new InvalidOperationException($"Invalid config file status: {status} for game config stamp: {key}");
        }

        // Update current game title and region in metadata helper.
        Interlocked.Exchange(ref CurrentGameTitleName,  gameTitle);
        Interlocked.Exchange(ref CurrentGameRegionName, gameRegion);
        Interlocked.Exchange(ref CurrentGameConfig,     gamePresetConfig);
        return gamePresetConfig;
    }

    public static string GetCurrentTranslatedTitleRegion()
    {
        string curGameName   = CurrentGameTitleName;
        string curGameRegion = CurrentGameRegionName;

        string curGameNameTranslate   = GetTranslatedTitle(curGameName);
        string curGameRegionTranslate = GetTranslatedRegion(curGameRegion);

        return $"{curGameNameTranslate} - {curGameRegionTranslate}";
    }

    [return: NotNullIfNotNull(nameof(gameTitle))]
    public static string? GetTranslatedTitle(string? gameTitle) =>
        Util.GetStringTranslation(Locale.Current.Lang?._GameClientTitles, gameTitle);

    [return: NotNullIfNotNull(nameof(gameRegion))]
    public static string? GetTranslatedRegion(string? gameRegion) =>
        Util.GetStringTranslation(Locale.Current.Lang?._GameClientRegions, gameRegion);

    public static List<PresetConfig> GetGameRegionList(string? gameTitle)
    {
        if (string.IsNullOrEmpty(gameTitle))
        {
            return [];
        }

        ref Dictionary<string, PresetConfig> gamePresetConfigs =
            ref CollectionsMarshal.GetValueRefOrNullRef(ConfigGameDict, gameTitle);

        return Unsafe.IsNullRef(ref gamePresetConfigs)
            ? []
            : gamePresetConfigs.Values.ToList();
    }

    public static string GetLastSavedGameTitleOrDefault()
    {
        string? savedGameTitle = LauncherConfig.GetAppConfigValue("GameCategory");

        if (string.IsNullOrEmpty(savedGameTitle) ||
            !ConfigGameDict.ContainsKey(savedGameTitle))
        {
            return ConfigGameDict.Keys.FirstOrDefault() ?? throw new NullReferenceException("Launcher has no loaded game titles!");
        }

        return savedGameTitle;
    }

    public static string GetLastSavedGameRegionOrDefault(string? gameTitle)
    {
        ArgumentException.ThrowIfNullOrEmpty(gameTitle);

        ref Dictionary<string, PresetConfig> gamePresetConfigs =
            ref CollectionsMarshal.GetValueRefOrNullRef(ConfigGameDict, gameTitle);

        if (Unsafe.IsNullRef(ref gamePresetConfigs))
        {
            throw new KeyNotFoundException($"Game title is not found in config game dictionary!: {gameTitle}");
        }

        string  regionSavedKey   = $"LastRegion_{Util.GetNonSpaceGameTitle(gameTitle)}";
        string? regionSavedValue = LauncherConfig.GetAppConfigValue(regionSavedKey).Value;

        return string.IsNullOrEmpty(regionSavedValue) || !gamePresetConfigs.ContainsKey(regionSavedValue)
            ? gamePresetConfigs.Keys.FirstOrDefault() ?? throw new NullReferenceException($"Game title: {gameTitle} has no loaded game regions!")
            : regionSavedValue;
    }

    public static int GetLastSavedGameRegionIndexOrDefault(string? gameTitle)
    {
        if (string.IsNullOrEmpty(gameTitle))
        {
            return 0; // Get to default if game title is null.
        }

        ref Dictionary<string, PresetConfig> gamePresetConfigs =
            ref CollectionsMarshal.GetValueRefOrNullRef(ConfigGameDict, gameTitle);

        if (Unsafe.IsNullRef(ref gamePresetConfigs))
        {
            throw new KeyNotFoundException($"Game title is not found in config game dictionary!: {gameTitle}");
        }

        string  regionSavedKey   = $"LastRegion_{Util.GetNonSpaceGameTitle(gameTitle)}";
        string? regionSavedValue = LauncherConfig.GetAppConfigValue(regionSavedKey).Value;

        return string.IsNullOrEmpty(regionSavedValue)
            ? 0
            : IndexUtil.GetValueIndexFromKeyOrDefault(gamePresetConfigs, regionSavedValue);
    }

    public static void GetGameCounts(out int gameTitleCount, out int currentGameRegionCount)
    {
        gameTitleCount = ConfigGameDict.Count;
        if (string.IsNullOrEmpty(CurrentGameTitleName) ||
            !ConfigGameDict.TryGetValue(CurrentGameTitleName, out Dictionary<string, PresetConfig>? regionDict))
        {
            currentGameRegionCount = 0;
            return;
        }

        currentGameRegionCount = regionDict.Count;
    }

    public static void SaveGame(string? gameTitle, bool isSave = true)
    {
        const string key = "GameCategory";
        if (isSave)
        {
            LauncherConfig.SetAndSaveConfigValue(key, gameTitle);
            return;
        }

        LauncherConfig.SetAppConfigValue(key, gameTitle);
    }

    public static void SaveGame(string? gameTitle, string? gameRegion, bool isSave = true)
    {
        SaveGame(gameTitle, false);
        string regionSavedKey = $"LastRegion_{Util.GetNonSpaceGameTitle(gameTitle)}";

        if (isSave)
        {
            LauncherConfig.SetAndSaveConfigValue(regionSavedKey, gameRegion);
            return;
        }

        LauncherConfig.SetAppConfigValue(regionSavedKey, gameRegion);
    }

    #endregion


}

file static class IndexUtil
{
    public static int GetValueIndexFromKeyOrDefault<TKey, TValue>(Dictionary<TKey, TValue> dict, TKey key)
        where TKey : notnull
    {
        // Get value reference
        ref TValue selectedRef = ref CollectionsMarshal.GetValueRefOrNullRef(dict, key);
        if (Unsafe.IsNullRef(ref selectedRef))
        {
            return 0;
        }

        // Get first value reference
        TKey? firstKey = dict.Keys.FirstOrDefault();
        if (firstKey == null)
        {
            throw new InvalidOperationException();
        }
        ref TValue firstRef = ref CollectionsMarshal.GetValueRefOrNullRef(dict, firstKey);
        if (Unsafe.IsNullRef(ref firstRef))
        {
            throw new InvalidOperationException();
        }

        int sizeOfValue = RuntimeHelpers.IsReferenceOrContainsReferences<TValue>() ? 24 : Marshal.SizeOf<TValue>();
        int refByteOffset = (int)Unsafe.ByteOffset(ref firstRef, ref selectedRef);
        int offset = refByteOffset / sizeOfValue;
        return offset;
    }
}