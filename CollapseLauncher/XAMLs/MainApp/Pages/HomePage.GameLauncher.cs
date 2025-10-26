#if !DISABLEDISCORD
using CollapseLauncher.DiscordPresence;
#endif
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Animation;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.Interfaces;
using CollapseLauncher.Plugins;
using H.NotifyIcon;
using Hi3Helper;
using Hi3Helper.EncTool.WindowTool;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.SentryHelper;
using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Screen;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using Size = System.Drawing.Size;

// ReSharper disable InconsistentNaming
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable AsyncVoidMethod
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable CheckNamespace

namespace CollapseLauncher.Pages;

public partial class HomePage
{
    #region Game Start/Stop Method

    private CancellationTokenSource WatchOutputLog = new();
    private CancellationTokenSource ResizableWindowHookToken;

#nullable enable
    private async void StartGame(object? sender, RoutedEventArgs? e)
    {
        // Initialize values
        IGameSettingsUniversal _Settings   = CurrentGameProperty!.GameSettings!.AsIGameSettingsUniversal();
        PresetConfig           _gamePreset = CurrentGameProperty!.GameVersion!.GamePreset;

        bool usePluginGameLaunchApi = _gamePreset is PluginPresetConfigWrapper { RunGameContext.IsFeatureAvailable: true };

        bool isGenshin  = CurrentGameProperty!.GameVersion.GameType == GameNameType.Genshin;
        bool giForceHDR = false;

        // Stop update check
        IsSkippingUpdateCheck = true;
        Process? proc           = null;
        bool     isUseGameBoost = _Settings.SettingsCollapseMisc is { UseGameBoost: true };

        try
        {
            if (!await CheckMediaPackInstalled()) return;

            if (isGenshin)
            {
                giForceHDR = GetAppConfigValue("ForceGIHDREnable").ToBool();
                if (giForceHDR) GenshinHDREnforcer();
            }

            if (_Settings is { SettingsCollapseMisc: { UseAdvancedGameSettings: true, UseGamePreLaunchCommand: true } })
            {
                int delay = _Settings.SettingsCollapseMisc.GameLaunchDelay;
                PreLaunchCommand(_Settings);
                if (delay > 0)
                    await Task.Delay(delay);
            }
                
            int height = _Settings.SettingsScreen?.height ?? 0;
            int width  = _Settings.SettingsScreen?.width ?? 0;

            string additionalArguments = GetLaunchArguments(_Settings);

            if (!usePluginGameLaunchApi)
            {
                proc = new Process();
                proc.StartInfo.FileName = Path.Combine(NormalizePath(GameDirPath)!, _gamePreset.GameExecutableName!);
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.Arguments = additionalArguments;
                LogWriteLine($"[HomePage::StartGame()] Running game with parameters:\r\n{proc.StartInfo.Arguments}");
                if (File.Exists(Path.Combine(GameDirPath, "@AltLaunchMode")))
                {
                    LogWriteLine("[HomePage::StartGame()] Using alternative launch method!", LogType.Warning, true);
                    proc.StartInfo.WorkingDirectory = (CurrentGameProperty!.GameVersion.GamePreset.ZoneName == "Bilibili" ||
                                                       (isGenshin && giForceHDR) ? NormalizePath(GameDirPath) :
                        Path.GetDirectoryName(NormalizePath(GameDirPath))!)!;
                }
                else
                {
                    proc.StartInfo.WorkingDirectory = NormalizePath(GameDirPath)!;
                }
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.Verb            = "runas";
                proc.Start();
            }
            else
            {
                _ = ((PluginPresetConfigWrapper)_gamePreset)
                   .UseToggledGameLaunchContext()
                   .RunGameFromGameManagerAsync(additionalArguments,
                                                isUseGameBoost,
                                                isUseGameBoost
                                                    ? ProcessPriorityClass.AboveNormal
                                                    : ProcessPriorityClass.Normal);
            }

            if (!usePluginGameLaunchApi)
            {
                if (GetAppConfigValue("EnableConsole").ToBool())
                {
                    WatchOutputLog = new CancellationTokenSource();
                    ReadOutputLog();
                }
            }

            if (_Settings.SettingsCollapseScreen.UseCustomResolution && height != 0 && width != 0)
            {
                SetBackScreenSettings(_Settings, height, width, CurrentGameProperty);
            }

            GameRunningWatcher(_Settings);
                
            switch (GetAppConfigValue("GameLaunchedBehavior").ToString())
            {
                case "ToTray":
                    WindowUtility.ToggleToTray_MainWindow();
                    break;
                case "Nothing":
                    break;
                // ReSharper disable once RedundantCaseLabel
                case "Minimize":
                default:
                    WindowUtility.WindowMinimize(false);
                    break;
            }

            if (GetAppConfigValue("LowerCollapsePrioOnGameLaunch").ToBool())
            {
                if (usePluginGameLaunchApi)
                {
                    CollapsePrioControl(((PluginPresetConfigWrapper)_gamePreset)
                                       .RunGameContext.WaitRunningGameAsync);
                }
                else
                {
                    CollapsePrioControl(x => proc?.WaitForExitAsync(x) ?? Task.CompletedTask);
                }
            }

            // Set game process priority to Above Normal when GameBoost is on
            if (isUseGameBoost && !usePluginGameLaunchApi)
            {
                _ = Task.FromResult(_ = GameBoost_Invoke(CurrentGameProperty));
            }

            // Run game process watcher
            await CheckRunningGameInstance(_gamePreset, PageToken.Token);
        }
        catch (Win32Exception ex)
        {
            LogWriteLine($"There is a problem while trying to launch Game with Region: {_gamePreset.ZoneName}\r\nTraceback: {ex}", LogType.Error, true);
            ErrorSender.SendException(new Win32Exception($"There was an error while trying to launch the game!\r\tThrow: {ex}", ex));
            IsSkippingUpdateCheck = false;
        }
    }

