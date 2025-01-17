using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
// ReSharper disable CheckNamespace
// ReSharper disable PartialTypeWithSinglePart

namespace CollapseLauncher.Statics
{
    internal static class GamePropertyVault
    {
        private static readonly Dictionary<int, GamePresetProperty> Vault = new();
        public static           int                                 LastGameHashID           { get; set; }
        public static           int                                 CurrentGameHashID        { get; set; }
        public static           GamePresetProperty                  GetCurrentGameProperty() => Vault[CurrentGameHashID];

        public static void LoadGameProperty(UIElement uiElementParent, RegionResourceProp apiResourceProp, string gameName, string gameRegion)
        {
            if (LauncherMetadataHelper.LauncherMetadataConfig != null)
            {
                PresetConfig gamePreset = LauncherMetadataHelper.LauncherMetadataConfig[gameName][gameRegion];

                LastGameHashID    = LastGameHashID == 0 ? gamePreset!.HashID : LastGameHashID;
                CurrentGameHashID = gamePreset!.HashID;
            }

            RegisterGameProperty(uiElementParent, apiResourceProp, gameName, gameRegion);
        }

        private static void RegisterGameProperty(UIElement uiElementParent, RegionResourceProp apiResourceProp, string gameName, string gameRegion)
        {
            if (LauncherMetadataHelper.LauncherMetadataConfig == null)
            {
                return;
            }

            PresetConfig gamePreset = LauncherMetadataHelper.LauncherMetadataConfig[gameName][gameRegion];

            CleanupUnusedGameProperty();
            if (Vault.TryGetValue(gamePreset.HashID, out GamePresetProperty value))
            {
            #if DEBUG
                Logger.LogWriteLine($"[GamePropertyVault] Game property has been cached by Hash ID: {gamePreset.HashID}", LogType.Debug, true);
            #endif
                value?._GameVersion?.Reinitialize();
                return;
            }

            GamePresetProperty property = new GamePresetProperty(uiElementParent, apiResourceProp, gameName, gameRegion);
            Vault.Add(gamePreset.HashID, property);
        #if DEBUG
            Logger.LogWriteLine($"[GamePropertyVault] Creating & caching game property by Hash ID: {gamePreset.HashID}", LogType.Debug, true);
        #endif
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private static async void CleanupUnusedGameProperty()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            if (Vault == null || Vault.Count == 0) return;

            int[] unusedGamePropertyHashID = Vault.Values
                .Where(x => !x!._GameInstall!.IsRunning && !x.IsGameRunning && x._GamePreset!.HashID != CurrentGameHashID)
                                                  .Select(x => x._GamePreset.HashID)
                                                  .ToArray();

            foreach (int key in unusedGamePropertyHashID)
            {
#if DEBUG
                Logger.LogWriteLine($"[GamePropertyVault] Cleaning up unused game property by Hash ID: {key}", LogType.Debug, true);
#endif
                Vault.Remove(key);
            }
        }

        public static async ValueTask AttachNotifForCurrentGame(int hashID = int.MinValue)
        {
            if (hashID < 0) hashID = CurrentGameHashID;
            if (Vault!.ContainsKey(hashID)) await AttachNotifForCurrentGame_Inner(hashID);
        }

        private static async ValueTask AttachNotifForCurrentGame_Inner(int hashID)
        {
            GamePresetProperty gameProperty = Vault![hashID];
            if (gameProperty!._GameInstall!.IsRunning)
            {
                var bgNotification = Locale.Lang!._BackgroundNotification!;
                string actTitle = string.Format((await gameProperty._GameVersion!.GetGameState() switch
                {
                    GameInstallStateEnum.InstalledHavePreload => bgNotification.CategoryTitle_DownloadingPreload,
                    GameInstallStateEnum.NeedsUpdate          => bgNotification.CategoryTitle_Updating,
                    GameInstallStateEnum.InstalledHavePlugin  => bgNotification.CategoryTitle_Updating,
                    _                                         => bgNotification.CategoryTitle_Downloading
                })!, gameProperty._GameVersion.GamePreset!.GameName);

                string actSubtitle = gameProperty._GameVersion.GamePreset.ZoneName;
                BackgroundActivityManager.Attach(hashID, gameProperty._GameInstall, actTitle, actSubtitle);
            }
        }

        public static void DetachNotifForCurrentGame(int hashID = int.MinValue)
        {
            if (hashID < 0) hashID = CurrentGameHashID;
            if (Vault!.ContainsKey(hashID)) BackgroundActivityManager.Detach(hashID);
        }
    }

    [SuppressMessage("ReSharper", "PartialTypeWithSinglePart")]
    internal partial class PageStatics
    {
        internal static CommunityToolsProperty CommunityToolsProperty { get; set; } = new CommunityToolsProperty()
        {
            OfficialToolsDictionary = new Dictionary<GameNameType, List<CommunityToolsEntry>>(),
            CommunityToolsDictionary = new Dictionary<GameNameType, List<CommunityToolsEntry>>()
        };
    }
}
