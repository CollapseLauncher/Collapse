using CollapseLauncher.GamePlaytime;
using CollapseLauncher.GameSettings.Base;
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
using CollapseLauncher.Plugins;
using Hi3Helper;
using Hi3Helper.Win32.Native.Enums;
using Hi3Helper.Win32.Native.ManagedTools;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using System;
using System.IO;
// ReSharper disable UnusedMember.Global

// ReSharper disable CheckNamespace
// ReSharper disable PartialTypeWithSinglePart

#nullable enable
namespace CollapseLauncher
{
    internal sealed partial class GamePresetProperty : IDisposable
    {
        internal static GamePresetProperty Create(UIElement uiElementParent, RegionResourceProp? apiResourceProp, string gameName, string gameRegion)
        {
            var gamePreset = LauncherMetadataHelper.LauncherMetadataConfig?[gameName]?[gameRegion];
            if (gamePreset == null)
            {
                throw new NullReferenceException($"Cannot find game with name: {gameName} and region: {gameRegion} on the currently loaded metadata config!");
            }

            if (gamePreset.GameType == GameNameType.Plugin &&
                gamePreset is not PluginPresetConfigWrapper)
            {
                throw new InvalidCastException($"[GamePresetProperty.Ctor] The game preset with name: {gameName} and region: {gameRegion} is not a valid PluginPresetConfigWrapper instance!");
            }

            GamePresetProperty property = new GamePresetProperty();

            property.ApiResourceProp = apiResourceProp?.Copy();
            switch (gamePreset.GameType)
            {
                case GameNameType.Honkai:
                    property.GameVersion  = new GameTypeHonkaiVersion(apiResourceProp, gameName, gameRegion);
                    property.GameSettings = new HonkaiSettings(property.GameVersion);
                    property.GameCache    = new HonkaiCache(uiElementParent, property.GameVersion);
                    property.GameRepair   = new HonkaiRepair(uiElementParent, property.GameVersion, property.GameCache, property.GameSettings);
                    property.GameInstall  = new HonkaiInstall(uiElementParent, property.GameVersion, property.GameCache);
                    break;
                case GameNameType.StarRail:
                    property.GameVersion  = new GameTypeStarRailVersion(apiResourceProp, gameName, gameRegion);
                    property.GameSettings = new StarRailSettings(property.GameVersion);
                    property.GameCache    = new StarRailCache(uiElementParent, property.GameVersion);
                    property.GameRepair   = new StarRailRepair(uiElementParent, property.GameVersion);
                    property.GameInstall  = new StarRailInstall(uiElementParent, property.GameVersion);
                    break;
                case GameNameType.Genshin:
                    property.GameVersion = new GameTypeGenshinVersion(apiResourceProp, gameName, gameRegion);
                    property.GameSettings = new GenshinSettings(property.GameVersion);
                    property.GameCache = null;
                    property.GameRepair = new GenshinRepair(uiElementParent, property.GameVersion, property.GameVersion.GameApiProp?.data?.game?.latest?.decompressed_path ?? "");
                    property.GameInstall = new GenshinInstall(uiElementParent, property.GameVersion);
                    break;
                case GameNameType.Zenless:
                    property.GameVersion = new GameTypeZenlessVersion(apiResourceProp, gamePreset, gameName, gameRegion);
                    property.GameSettings = new ZenlessSettings(property.GameVersion);
                    property.GameCache = new ZenlessCache(uiElementParent, property.GameVersion, (property.GameSettings as ZenlessSettings)!);
                    property.GameRepair = new ZenlessRepair(uiElementParent, property.GameVersion, (property.GameSettings as ZenlessSettings)!);
                    property.GameInstall = new ZenlessInstall(uiElementParent, property.GameVersion, (property.GameSettings as ZenlessSettings)!);
                    break;
                case GameNameType.Plugin:
                    PluginPresetConfigWrapper pluginPresetConfig = (PluginPresetConfigWrapper)gamePreset;
                    PluginGameVersionWrapper  pluginGameVersion  = new PluginGameVersionWrapper(pluginPresetConfig);

                    property.GameVersion  = pluginGameVersion;
                    property.GameSettings = SettingsBase.CreateBaseFrom(pluginGameVersion, true);
                    property.GameCache    = null;
                    property.GameRepair   = null;
                    property.GameInstall  = new PluginGameInstallWrapper(uiElementParent, pluginPresetConfig, pluginGameVersion);
                    break;
                case GameNameType.Unknown:
                default:
                    throw new NotSupportedException($"[GamePresetProperty.Ctor] Game type: {gamePreset.GameType} ({gamePreset.ProfileName} - {gamePreset.ZoneName}) is not supported!");
            }

            property.GamePlaytime  = new Playtime(property.GameVersion, property.GameSettings);
            property.GamePropLogger = ILoggerHelper.GetILogger($"GameProp: {gameName} - {gameRegion}");

            return property;
        }

        internal bool                 IsPlugin        => GamePreset is PluginPresetConfigWrapper;
        internal RegionResourceProp?  ApiResourceProp { get; set; }
        internal IGameSettings?       GameSettings    { get; set; }
        internal IGamePlaytime?       GamePlaytime    { get; set; }
        internal IRepair?             GameRepair      { get; set; }
        internal ICache?              GameCache       { get; set; }
        internal ILogger?             GamePropLogger  { get; set; }
        internal IGameVersion?        GameVersion     { get; set; }
        internal IGameInstallManager? GameInstall     { get; set; }
        internal PresetConfig         GamePreset      { get => GameVersion?.GamePreset ?? throw new NullReferenceException(); }
        
        internal string GameExecutableName
        {
            get => GamePreset.GameExecutableName ?? "";
        }

        internal string GameExecutableNameWithoutExtension
        {
            get => Path.GetFileNameWithoutExtension(GameExecutableName);
        }

        internal string GameExecutableDir
        {
            get => GameVersion?.GameDirPath ?? throw new NullReferenceException();
        }

        internal string GameExecutablePath
        {
            get => Path.Combine(GameExecutableDir, GameExecutableName);
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
            GameInstall?.CancelRoutine();

            GameRepair?.Dispose();
            GameCache?.Dispose();
            GameInstall?.Dispose();
            GamePlaytime?.Dispose();

            ApiResourceProp = null;
            GameSettings    = null;
            GameRepair      = null;
            GameCache       = null;
            GameInstall     = null;
            GamePlaytime    = null;

            GC.SuppressFinalize(this);
        #if DEBUG
            var hashID = GameVersion != null ? GamePreset.HashID.ToString() : "null";
            Logger.LogWriteLine($"[GamePresetProperty::Dispose()] GamePresetProperty has been disposed for Hash ID: {hashID}",
                                LogType.Warning, true);
        #endif
        }
    }
}
