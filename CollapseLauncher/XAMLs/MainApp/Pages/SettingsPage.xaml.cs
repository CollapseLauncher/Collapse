#if !DISABLEDISCORD
    using CollapseLauncher.DiscordPresence;
#endif
    using CollapseLauncher.Dialogs;
    using CollapseLauncher.Extension;
    using CollapseLauncher.Helper;
    using CollapseLauncher.Helper.Animation;
    using CollapseLauncher.Helper.Background;
    using CollapseLauncher.Helper.Database;
    using CollapseLauncher.Helper.Image;
    using CollapseLauncher.Helper.Metadata;
    using CollapseLauncher.Helper.Update;
    using CollapseLauncher.Pages.OOBE;
    using CollapseLauncher.Statics;
    using CommunityToolkit.WinUI;
    using Hi3Helper;
    using Hi3Helper.SentryHelper;
    using Hi3Helper.Shared.ClassStruct;
    using Hi3Helper.Shared.Region;
    using Microsoft.UI.Input;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
    using Microsoft.UI.Xaml.Data;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Media;
    using Microsoft.UI.Xaml.Media.Animation;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using static CollapseLauncher.Dialogs.SimpleDialogs;
    using static CollapseLauncher.Helper.Image.Waifu2X;
    using static CollapseLauncher.InnerLauncherConfig;
    using static CollapseLauncher.WindowSize.WindowSize;
    using static CollapseLauncher.FileDialogCOM.FileDialogNative;
    using static Hi3Helper.Locale;
    using static Hi3Helper.Logger;
    using static Hi3Helper.Shared.Region.LauncherConfig;
    using CollapseUIExt = CollapseLauncher.Extension.UIElementExtensions;
    using MediaType = CollapseLauncher.Helper.Background.BackgroundMediaUtility.MediaType;
    using Task = System.Threading.Tasks.Task;

// ReSharper disable CheckNamespace
// ReSharper disable PossibleNullReferenceException
// ReSharper disable AssignNullToNotNullAttribute

namespace CollapseLauncher.Pages
{
    // ReSharper disable once RedundantExtendsListEntry
    public sealed partial class SettingsPage : Page
    {
        #region Properties

        private const string RepoUrl                  = "https://github.com/CollapseLauncher/Collapse/commit/";
        
        private readonly bool _initIsInstantRegionChange;
        private readonly bool _initIsShowRegionChangeWarning;
        #endregion

        #region Settings Page Handler
        public SettingsPage()
        {
            _initIsInstantRegionChange     = LauncherConfig.IsInstantRegionChange;
            _initIsShowRegionChangeWarning = LauncherConfig.IsShowRegionChangeWarning;
                
            InitializeComponent();
            this.EnableImplicitAnimation(true);
            this.SetAllControlsCursorRecursive(InputSystemCursor.Create(InputSystemCursorShape.Hand));
            AboutApp.FindAndSetTextBlockWrapping(TextWrapping.Wrap, HorizontalAlignment.Center, TextAlignment.Center, true);

            LoadAppConfig();
            DataContext = this;

            string Version = $" {LauncherUpdateHelper.LauncherCurrentVersionString}";
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
            GitVersionIndicatorHyperlink.NavigateUri = 
                new Uri(new StringBuilder()
                    .Append(RepoUrl)
                    .Append(ThisAssembly.Git.Sha).ToString());

            if (IsAppLangNeedRestart)
                AppLangSelectionWarning.Visibility = Visibility.Visible;

            if (IsChangeRegionWarningNeedRestart)
                ChangeRegionToggleWarning.Visibility = Visibility.Visible;

            if (IsInstantRegionNeedRestart)
                InstantRegionToggleWarning.Visibility = Visibility.Visible;

            string SwitchToVer = IsPreview ? "Stable" : "Preview";
            ChangeReleaseBtnText.Text = string.Format(Lang._SettingsPage.AppChangeReleaseChannel, SwitchToVer);
#if DISABLEDISCORD
            ToggleDiscordRPC.Visibility = Visibility.Collapsed;
#endif

            AppBGCustomizerNote.Text = string.Format(Lang._SettingsPage.AppBG_Note,
                string.Join("; ", BackgroundMediaUtility.SupportedImageExt),
                string.Join("; ", BackgroundMediaUtility.SupportedMediaPlayerExt)
            );
            
            UpdateBindingsInvoker.UpdateEvents += UpdateBindingsEvents;
        }

