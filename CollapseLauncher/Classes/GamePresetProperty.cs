using CollapseLauncher.GamePlaytime;
using CollapseLauncher.GameSettings.Genshin;
using CollapseLauncher.GameSettings.Honkai;
using CollapseLauncher.GameSettings.StarRail;
using CollapseLauncher.GameSettings.Zenless;
using CollapseLauncher.GameVersioning;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.InstallManager.Genshin;
using CollapseLauncher.InstallManager.Honkai;
using CollapseLauncher.InstallManager.StarRail;
using CollapseLauncher.InstallManager.Zenless;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Win32.Native.Enums;
using Hi3Helper.Win32.Native.ManagedTools;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
// ReSharper disable UnusedMember.Global

// ReSharper disable CheckNamespace
// ReSharper disable PartialTypeWithSinglePart

#nullable enable
namespace CollapseLauncher
{
    internal sealed partial class GamePresetProperty : IDisposable
    {
    #pragma warning disable CS8618, CS9264
        internal GamePresetProperty(UIElement uiElementParent, RegionResourceProp apiResourceProp, string gameName, string gameRegion)
    #pragma warning restore CS8618, CS9264
        {
            if (LauncherMetadataHelper.LauncherMetadataConfig == null)
            {
                return;
            }

            PresetConfig? gamePreset = LauncherMetadataHelper.LauncherMetadataConfig[gameName]?[gameRegion];

            if (gamePreset == null)
            {
                throw new NullReferenceException($"Cannot find game with name: {gameName} and region: {gameRegion} on the currently loaded metadata config!");
            }

            ApiResourceProp = apiResourceProp.Copy();
            switch (gamePreset.GameType)
            {
                case GameNameType.Honkai:
                    GameVersion = new GameTypeHonkaiVersion(ApiResourceProp, gameName, gameRegion);
                    GameSettings = new HonkaiSettings(GameVersion);
                    GameCache = new HonkaiCache(uiElementParent, GameVersion);
                    GameRepair = new HonkaiRepair(uiElementParent, GameVersion, GameCache, GameSettings);
                    GameInstall = new HonkaiInstall(uiElementParent, GameVersion, GameCache);
                    break;
                case GameNameType.StarRail:
                    GameVersion = new GameTypeStarRailVersion(ApiResourceProp, gameName, gameRegion);
                    GameSettings = new StarRailSettings(GameVersion);
                    GameCache = new StarRailCache(uiElementParent, GameVersion);
                    GameRepair = new StarRailRepair(uiElementParent, GameVersion);
                    GameInstall = new StarRailInstall(uiElementParent, GameVersion);
                    break;
                case GameNameType.Genshin:
                    GameVersion = new GameTypeGenshinVersion(ApiResourceProp, gameName, gameRegion);
                    GameSettings = new GenshinSettings(GameVersion);
                    GameCache = null;
                    GameRepair = new GenshinRepair(uiElementParent, GameVersion, GameVersion.GameApiProp.data?.game?.latest?.decompressed_path ?? "");
                    GameInstall = new GenshinInstall(uiElementParent, GameVersion);
                    break;
                case GameNameType.Zenless:
                    GameVersion = new GameTypeZenlessVersion(ApiResourceProp, gamePreset, gameName, gameRegion);
                    GameSettings = new ZenlessSettings(GameVersion);
                    GameCache = new ZenlessCache(uiElementParent, GameVersion, (GameSettings as ZenlessSettings)!);
                    GameRepair = new ZenlessRepair(uiElementParent, GameVersion, (GameSettings as ZenlessSettings)!);
                    GameInstall = new ZenlessInstall(uiElementParent, GameVersion, (GameSettings as ZenlessSettings)!);
                    break;
                case GameNameType.Unknown:
                default:
                    throw new NotSupportedException($"[GamePresetProperty.Ctor] Game type: {gamePreset.GameType} ({gamePreset.ProfileName} - {gamePreset.ZoneName}) is not supported!");
            }

            GamePlaytime = new Playtime(GameVersion, GameSettings);
            GamePropLogger = ILoggerHelper.GetILogger($"GameProp: {GameVersion.GameName} - {GameVersion.GameRegion}");

            SentryHelper.CurrentGameCategory   = GameVersion.GameName;
            SentryHelper.CurrentGameRegion     = GameVersion.GameRegion;
            SentryHelper.CurrentGameLocation   = GameVersion.GameDirPath;
            SentryHelper.CurrentGameInstalled  = GameVersion.IsGameInstalled();
            SentryHelper.CurrentGameUpdated    = GameVersion.IsGameVersionMatch();
            SentryHelper.CurrentGameHasPreload = GameVersion.IsGameHasPreload();
            SentryHelper.CurrentGameHasDelta   = GameVersion.IsGameHasDeltaPatch();
        }