    // Use this method to do something when game is closed
    private async void GameRunningWatcher(IGameSettingsUniversal _settings)
    {
        ArgumentNullException.ThrowIfNull(_settings);

        await Task.Delay(5000);
        while (_cachedIsGameRunning)
        {
            await Task.Delay(3000);
        }

        if (ResizableWindowHookToken != null)
        {
            await ResizableWindowHookToken.CancelAsync();
            ResizableWindowHookToken.Dispose();
        }

        // Stopping GameLogWatcher
        if (GetAppConfigValue("EnableConsole").ToBool())
        {
            if (WatchOutputLog == null) return;
            await WatchOutputLog.CancelAsync();
        }

        // Stop PreLaunchCommand process
        if (_settings.SettingsCollapseMisc!.GamePreLaunchExitOnGameStop) PreLaunchCommand_ForceClose();

        // Window manager on game closed
        switch (GetAppConfigValue("GameLaunchedBehavior").ToString())
        {
            case "Minimize":
                WindowUtility.WindowRestore();
                break;
            case "ToTray":
                WindowUtility.CurrentWindow!.Show();
                WindowUtility.WindowRestore();
                break;
            case "Nothing":
                break;
            default:
                WindowUtility.WindowRestore();
                break;
        }

        // Run Post Launch Command
        if (_settings.SettingsCollapseMisc.UseAdvancedGameSettings && _settings.SettingsCollapseMisc.UseGamePostExitCommand) PostExitCommand(_settings);

        // Re-enable update check
        IsSkippingUpdateCheck = false;
    }

    private static void StopGame(PresetConfig gamePreset)
    {
        ArgumentNullException.ThrowIfNull(gamePreset);
        try
        {
            if (gamePreset is PluginPresetConfigWrapper { RunGameContext.IsFeatureAvailable: true } pluginGamePreset)
            {
                LogWriteLine("Trying to stop game process from plugin...", LogType.Scheme, true);
                pluginGamePreset.RunGameContext.KillRunningGame(out _, out _, out _);
            }
            else
            {
                Process[] gameProcess = Process.GetProcessesByName(gamePreset.GameExecutableName!.Split('.')[0]);
                foreach (Process p in gameProcess)
                {
                    LogWriteLine($"Trying to stop game process {gamePreset.GameExecutableName.Split('.')[0]} at PID {p.Id}", LogType.Scheme, true);
                    p.Kill();
                    p.Dispose();
                }
            }
        }
        catch (Win32Exception ex)
        {
            SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
            LogWriteLine($"There is a problem while trying to stop Game with Region: {gamePreset.ZoneName}\r\nTraceback: {ex}", LogType.Error, true);
        }
    }
#nullable restore
    #endregion

    #region Game Launch Argument Builder

    private bool RequireWindowExclusivePayload;