        private string GitVersionIndicator_Builder()
        {
            var branchName  = ThisAssembly.Git.Branch;
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
        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            FallbackCDNUtil.InitializeHttpClient();
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
                    catch (Exception ex)
                    {
                        await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
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
                        if (collapsePath == null) return;
                        Directory.Delete(LauncherMetadataHelper.LauncherMetadataFolder, true);
                        Process.Start(collapsePath);
                        (WindowUtility.CurrentWindow as MainWindow)?.CloseApp();
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
            new Process
            {
                StartInfo = new ProcessStartInfo
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
                _log?.ResetLogFiles(AppGameLogsFolder, Encoding.UTF8);
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
                (WindowUtility.CurrentWindow as MainWindow)?.CloseApp();
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                // Pipe error
            }
        }

        private async void CheckUpdate(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateLoadingStatus.Visibility = Visibility.Visible;
                UpdateAvailableStatus.Visibility = Visibility.Collapsed;
                UpToDateStatus.Visibility = Visibility.Collapsed;
                CheckUpdateBtn.IsEnabled = false;
                ForceInvokeUpdate = true;

                LauncherUpdateInvoker.UpdateEvent += LauncherUpdateInvoker_UpdateEvent;
                bool isUpdateAvailable = await LauncherUpdateHelper.IsUpdateAvailable(true);
                LauncherUpdateWatcher.GetStatus(new LauncherUpdateProperty
                {
                    IsUpdateAvailable = isUpdateAvailable, 
                    // ReSharper disable PossibleInvalidOperationException
                    NewVersionName = (GameVersion)(isUpdateAvailable
                        ? LauncherUpdateHelper.AppUpdateVersionProp.Version.Value
                        : LauncherUpdateHelper.LauncherCurrentVersion)
                    // ReSharper restore PossibleInvalidOperationException
                });
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex);
                UpdateLoadingStatus.Visibility = Visibility.Collapsed;
                UpdateAvailableStatus.Visibility = Visibility.Collapsed;
                UpToDateStatus.Visibility = Visibility.Collapsed;
                CheckUpdateBtn.IsEnabled = true;
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
            }
            else
            {
                UpdateLoadingStatus.Visibility = Visibility.Collapsed;
                UpdateAvailableStatus.Visibility = Visibility.Collapsed;
                UpToDateStatus.Visibility = Visibility.Visible;
            }
            LauncherUpdateInvoker.UpdateEvent -= LauncherUpdateInvoker_UpdateEvent;
        }

        private void OpenChangelog(object sender, RoutedEventArgs e)
        {
            #nullable enable
            var uri =
                $"https://github.com/CollapseLauncher/CollapseLauncher-ReleaseRepo/blob/main/changelog_{(IsPreview ? "preview" : "stable")}.md";

            var mdParam = new MarkdownFramePage.MarkdownFramePageParams
            {
                MarkdownUriCdn = $"changelog_{(IsPreview ? "preview" : "stable")}.md",
                WebUri         = uri,
                Title          = Lang._SettingsPage.Update_ChangelogTitle
            };
            
            if (WindowUtility.CurrentWindow is MainWindow mainWindow)
            {
                mainWindow.overlayFrame.BackStack?.Clear();
                mainWindow.overlayFrame.Navigate(typeof(NullPage));
                mainWindow.overlayFrame.Navigate(typeof(MarkdownFramePage), mdParam,
                                                 new DrillInNavigationTransitionInfo());
            }

            MarkdownFramePage.Current!.MarkdownCloseBtn.Click += ExitFromOverlay;
            return;
            
            static void ExitFromOverlay(object? sender, RoutedEventArgs args)
            {
                if (WindowUtility.CurrentWindow is not MainWindow mainWindow)
                    return;

                mainWindow.overlayFrame.GoBack();
                mainWindow.overlayFrame.BackStack?.Clear();
            }
            #nullable restore
        }

        private void ClickTextLinkFromTag(object sender, PointerRoutedEventArgs e)
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
                var currentMediaType = BackgroundMediaUtility.GetMediaType(file);

                if (currentMediaType == MediaType.StillImage)
                {
                    FileStream croppedImage = await ImageLoaderHelper.LoadImage(file, true, true);

                    if (croppedImage == null) return;
                    BackgroundMediaUtility.SetAlternativeFileStream(croppedImage);
                }

                LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal = file;
                SetAndSaveConfigValue("CustomBGPath", file);
                BGPathDisplay.Text = file;
                
