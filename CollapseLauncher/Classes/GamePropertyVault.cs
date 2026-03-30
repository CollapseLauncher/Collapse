using CollapseLauncher.Helper;
using CollapseLauncher.Helper.LauncherApiLoader;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.LocaleSourceGen;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
// ReSharper disable CheckNamespace
// ReSharper disable PartialTypeWithSinglePart

#nullable enable
namespace CollapseLauncher.Statics;

internal static class GamePropertyVault
{
    private static Dictionary<PresetConfig, GamePresetProperty> Vault { get; } = new();

    private static UIElement? LastElementParent { get; set; }

    public static GamePresetProperty GetCurrentGameProperty()
    {
        string currentGameTitle  = MetadataHelper.CurrentGameTitleName;
        string currentGameRegion = MetadataHelper.CurrentGameRegionName;

        if (!MetadataHelper.TryGetGameConfig(currentGameTitle, currentGameRegion, out PresetConfig? gamePreset))
        {
            throw new KeyNotFoundException($"Game is not loaded into the launcher! ({currentGameTitle} - {currentGameRegion})");
        }

        // Get the cached game property from the vault
        if (Vault.TryGetValue(gamePreset, out GamePresetProperty? value))
        {
            return value;
        }

        // If the cached one failed to be gathered, try to reinitialize the game property
        if (!string.IsNullOrEmpty(currentGameTitle) && !string.IsNullOrEmpty(currentGameRegion))
        {
            // Try to reinitialize the game property into the vault if
            // the cached one is unavailable.
            RegisterGameProperty(LastElementParent!, gamePreset.GameLauncherApi!, currentGameTitle, currentGameRegion);

            // Try to get the value from the cache vault and return if we get one.
            if (Vault.TryGetValue(gamePreset, out GamePresetProperty? reRegisteredValue))
            {
                return reRegisteredValue;
            }
        }

        // If all attempts failed, throw an exception.
        throw new KeyNotFoundException($"Cached region: {gamePreset} was not found in the vault!");
    }

    public static void RegisterGameProperty(UIElement    uiElementParent,
                                            ILauncherApi launcherApis,
                                            string       gameName,
                                            string       gameRegion)
    {
        LastElementParent ??= uiElementParent;

        if (!MetadataHelper.TryGetGameConfig(gameName, gameRegion, out PresetConfig? gamePreset))
        {
            return;
        }

        // This is disabled due to early Weak Reference by GC, causing the property
        // (especially the one with background activity) being garbage collected.
        // CleanupUnusedGameProperty();
        if (Vault.TryGetValue(gamePreset, out GamePresetProperty? value))
        {
        #if DEBUG
            Logger.LogWriteLine($"[GamePropertyVault] Game property has been cached by Hash ID: {gamePreset}", LogType.Debug, true);
        #endif
            value.GameVersion?.Reinitialize();
            UpdateSentryState(value);
            return;
        }

        GamePresetProperty property = GamePresetProperty.Create(uiElementParent, launcherApis, gameName, gameRegion);
        UpdateSentryState(property);
        _ = Vault.TryAdd(gamePreset, property);
    #if DEBUG
        Logger.LogWriteLine($"[GamePropertyVault] Creating & caching game property for preset: {gamePreset}", LogType.Debug, true);
    #endif
    }

    private static void UpdateSentryState(GamePresetProperty property)
    {
        SentryHelper.CurrentGameCategory   = property.GameVersion?.GameName ?? string.Empty;
        SentryHelper.CurrentGameRegion     = property.GameVersion?.GameRegion ?? string.Empty;
        SentryHelper.CurrentGameLocation   = property.GameVersion?.GameDirPath ?? string.Empty;
        SentryHelper.CurrentGameInstalled  = property.GameVersion?.IsGameInstalled() ?? false;
        SentryHelper.CurrentGameUpdated    = property.GameVersion?.IsGameVersionMatch() ?? false;
        SentryHelper.CurrentGameHasPreload = property.GameVersion?.IsGameHasPreload() ?? false;
        SentryHelper.CurrentGameHasDelta   = property.GameVersion?.IsGameHasDeltaPatch() ?? false;
        SentryHelper.CurrentGameIsPlugin   = property.GamePreset.GameType == GameNameType.Plugin;
    }

    /*
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    private static async void CleanupUnusedGameProperty()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        if (Vault.IsEmpty) return;

        int[] unusedGamePropertyHashID = Vault.Values
            .Where(x => (!x.GameInstall?.IsRunning ?? false) && !x.IsGameRunning && x.GamePreset.HashID != CurrentGameHashID)
            .Select(x => x.GamePreset.HashID)
            .ToArray();

        foreach (int key in unusedGamePropertyHashID)
        {
        #if DEBUG
            Logger.LogWriteLine($"[GamePropertyVault] Cleaning up unused game property by Hash ID: {key}", LogType.Debug, true);
        #endif
            Vault.Remove(key, out _);
        }
    }
    */

    public static async Task AttachNotificationForCurrentGame(PresetConfig presetConfig)
    {
        if (Vault.TryGetValue(presetConfig, out GamePresetProperty? gameProperty) &&
            gameProperty is { GameInstall.IsRunning: true })
        {
            LangParamsBackgroundNotification? bgNotification = Locale.Current.Lang?._BackgroundNotification;
            string actTitle = string.Format(await (gameProperty.GameVersion?.GetGameState() ?? ValueTask.FromResult(GameInstallStateEnum.NotInstalled)) switch
            {
                GameInstallStateEnum.InstalledHavePreload => bgNotification?.CategoryTitle_DownloadingPreload,
                GameInstallStateEnum.NeedsUpdate          => bgNotification?.CategoryTitle_Updating,
                GameInstallStateEnum.InstalledHavePlugin  => bgNotification?.CategoryTitle_Updating,
                _                                         => bgNotification?.CategoryTitle_Downloading
            } ?? "", gameProperty.GameVersion?.GamePreset.GameName);

            string? actSubtitle = gameProperty.GameVersion?.GamePreset.ZoneName;
            BackgroundActivityManager.Attach(presetConfig, gameProperty.GameInstall, actTitle, actSubtitle);
        }
    }

    public static void DetachNotificationForCurrentRegion(PresetConfig presetConfig)
    {
        if (Vault.ContainsKey(presetConfig)) BackgroundActivityManager.Detach(presetConfig);
    }

    public static void SafeDisposeVaults()
    {
        PresetConfig[] cachedPropertyKeys = Vault.Keys.ToArray();

        int i = cachedPropertyKeys.Length;
        while (i > 0)
        {
            try
            {
                if (Vault.Remove(cachedPropertyKeys[--i], out GamePresetProperty? value))
                {
                    value.Dispose();
#if DEBUG
                    Logger.LogWriteLine($"[GamePropertyVault] A preset property at index: {i} for: {value.GamePreset.GameName} - {value.GamePreset.ZoneName} has been disposed!", LogType.Debug, true);
#endif
                    return;
                }

#if DEBUG
                Logger.LogWriteLine($"[GamePropertyVault] Cannot dispose the preset property as it cannot be detached from vault on index: {i}", LogType.Debug, true);
#endif
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
            }
        }

        try
        {
            foreach (GamePresetProperty value in Vault.Values)
            {
                value.Dispose();
#if DEBUG
                Logger.LogWriteLine($"[GamePropertyVault] Other preset property for: {value.GamePreset.GameName} - {value.GamePreset.ZoneName} has been disposed!", LogType.Debug, true);
#endif
            }

            Vault.Clear();
        }
        catch (Exception ex)
        {
            SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
        }
    }
}