    private string GetLaunchArguments(IGameSettingsUniversal _Settings)
    {
        StringBuilder parameter = new StringBuilder();

        switch (CurrentGameProperty.GameVersion?.GameType)
        {
            case GameNameType.Honkai:
            {
                if (_Settings.SettingsCollapseScreen.UseExclusiveFullscreen)
                {
                    parameter.Append("-window-mode exclusive ");
                    RequireWindowExclusivePayload = true;
                }

                Size screenSize = _Settings.SettingsScreen.sizeRes;

                byte apiID = _Settings.SettingsCollapseScreen.GameGraphicsAPI;

                if (apiID == 4)
                {
                    LogWriteLine("You are going to use DX12 mode in your game.\r\n\tUsing CustomScreenResolution or FullscreenExclusive value may break the game!", LogType.Warning);
                    if (_Settings.SettingsCollapseScreen.UseCustomResolution && _Settings.SettingsScreen.isfullScreen)
                    {
                        var size = ScreenProp.CurrentResolution;
                        parameter.Append($"-screen-width {size.Width} -screen-height {size.Height} ");
                    }
                    else
                        parameter.Append($"-screen-width {screenSize.Width} -screen-height {screenSize.Height} ");
                }
                else
                    parameter.Append($"-screen-width {screenSize.Width} -screen-height {screenSize.Height} ");

                switch (apiID)
                {
                    case 0:
                        parameter.Append("-force-feature-level-10-1 ");
                        break;
                    // case 1 is default
                    default:
                        parameter.Append("-force-feature-level-11-0 -force-d3d11-no-singlethreaded ");
                        break;
                    case 2:
                        parameter.Append("-force-feature-level-11-1 ");
                        break;
                    case 3:
                        parameter.Append("-force-feature-level-11-1 -force-d3d11-no-singlethreaded ");
                        break;
                    case 4:
                        parameter.Append("-force-d3d12 ");
                        break;
                }

                break;
            }
            case GameNameType.StarRail:
            {
                if (_Settings.SettingsCollapseScreen.UseExclusiveFullscreen)
                {
                    parameter.Append("-window-mode exclusive -screen-fullscreen 1 ");
                    RequireWindowExclusivePayload = true;
                }

                // Enable mobile mode
                //if (_Settings.SettingsCollapseMisc.LaunchMobileMode)
                if (false) // Force disable Mobile mode due to reported bannable offense in GI. Thank you HoYo.
                    // Added pragma in-case this will be reused in the future.
            #pragma warning disable CS0162 // Unreachable code detected
                {
                    const string regLoc  = GameSettings.StarRail.Model.ValueName;
                    var          regRoot = GameSettings.Base.SettingsBase.RegistryRoot;

                    if (regRoot != null || !string.IsNullOrEmpty(regLoc))
                    {
                        var regModel = (byte[])regRoot!.GetValue(regLoc, null);

                        if (regModel != null)
                        {
                            string regB64 = Convert.ToBase64String(regModel);
                            parameter.Append($"-is_cloud 1 -platform_type CLOUD_WEB_TOUCH -graphics_setting {regB64} ");
                        }
                        else
                        {
                            LogWriteLine("Failed enabling MobileMode for HSR: regModel is null.", LogType.Error, true);
                        }
                    }
                    else
                    {
                        LogWriteLine("Failed enabling MobileMode for HSR: regRoot/regLoc is unexpectedly uninitialized.",
                                     LogType.Error, true);
                    }
                }
            #pragma warning restore CS0162 // Unreachable code detected

                Size screenSize = _Settings.SettingsScreen.sizeRes;

                byte apiID = _Settings.SettingsCollapseScreen.GameGraphicsAPI;

                if (apiID == 4)
                {
                    LogWriteLine("You are going to use DX12 mode in your game.\r\n\tUsing CustomScreenResolution or FullscreenExclusive value may break the game!", LogType.Warning);
                    if (_Settings.SettingsCollapseScreen.UseCustomResolution && _Settings.SettingsScreen.isfullScreen)
                    {
                        var size = ScreenProp.CurrentResolution;
                        parameter.Append($"-screen-width {size.Width} -screen-height {size.Height} ");
                    }
                    else
                        parameter.Append($"-screen-width {screenSize.Width} -screen-height {screenSize.Height} ");
                }
                else
                    parameter.Append($"-screen-width {screenSize.Width} -screen-height {screenSize.Height} ");

                break;
            }
            case GameNameType.Genshin:
            {
                if (_Settings.SettingsCollapseScreen.UseExclusiveFullscreen)
                {
                    parameter.Append("-window-mode exclusive -screen-fullscreen 1 ");
                    RequireWindowExclusivePayload = true;
                    LogWriteLine("Exclusive mode is enabled in Genshin Impact, stability may suffer!\r\nTry not to Alt+Tab when game is on its loading screen :)", LogType.Warning, true);
                }

                // Enable mobile mode
                // Enable mobile mode
                //if (_Settings.SettingsCollapseMisc.LaunchMobileMode)
                if (false) // Force disable Mobile mode due to reported bannable offense in GI. Thank you HoYo.
                    // Added pragma in-case this will be reused in the future.
                #pragma warning disable CS0162 // Unreachable code detected
                    parameter.Append("use_mobile_platform -is_cloud 1 -platform_type CLOUD_THIRD_PARTY_MOBILE ");
                #pragma  warning enable CS0162 // Unreachable code detected

                Size screenSize = _Settings.SettingsScreen.sizeRes;

                byte apiID = _Settings.SettingsCollapseScreen.GameGraphicsAPI;

                if (apiID == 4)
                {
                    LogWriteLine("You are going to use DX12 mode in your game.\r\n\tUsing CustomScreenResolution or FullscreenExclusive value may break the game!", LogType.Warning);
                    if (_Settings.SettingsCollapseScreen.UseCustomResolution && _Settings.SettingsScreen.isfullScreen)
                    {
                        var size = ScreenProp.CurrentResolution;
                        parameter.Append($"-screen-width {size.Width} -screen-height {size.Height} ");
                    }
                    else
                        parameter.Append($"-screen-width {screenSize.Width} -screen-height {screenSize.Height} ");
                }
                else
                    parameter.Append($"-screen-width {screenSize.Width} -screen-height {screenSize.Height} ");

                break;
            }
            case GameNameType.Zenless:
            {
                // does not support exclusive mode at all
                // also doesn't properly support dx12 or dx11 st
                
                if (_Settings.SettingsCollapseScreen.UseCustomResolution)
                {
                    Size screenSize = _Settings.SettingsScreen.sizeRes;
                    parameter.Append($"-screen-width {screenSize.Width} -screen-height {screenSize.Height} ");
                }

                break;
            }
        }

        if (_Settings.SettingsCollapseScreen.UseBorderlessScreen)
        {
            parameter.Append("-popupwindow ");
        }

        if (!_Settings.SettingsCollapseMisc.UseCustomArguments)
        {
            return parameter.ToString();
        }

        string customArgs = _Settings.SettingsCustomArgument.CustomArgumentValue;
        if (!string.IsNullOrEmpty(customArgs))
            parameter.Append(customArgs);

        return parameter.ToString();
    }

