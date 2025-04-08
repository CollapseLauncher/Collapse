using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
// ReSharper disable CheckNamespace
// ReSharper disable PartialTypeWithSinglePart

#nullable enable
namespace CollapseLauncher.Statics
{
    internal static class GamePropertyVault
    {
        private static ConcurrentDictionary<int, GamePresetProperty> Vault             { get; } = new();
        private static UIElement?                                    LastElementParent { get; set; }
        public static  int                                           LastGameHashID    { get; set; }
        public static  int                                           CurrentGameHashID { get; set; }
        public static  string?                                       CurrentGameName   { get; set; }
        public static  string?                                       CurrentGameRegion { get; set; }

        public static GamePresetProperty GetCurrentGameProperty()
        {
            // Get the cached game property from the vault
            int hashId = CurrentGameHashID;
            if (Vault.TryGetValue(hashId, out GamePresetProperty? value))
            {
                return value;
            }

            // If the cached one failed to be gathered, try to reinitialize the game property
            if (!string.IsNullOrEmpty(CurrentGameName) && !string.IsNullOrEmpty(CurrentGameRegion))
            {
                // Try to reinitialize the game property into the vault if
                // the cached one is unavailable.
                if (LauncherMetadataHelper.LauncherMetadataConfig?[CurrentGameName]?
                       .TryGetValue(CurrentGameRegion, out PresetConfig? gamePreset) ?? false)
                {
                    // Try register the game property and get its hash id
                    RegisterGameProperty(LastElementParent!, gamePreset.GameLauncherApi?.LauncherGameResource!, CurrentGameName, CurrentGameRegion);
                    int reRegisteredHashId = gamePreset.HashID;

                    // Try to get the value from the cache vault and return if we get one.
                    if (Vault.TryGetValue(reRegisteredHashId, out GamePresetProperty? reRegisteredValue))
                    {
                        return reRegisteredValue;
                    }
                }
            }

            // If all attempts failed, throw an exception.
            throw new KeyNotFoundException($"Cached region with Hash ID: {hashId} was not found in the vault!");
        }

        public static async Task LoadGameProperty(UIElement uiElementParent, RegionResourceProp apiResourceProp, string gameName, string gameRegion)
        {
            if (LauncherMetadataHelper.LauncherMetadataConfig?[gameName]?
                .TryGetValue(gameRegion, out PresetConfig? gamePreset) ?? false)
            {
                LastGameHashID = LastGameHashID == 0 ? gamePreset.HashID : LastGameHashID;
                CurrentGameHashID = gamePreset.HashID;
            }

            await Task.Run(() => RegisterGameProperty(uiElementParent, apiResourceProp, gameName, gameRegion));
        }

        private static void RegisterGameProperty(UIElement uiElementParent, RegionResourceProp apiResourceProp, string gameName, string gameRegion)
        {
            CurrentGameName   =   gameName;
            CurrentGameRegion =   gameRegion;
            LastElementParent ??= uiElementParent;

            if (!(LauncherMetadataHelper.LauncherMetadataConfig?[gameName]?
                   .TryGetValue(gameRegion, out PresetConfig? gamePreset) ?? false))
            {
                return;
            }

            CleanupUnusedGameProperty();
            if (Vault.TryGetValue(gamePreset.HashID, out GamePresetProperty? value))
            {
            #if DEBUG
                Logger.LogWriteLine($"[GamePropertyVault] Game property has been cached by Hash ID: {gamePreset.HashID}", LogType.Debug, true);
            #endif
                value.GameVersion?.Reinitialize();
                return;
            }

            GamePresetProperty property = new GamePresetProperty(uiElementParent, apiResourceProp, gameName, gameRegion);
            _ = Vault.TryAdd(gamePreset.HashID, property);
        #if DEBUG
            Logger.LogWriteLine($"[GamePropertyVault] Creating & caching game property by Hash ID: {gamePreset.HashID}", LogType.Debug, true);
        #endif
        }

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

        public static async Task AttachNotificationForCurrentGame(int hashID = int.MinValue)
        {
            if (hashID < 0) hashID = CurrentGameHashID;
            if (Vault.TryGetValue(hashID, out GamePresetProperty? gameProperty))
            {
                if (gameProperty is { GameInstall.IsRunning: true })
                {
                    Locale.LocalizationParams.LangBackgroundNotification? bgNotification = Locale.Lang._BackgroundNotification;
                    string actTitle = string.Format(await gameProperty.GameVersion!.GetGameState() switch
                                                    {
                                                        GameInstallStateEnum.InstalledHavePreload => bgNotification.CategoryTitle_DownloadingPreload,
                                                        GameInstallStateEnum.NeedsUpdate => bgNotification.CategoryTitle_Updating,
                                                        GameInstallStateEnum.InstalledHavePlugin => bgNotification.CategoryTitle_Updating,
                                                        _ => bgNotification.CategoryTitle_Downloading
                                                    }, gameProperty.GameVersion.GamePreset.GameName);

                    string? actSubtitle = gameProperty.GameVersion.GamePreset.ZoneName;
                    BackgroundActivityManager.Attach(hashID, gameProperty.GameInstall, actTitle, actSubtitle);
                }
            }
        }

        public static void DetachNotificationForCurrentRegion(int hashID = int.MinValue)
        {
            if (hashID < 0) hashID = CurrentGameHashID;
            if (Vault.ContainsKey(hashID)) BackgroundActivityManager.Detach(hashID);
        }

        public static void SafeDisposeVaults()
        {
            int[] cachedPropertyKeys = Vault.Keys.ToArray();

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
                foreach (KeyValuePair<int, GamePresetProperty> keyValuePair in Vault)
                {
                    GamePresetProperty value = keyValuePair.Value;
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

    internal partial class PageStatics
    {
        internal static CommunityToolsProperty? CommunityToolsProperty { get; set; } = new()
        {
            OfficialToolsDictionary = new Dictionary<GameNameType, List<CommunityToolsEntry>>(),
            CommunityToolsDictionary = new Dictionary<GameNameType, List<CommunityToolsEntry>>()
        };
    }
}