        internal RegionResourceProp?  ApiResourceProp { get; set; }
        internal IGameSettings?       GameSettings    { get; set; }
        internal IGamePlaytime        GamePlaytime    { get; set; }
        internal IRepair?             GameRepair      { get; set; }
        internal ICache?              GameCache       { get; set; }
        internal ILogger              GamePropLogger  { get; set; }
        internal IGameVersion         GameVersion     { get; set; }
        internal IGameInstallManager  GameInstall     { get; set; }
        internal PresetConfig         GamePreset      { get => GameVersion.GamePreset; }

        [field: AllowNull, MaybeNull]
        internal string GameExecutableName
        {
            get
            {
                if (string.IsNullOrEmpty(field))
                    field = GamePreset.GameExecutableName ?? "";
                return field;
            }
        }

        [field: AllowNull, MaybeNull]
        internal string GameExecutableNameWithoutExtension
        {
            get
            {
                if (string.IsNullOrEmpty(field))
                    field = Path.GetFileNameWithoutExtension(GameExecutableName);
                return field;
            }
        }

        [field: AllowNull, MaybeNull]
        internal string GameExecutableDir
        {
            get => field ??= GameVersion.GameDirPath;
        }

        [field: AllowNull, MaybeNull]
        internal string GameExecutablePath
        {
            get => field ??= Path.Combine(GameExecutableDir, GameExecutableName);
        }

        internal bool IsGameRunning
        {
            get => ProcessChecker.IsProcessExist(GameExecutableName, out _, out _, GameExecutablePath, GamePropLogger);
        }

        internal bool GetIsGameProcessRunning(int processId)
            => ProcessChecker.IsProcessExist(processId);

        internal bool TryGetGameProcessIdWithActiveWindow(out int processId, out nint windowHandle)
            => ProcessChecker.TryGetProcessIdWithActiveWindow(GameExecutableNameWithoutExtension,
                                                              out processId,
                                                              out windowHandle,
                                                              GameExecutableDir,
                                                              logger: GamePropLogger);

        internal bool TrySetGameProcessPriority(PriorityClass priorityClass = PriorityClass.NORMAL_PRIORITY_CLASS)
            => ProcessChecker.TrySetProcessPriority(GameExecutableNameWithoutExtension,
                                                    priorityClass,
                                                    GameExecutableDir,
                                                    logger: GamePropLogger);

        internal bool TrySetGameProcessPriority(int processId, PriorityClass priorityClass = PriorityClass.NORMAL_PRIORITY_CLASS)
            => ProcessChecker.TrySetProcessPriority(processId, priorityClass, GamePropLogger);

        ~GamePresetProperty()
        {
            Dispose();
        }

        public void Dispose()
        {
            GameRepair?.CancelRoutine();
            GameCache?.CancelRoutine();
            GameInstall.CancelRoutine();

            GameRepair?.Dispose();
            GameCache?.Dispose();
            GameInstall.Dispose();
            GamePlaytime.Dispose();

            ApiResourceProp = null;
            GameSettings    = null;
            GameRepair      = null;
            GameCache       = null;

            GC.SuppressFinalize(this);
        #if DEBUG
            Logger.LogWriteLine($"[GamePresetProperty::Dispose()] GamePresetProperty has been disposed for Hash ID: {GamePreset.HashID}", LogType.Warning, true);
        #endif
        }
    }
}