    public string CustomArgsValue
    {
        get => CurrentGameProperty?.GameSettings?.SettingsCustomArgument.CustomArgumentValue;
        set
        {
            if (CurrentGameProperty.GameSettings == null)
                return;

            CurrentGameProperty.GameSettings.SettingsCustomArgument.CustomArgumentValue = value;
        }
    }

    public bool UseCustomArgs
    {
        get => CurrentGameProperty?.GameSettings?.SettingsCollapseMisc.UseCustomArguments ?? false;
        set
        {
            if (CurrentGameProperty.GameSettings == null)
                return;

            CustomArgsTextBox.IsEnabled                                              = CustomStartupArgsSwitch.IsOn;
            CurrentGameProperty.GameSettings.SettingsCollapseMisc.UseCustomArguments = value;
        } 
            
    }
        
    public bool UseCustomBGRegion
    {
        get
        {
            bool value = CurrentGameProperty?.GameSettings?.SettingsCollapseMisc?.UseCustomRegionBG ?? false;
            ChangeGameBGButton.IsEnabled = value;
            string path = CurrentGameProperty?.GameSettings?.SettingsCollapseMisc?.CustomRegionBGPath ?? "";
            BGPathDisplay.Text = Path.GetFileName(path);
            return value;
        }
        set
        {
            ChangeGameBGButton.IsEnabled = value;

            if (CurrentGameProperty?.GameSettings == null)
                return;

            var regionBgPath = CurrentGameProperty.GameSettings.SettingsCollapseMisc.CustomRegionBGPath;
            if (string.IsNullOrEmpty(regionBgPath) || !File.Exists(regionBgPath))
            {
                regionBgPath = Path.GetFileName(GetAppConfigValue("CustomBGPath").ToString());
                CurrentGameProperty.GameSettings.SettingsCollapseMisc
                                   .CustomRegionBGPath = regionBgPath;
            }

            CurrentGameProperty.GameSettings.SettingsCollapseMisc.UseCustomRegionBG = value;
            CurrentGameProperty.GameSettings.SaveBaseSettings();
            _ = m_mainPage?.ChangeBackgroundImageAsRegionAsync();

            BGPathDisplay.Text = Path.GetFileName(regionBgPath);
        } 
    }
    #endregion