                GamePresetProperty currentGameProperty = GamePropertyVault.GetCurrentGameProperty();
                bool isUseRegionCustomBG = currentGameProperty?._GameSettings?.SettingsCollapseMisc?.UseCustomRegionBG ?? false;
                if (!isUseRegionCustomBG)
                {
                    BackgroundImgChanger.ChangeBackground(LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal, null, true, true, true);
                }
                else if (!string.IsNullOrEmpty(currentGameProperty._GameSettings?.SettingsCollapseMisc?.CustomRegionBGPath))
                {
                    _ = BackgroundMediaUtility.GetMediaType(currentGameProperty._GameSettings?.SettingsCollapseMisc?.CustomRegionBGPath);
                }
            }
        }

        int EggsAttempt = 1;
        private void Egg(object sender, PointerRoutedEventArgs e)
        {
            if (EggsAttempt++ >= 10)
                HerLegacy.Visibility = Visibility.Visible;
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
                LogWriteLine("Read " + isEnabled + " BG Path: " + BGPath + " from config", LogType.Scheme, true);
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
                    AppBGCustomizer.Visibility       = Visibility.Collapsed;
                    AppBGCustomizerNote.Visibility   = Visibility.Collapsed;
                }

                BGSelector.IsEnabled = isEnabled;
                return isEnabled;
            }
            set
            {
                SetAndSaveConfigValue("UseCustomBG", value);
                GamePresetProperty currentGameProperty = GamePropertyVault.GetCurrentGameProperty();
                bool isUseRegionCustomBG = currentGameProperty?._GameSettings?.SettingsCollapseMisc?.UseCustomRegionBG ?? false;
                if (!value)
                {
                    LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal = GetAppConfigValue("CurrentBackground").ToString();
                    m_mainPage?.ChangeBackgroundImageAsRegionAsync();

                    ToggleCustomBgButtons();
                }
                else if (isUseRegionCustomBG)
                {
                    string currentRegionCustomBg = currentGameProperty._GameSettings.SettingsCollapseMisc.CustomRegionBGPath;
                    LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal = currentRegionCustomBg;
                    m_mainPage?.ChangeBackgroundImageAsRegionAsync();

                    ToggleCustomBgButtons();
                }
                else
                {
                    var BGPath = GetAppConfigValue("CustomBGPath").ToString();
                    if (string.IsNullOrEmpty(BGPath))
                    {
                        LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal = AppDefaultBG;
                    }
                    else
                    {
                        if (!File.Exists(BGPath))
                        {
                            LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal = AppDefaultBG;
                        }
                        else
                        {
                            LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal = BGPath;
                        }
                    }
                    BGPathDisplay.Text = LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal;
                    BackgroundImgChanger.ChangeBackground(LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal, null, true, true);
                }

                if (value)
                {
                    BGPathDisplay.Text = GetAppConfigValue("CustomBGPath").ToString();
                    AppBGCustomizer.Visibility = Visibility.Visible;
                    AppBGCustomizerNote.Visibility = Visibility.Visible;
                }

                BGSelector.IsEnabled = value;

                return;
                void ToggleCustomBgButtons()
                {
                    AppBGCustomizer.Visibility = Visibility.Collapsed;
                    AppBGCustomizerNote.Visibility = Visibility.Collapsed;
                }
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

        private bool IsSendRemoteCrashData
        {
            get => SentryHelper.IsEnabled;
            set => SentryHelper.IsEnabled = value;
        }

        private bool IsIntroEnabled
        {
            get => LauncherConfig.IsIntroEnabled;
            set => LauncherConfig.IsIntroEnabled = value;
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

        private bool IsVideoBackgroundAudioMute
        {
            get => !GetAppConfigValue("BackgroundAudioIsMute").ToBool();
            set
            {
                if (!value)
                    MainPage.CurrentBackgroundHandler?.Mute();
                else
                    MainPage.CurrentBackgroundHandler?.Unmute();
            }
        }

        private double VideoBackgroundAudioVolume
        {
            get
            {
                double value = GetAppConfigValue("BackgroundAudioVolume").ToDouble();
                if (value < 0)
                    MainPage.CurrentBackgroundHandler?.SetVolume(0d);
                if (value > 1)
                    MainPage.CurrentBackgroundHandler?.SetVolume(1d);

                return value * 100d;
            }
            set
            {
                if (value < 0) return;
                double downValue = value / 100d;
                MainPage.CurrentBackgroundHandler?.SetVolume(downValue);
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
            get => LauncherConfig.EnableAcrylicEffect;
            set
            {
                LauncherConfig.EnableAcrylicEffect = value;

                if (BackgroundMediaUtility.CurrentAppliedMediaType == MediaType.Media
                 && value && !IsUseVideoBGDynamicColorUpdate)
                    return;

                App.ToggleBlurBackdrop(value);
            }
        }

        private bool IsUseVideoBGDynamicColorUpdate
        {
            get => LauncherConfig.IsUseVideoBGDynamicColorUpdate;
            set
            {
                LauncherConfig.IsUseVideoBGDynamicColorUpdate = value;
                if (MediaType.StillImage == BackgroundMediaUtility.CurrentAppliedMediaType)
                    return;

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
                    BackgroundImgChanger.ChangeBackground(LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal, null, IsCustomBG);
                else
                    Bindings.Update();
            }
        }

        private string Waifu2XToolTip
        {
            get
            {
                var tooltip = $"{Lang._SettingsPage.Waifu2X_Help}\r\n{Lang._SettingsPage.Waifu2X_Help2}";
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

        private int lastLanguageSelectedIndex = -1;

        private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox combobox)
            {
                if (lastLanguageSelectedIndex == combobox.SelectedIndex
                || combobox.SelectedIndex == -1)
                    return;

                LanguageSelectedIndex = combobox.SelectedIndex;
                lastLanguageSelectedIndex = LanguageSelectedIndex;
                Bindings.Update();

                foreach (ComboBox comboBoxOthers in this.FindDescendants().OfType<ComboBox>())
                {
                    if (comboBoxOthers == combobox)
                        continue;

                    int lastSelected = comboBoxOthers.SelectedIndex;
                    comboBoxOthers.SelectedIndex = -1;
                    comboBoxOthers.SelectedIndex = lastSelected;
                }

                UpdateBindings.Update();
            }
        }

        private void UpdateBindingsEvents(object sender, EventArgs e)
        {
            Bindings.Update();
            UpdateLayout();

            string SwitchToVer = IsPreview ? "Stable" : "Preview";
            ChangeReleaseBtnText.Text = string.Format(Lang._SettingsPage.AppChangeReleaseChannel, SwitchToVer);
            
            AppBGCustomizerNote.Text = string.Format(Lang._SettingsPage.AppBG_Note,
                string.Join("; ", BackgroundMediaUtility.SupportedImageExt),
                string.Join("; ", BackgroundMediaUtility.SupportedMediaPlayerExt)
            );

            string url = GetAppConfigValue("HttpProxyUrl").ToString();
            ValidateHttpProxyUrl(url);
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
                CurrentWindowSizeName     = WindowSizeProfilesKey[value];
                BGPathDisplayViewer.Width = CurrentWindowSize.SettingsPanelWidth;
                var delayedDragAreaChange = async () =>
                {
                    await Task.Delay(250);
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
            get
            { 
                var value = LauncherConfig.IsShowRegionChangeWarning;

                PanelChangeRegionInstant.Visibility = !value ? Visibility.Visible : Visibility.Collapsed;
                return value;
            }
            set
            {
                LauncherConfig.IsShowRegionChangeWarning = value;
                IsChangeRegionWarningNeedRestart         = true;
                
                var valueConfig = _initIsShowRegionChangeWarning;
                ChangeRegionToggleWarning.Visibility = value != valueConfig ? Visibility.Visible : Visibility.Collapsed;
                PanelChangeRegionInstant.Visibility  = !value ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        
        private bool IsInstantRegionChange
        {
            get => LauncherConfig.IsInstantRegionChange;
            set
            {
                IsInstantRegionNeedRestart = true;
                var valueConfig = _initIsInstantRegionChange;
                InstantRegionToggleWarning.Visibility = value != valueConfig ? Visibility.Visible : Visibility.Collapsed;
                
                LauncherConfig.IsInstantRegionChange = value;
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
                bool value = TaskSchedulerHelper.IsEnabled();

                if (value) StartupToTrayToggle.Visibility = Visibility.Visible;
                else StartupToTrayToggle.Visibility       = Visibility.Collapsed;

                return value;
            }
            set
            {
                TaskSchedulerHelper.ToggleEnabled(value);

                if (value) StartupToTrayToggle.Visibility = Visibility.Visible;
                else StartupToTrayToggle.Visibility       = Visibility.Collapsed;
            }
        }

        private bool IsStartupToTray
        {
            get => TaskSchedulerHelper.IsOnTrayEnabled();
            set => TaskSchedulerHelper.ToggleTrayEnabled(value);
        }

        private bool IsEnableSophon
        {
            get => GetAppConfigValue("IsEnableSophon").ToBool();
            set => SetAndSaveConfigValue("IsEnableSophon", value);
        }

        private int SophonDownThread
        {
            get => GetAppConfigValue("SophonCpuThread").ToInt();
            set => SetAndSaveConfigValue("SophonCpuThread", value);
        }

        private int SophonHttpConn
        {
            get => GetAppConfigValue("SophonHttpConnInt").ToInt();
            set => SetAndSaveConfigValue("SophonHttpConnInt", value);
        }

        private bool IsSophonPreloadPerfMode
        {
            get => GetAppConfigValue("SophonPreloadApplyPerfMode").ToBool();
            set => SetAndSaveConfigValue("SophonPreloadApplyPerfMode", value);
        }

#nullable enable
        private bool IsUseProxy
        {
            get => GetAppConfigValue("IsUseProxy").ToBool();
            set => SetAndSaveConfigValue("IsUseProxy", value);
        }
        private bool IsAllowHttpRedirections
        {
            get => GetAppConfigValue("IsAllowHttpRedirections").ToBool();
            set => SetAndSaveConfigValue("IsAllowHttpRedirections", value);
        }
        private bool IsAllowHttpCookies
        {
            get => GetAppConfigValue("IsAllowHttpCookies").ToBool();
            set => SetAndSaveConfigValue("IsAllowHttpCookies", value);
        }

        private bool IsAllowUntrustedCert
        {
            get => GetAppConfigValue("IsAllowUntrustedCert").ToBool();
            set => SetAndSaveConfigValue("IsAllowUntrustedCert", value);
        }

        private double HttpClientTimeout
        {
            get => GetAppConfigValue("HttpClientTimeout").ToDouble();
            set
            {
                if (double.IsNaN(value))
                {
                    value = HttpClientTimeoutNumberBox.Minimum;
                    HttpClientTimeoutNumberBox.Value = value;
                }
                SetAndSaveConfigValue("HttpClientTimeout", value);
            }
        }

        private string? HttpProxyUrl
        {
            get
            {
                string url = GetAppConfigValue("HttpProxyUrl").ToString();
                ValidateHttpProxyUrl(url);
                return url;
            }
            set
            {
                ValidateHttpProxyUrl(value);
                SetAndSaveConfigValue("HttpProxyUrl", value);
            }
        }

        private void ValidateHttpProxyUrl(string? url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? urlResult)
                || string.IsNullOrEmpty(urlResult.Host)
                || urlResult.Port > 65535)
                {
                    Brush brush = CollapseUIExt.GetApplicationResource<Brush>("SystemFillColorCriticalBackgroundBrush");
                    Brush fgbrush = CollapseUIExt.GetApplicationResource<Brush>("SystemFillColorCriticalBrush");
                    ProxyHostnameTextbox.SetForeground(fgbrush);
                    ProxyHostnameTextbox.SetBackground(brush);
                    ProxyHostnameTextboxError.Visibility = Visibility.Visible;
                    ProxyHostnameTextboxError.Text = Lang._SettingsPage.NetworkSettings_ProxyWarn_UrlInvalid;
                    return;
                }

                if (!(urlResult.Scheme.Equals("http")
                || urlResult.Scheme.Equals("https")
                || urlResult.Scheme.Equals("socks4")
                || urlResult.Scheme.Equals("socks4a")
                || urlResult.Scheme.Equals("socks5")))
                {
                    Brush brush = CollapseUIExt.GetApplicationResource<Brush>("SystemFillColorCriticalBackgroundBrush");
                    Brush fgbrush = CollapseUIExt.GetApplicationResource<Brush>("SystemFillColorCriticalBrush");
                    ProxyHostnameTextbox.SetForeground(fgbrush);
                    ProxyHostnameTextbox.SetBackground(brush);
                    ProxyHostnameTextboxError.Visibility = Visibility.Visible;
                    ProxyHostnameTextboxError.Text = Lang._SettingsPage.NetworkSettings_ProxyWarn_NotSupported;
                    return;
                }
            }

            ProxyHostnameTextboxError.Visibility = Visibility.Collapsed;
            ProxyHostnameTextbox.SetForeground(CollapseUIExt.GetApplicationResource<SolidColorBrush>("TextControlForeground"));
            ProxyHostnameTextbox.SetBackground(CollapseUIExt.GetApplicationResource<Brush>("TextControlBackground"));
        }

        private async void ProxyConnectivityTestButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.IsEnabled = false;
                ProxyConnectivityTestTextChecking.Visibility = Visibility.Visible;
                ProxyConnectivityTestTextSuccess.Visibility = Visibility.Collapsed;
                ProxyConnectivityTestTextFailed.Visibility = Visibility.Collapsed;

                ProgressRing? progressRing = ProxyConnectivityTestTextChecking.Children
                    .OfType<ProgressRing>()
                    .FirstOrDefault();

                FallbackCDNUtil.InitializeHttpClient();

                try
                {
                    if (progressRing != null)
                        progressRing.IsIndeterminate = true;
                    UrlStatus urlStatus = await FallbackCDNUtil.GetURLStatusCode("https://gitlab.com/bagusnl/CollapseLauncher-ReleaseRepo/-/raw/main/LICENSE", default);
                    if (!urlStatus.IsSuccessStatusCode)
                    {
                        InvokeError();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    await SentryHelper.ExceptionHandlerAsync(ex);
                    InvokeError();
                    return;
                }

                InvokeSuccess();
            }

            return;

            async void InvokeError()
            {
                ProxyConnectivityTestTextChecking.Visibility = Visibility.Collapsed;
                ProxyConnectivityTestTextSuccess.Visibility = Visibility.Collapsed;
                ProxyConnectivityTestTextFailed.Visibility = Visibility.Visible;

                await Task.Delay(2000);
                ProxyConnectivityTestTextFailed.Visibility = Visibility.Collapsed;
                RestoreButtonState();
            }

            async void InvokeSuccess()
            {
                ProxyConnectivityTestTextChecking.Visibility = Visibility.Collapsed;
                ProxyConnectivityTestTextSuccess.Visibility = Visibility.Visible;
                ProxyConnectivityTestTextFailed.Visibility = Visibility.Collapsed;

                await Task.Delay(2000);
                ProxyConnectivityTestTextSuccess.Visibility = Visibility.Collapsed;
                RestoreButtonState();
            }

            void RestoreButtonBinding()
            {
                BindingOperations.SetBinding(button, IsEnabledProperty, new Binding()
                {
                    Source = NetworkSettingsProxyToggle,
                    Path = new PropertyPath("IsOn"),
                    Mode = BindingMode.OneWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                });
            }

            void RestoreButtonState()
            {
                RestoreButtonBinding();
                ProgressRing? progressRing = ProxyConnectivityTestTextChecking.Children
                    .OfType<ProgressRing>()
                    .FirstOrDefault();
                if (progressRing != null)
                    progressRing.IsIndeterminate = false;
            }
        }

        private string? HttpProxyUsername
        {
            get => GetAppConfigValue("HttpProxyUsername").ToString();
            set => SetAndSaveConfigValue("HttpProxyUsername", value);
        }

        private string? HttpProxyPassword
        {
            get
            {
                string encData = GetAppConfigValue("HttpProxyPassword").ToString();
                if (string.IsNullOrEmpty(encData))
                    return null;

                string? rawString = SimpleProtectData.UnprotectString(encData);
                return rawString;
            }
            set
            {
                string? protectedString = SimpleProtectData.ProtectString(value);
                SetAndSaveConfigValue("HttpProxyPassword", protectedString, true);
            }
        }
        
        private bool IsBurstDownloadModeEnabled
        {
            get => LauncherConfig.IsBurstDownloadModeEnabled;
            set => LauncherConfig.IsBurstDownloadModeEnabled = value;
        }

        private bool IsUseDownloadSpeedLimiter
        {
            get
            {
                bool value = LauncherConfig.IsUseDownloadSpeedLimiter;
                NetworkDownloadSpeedLimitGrid.Opacity = value ? 1 : 0.45;
                if (value)
                    NetworkBurstDownloadModeToggle.IsOn = false;
                NetworkBurstDownloadModeToggle.IsEnabled = !value;
                return value;
            }
            set
            {
                NetworkDownloadSpeedLimitGrid.Opacity = value ? 1 : 0.45;
                if (value)
                    NetworkBurstDownloadModeToggle.IsOn = false;
                NetworkBurstDownloadModeToggle.IsEnabled = !value;
                LauncherConfig.IsUseDownloadSpeedLimiter = value;
            }
        }

        private bool IsUsePreallocatedDownloader
        {
            get
            {
                bool value = LauncherConfig.IsUsePreallocatedDownloader;
                NetworkDownloadChunkSizeGrid.Opacity = value ? 1 : 0.45;
                OldDownloadChunksMergingToggle.IsEnabled = !value;

                if (!value)
                {
                    NetworkDownloadSpeedLimitToggle.IsOn = value;
                    NetworkBurstDownloadModeToggle.IsOn = value;
                }
                NetworkDownloadSpeedLimitToggle.IsEnabled = value;
                NetworkBurstDownloadModeToggle.IsEnabled = value;
                return value;
            }
            set
            {
                NetworkDownloadChunkSizeGrid.Opacity = value ? 1 : 0.45;
                OldDownloadChunksMergingToggle.IsEnabled = !value;

                if (!value)
                {
                    NetworkDownloadSpeedLimitToggle.IsOn = value;
                    NetworkBurstDownloadModeToggle.IsOn = value;
                }
                NetworkDownloadSpeedLimitToggle.IsEnabled = value;
                NetworkBurstDownloadModeToggle.IsEnabled = value;
                LauncherConfig.IsUsePreallocatedDownloader = value;
            }
        }

        private bool IsEnforceToUse7zipOnExtract
        {
            get => LauncherConfig.IsEnforceToUse7zipOnExtract;
            set => LauncherConfig.IsEnforceToUse7zipOnExtract = value;
        }

        private double DownloadSpeedLimit
        {
            get
            {
                double val = LauncherConfig.DownloadSpeedLimit;
                double valDividedM = val / (1 << 20);
                return valDividedM;
            }
            set
            {
                long valBfromM = (long)(value * (1 << 20));
                
                LauncherConfig.DownloadSpeedLimit = Math.Max(valBfromM, 0);
            }
        }

        private double DownloadChunkSize
        {
            get
            {
                double val = LauncherConfig.DownloadChunkSize;
                double valDividedM = val / (1 << 20);
                return valDividedM;
            }
            set
            {
                int valBfromM = (int)(value * (1 << 20));

                LauncherConfig.DownloadChunkSize = Math.Max(valBfromM, 0);
            }
        }
#nullable restore

        #region Database

        // Temporary prop store
        private string _dbUrl;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string _dbToken;
        private string _dbUserId;
        
        private bool IsDbEnabled
        {
            get => DbHandler.IsEnabled;
            set
            {
                DbHandler.IsEnabled = value;
                if (value) _ = DbHandler.Init();
            }
        }

        private string DbUrl
        {
            get
            {
                var c = DbHandler.Uri;
                _dbUrl = c;
                return c;
            }  
            set
            {
                // Automatically replace libsql protocol to https
                if (value.Contains("libsql://", StringComparison.InvariantCultureIgnoreCase))
                    value = value.Replace("libsql", "https");
                if (!value.Contains("https://"))
                {
                    DbUriTextBox.Text = DbHandler.Uri;
                    return;
                }

                DbUriTextBox.Text = value;
                _dbUrl            = value;

                ShowDbWarningStatus(Lang._SettingsPage.Database_Warning_PropertyChanged);
            }
        }

        private string DbToken
        {
            get
            {
                var c =  DbHandler.Token;
                _dbToken = c;
                return c;
            }
            set
            {
                _dbToken = value;
                if (string.IsNullOrEmpty(value))
                    return;

                ShowDbWarningStatus(Lang._SettingsPage.Database_Warning_PropertyChanged);
            }
        }

        private string _currentDbGuid;
        private string DbUserId
        {
            get
            {
              _dbUserId = DbHandler.UserId;
              return  DbHandler.UserId;
            } 
            set
            {
                _dbUserId        = value;
                DbHandler.UserId = value;
            }
        }

        private void DbUserIdTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            _currentDbGuid = DbUserIdTextBox.Text;
        }

        private async void DbUserIdTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var t = sender as TextBox;
            if (t == null) return;

            var newGuid = t.Text;
            if (_currentDbGuid == newGuid) return; // Return if no change
            
            if (!string.IsNullOrEmpty(newGuid) && !Guid.TryParse(newGuid, out _))
            {
                ShowDbWarningStatus(Lang._SettingsPage.Database_Error_InvalidGuid);
                return;
            }

            if (await Dialog_DbGenerateUid((UIElement)sender) != ContentDialogResult.Primary) // Send warning dialog
            {
                t.Text = _currentDbGuid; // Rollback text if user doesn't select yes
            }
            else
            {
                _currentDbGuid = t.Text;
                _dbUserId      = t.Text; // Store to temp prop

                ShowDbWarningStatus(Lang._SettingsPage.Database_Warning_PropertyChanged);
            }
        }

        private void ShowDbWarningStatus(string message)
        {
            DatabaseWarningBox.Visibility = Visibility.Visible;

            TextBlock textBlock = DatabaseWarningBox.Children.OfType<TextBlock>().FirstOrDefault();
            if (textBlock == null)
                return;

            textBlock.Text = message;
        }

        [DebuggerHidden]
        private async void ValidateAndSaveDbButton_Click(object sender, RoutedEventArgs e)
        {
            // Disable the validation button while the check is happening
            ButtonBase senderAsButtonBase = sender as ButtonBase;
            senderAsButtonBase.IsEnabled = false;

            // Store current value in local vars
            var curUrl   = DbHandler.Uri;
            var curToken = DbHandler.Token;
            var curGuid  = DbHandler.UserId;

            try
            {
                // Show checking bar status
                ShowChecking();

                // Set the value from prop
                DbHandler.Uri    = _dbUrl;
                DbHandler.Token  = _dbToken;
                DbHandler.UserId = _dbUserId;
                
                var r = Random.Shared.Next(100); // Generate random int for data verification

                await DbHandler.Init(true, true); // Initialize database
                await DbHandler.StoreKeyValue("TestKey", r.ToString(), true); // Store random number in TestKey
                if (Convert.ToInt32(await DbHandler.QueryKey("TestKey", true)) != r) // Query key and check if value is correct
                    throw new InvalidDataException("Data validation failed!"); // Throw if value does not match (then catch), unlikely but maybe for really unstable db server

                // Show success bar status
                ShowSuccess();
                await SpawnDialog(
                                  Lang._Misc.EverythingIsOkay,
                                  Lang._SettingsPage.Database_ConnectionOk,
                                  sender as UIElement,
                                  Lang._Misc.Close,
                                  null,
                                  null,
                                  ContentDialogButton.Close,
                                  CustomControls.ContentDialogTheme.Success
                                 ); // Show success dialog
            }
            catch (Exception ex)
            {
                // Revert value if fail
                DbHandler.Uri    = curUrl;
                DbHandler.Token  = curToken;
                DbHandler.UserId = curGuid;
                
                var newEx = new Exception(Lang._SettingsPage.Database_ConnectFail, ex);

                // Show exception
                ShowFailed(ex);
                ErrorSender.SendException(newEx); // Send error with dialog
            }
            finally
            {
                // Re-enable the validation button
                senderAsButtonBase.IsEnabled = true;
            }

            void ShowChecking()
            {
                // Reset status
                DatabaseConnectivityTestTextSuccess.Visibility = Visibility.Collapsed;
                DatabaseConnectivityTestTextFailed.Visibility = Visibility.Collapsed;
                DatabaseWarningBox.Visibility = Visibility.Collapsed;

                // Show checking bar status
                DatabaseConnectivityTestTextChecking.Visibility = Visibility.Visible;
            }

            async void ShowSuccess()
            {
                // Show success bar status
                DatabaseConnectivityTestTextChecking.Visibility = Visibility.Collapsed;
                DatabaseConnectivityTestTextSuccess.Visibility = Visibility.Visible;

                // Hide success bar status after 2 seconds
                await Task.Delay(TimeSpan.FromSeconds(2));
                DatabaseConnectivityTestTextSuccess.Visibility = Visibility.Collapsed;
            }

            void ShowFailed(Exception ex)
            {
                // Show failed bar status
                DatabaseConnectivityTestTextChecking.Visibility = Visibility.Collapsed;
                DatabaseConnectivityTestTextFailed.Visibility = Visibility.Visible;

                // Find the text box
                TextBox textBox = DatabaseConnectivityTestTextFailed.Children.OfType<TextBox>().FirstOrDefault();
                if (textBox == null)
                    return;

                // Set the exception text
                textBox.Text = ex.ToString();
            }
        }

        private async void GenerateGuidButton_Click(object sender, RoutedEventArgs e)
        {
            if (await Dialog_DbGenerateUid(sender as UIElement) == ContentDialogResult.Primary)
            {
                var g = Guid.CreateVersion7();
                DbUserIdTextBox.Text         = g.ToString();
                _dbUserId                    = g.ToString();

                ShowDbWarningStatus(Lang._SettingsPage.Database_Warning_PropertyChanged);
            }
        }
        #endregion
        #endregion

        #region Keyboard Shortcuts
        private async void ShowKbScList_Click(Object sender, RoutedEventArgs e) => await KeyboardShortcuts.Dialog_ShowKbShortcuts(this);

        private async void ResetKeylist_Click(object sender, RoutedEventArgs e)
        {
            if (await Dialog_ResetKeyboardShortcuts(sender as UIElement) == ContentDialogResult.Primary)
            {
                KeyboardShortcuts.ResetKeyboardShortcuts();
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
