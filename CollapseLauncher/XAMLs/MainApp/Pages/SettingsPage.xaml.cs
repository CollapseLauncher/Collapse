using CollapseLauncher.Helper.Animation;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Pages.OOBE;
using Hi3Helper;
using Hi3Helper.Data;
#if !DISABLEDISCORD
using Hi3Helper.DiscordPresence;
#endif
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32.TaskScheduler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static CollapseLauncher.Helper.Image.Waifu2X;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.RegionResourceListHelper;
using static CollapseLauncher.WindowSize.WindowSize;
using static CollapseLauncher.FileDialogCOM.FileDialogNative;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using TaskSched = Microsoft.Win32.TaskScheduler.Task;
using Task = System.Threading.Tasks.Task;
using CollapseLauncher.Extension;

// ReSharper disable PossibleNullReferenceException
// ReSharper disable AssignNullToNotNullAttribute

namespace CollapseLauncher.Pages
{
    // ReSharper disable once RedundantExtendsListEntry
    public sealed partial class SettingsPage : Page
    {
        #region Properties
        private readonly string _collapseStartupTaskName = "CollapseLauncherStartupTask";
        #endregion

        #region Settings Page Handler
        public SettingsPage()
        {
            this.InitializeComponent();
            this.EnableImplicitAnimation(true);
            AboutApp.FindAndSetTextBlockWrapping(TextWrapping.Wrap, HorizontalAlignment.Center, TextAlignment.Center, true);

            LoadAppConfig();
            this.DataContext = this;

            string Version = $" {AppCurrentVersion.VersionString}";
#if DEBUG
            Version = Version + "d";
#endif
            if (IsPreview)
                Version = Version + " Preview";
            else
                Version = Version + " Stable";

            AppVersionTextBlock.Text = Version;
            CurrentVersion.Text = Version;
            
            GitVersionIndicator.Text = GitVersionIndicator_Builder();
            GitVersionIndicator_Hyperlink.NavigateUri = 
                new Uri(new StringBuilder()
                    .Append(ThisAssembly.Git.RepositoryUrl)
                    .Append("commit/")
                    .Append(ThisAssembly.Git.Sha).ToString());

            if (IsAppLangNeedRestart)
                AppLangSelectionWarning.Visibility = Visibility.Visible;

            if (IsChangeRegionWarningNeedRestart)
                ChangeRegionToggleWarning.Visibility = Visibility.Visible;

            string SwitchToVer = IsPreview ? "Stable" : "Preview";
            ChangeReleaseBtnText.Text = string.Format(Lang._SettingsPage.AppChangeReleaseChannel, SwitchToVer);
#if DISABLEDISCORD
            ToggleDiscordRPC.Visibility = Visibility.Collapsed;
#endif

            UpdateBindingsInvoker.UpdateEvents += UpdateBindingsEvents;
        }

        private string GitVersionIndicator_Builder()
        {
            var branchName = ThisAssembly.Git.Branch;
            var commitShort = ThisAssembly.Git.Commit;

            // Add indicator if the commit is dirty
            // CS0162: Unreachable code detected
#pragma warning disable CS0162
            if (ThisAssembly.Git.IsDirty) commitShort = $"{commitShort}*";
#pragma warning restore CS0162

            var outString =
                // If branch is not HEAD, show branch name and short commit
                // Else, show full SHA 
                branchName == "HEAD" ? ThisAssembly.Git.Sha : $"{branchName} - {commitShort}";

            return outString;
        }
        
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            BackgroundImgChanger.ToggleBackground(true);

#if !DISABLEDISCORD
            AppDiscordPresence.SetActivity(ActivityType.AppSettings);
#endif
        }
        #endregion

        #region Settings Methods
        private async void RelocateFolder(object sender, RoutedEventArgs e)
        {
            switch (await Dialog_RelocateFolder(Content))
            {
                case ContentDialogResult.Primary:
                    IsFirstInstall = true;
                    try
                    {
                        File.Delete(AppConfigFile);
                        File.Delete(AppNotifIgnoreFile);
                        Directory.Delete(AppGameConfigMetadataFolder, true);
                    }
                    catch
                    {
                        // Pipe error
                    }
                    MainFrameChanger.ChangeWindowFrame(typeof(OOBEStartUpMenu));
                    break;
                // ReSharper disable once RedundantEmptySwitchSection
                default:
                    break;
            }
        }