    #region Game Log Method
    private async void ReadOutputLog()
    {
        var saveGameLog = GetAppConfigValue("IncludeGameLogs").ToBool();
        InitializeConsoleValues();
            
        // JUST IN CASE
        // Sentry issue ref : COLLAPSE-LAUNCHER-55; Event ID: 13059407
        if (int.IsNegative(barWidth)) barWidth = 30;
            
        LogWriteLine($"Are Game logs getting saved to Collapse logs: {saveGameLog}", LogType.Scheme, true);
            
        try
        {
            var gameDirAppDataPath = CurrentGameProperty.GameVersion?.GameDirAppDataPath;
            var gameOutputLogName = CurrentGameProperty.GameVersion?.GameOutputLogName;
            if (string.IsNullOrEmpty(gameDirAppDataPath) || string.IsNullOrEmpty(gameOutputLogName))
            {
                LogWriteLine("Game log path is not set! Cannot read game logs!", LogType.Error, saveGameLog);
                return;
            }
            var logPath = Path.Combine(gameDirAppDataPath, gameOutputLogName);
            if (!Directory.Exists(Path.GetDirectoryName(logPath)))
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                
            if (CurrentGameProperty.GamePreset.GameType == GameNameType.Zenless)
            {
                var logDir = Path.Combine(CurrentGameProperty.GameVersion.GameDirPath,
                                          @"ZenlessZoneZero_Data\Persistent\LogDir\");

                _ = Directory.CreateDirectory(logDir); // Always ensure that the LogDir will always be created.

                var newLog = await FileUtility.WaitForNewFileAsync(logDir, 20000);
                if (!newLog)
                {
                    LogWriteLine("Cannot get Zenless' log file due to timeout! Your computer too fast XD",
                                 LogType.Warning, saveGameLog);
                    return;
                }

                var logPat = FileUtility.GetLatestFile(logDir, "NAP_*.log");

                if (!string.IsNullOrEmpty(logPat)) logPath = logPat;
            }
            else
            {
                // If the log file exist beforehand, move it and make a new one
                if (File.Exists(logPath))
                {
                    FileUtility.RenameFileWithPrefix(logPath, "-old", true);
                } 
            }
                
            LogWriteLine($"Reading Game's log file from {logPath}", LogType.Default, saveGameLog);

            await using FileStream fs =
                new FileStream(logPath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
            using StreamReader reader = new StreamReader(fs);
            while (true)
            {
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync(WatchOutputLog.Token);
                    if (RequireWindowExclusivePayload && line == "MoleMole.MonoGameEntry:Awake()")
                    {
                        StartExclusiveWindowPayload();
                        RequireWindowExclusivePayload = false;
                    }

                    LogWriteLine(line!, LogType.Game, saveGameLog);
                }

                await Task.Delay(100, WatchOutputLog.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore when cancelled
        }
        catch (Exception ex)
        {
            await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
            LogWriteLine($"There were a problem in Game Log Reader\r\n{ex}", LogType.Error);
        }
    }
    #endregion

    #region Exclusive Window Payload
    private async void StartExclusiveWindowPayload()
    {
        IntPtr _windowPtr = ProcessChecker.GetProcessWindowHandle(CurrentGameProperty.GameVersion?.GamePreset.GameExecutableName ?? "");
        await Task.Delay(1000);
        Windowing.HideWindow(_windowPtr);
        await Task.Delay(1000);
        Windowing.ShowWindow(_windowPtr);
    }
    #endregion

    #region Game Resizable Window Payload
    private async void StartResizableWindowPayload(string                 executableName,
                                                   IGameSettingsUniversal settings,
                                                   PresetConfig           gamePreset,
                                                   int?                   height,
                                                   int?                   width)
    {
        try
        {
            GameNameType gameType = gamePreset.GameType;

            // Check if the game is using Resizable Window settings
            if (!settings.SettingsCollapseScreen.UseResizableWindow) return;
            ResizableWindowHookToken = new CancellationTokenSource();

            while (!ResizableWindowHookToken.IsCancellationRequested &&
                   !CurrentGameProperty.TryGetGameProcessIdWithActiveWindow(out _, out _))
            {
                await Task.Delay(200, ResizableWindowHookToken.Token);
            }

            string gameExecutablePath = Path.Combine(CurrentGameProperty.GameVersion?.GameDirPath.NormalizePath() ?? "",
                                                     executableName.NormalizePath());
            executableName = Path.GetFileNameWithoutExtension(gameExecutablePath);
            var gameExecutableDirectory = Path.GetDirectoryName(gameExecutablePath);
            if (string.IsNullOrEmpty(gameExecutableDirectory))
            {
                LogWriteLine("Game executable directory is not set! Cannot start Resizable Window payload!", LogType.Error);
                return;
            }

            // Set the pos + size reinitialization to true if the game is Honkai: Star Rail or ZZZ
            // This is required for Honkai: Star Rail or ZZZ since the game will reset its pos + size. Making
            // it impossible to use custom resolution (but since you are using Collapse, it's now
            // possible :teriStare:)
            bool isNeedToResetPos = gameType is GameNameType.StarRail or GameNameType.Zenless;
            await Task.Run(() => ResizableWindowHook.StartHook(executableName,
                                                               height,
                                                               width,
                                                               isNeedToResetPos,
                                                               ILoggerHelper.GetILogger(),
                                                               gameExecutableDirectory,
                                                               ResizableWindowHookToken.Token));
        }
        catch (Exception ex)
        {
            LogWriteLine($"Error while initializing Resizable Window payload!\r\n{ex}");
            ErrorSender.SendException(ex, ErrorType.GameError);
        }
    }

    private static async void SetBackScreenSettings(IGameSettingsUniversal settingsUniversal,
                                                    int                    height,
                                                    int                    width,
                                                    GamePresetProperty     gameProp)
    {
        // Wait for the game to fully initialize
        await Task.Delay(20000);
        try
        {
            settingsUniversal.SettingsScreen.height = height;
            settingsUniversal.SettingsScreen.width  = width;
            settingsUniversal.SettingsScreen.Save();

            // For those stubborn game
            // Kinda unneeded but :FRICK:
            switch (gameProp.GamePreset.GameType)
            {
                case GameNameType.Zenless:
                    var screenManagerZ = GameSettings.Zenless.ScreenManager.Load();
                    screenManagerZ.width  = width;
                    screenManagerZ.height = height;
                    screenManagerZ.Save();
                    break;
                    
                case GameNameType.Honkai:
                    var screenManagerH = GameSettings.Honkai.ScreenSettingData.Load();
                    screenManagerH.width  = width;
                    screenManagerH.height = height;
                    screenManagerH.Save();
                    break;
            }
                
            LogWriteLine($"[SetBackScreenSettings] Completed task! {width}x{height}", LogType.Scheme, true);
        }
        catch(Exception ex)
        {
            await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
            LogWriteLine($"[SetBackScreenSettings] Failed to set Screen Settings!\r\n{ex}", LogType.Error, true);
        }

    }
    #endregion

    #region Pre/Post Game Launch Command

    private static readonly string CmdPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");

    private Process _procPreGLC;

    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    private async void PreLaunchCommand(IGameSettingsUniversal settings)
    {
        try
        {
            var preGameLaunchCommand = settings?.SettingsCollapseMisc?.GamePreLaunchCommand;
            if (string.IsNullOrEmpty(preGameLaunchCommand)) return;

            LogWriteLine($"Using Pre-launch command : {preGameLaunchCommand}\r\n" +
                         $"Game launch is delayed by {settings.SettingsCollapseMisc.GameLaunchDelay} ms\r\n\t" +
                         $"BY USING THIS, NO SUPPORT IS PROVIDED IF SOMETHING HAPPENED TO YOUR ACCOUNT, GAME, OR SYSTEM!",
                         LogType.Warning, true);

            _procPreGLC = new Process();

            _procPreGLC.StartInfo.FileName               = CmdPath;
            _procPreGLC.StartInfo.Arguments              = "/S /C " + "\"" + preGameLaunchCommand + "\"";
            _procPreGLC.StartInfo.CreateNoWindow         = true;
            _procPreGLC.StartInfo.UseShellExecute        = false;
            _procPreGLC.StartInfo.RedirectStandardOutput = true;
            _procPreGLC.StartInfo.RedirectStandardError  = true;

            _procPreGLC.OutputDataReceived += GLC_OutputHandler;
            _procPreGLC.ErrorDataReceived  += GLC_ErrorHandler;

            _procPreGLC.Start();

            _procPreGLC.BeginOutputReadLine();
            _procPreGLC.BeginErrorReadLine();

            await _procPreGLC.WaitForExitAsync();
                
            _procPreGLC.OutputDataReceived -= GLC_OutputHandler;
            _procPreGLC.ErrorDataReceived  -= GLC_ErrorHandler;
        }
        catch (Win32Exception ex)
        {
            LogWriteLine($"There is a problem while trying to launch Pre-Game Command with Region: " +
                         $"{CurrentGameProperty.GameVersion?.GamePreset.ZoneName}\r\nTraceback: {ex}", LogType.Error, true);
            ErrorSender.SendException(new Win32Exception($"There was an error while trying to launch Pre-Launch command!\r\tThrow: {ex}", ex));
        }
        finally
        {
            _procPreGLC?.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    private void PreLaunchCommand_ForceClose()
    {
        try
        {
            if (_procPreGLC == null || _procPreGLC.HasExited || _procPreGLC.Id == 0 ) return;

            // Kill main and child processes
            var taskKill = new Process();
            taskKill.StartInfo.FileName =
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "taskkill.exe");
            
            taskKill.StartInfo.Arguments = $"/F /T /PID {_procPreGLC.Id}";
            taskKill.Start();
            taskKill.WaitForExit();

            LogWriteLine("Pre-launch command has been forced to close!", LogType.Warning, true);
        }
        // Ignore external errors
        catch (InvalidOperationException ioe)
        {
            SentryHelper.ExceptionHandler(ioe);
        }
        catch (Win32Exception we)
        {
            SentryHelper.ExceptionHandler(we);
        }
        catch (Exception ex)
        {
            SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
            LogWriteLine($"Error when trying to close Pre-GLC!\r\n{ex}", LogType.Error, true);
        }
    }

    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    private static async void PostExitCommand(IGameSettingsUniversal settings)
    {
        try
        {
            var postGameExitCommand = settings?.SettingsCollapseMisc?.GamePostExitCommand;
            if (string.IsNullOrEmpty(postGameExitCommand)) return;

            LogWriteLine($"Using Post-launch command : {postGameExitCommand}\r\n\t" +
                         $"BY USING THIS, NO SUPPORT IS PROVIDED IF SOMETHING HAPPENED TO YOUR ACCOUNT, GAME, OR SYSTEM!",
                         LogType.Warning, true);

            Process procPostGLC = new Process();

            procPostGLC.StartInfo.FileName               = CmdPath;
            procPostGLC.StartInfo.Arguments              = "/S /C " + "\"" + postGameExitCommand + "\"";
            procPostGLC.StartInfo.CreateNoWindow         = true;
            procPostGLC.StartInfo.UseShellExecute        = false;
            procPostGLC.StartInfo.RedirectStandardOutput = true;
            procPostGLC.StartInfo.RedirectStandardError  = true;

            procPostGLC.OutputDataReceived += GLC_OutputHandler;
            procPostGLC.ErrorDataReceived  += GLC_ErrorHandler;

            procPostGLC.Start();
            procPostGLC.BeginOutputReadLine();
            procPostGLC.BeginErrorReadLine();

            await procPostGLC.WaitForExitAsync();

            procPostGLC.OutputDataReceived -= GLC_OutputHandler;
            procPostGLC.ErrorDataReceived  -= GLC_ErrorHandler;
        }
        catch (Win32Exception ex)
        {
            LogWriteLine($"There is a problem while trying to launch Post-Game Command with command:\r\n\t" +
                         $"{settings?.SettingsCollapseMisc?.GamePostExitCommand}\r\n" +
                         $"Traceback: {ex}", LogType.Error, true);
            ErrorSender.SendException(new Win32Exception($"There was an error while trying to launch Post-Exit command\r\tThrow: {ex}", ex));
        }
    }

    private static void GLC_OutputHandler(object _, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data)) LogWriteLine(e.Data, LogType.GLC, true);
    }

    private static void GLC_ErrorHandler(object _, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data)) LogWriteLine($"ERROR RECEIVED!\r\n\t" + $"{e.Data}", LogType.GLC, true);
    }
    #endregion

