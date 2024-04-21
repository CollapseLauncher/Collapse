﻿using CollapseLauncher.GameSettings.Genshin;
using CollapseLauncher.GameSettings.Honkai;
using CollapseLauncher.GameSettings.StarRail;
using CollapseLauncher.GameVersioning;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.InstallManager.Genshin;
using CollapseLauncher.InstallManager.Honkai;
using CollapseLauncher.InstallManager.StarRail;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace CollapseLauncher.Statics
{
    internal class GamePresetProperty : IDisposable
    {
        internal GamePresetProperty(UIElement UIElementParent, RegionResourceProp APIResouceProp, string GameName, string GameRegion)
        {
            PresetConfig GamePreset = LauncherMetadataHelper.LauncherMetadataConfig[GameName][GameRegion];

            _APIResouceProp = APIResouceProp!.Copy();
            switch (GamePreset!.GameType)
            {
                case GameNameType.Honkai:
                    _GameVersion = new GameTypeHonkaiVersion(UIElementParent, _APIResouceProp, GameName, GameRegion);
                    _GameSettings = new HonkaiSettings(_GameVersion);
                    _GameCache = new HonkaiCache(UIElementParent, _GameVersion);
                    _GameRepair = new HonkaiRepair(UIElementParent, _GameVersion, _GameCache, _GameSettings);
                    _GameInstall = new HonkaiInstall(UIElementParent, _GameVersion, _GameCache, _GameSettings);
                    break;
                case GameNameType.StarRail:
                    _GameVersion = new GameTypeStarRailVersion(UIElementParent, _APIResouceProp, GameName, GameRegion);
                    _GameSettings = new StarRailSettings(_GameVersion);
                    _GameCache = new StarRailCache(UIElementParent, _GameVersion);
                    _GameRepair = new StarRailRepair(UIElementParent, _GameVersion);
                    _GameInstall = new StarRailInstall(UIElementParent, _GameVersion);
                    break;
                case GameNameType.Genshin:
                    _GameVersion = new GameTypeGenshinVersion(UIElementParent, _APIResouceProp, GameName, GameRegion);
                    _GameSettings = new GenshinSettings(_GameVersion);
                    _GameCache = null;
                    _GameRepair = new GenshinRepair(UIElementParent, _GameVersion, _GameVersion.GameAPIProp!.data!.game!.latest!.decompressed_path);
                    _GameInstall = new GenshinInstall(UIElementParent, _GameVersion);
                    break;
                case GameNameType.Zenless:
                    _GameVersion = new GameTypeStarRailVersion(UIElementParent, _APIResouceProp, GameName, GameRegion);
                    _GameSettings = new StarRailSettings(_GameVersion);
                    _GameCache = null;
                    _GameRepair = null;
                    _GameInstall = new StarRailInstall(UIElementParent, _GameVersion);
                    break;
                default:
                    throw new NotSupportedException($"[GamePresetProperty.Ctor] Game type: {GamePreset.GameType} ({GamePreset.ProfileName} - {GamePreset.ZoneName}) is not supported!");
            }
        }

        internal RegionResourceProp _APIResouceProp { get; set; }
        internal PresetConfig _GamePreset { get => _GameVersion.GamePreset; }
        internal IGameSettings _GameSettings { get; set; }
        internal IRepair _GameRepair { get; set; }
        internal ICache _GameCache { get; set; }
        internal IGameVersionCheck _GameVersion { get; set; }
        internal IGameInstallManager _GameInstall { get; set; }
        internal bool IsGameRunning
        {
            get
            {
                string name = Path.GetFileNameWithoutExtension(_GamePreset!.GameExecutableName);
                Process[] processes = Process.GetProcessesByName(name);
                return processes.Length > 0;
            }
        }

#nullable enable
        // Translation:
        // The Process.GetProcessesByName(procName) will get an array of the process list. The output is piped into null-break operator "?" which will
        // returns a null if something goes wrong. If not, then pass it to .Where(x) method which will select the given value with the certain logic.
        // (in this case, we need to ensure that the MainWindowHandle is not a non-zero pointer) and then piped into null-break operator.
        internal Process? GetGameProcessWithActiveWindow() =>
            Process.GetProcessesByName(Path.GetFileNameWithoutExtension(_GamePreset!.GameExecutableName))?
                .Where(x => x.MainWindowHandle != IntPtr.Zero)?
                .FirstOrDefault();
#nullable disable

        /*
        ~GamePresetProperty()
        {
#if DEBUG
            Logger.LogWriteLine($"[~GamePresetProperty()] Deconstructor getting called in GamePresetProperty for Hash ID: {_GamePreset.HashID}", LogType.Warning, true);
#endif
            Dispose();
        }
        */

        public void Dispose()
        {
            _GameRepair?.CancelRoutine();
            _GameCache?.CancelRoutine();
            _GameInstall?.CancelRoutine();

            _GameRepair?.Dispose();
            _GameCache?.Dispose();
            _GameInstall?.Dispose();

            _APIResouceProp = null;
            _GameSettings = null;
            _GameRepair = null;
            _GameCache = null;
            _GameVersion = null;
            _GameInstall = null;
        }
    }

    internal static class GamePropertyVault
    {
        private static Dictionary<int, GamePresetProperty> Vault = new Dictionary<int, GamePresetProperty>();
        public static int LastGameHashID { get; set; }
        public static int CurrentGameHashID { get; set; }
        public static GamePresetProperty GetCurrentGameProperty() => Vault![CurrentGameHashID];

        public static void LoadGameProperty(UIElement UIElementParent, RegionResourceProp APIResouceProp, string GameName, string GameRegion)
        {
            PresetConfig GamePreset = LauncherMetadataHelper.LauncherMetadataConfig[GameName][GameRegion];

            LastGameHashID = LastGameHashID == 0 ? GamePreset!.HashID : LastGameHashID;
            CurrentGameHashID = GamePreset!.HashID;
            RegisterGameProperty(UIElementParent, APIResouceProp, GameName, GameRegion);
        }

        private static void RegisterGameProperty(UIElement UIElementParent, RegionResourceProp APIResouceProp, string GameName, string GameRegion)
        {
            PresetConfig GamePreset = LauncherMetadataHelper.LauncherMetadataConfig[GameName][GameRegion];

            CleanupUnusedGameProperty();
            if (Vault!.ContainsKey(GamePreset!.HashID))
            {
#if DEBUG
                Logger.LogWriteLine($"[GamePropertyVault] Game property has been cached by Hash ID: {GamePreset.HashID}", LogType.Debug, true);
#endif
                return;
            }

            GamePresetProperty Property = new GamePresetProperty(UIElementParent, APIResouceProp, GameName, GameRegion);
            Vault.Add(GamePreset.HashID, Property);
#if DEBUG
            Logger.LogWriteLine($"[GamePropertyVault] Creating & caching game property by Hash ID: {GamePreset.HashID}", LogType.Debug, true);
#endif
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private static async void CleanupUnusedGameProperty()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            if (Vault == null || Vault.Count == 0) return;

            int[] unusedGamePropertyHashID = Vault.Values
                .Where(x => !x!._GameInstall!.IsRunning && !x.IsGameRunning && x._GamePreset!.HashID != CurrentGameHashID)?
                .Select(x => x._GamePreset.HashID)?
                .ToArray();

            foreach (int key in unusedGamePropertyHashID)
            {
#if DEBUG
                Logger.LogWriteLine($"[GamePropertyVault] Cleaning up unused game property by Hash ID: {key}", LogType.Debug, true);
#endif
                Vault.Remove(key);
            }
        }

        public static void AttachNotifForCurrentGame(int hashID = int.MinValue)
        {
            if (hashID < 0) hashID = CurrentGameHashID;
            if (Vault!.ContainsKey(hashID)) AttachNotifForCurrentGame_Inner(hashID);
        }

        private static void AttachNotifForCurrentGame_Inner(int HashID)
        {
            GamePresetProperty GameProperty = Vault![HashID];
            if (GameProperty!._GameInstall!.IsRunning)
            {
                var bgNotification = Locale.Lang!._BackgroundNotification!;
                string actTitle = string.Format((GameProperty._GameVersion!.GetGameState() switch
                {
                    GameInstallStateEnum.InstalledHavePreload => bgNotification.CategoryTitle_DownloadingPreload,
                    GameInstallStateEnum.NeedsUpdate          => bgNotification.CategoryTitle_Updating,
                    GameInstallStateEnum.InstalledHavePlugin  => bgNotification.CategoryTitle_Updating,
                    _                                         => bgNotification.CategoryTitle_Downloading
                })!, GameProperty._GameVersion.GamePreset!.GameName);

                string actSubtitle = GameProperty._GameVersion.GamePreset.ZoneName;
                BackgroundActivityManager.Attach(HashID, GameProperty._GameInstall, actTitle, actSubtitle);
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
        internal static CommunityToolsProperty _CommunityToolsProperty { get; set; }
    }
}