        private async void ClearMetadataFolder(object sender, RoutedEventArgs e)
        {
            switch (await Dialog_ClearMetadata(Content))
            {
                case ContentDialogResult.Primary:
                    try
                    {
                        var collapsePath = Process.GetCurrentProcess().MainModule?.FileName;
                        if (collapsePath == null || AppGameConfigMetadataFolder == null) return;
                        Directory.Delete(AppGameConfigMetadataFolder, true);
                        Process.Start(collapsePath);
                        (m_window as MainWindow)?.CloseApp();
                    }
                    catch (Exception ex)
                    {
                        string msg = $"An error occurred while attempting to clear metadata folder. Exception stacktrace below:\r\n{ex}";
                        ErrorSender.SendException(ex);
                        LogWriteLine(msg, LogType.Error, true);
                    }
                    break;
                // ReSharper disable once RedundantEmptySwitchSection
                default:
                    break;
            }
        }

        private void OpenAppDataFolder(object sender, RoutedEventArgs e)
        {
            new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    FileName = "explorer.exe",
                    Arguments = AppGameFolder
                }
            }.Start();
        }

        private async void ClearImgFolder(object sender, RoutedEventArgs e)
        {
            try
            {
                (sender as Button).IsEnabled = false;
                if (Directory.Exists(AppGameImgFolder))
                    Directory.Delete(AppGameImgFolder, true);

                Directory.CreateDirectory(AppGameImgFolder);
            }
            catch (Exception ex)
            {
                string msg = $"An error occurred while attempting to clear image folder. Exception stacktrace below:\r\n{ex}";
                ErrorSender.SendException(ex);
                LogWriteLine(msg, LogType.Error, true);
            }

            await Task.Delay(1000);
            (sender as Button).IsEnabled = true;
        }

        private async void ClearLogsFolder(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(AppGameLogsFolder))
                {
                    _log.Dispose();
                    Directory.Delete(AppGameLogsFolder, true);
                    _log.SetFolderPathAndInitialize(AppGameLogsFolder, Encoding.UTF8);
                }

                Directory.CreateDirectory(AppGameLogsFolder);
                (sender as Button).IsEnabled = false;
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex);
            }

            await Task.Delay(1000);
            (sender as Button).IsEnabled = true;
        }

        private void ForceUpdate(object sender, RoutedEventArgs e)
        {
            string ChannelName = IsPreview ? "Preview" : "Stable";
            LaunchUpdater(ChannelName);
        }

        private async void ChangeRelease(object sender, RoutedEventArgs e)
        {
            string ChannelName = IsPreview ? "Stable" : "Preview";
            switch (await Dialog_ChangeReleaseChannel(ChannelName, this))
            {
                case ContentDialogResult.Primary:
                    // Delete Metadata upon switching release
                    Directory.Delete(AppGameConfigMetadataFolder, true);
                    LaunchUpdater(ChannelName);
                    break;
            }
        }

        private void LaunchUpdater(string ChannelName)
        {
            string ExecutableLocation = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            string UpdateArgument = $"elevateupdate --input \"{ExecutableLocation.Replace('\\', '/')}\" --channel {ChannelName}";
            Console.WriteLine(UpdateArgument);
            try
            {
                new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        FileName = Path.Combine(ExecutableLocation, "CollapseLauncher.exe"),
                        Arguments = UpdateArgument,
                        Verb = "runas"
                    }
                }.Start();
                (m_window as MainWindow)?.CloseApp();
            }
            catch
            {
                // Pipe error
            }
        }

        private async void CheckUpdate(object sender, RoutedEventArgs e)
        {

            if (LauncherUpdateWatcher.isMetered)
            {
                switch (await Dialog_MeteredConnectionWarning(Content))
                {
                    case ContentDialogResult.Primary:
                        UpdateLoadingStatus.Visibility = Visibility.Visible;
                        UpdateAvailableStatus.Visibility = Visibility.Collapsed;
                        UpToDateStatus.Visibility = Visibility.Collapsed;
                        CheckUpdateBtn.IsEnabled = false;
                        ForceInvokeUpdate = true;
                        LauncherUpdateInvoker.UpdateEvent += LauncherUpdateInvoker_UpdateEvent;
                        LauncherUpdateWatcher.StartCheckUpdate(true);
                        break;

                    case ContentDialogResult.Secondary:
                        return;
                }
            }
            else
            {
                UpdateLoadingStatus.Visibility = Visibility.Visible;
                UpdateAvailableStatus.Visibility = Visibility.Collapsed;
                UpToDateStatus.Visibility = Visibility.Collapsed;
                CheckUpdateBtn.IsEnabled = false;
                ForceInvokeUpdate = true;
                LauncherUpdateInvoker.UpdateEvent += LauncherUpdateInvoker_UpdateEvent;
                LauncherUpdateWatcher.StartCheckUpdate(true);
            }
        }

        private void LauncherUpdateInvoker_UpdateEvent(object sender, LauncherUpdateProperty e)
        {
            string ChannelName = IsPreview ? " Preview" : " Stable";

            CheckUpdateBtn.IsEnabled = true;
            if (e.IsUpdateAvailable)
            {
                UpdateLoadingStatus.Visibility = Visibility.Collapsed;
                UpdateAvailableStatus.Visibility = Visibility.Visible;
                UpToDateStatus.Visibility = Visibility.Collapsed;
                UpdateAvailableLabel.Text = e.NewVersionName.VersionString + (ChannelName);
                LauncherUpdateInvoker.UpdateEvent -= LauncherUpdateInvoker_UpdateEvent;
            }
            else
            {
                UpdateLoadingStatus.Visibility = Visibility.Collapsed;
                UpdateAvailableStatus.Visibility = Visibility.Collapsed;
                UpToDateStatus.Visibility = Visibility.Visible;
                LauncherUpdateInvoker.UpdateEvent -= LauncherUpdateInvoker_UpdateEvent;
            }
        }

        private void ClickTextLinkFromTag(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (!e.GetCurrentPoint((UIElement)sender).Properties.IsLeftButtonPressed) return;
            new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = (sender as TextBlock).Tag.ToString(),
                    UseShellExecute = true
                }
            }.Start();
        }

        private void ClickButtonLinkFromTag(object sender, RoutedEventArgs e)
        {
            new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = (sender as Button).Tag.ToString(),
                    UseShellExecute = true
                }
            }.Start();
        }

        private async void SelectBackgroundImg(object sender, RoutedEventArgs e)
        {
            string file = await GetFilePicker(ImageLoaderHelper.SupportedImageFormats);
            if (!string.IsNullOrEmpty(file))
            {
                FileStream dummyStream = await ImageLoaderHelper.LoadImage(file, true, true);
                if (dummyStream != null)
                {
                    await dummyStream.DisposeAsync();
                    regionBackgroundProp.imgLocalPath = file;
                    SetAndSaveConfigValue("CustomBGPath", file);
                    BGPathDisplay.Text = file;
                    BackgroundImgChanger.ChangeBackground(file);
                }
            }
        }

        int EggsAttempt = 1;
        private void Egg(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (EggsAttempt++ >= 10)
                HerLegacy.Visibility = Visibility.Visible;
        }

        private TaskSched CreateScheduledTask(string taskName)
        {
            string collapseStartupTarget = MainEntryPoint.FindCollapseStubPath();

            using TaskService ts = new TaskService();

            TaskDefinition taskDefinition = TaskService.Instance.NewTask();
            taskDefinition.RegistrationInfo.Author      = "CollapseLauncher";
            taskDefinition.RegistrationInfo.Description = "Run Collapse Launcher automatically when computer starts";
            taskDefinition.Principal.LogonType          = TaskLogonType.InteractiveToken;
            taskDefinition.Principal.RunLevel           = TaskRunLevel.Highest;
            taskDefinition.Settings.Enabled             = false;
            taskDefinition.Triggers.Add(new LogonTrigger());
            taskDefinition.Actions.Add(new ExecAction(collapseStartupTarget, null, null));

            TaskSched task = TaskService.Instance.RootFolder.RegisterTaskDefinition(taskName, taskDefinition);
            taskDefinition.Dispose();
            return task;
        }

        private void EnableHeaderMouseEvent(object sender, RoutedEventArgs e)
        {
            ((UIElement)VisualTreeHelper.GetParent((DependencyObject)sender)).IsHitTestVisible = true;
        }
        #endregion

        #region Settings UI Backend
        private bool IsBGCustom
        {
            get
            {
                bool isEnabled = GetAppConfigValue("UseCustomBG").ToBool();
                string BGPath = GetAppConfigValue("CustomBGPath").ToString();
                if (!string.IsNullOrEmpty(BGPath))
                    BGPathDisplay.Text = BGPath;
                else
                    BGPathDisplay.Text = Lang._Misc.NotSelected;

                if (isEnabled)
                {
                    AppBGCustomizer.Visibility = Visibility.Visible;
                    AppBGCustomizerNote.Visibility = Visibility.Visible;
                }
                else
                {
                    AppBGCustomizer.Visibility = Visibility.Collapsed;
                    AppBGCustomizerNote.Visibility = Visibility.Collapsed;
                }

                BGSelector.IsEnabled = isEnabled;
                return isEnabled;
            }
            set
            {
                SetAndSaveConfigValue("UseCustomBG", new IniValue(value));
                if (!value)
                {
                    BGPathDisplay.Text = Lang._Misc.NotSelected;
                    regionBackgroundProp.imgLocalPath = GetAppConfigValue("CurrentBackground").ToString();
                    m_mainPage?.ChangeBackgroundImageAsRegionAsync();
                    AppBGCustomizer.Visibility = Visibility.Collapsed;
                    AppBGCustomizerNote.Visibility = Visibility.Collapsed;
                }
                else
                {
                    string BGPath = GetAppConfigValue("CustomBGPath").ToString();
                    if (string.IsNullOrEmpty(BGPath))
                    {
                        regionBackgroundProp.imgLocalPath = AppDefaultBG;
                    }
                    else
                    {
                        if (!File.Exists(BGPath))
                        {
                            regionBackgroundProp.imgLocalPath = AppDefaultBG;
                        }
                        else
                        {
                            regionBackgroundProp.imgLocalPath = BGPath;
                        }
                    }
                    BGPathDisplay.Text = regionBackgroundProp.imgLocalPath;
                    BackgroundImgChanger.ChangeBackground(regionBackgroundProp.imgLocalPath);
                    AppBGCustomizer.Visibility = Visibility.Visible;
                    AppBGCustomizerNote.Visibility = Visibility.Visible;
                }
                BGSelector.IsEnabled = value;
            }
        }

        private bool IsConsoleEnabled
        {
            get
            {
                bool isEnabled = GetAppConfigValue("EnableConsole").ToBool();
                if (isEnabled)
                {
                    ToggleIncludeGameLogs.Visibility = Visibility.Visible;
                }
                else
                {
                    ToggleIncludeGameLogs.Visibility = Visibility.Collapsed;
                }
                return isEnabled;
            }
            set
            {
                _log.Dispose();
                if (value)
                {
                    _log = new LoggerConsole(AppGameLogsFolder, Encoding.UTF8);
                    ToggleIncludeGameLogs.Visibility = Visibility.Visible;
                }
                else
                {
                    _log = new LoggerNull(AppGameLogsFolder, Encoding.UTF8);
                    ToggleIncludeGameLogs.Visibility = Visibility.Collapsed;
                }
                SetAndSaveConfigValue("EnableConsole", value);
            }
        }

        private bool IsMultipleInstanceEnabled
        {
            get => LauncherConfig.IsMultipleInstanceEnabled;
            set => LauncherConfig.IsMultipleInstanceEnabled = value;
        }

