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
using Hi3Helper.SentryHelper;
using Hi3Helper.Win32.Native.ManagedTools;
using Hi3Helper;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.Diagnostics.CodeAnalysis;

// ReSharper disable CheckNamespace
// ReSharper disable PartialTypeWithSinglePart

#nullable enable
namespace CollapseLauncher
{
    internal sealed partial class GamePresetProperty : IDisposable
    {
        internal GamePresetProperty(UIElement uiElementParent, RegionResourceProp apiResourceProp, string gameName, string gameRegion)
        {
            if (LauncherMetadataHelper.LauncherMetadataConfig == null)
            {
                return;
            }

            PresetConfig gamePreset = LauncherMetadataHelper.LauncherMetadataConfig[gameName][gameRegion];

            _APIResourceProp = apiResourceProp!.Copy();
            switch (gamePreset!.GameType)
            {
                case GameNameType.Honkai:
                    _GameVersion = new GameTypeHonkaiVersion(uiElementParent, _APIResourceProp, gameName, gameRegion);
                    _GameSettings = new HonkaiSettings(_GameVersion);
                    _GameCache = new HonkaiCache(uiElementParent, _GameVersion);
                    _GameRepair = new HonkaiRepair(uiElementParent, _GameVersion, _GameCache, _GameSettings);
                    _GameInstall = new HonkaiInstall(uiElementParent, _GameVersion, _GameCache, _GameSettings);
                    break;
                case GameNameType.StarRail:
                    _GameVersion = new GameTypeStarRailVersion(uiElementParent, _APIResourceProp, gameName, gameRegion);
                    _GameSettings = new StarRailSettings(_GameVersion);
                    _GameCache = new StarRailCache(uiElementParent, _GameVersion);
                    _GameRepair = new StarRailRepair(uiElementParent, _GameVersion);
                    _GameInstall = new StarRailInstall(uiElementParent, _GameVersion);
                    break;
                case GameNameType.Genshin:
                    _GameVersion = new GameTypeGenshinVersion(uiElementParent, _APIResourceProp, gameName, gameRegion);
                    _GameSettings = new GenshinSettings(_GameVersion);
                    _GameCache = null;
                    _GameRepair = new GenshinRepair(uiElementParent, _GameVersion, _GameVersion.GameAPIProp!.data!.game!.latest!.decompressed_path);
                    _GameInstall = new GenshinInstall(uiElementParent, _GameVersion);
                    break;
                case GameNameType.Zenless:
                    _GameVersion = new GameTypeZenlessVersion(uiElementParent, _APIResourceProp, gamePreset, gameName, gameRegion);
                    ZenlessSettings gameSettings = new ZenlessSettings(_GameVersion);
                    _GameSettings = gameSettings;
                    _GameCache = new ZenlessCache(uiElementParent, _GameVersion, gameSettings);
                    _GameRepair = new ZenlessRepair(uiElementParent, _GameVersion, gameSettings);
                    _GameInstall = new ZenlessInstall(uiElementParent, _GameVersion, gameSettings);
                    break;
                case GameNameType.Unknown:
                default:
                    throw new NotSupportedException($"[GamePresetProperty.Ctor] Game type: {gamePreset.GameType} ({gamePreset.ProfileName} - {gamePreset.ZoneName}) is not supported!");
            }

            _GamePlaytime = new Playtime(_GameVersion, _GameSettings);

            SentryHelper.CurrentGameCategory = _GameVersion.GameName;
            SentryHelper.CurrentGameRegion = _GameVersion.GameRegion;
            SentryHelper.CurrentGameLocation = _GameVersion.GameDirPath;
            SentryHelper.CurrentGameInstalled = _GameVersion.IsGameInstalled();
            SentryHelper.CurrentGameUpdated = _GameVersion.IsGameVersionMatch();
            SentryHelper.CurrentGameHasPreload = _GameVersion.IsGameHasPreload();
            SentryHelper.CurrentGameHasDelta = _GameVersion.IsGameHasDeltaPatch();
        }

        internal RegionResourceProp _APIResourceProp { get; set; }
        internal PresetConfig _GamePreset { get => _GameVersion.GamePreset; }
        internal IGameSettings _GameSettings { get; set; }
        internal IGamePlaytime _GamePlaytime { get; set; }
        internal IRepair _GameRepair { get; set; }
        internal ICache _GameCache { get; set; }
        internal IGameVersionCheck _GameVersion { get; set; }
        internal IGameInstallManager _GameInstall { get; set; }

        [field: AllowNull, MaybeNull]
        internal string GameExecutableName
        {
            get
            {
                if (string.IsNullOrEmpty(field))
                    field = _GamePreset!.GameExecutableName;
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

        internal bool IsGameRunning
        {
            get => ProcessChecker.IsProcessExist(GameExecutableName, out _, out _, Path.Combine(_GameVersion?.GameDirPath ?? "", GameExecutableName), ILoggerHelper.GetILogger());
        }

        // Translation:
        // The Process.GetProcessesByName(procName) will get an array of the process list. The output is piped into null-break operator "?" which will
        // returns a null if something goes wrong. If not, then pass it to .Where(x) method which will select the given value with the certain logic.
        // (in this case, we need to ensure that the MainWindowHandle is not a non-zero pointer) and then piped into null-break operator.
        internal Process? GetGameProcessWithActiveWindow()
        {
            Process[] processArr = Process.GetProcessesByName(GameExecutableNameWithoutExtension);
            int selectedIndex = -1;
            try
            {
                for (int i = 0; i < processArr.Length; i++)
                {
                    Process process = processArr[i];
                    int processId = process.Id;

                    string? processPath = ProcessChecker.GetProcessPathByProcessId(processId, ILoggerHelper.GetILogger());
                    string expectedProcessPath = Path.Combine(_GameVersion?.GameDirPath ?? "", GameExecutableName);
                    if (string.IsNullOrEmpty(processPath) || !expectedProcessPath.Equals(processPath, StringComparison.OrdinalIgnoreCase)
                     || process.MainWindowHandle == IntPtr.Zero)
                        continue;

                    selectedIndex = i;
                    return process;
                }
            }
            finally
            {
                for (int i = 0; i < processArr.Length; i++)
                {
                    if (i == selectedIndex)
                        continue;
                    processArr[i].Dispose();
                }
            }
            return null;
        }

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
            _GamePlaytime?.Dispose();

            _APIResourceProp = null;
            _GameSettings = null;
            _GameRepair = null;
            _GameCache = null;
            _GameVersion = null;
            _GameInstall = null;
            _GamePlaytime = null;
        }
    }
}