    #region Game Running State
    private async Task CheckRunningGameInstance(PresetConfig presetConfig, CancellationToken token)
    {
        bool usePluginGameLaunchApi = presetConfig is PluginPresetConfigWrapper { RunGameContext.IsFeatureAvailable: true };
        DateTime pluginLaunchedGameTime =
            (presetConfig as PluginPresetConfigWrapper)?.RunGameContext.GameLaunchStartTime ?? default;

        TextBlock startGameBtnText = (StartGameBtn.Content as Grid)!.Children.OfType<TextBlock>().FirstOrDefault();
        FontIcon startGameBtnIcon = (StartGameBtn.Content as Grid)!.Children.OfType<FontIcon>().FirstOrDefault();
        Grid startGameBtnAnimatedIconGrid = (StartGameBtn.Content as Grid)!.Children.OfType<Grid>().FirstOrDefault();
        // AnimatedVisualPlayer    StartGameBtnAnimatedIcon      = StartGameBtnAnimatedIconGrid!.Children.OfType<AnimatedVisualPlayer>().FirstOrDefault();
        string       startGameBtnIconGlyph        = startGameBtnIcon!.Glyph;
        const string startGameBtnRunningIconGlyph = "";

        startGameBtnIcon.EnableSingleImplicitAnimation(VisualPropertyType.Opacity);
        startGameBtnAnimatedIconGrid.EnableSingleImplicitAnimation(VisualPropertyType.Opacity);

        try
        {
            while (CurrentGameProperty.IsGameRunning)
            {
                _cachedIsGameRunning = true;

                StartGameBtn.IsEnabled = false;
                if (startGameBtnText != null && startGameBtnAnimatedIconGrid != null)
                {
                    startGameBtnText.Text                = Lang._HomePage.StartBtnRunning;
                    startGameBtnIcon.Glyph               = startGameBtnRunningIconGlyph;
                    startGameBtnAnimatedIconGrid.Opacity = 0;
                    startGameBtnIcon.Opacity             = 1;

                    startGameBtnText.UpdateLayout();

                    RepairGameButton.IsEnabled       = false;
                    UninstallGameButton.IsEnabled    = false;
                    ConvertVersionButton.IsEnabled   = false;
                    CustomArgsTextBox.IsEnabled      = false;
                    MoveGameLocationButton.IsEnabled = false;
                    StopGameButton.IsEnabled         = true;

                    PlaytimeIdleStack.Visibility    = Visibility.Collapsed;
                    PlaytimeRunningStack.Visibility = Visibility.Visible;

                    int processId = 0;
                    if ((!usePluginGameLaunchApi && CurrentGameProperty.TryGetGameProcessIdWithActiveWindow(out processId, out _)) ||
                        (usePluginGameLaunchApi && CurrentGameProperty.IsGameRunning))
                    {
                        LogWriteLine($"{new string('=', barWidth)} GAME STARTED {new string('=', barWidth)}", LogType.Warning, true);

                        Process currentGameProcess = null!;
                        if (!usePluginGameLaunchApi)
                            currentGameProcess = Process.GetProcessById(processId);

                        try
                        {
                            // HACK: For some reason, the text still unchanged.
                            //       Make sure the start game button text also changed.
                            startGameBtnText.Text = Lang._HomePage.StartBtnRunning;
                            DateTime fromActivityOffset = currentGameProcess?.StartTime ?? pluginLaunchedGameTime;
                            IGameSettingsUniversal gameSettings = CurrentGameProperty!.GameSettings!.AsIGameSettingsUniversal();
                            PresetConfig gamePreset = CurrentGameProperty.GamePreset;

                        #if !DISABLEDISCORD
                            if (ToggleRegionPlayingRpc)
                                AppDiscordPresence?.SetActivity(ActivityType.Play, fromActivityOffset.ToUniversalTime());
                        #endif

                            int height = gameSettings.SettingsScreen?.height ?? 0;
                            int width  = gameSettings.SettingsScreen?.width ?? 0;

                            // Start the resizable window payload
                            StartResizableWindowPayload(gamePreset.GameExecutableName,
                                                        gameSettings,
                                                        gamePreset,
                                                        height,
                                                        width);

                            Task ProcessAwaiter(CancellationToken x) =>
                                // ReSharper disable once AccessToDisposedClosure
                                currentGameProcess?.WaitForExitAsync(x) ?? (!usePluginGameLaunchApi
                                    ? Task.CompletedTask
                                    : ((PluginPresetConfigWrapper)presetConfig).RunGameContext.WaitRunningGameAsync(x));

                            _ = CurrentGameProperty!.GamePlaytime!.StartSessionFromAwaiter(ProcessAwaiter);

                            await ProcessAwaiter(token);

                            LogWriteLine($"{new string('=', barWidth)} GAME STOPPED {new string('=', barWidth)}", LogType.Warning, true);
                        }
                        finally
                        {
                            currentGameProcess?.Dispose();
                        }
                    }
                }

                await Task.Delay(RefreshRate, token);
            }

            _cachedIsGameRunning = false;

            StartGameBtn.IsEnabled = true;
            startGameBtnText!.Text = Lang._HomePage.StartBtn;
            startGameBtnIcon.Glyph = startGameBtnIconGlyph;
            if (startGameBtnAnimatedIconGrid != null)
            {
                startGameBtnAnimatedIconGrid.Opacity = 1;
            }

            startGameBtnIcon.Opacity = 0;

            GameStartupSetting.IsEnabled     = true;
            RepairGameButton.IsEnabled       = true;
            MoveGameLocationButton.IsEnabled = true;
            UninstallGameButton.IsEnabled    = true;
            ConvertVersionButton.IsEnabled   = true;
            CustomArgsTextBox.IsEnabled      = true;
            StopGameButton.IsEnabled         = false;

            PlaytimeIdleStack.Visibility    = Visibility.Visible;
            PlaytimeRunningStack.Visibility = Visibility.Collapsed;
                
        #if !DISABLEDISCORD
            AppDiscordPresence?.SetActivity(ActivityType.Idle);
        #endif
        }
        catch (TaskCanceledException)
        {
            // Ignore
            LogWriteLine("Game run watcher has been terminated!");
        }
        catch (Exception ex)
        {
            await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
            LogWriteLine($"Error when checking if game is running!\r\n{ex}", LogType.Error, true);
        }
    }
    #endregion
}