#if !DISABLEDISCORD
        private bool IsDiscordRPCEnabled
        {
            get
            {
                bool isEnabled = GetAppConfigValue("EnableDiscordRPC").ToBool();
                ToggleDiscordGameStatus.IsEnabled = IsEnabled;
                if (isEnabled)
                {
                    ToggleDiscordGameStatus.Visibility = Visibility.Visible;
                    ToggleDiscordIdleStatus.Visibility = Visibility.Visible;
                }
                else
                {
                    ToggleDiscordGameStatus.Visibility = Visibility.Collapsed;
                    ToggleDiscordIdleStatus.Visibility = Visibility.Collapsed;
                }
                return isEnabled;
            }
            set
            {
                if (value)
                {
                    AppDiscordPresence.SetupPresence();
                    ToggleDiscordGameStatus.Visibility = Visibility.Visible;
                    ToggleDiscordIdleStatus.Visibility = Visibility.Visible;
                }
                else
                {
                    AppDiscordPresence.DisablePresence();
                    ToggleDiscordGameStatus.Visibility = Visibility.Collapsed;
                    ToggleDiscordIdleStatus.Visibility = Visibility.Collapsed;
                }
                SetAndSaveConfigValue("EnableDiscordRPC", value);
                ToggleDiscordGameStatus.IsEnabled = value;
            }
        }

        private bool IsDiscordGameStatusEnabled
        {
            get => GetAppConfigValue("EnableDiscordGameStatus").ToBool();
            set
            {
                SetAndSaveConfigValue("EnableDiscordGameStatus", value);
                AppDiscordPresence.SetupPresence();
            }
        }

        private bool IsDiscordIdleStatusEnabled
        {
            get => AppDiscordPresence.IdleEnabled;
            set => AppDiscordPresence.IdleEnabled = value;
        }
#else
        private bool IsDiscordRPCEnabled
        {
            get => false;
            set => _ = value;
        }

        private bool IsDiscordGameStatusEnabled
        {
            get => false;
            set => _ = value;
        }

        private bool IsDiscordIdleStatusEnabled
        {
            get => false;
            set => _ = value;
        }
#endif
        private bool IsAcrylicEffectEnabled
        {
            get => GetAppConfigValue("EnableAcrylicEffect").ToBool();
            set
            {
                SetAndSaveConfigValue("EnableAcrylicEffect", value);
                App.ToggleBlurBackdrop(value);
            }
        }

        private bool IsWaifu2XEnabled
        {
            get => ImageLoaderHelper.IsWaifu2XEnabled;
            set
            {
                ImageLoaderHelper.IsWaifu2XEnabled = value;
                if (ImageLoaderHelper.Waifu2XStatus < Waifu2XStatus.Error)
                    BackgroundImgChanger.ChangeBackground(regionBackgroundProp.imgLocalPath, IsCustomBG);
                else
                    Bindings.Update();
            }
        }

        private string Waifu2XToolTip
        {
            get
            {
                var tooltip = Lang._SettingsPage.Waifu2X_Help;
                switch (ImageLoaderHelper.Waifu2XStatus)
                {
                    case Waifu2XStatus.CpuMode:
                        tooltip += "\n\n" + Lang._SettingsPage.Waifu2X_Warning_CpuMode;
                        break;
                    case Waifu2XStatus.D3DMappingLayers:
                        tooltip += "\n\n" + Lang._SettingsPage.Waifu2X_Warning_CpuMode +
                                   "\n\n" + Lang._SettingsPage.Waifu2X_Warning_D3DMappingLayers;
                        break;
                    case Waifu2XStatus.NotAvailable:
                        tooltip += "\n\n" + Lang._SettingsPage.Waifu2X_Error_Loader;
                        break;
                    case Waifu2XStatus.TestNotPassed:
                        tooltip += "\n\n" + Lang._SettingsPage.Waifu2X_Error_Output;
                        break;
                }
                
                return tooltip;
            }
        }

        private string Waifu2XToolTipIcon
        {
            get
            {
                switch (ImageLoaderHelper.Waifu2XStatus)
                {
                    case <= Waifu2XStatus.Ok:
                        return "\uf05a";
                    case < Waifu2XStatus.Error:
                        return "\uf071";
                    case >= Waifu2XStatus.Error:
                        return "\uf06a";
                }
            }
        }

        private bool IsWaifu2XUsable => ImageLoaderHelper.IsWaifu2XUsable;

        private int CurrentThemeSelection
        {
            get
            {
                if (IsAppThemeNeedRestart)
                    AppThemeSelectionWarning.Visibility = Visibility.Visible;

                string AppTheme = GetAppConfigValue("ThemeMode").ToString();
                bool IsParseSuccess = Enum.TryParse(typeof(AppThemeMode), AppTheme, out object ThemeIndex);
                return IsParseSuccess ? (int)ThemeIndex : -1;
            }
            set
            {
                if (value < 0) return;
                SetAndSaveConfigValue("ThemeMode", Enum.GetName(typeof(AppThemeMode), value));
                AppThemeSelectionWarning.Visibility = Visibility.Visible;
                IsAppThemeNeedRestart = true;
            }
        }

        private int CurrentAppThreadDownloadValue
        {
            get => GetAppConfigValue("DownloadThread").ToInt();
            set => SetAndSaveConfigValue("DownloadThread", value);
        }

        private int CurrentAppThreadExtractValue
        {
            get => GetAppConfigValue("ExtractionThread").ToInt();
            set => SetAndSaveConfigValue("ExtractionThread", value);
        }

        private List<string> LanguageList => LanguageNames
            .Select(a => string.Format(Lang._SettingsPage.LanguageEntry, a.Value.LangName, a.Value.LangAuthor))
            .ToList();

        private int LanguageSelectedIndex
        {
            get
            {
                string key = GetAppConfigValue("AppLanguage").ToString().ToLower();
                return LanguageNames.ContainsKey(key) ? LanguageNames[key].LangIndex : 0;
            }
            set
            {
                if (value < 0) return;

                string key = LanguageIDIndex[value];
                SetAndSaveConfigValue("AppLanguage", key);
                LoadLocale(key);
            }
        }

        private void UpdateEveryComboBoxLayout(object sender, object e) => UpdateBindings.Update();

        private void UpdateBindingsEvents(object sender, EventArgs e)
        {
            Bindings.Update();
            UpdateLayout();
            int lastAppBehavSelected = GameLaunchedBehaviorSelector.SelectedIndex;
            GameLaunchedBehaviorSelector.SelectedIndex = -1;
            GameLaunchedBehaviorSelector.SelectedIndex = lastAppBehavSelected;

            string SwitchToVer = IsPreview ? "Stable" : "Preview";
            ChangeReleaseBtnText.Text = string.Format(Lang._SettingsPage.AppChangeReleaseChannel, SwitchToVer);
        }

        private List<string> WindowSizeProfilesKey = WindowSizeProfiles.Keys.ToList();
        private int SelectedWindowSizeProfile
        {
            get
            {
                string val = CurrentWindowSizeName;
                return WindowSizeProfilesKey.IndexOf(val);
            }
            set
            {
                if (value < 0) return;
                CurrentWindowSizeName = WindowSizeProfilesKey[value];
                var delayedDragAreaChange = async () =>
                {
                    await System.Threading.Tasks.Task.Delay(250);
                    ChangeTitleDragArea.Change(DragAreaTemplate.Default);
                };
                delayedDragAreaChange();
                SaveAppConfig();
            }
        }

        private int SelectedCDN
        {
            get => GetAppConfigValue("CurrentCDN").ToInt();
            set
            {
                if (value < 0) return;
                SetAppConfigValue("CurrentCDN", value);
                SaveAppConfig();
            }
        }

        private bool IsIncludeGameLogs
        {
            get => GetAppConfigValue("IncludeGameLogs").ToBool();
            set => SetAndSaveConfigValue("IncludeGameLogs", value);
        }

        private bool IsShowRegionChangeWarning
        {
            get => LauncherConfig.IsShowRegionChangeWarning;
            set
            {
                IsChangeRegionWarningNeedRestart = true;
                ChangeRegionToggleWarning.Visibility = Visibility.Visible;

                LauncherConfig.IsShowRegionChangeWarning = value;
            }
        }

        private bool IsUseDownloadChunksMerging
        {
            get => GetAppConfigValue("UseDownloadChunksMerging").ToBool();
            set => SetAndSaveConfigValue("UseDownloadChunksMerging", value);
        }

        private bool IsLowerCollapsePriorityOnGameLaunch
        {
            get => GetAppConfigValue("LowerCollapsePrioOnGameLaunch").ToBool();
            set => SetAndSaveConfigValue("LowerCollapsePrioOnGameLaunch", value);
        }

        private bool IsAlwaysUseExternalBrowser
        {
            get => GetAppConfigValue("UseExternalBrowser").ToBool();
            set => SetAndSaveConfigValue("UseExternalBrowser", value);
        }

        private int AppGameLaunchedBehaviorIndex
        {
            get => GetAppConfigValue("GameLaunchedBehavior").ToString() switch
                   {
                       "Minimize" => 0,
                       "ToTray"   => 1,
                       "Nothing"  => 2,
                       _ => 0
                   };
            set
            {
                if (value < 0) return;
                switch (value)
                {
                    case 0:
                        SetAndSaveConfigValue("GameLaunchedBehavior", "Minimize");
                        break;
                    case 1:
                        SetAndSaveConfigValue("GameLaunchedBehavior", "ToTray");
                        break;
                    case 2:
                        SetAndSaveConfigValue("GameLaunchedBehavior", "Nothing");
                        break;
                    default:
                        LogWriteLine("Invalid GameLaunchedBehavior selection! Reverting to default 'Minimize'", LogType.Error, true);
                        SetAndSaveConfigValue("GameLaunchedBehavior", "Minimize");
                        break;
                }
            }
        }

        private bool IsMinimizeToTaskbar
        {
            get => GetAppConfigValue("MinimizeToTray").ToBool();
            set => SetAndSaveConfigValue("MinimizeToTray", value);
        }

        private bool IsLaunchOnStartup
        {
            get
            {
                using TaskService ts = new TaskService();

                TaskSched task = ts.GetTask(_collapseStartupTaskName);
                if (task == null) task = CreateScheduledTask(_collapseStartupTaskName);

                bool value = task.Definition.Settings.Enabled;
                task.Dispose();

                if (value) StartupToTrayToggle.Visibility = Visibility.Visible;
                else StartupToTrayToggle.Visibility       = Visibility.Collapsed;

                return value;
            }
            set
            {
                using TaskService ts = new TaskService();

                TaskSched task = ts.GetTask(_collapseStartupTaskName);
                task.Definition.Settings.Enabled = value;
                task.RegisterChanges();
                task.Dispose();

                if (value) StartupToTrayToggle.Visibility = Visibility.Visible;
                else StartupToTrayToggle.Visibility       = Visibility.Collapsed;
            }
        }

        private bool IsStartupToTray
        {
            get
            {
                using TaskService ts = new TaskService();

                TaskSched task = ts.GetTask(_collapseStartupTaskName);
                if (task == null) task = CreateScheduledTask(_collapseStartupTaskName);

                bool? value = false;
                if (task.Definition.Actions[0] is ExecAction execAction)
                    value = execAction.Arguments?.Trim().Contains("tray", StringComparison.CurrentCultureIgnoreCase);

                task.Dispose();
                return value ?? false;
            }
            set
            {
                string collapseStartupTarget = MainEntryPoint.FindCollapseStubPath();
                using TaskService ts = new TaskService();

                TaskSched task = ts.GetTask(_collapseStartupTaskName);
                task.Definition.Actions.Clear();
                task.Definition.Actions.Add(new ExecAction(collapseStartupTarget, value ? "tray" : null, null));
                task.RegisterChanges();
                task.Dispose();
            }
        }
        #endregion

        #region Keyboard Shortcuts
        private async void ShowKbScList_Click(Object sender, RoutedEventArgs e) => await Dialogs.KeyboardShortcuts.Dialog_ShowKbShortcuts(this);

        private async void ResetKeylist_Click(object sender, RoutedEventArgs e)
        {
            if (await Dialogs.SimpleDialogs.Dialog_ResetKeyboardShortcuts(sender as UIElement) == ContentDialogResult.Primary)
            {
                Dialogs.KeyboardShortcuts.ResetKeyboardShortcuts();
                KeyboardShortcutsEvent(null, AreShortcutsEnabled ? 1 : 2);
            }
        }

        public static event EventHandler<int> KeyboardShortcutsEvent;
        private bool AreShortcutsEnabled
        {
            get
            {
                bool value = GetAppConfigValue("EnableShortcuts").ToBool();
                if (value)
                {
                    KbScBtns.Visibility = Visibility.Visible;
                }
                return value;
            }
            set
            {
                if (value)
                {
                    KbScBtns.Visibility = Visibility.Visible;
                }
                else
                {
                    KbScBtns.Visibility = Visibility.Collapsed;
                }
                SetAndSaveConfigValue("EnableShortcuts", value);
                KeyboardShortcutsEvent(this, value ? 0 : 2);
            }
        }
        #endregion
    }
}
