#if !DISABLEDISCORD
using CollapseLauncher.DiscordPresence;
#endif
using CollapseLauncher.CustomControls;
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
using CollapseLauncher.Pages.SettingsContext;
using CollapseLauncher.Plugins;
using CollapseLauncher.Statics;
#if ENABLEUSERFEEDBACK
using CollapseLauncher.Helper.Loading;
using CollapseLauncher.XAMLs.Theme.CustomControls.UserFeedbackDialog;
#endif
using CollapseLauncher.XAMLs.Theme.CustomControls.FullPageOverlay;
using CommunityToolkit.WinUI;
using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using Hi3Helper.Win32.FileDialogCOM;
using Hi3Helper.Win32.ManagedTools;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TurnerSoftware.DinoDNS;
using WinRT;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static CollapseLauncher.Helper.Image.Waifu2X;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.WindowSize.WindowSize;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using CollapseUIExt = CollapseLauncher.Extension.UIElementExtensions;
using MediaType = CollapseLauncher.Helper.Background.BackgroundMediaUtility.MediaType;
// ReSharper disable AsyncVoidMethod
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
// ReSharper disable CheckNamespace
// ReSharper disable PossibleNullReferenceException
// ReSharper disable AssignNullToNotNullAttribute
// ReSharper disable RedundantExtendsListEntry
// ReSharper disable HeuristicUnreachableCode


namespace CollapseLauncher.Pages
{
    public sealed partial class SettingsPage : Page
    {
        #region Properties

        private const string RepoUrl = "https://github.com/CollapseLauncher/Collapse/commit/";
        private readonly string _explorerPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

        private readonly DnsSettingsContext _dnsSettingsContext;

        private readonly Dictionary<string, FrameworkElement>               _settingsControls    = new();
        private readonly Lock                                               _highlightLock       = new();
        private readonly ObservableCollection<HighlightableControlProperty> _highlightedControls = new([]);
        private          int                                                _highlightCurrentIndex;
        private          Brush                                              _highlightBrush;
        private          Brush                                              _highlightSelectedBrush;
        private readonly List<MethodInfo>                                   _dialogMethods;
        private          List<string>                                       DialogMethodNames        { get; }
        public           string                                             SelectedDialogMethodName { get; set; }

    #nullable enable
        private string? _previousSearchQuery;
#nullable restore

        #endregion

        #region Settings Page Handler
        public SettingsPage()
        {
            // This is a waste of memory because it initalizes the list even if DEBUG is not defined.
            _dialogMethods = new List<MethodInfo>();
            DialogMethodNames = new List<string>();
#if DEBUG
            _dialogMethods = typeof(SimpleDialogs)
                            .GetMethods(BindingFlags.Public | BindingFlags.Static)
                            .Where(m => m.ReturnType == typeof(Task<ContentDialogResult>))
                            .ToList();
            
            DialogMethodNames = _dialogMethods.Select(m => m.Name).ToList();
            
            if (DialogMethodNames.Count > 0)
                SelectedDialogMethodName = DialogMethodNames[0];
#endif
    
            InitializeComponent();

            _dnsSettingsContext = new DnsSettingsContext(CustomDnsHostTextbox);
            DataContext = this;

            this.EnableImplicitAnimation(true);
            this.SetAllControlsCursorRecursive(InputSystemCursor.Create(InputSystemCursorShape.Hand));
            ShareYourFeedbackButton.SetCursor(InputSystemCursor.Create(InputSystemCursorShape.Hand));
            AboutApp.FindAndSetTextBlockWrapping(TextWrapping.Wrap, HorizontalAlignment.Center, TextAlignment.Center, true);

            IsInstantRegionChange     = LauncherConfig.IsInstantRegionChange;
            IsShowRegionChangeWarning = LauncherConfig.IsShowRegionChangeWarning;

            string version = $" {LauncherUpdateHelper.LauncherCurrentVersionString}";
#if DEBUG
            version                       += "d";
            DebugItemSeparator.Visibility =  Visibility.Visible;
            DebugStackPanel.Visibility    =  Visibility.Visible;
#endif
            if (IsPreview)
                version += " Preview";
            else
                version += " Stable";

            AppVersionTextBlock.Text = version;
            CurrentVersion.Text = version;
            
            GitVersionIndicator.Text = GitVersionIndicator_Builder();
#pragma warning disable CS0618 // Type or member is obsolete
            GitVersionIndicatorHyperlink.NavigateUri = 
                new Uri(new StringBuilder()
                    .Append(RepoUrl)
                    .Append(ThisAssembly.Git.Sha).ToString());
#pragma warning restore CS0618 // Type or member is obsolete
            if (IsAppLangNeedRestart)
                AppLangSelectionWarning.Visibility = Visibility.Visible;

            if (IsChangeRegionWarningNeedRestart)
                ChangeRegionToggleWarning.Visibility = Visibility.Visible;

            if (IsInstantRegionNeedRestart)
                InstantRegionToggleWarning.Visibility = Visibility.Visible;

            string switchToVer = IsPreview ? "Stable" : "Preview";
            ChangeReleaseBtnText.Text = string.Format(Lang._SettingsPage.AppChangeReleaseChannel, switchToVer);
#if DISABLEDISCORD
            ToggleDiscordRPC.Visibility = Visibility.Collapsed;
#endif

            AppBGCustomizerNote.Text = string.Format(Lang._SettingsPage.AppBG_Note,
                string.Join("; ", BackgroundMediaUtility.SupportedImageExt),
                string.Join("; ", BackgroundMediaUtility.SupportedMediaPlayerExt)
            );
            
            UpdateBindingsInvoker.UpdateEvents += UpdateBindingsEvents;

#if !ENABLEUSERFEEDBACK
            ShareYourFeedbackButton.Visibility = Visibility.Collapsed;
#endif

            Task.Run(() =>
                     {
                         if (ImageLoaderHelper.EnsureWaifu2X())
                         {
                             DispatcherQueue.TryEnqueue(Bindings.Update);
                         }
                     });
        }

        private string GitVersionIndicator_Builder()
        {
#pragma warning disable CS0618, CS0162 // Type or member is obsolete
            var branchName  = ThisAssembly.Git.Branch;
            var commitShort = ThisAssembly.Git.Commit;

            // Add indicator if the commit is dirty
            // CS0162: Unreachable code detected
            if (ThisAssembly.Git.IsDirty)
            {
                commitShort += '*';
            }

            var outString =
                // If branch is not HEAD, show branch name and short commit
                // Else, show full SHA 
                branchName == "HEAD" ? ThisAssembly.Git.Sha : $"{branchName} - {commitShort}";
#pragma warning restore CS0618, CS0162 // Type or member is obsolete
            return outString;
        }
        
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            BackgroundImgChanger.ToggleBackground(true);
            
            InitializeSettingsSearch();

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
            switch (await Dialog_RelocateFolder())
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
            switch (await Dialog_ClearMetadata())
            {
                case ContentDialogResult.Primary:
                    try
                    {
                        Directory.Delete(LauncherMetadataHelper.LauncherMetadataFolder, true);
                        MainEntryPoint.ForceRestart();
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
                    FileName        = _explorerPath,
                    Arguments       = AppGameFolder
                }
            }.Start();
        }

        private async void ClearImgFolder(object sender, RoutedEventArgs e)
        {
            try
            {
                var stream = BackgroundMediaUtility.GetAlternativeFileStream();
                if (stream != null)
                    await stream.DisposeAsync();

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
                CurrentLogger?.ResetLogFiles(AppGameLogsFolder, Encoding.UTF8);
                (sender as Button).IsEnabled = false;
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex);
            }

            await Task.Delay(1000);
            (sender as Button).IsEnabled = true;
        }

        private async void ForceUpdate(object sender, RoutedEventArgs e)
        {
            string channelName = IsPreview ? "Preview" : "Stable";
            if (ContentDialogResult.Primary != await Dialog_ForceUpdateOnChannel(channelName))
            {
                return;
            }

            LaunchUpdater(channelName);
        }

        private async void ChangeRelease(object sender, RoutedEventArgs e)
        {
            string channelName = IsPreview ? "Stable" : "Preview";
            if (ContentDialogResult.Primary != await Dialog_ChangeReleaseToChannel(channelName))
            {
                return;
            }

            // Delete Metadata upon switching release
            if (Directory.Exists(AppGameConfigMetadataFolder))
                Directory.Delete(AppGameConfigMetadataFolder, true);

            LaunchUpdater(channelName);
        }

        private static void LaunchUpdater(string channelName)
        {
            string executableLocation     = AppExecutablePath;
            string installationTargetPath = Path.GetDirectoryName(AppExecutableDir);
            string updateArgument = "elevateupdate --input \""
                                    + installationTargetPath.Replace('\\', '/')
                                    + $"\" --channel {channelName}";

            Console.WriteLine(updateArgument);
            try
            {
                new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        FileName        = executableLocation,
                        Arguments       = updateArgument,
                        Verb            = "runas"
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
            string channelName = IsPreview ? " Preview" : " Stable";

            CheckUpdateBtn.IsEnabled = true;
            if (e.IsUpdateAvailable)
            {
                UpdateLoadingStatus.Visibility = Visibility.Collapsed;
                UpdateAvailableStatus.Visibility = Visibility.Visible;
                UpToDateStatus.Visibility = Visibility.Collapsed;
                UpdateAvailableLabel.Text = e.NewVersionName.VersionString + channelName;
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
                mainWindow.OverlayFrame.BackStack?.Clear();
                mainWindow.OverlayFrame.Navigate(typeof(NullPage));
                mainWindow.OverlayFrame.Navigate(typeof(MarkdownFramePage), mdParam,
                                                 new DrillInNavigationTransitionInfo());
            }

            MarkdownFramePage.Current!.MarkdownCloseBtn.Click += ExitFromOverlay;
            return;
            
            static void ExitFromOverlay(object? sender, RoutedEventArgs args)
            {
                if (WindowUtility.CurrentWindow is not MainWindow mainWindow)
                    return;

                mainWindow.OverlayFrame.GoBack();
                mainWindow.OverlayFrame.BackStack?.Clear();
            }
#nullable restore
        }

        private async void ShareYourFeedbackClick(object sender, PointerRoutedEventArgs e)
        {
#if ENABLEUSERFEEDBACK
            var content = UserFeedbackTemplate.FeedbackTemplate;
            
            UserFeedbackDialog userFeedbackDialog = new UserFeedbackDialog(XamlRoot, true)
            { 
                Message   = content
            };
            
            UserFeedbackResult userFeedbackResult = await userFeedbackDialog.ShowAsync();

            if (userFeedbackResult == null)
            {
                LogWriteLine("User feedback dialog cancelled!");
                return;
            }

            var parsedFeedback       = UserFeedbackTemplate.ParseTemplate(userFeedbackResult);
            var feedbackLoadingTitle = Lang._Misc.Feedback;
            
            // Show pseudo-loading message so user knows the feedback is being sent
            LoadingMessageHelper.Initialize();
            LoadingMessageHelper.SetMessage(feedbackLoadingTitle, Lang._Misc.FeedbackSending);
            LoadingMessageHelper.ShowLoadingFrame();
            
            if (parsedFeedback == null)
            {
                LogWriteLine("Feedback result failed to be parsed! Feedback not sent.", LogType.Error, true);
                LoadingMessageHelper.SetMessage(feedbackLoadingTitle, Lang._Misc.FeedbackSendFailure);
                await Task.Delay(1000);
                LoadingMessageHelper.HideLoadingFrame();
                return;
            }

            if (SentryHelper.SendGenericFeedback(parsedFeedback.Message, parsedFeedback.Email, parsedFeedback.User))
            {
                // Hide the loading message after 200ms
                await Task.Delay(500);
                LoadingMessageHelper.SetMessage(feedbackLoadingTitle, Lang._Misc.FeedbackSent);
                await Task.Delay(1000);
                LoadingMessageHelper.HideLoadingFrame();
            }
            else
            {
                await Task.Delay(250);
                LoadingMessageHelper.SetMessage(feedbackLoadingTitle, Lang._Misc.FeedbackSendFailure);
                await Task.Delay(1000);
                LoadingMessageHelper.HideLoadingFrame();
            }
        #endif
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
            string file = await FileDialogNative.GetFilePicker(ImageLoaderHelper.SupportedImageFormats);
            if (string.IsNullOrEmpty(file))
            {
                return;
            }

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
            bool               isUseRegionCustomBg = currentGameProperty.GameSettings?.SettingsCollapseMisc?.UseCustomRegionBG ?? false;
            if (!isUseRegionCustomBg)
            {
                BackgroundImgChanger.ChangeBackground(LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal, null, true, true, true);
            }
            else if (!string.IsNullOrEmpty(currentGameProperty.GameSettings?.SettingsCollapseMisc?.CustomRegionBGPath))
            {
                _ = BackgroundMediaUtility.GetMediaType(currentGameProperty.GameSettings?.SettingsCollapseMisc?.CustomRegionBGPath);
            }
        }

        private int _eggsAttempt = 1;
        private void Egg(object sender, PointerRoutedEventArgs e)
        {
            if (_eggsAttempt++ >= 10)
                HerLegacy.Visibility = Visibility.Visible;
        }

        private void EnableHeaderMouseEvent(object sender, RoutedEventArgs e)
        {
            ((UIElement)VisualTreeHelper.GetParent((DependencyObject)sender)).IsHitTestVisible = true;
        }
#endregion

        #region Settings UI Backend
        private bool IsBgCustom
        {
            get
            {
                bool isEnabled = GetAppConfigValue("UseCustomBG");
                string bgPath = GetAppConfigValue("CustomBGPath");
                LogWriteLine("Read " + isEnabled + " BG Path: " + bgPath + " from config", LogType.Scheme, true);
                BGPathDisplay.Text = !string.IsNullOrEmpty(bgPath) ? bgPath : Lang._Misc.NotSelected;

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
                bool isUseRegionCustomBg = currentGameProperty.GameSettings?.SettingsCollapseMisc?.UseCustomRegionBG ?? false;
                if (!value)
                {
                    LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal = GetAppConfigValue("CurrentBackground").ToString();
                    _ = m_mainPage?.ChangeBackgroundImageAsRegionAsync();

                    ToggleCustomBgButtons();
                }
                else if (isUseRegionCustomBg)
                {
                    string currentRegionCustomBg = currentGameProperty.GameSettings.SettingsCollapseMisc.CustomRegionBGPath;
                    LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal = currentRegionCustomBg;
                    _ = m_mainPage?.ChangeBackgroundImageAsRegionAsync();

                    ToggleCustomBgButtons();
                }
                else
                {
                    var bgPath = GetAppConfigValue("CustomBGPath");
                    if (string.IsNullOrEmpty(bgPath))
                    {
                        LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal = BackgroundMediaUtility.GetDefaultRegionBackgroundPath();
                    }
                    else
                    {
                        LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal = !File.Exists(bgPath) ? BackgroundMediaUtility.GetDefaultRegionBackgroundPath() : bgPath;
                    }
                    BGPathDisplay.Text = LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal;
                    BackgroundImgChanger.ChangeBackground(LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal, null, true, true);
                }

                if (value)
                {
                    BGPathDisplay.Text = GetAppConfigValue("CustomBGPath");
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
                bool isEnabled = GetAppConfigValue("EnableConsole");
                ToggleIncludeGameLogs.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
                return isEnabled;
            }
            set
            {
                CurrentLogger.Dispose();
                if (value)
                {
                    CurrentLogger                    = new LoggerConsole(AppGameLogsFolder, Encoding.UTF8);
                    ToggleIncludeGameLogs.Visibility = Visibility.Visible;
                }
                else
                {
                    CurrentLogger                    = new LoggerNull(AppGameLogsFolder, Encoding.UTF8);
                    ToggleIncludeGameLogs.Visibility = Visibility.Collapsed;
                }
                SetAndSaveConfigValue("EnableConsole", value);
            }
        }

        private bool IsSendRemoteCrashData
        {
            get
            {
                if (!SentryHelper.IsDisableEnvVarDetected)
                {
                    return SentryHelper.IsEnabled;
                }

                ToggleSendRemoteCrashData.IsEnabled = false;
                ToolTipService.SetToolTip(ToggleSendRemoteCrashData, Lang._SettingsPage.Debug_SendRemoteCrashData_EnvVarDisablement);
                return SentryHelper.IsEnabled;
            }
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

        private bool IsVideoBackgroundAudioMute
        {
            get => !GetAppConfigValue("BackgroundAudioIsMute");
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
                double value = GetAppConfigValue("BackgroundAudioVolume");
                switch (value)
                {
                    case < 0:
                        MainPage.CurrentBackgroundHandler?.SetVolume(0d);
                        break;
                    case > 1:
                        MainPage.CurrentBackgroundHandler?.SetVolume(1d);
                        break;
                }

                return value * 100d;
            }
            set
            {
                if (value < 0) return;
                double downValue = value / 100d;
                MainPage.CurrentBackgroundHandler?.SetVolume(downValue);
            }
        }

#if !DISABLEDISCORD
        private bool IsDiscordRpcEnabled
        {
            get
            {
                bool isEnabled = GetAppConfigValue("EnableDiscordRPC");
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
            get => GetAppConfigValue("EnableDiscordGameStatus");
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
            get => EnableAcrylicEffect;
            set
            {
                EnableAcrylicEffect = value;

                if (BackgroundMediaUtility.CurrentAppliedMediaType == MediaType.Media
                 && value && !IsUseVideoBgDynamicColorUpdate)
                    return;

                App.ToggleBlurBackdrop(value);
            }
        }

        private bool IsUseVideoBgDynamicColorUpdate
        {
            get => IsUseVideoBGDynamicColorUpdate;
            set
            {
                IsUseVideoBGDynamicColorUpdate = value;
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
                    case Waifu2XStatus.NotInitialized:
                        tooltip += "\n\n" + Lang._SettingsPage.Waifu2X_Initializing;
                        break;
                }

                return tooltip;
            }
        }

        private string Waifu2XToolTipIcon
        {
            get
            {
                return ImageLoaderHelper.Waifu2XStatus switch
                       {
                           <= Waifu2XStatus.Ok => "\uf05a",
                           < Waifu2XStatus.Error => "\uf071",
                           >= Waifu2XStatus.Error => "\uf06a"
                       };
            }
        }

        private bool IsWaifu2XUsable => ImageLoaderHelper.IsWaifu2XUsable;

        private int CurrentThemeSelection
        {
            get
            {
                if (IsAppThemeNeedRestart)
                    AppThemeSelectionWarning.Visibility = Visibility.Visible;

                string appTheme = GetAppConfigValue("ThemeMode");
                bool isParseSuccess = Enum.TryParse(typeof(AppThemeMode), appTheme, out object themeIndex);
                return isParseSuccess ? (int)themeIndex : -1;
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
            get => GetAppConfigValue("DownloadThread");
            set => SetAndSaveConfigValue("DownloadThread", value);
        }

        private int CurrentAppThreadExtractValue
        {
            get => GetAppConfigValue("ExtractionThread");
            set => SetAndSaveConfigValue("ExtractionThread", value);
        }

        [field: AllowNull, MaybeNull]
        private List<LangMetadata> LanguageList
        {
            get
            {
                if (field != null)
                {
                    return field;
                }

                var keys = LanguageNames.Keys;
                int dataLen = keys.Count;

                List<LangMetadata> metadataList = [];

                CollectionsMarshal.SetCount(metadataList, dataLen);
                Span<LangMetadata> metadataSpan = CollectionsMarshal.AsSpan(metadataList);

                int index = 0;
                foreach (string key in keys)
                {
                    ref LangMetadata metadata = ref CollectionsMarshal.GetValueRefOrNullRef(LanguageNames, key);
                    if (Unsafe.IsNullRef(ref metadata))
                    {
                        continue;
                    }

                    metadataSpan[index] = metadata;
                    ++index;
                }

                return field = metadataList;
            }
        }

        private int LanguageSelectedIndex
        {
            get
            {
                string key = GetAppConfigValue("AppLanguage").Value.ToLower();
                return LanguageNames.TryGetValue(key, out var name) ? name.LangIndex : 0;
            }
        }

        private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox combobox)
            {
                return;
            }

            if (e.AddedItems.FirstOrDefault()   is not LangMetadata asLangMetadataNew ||
                e.RemovedItems.FirstOrDefault() is not LangMetadata asLangMetadataOld ||
                asLangMetadataNew.LangID == asLangMetadataOld.LangID)
            {
                return;
            }

            string selectedKey = asLangMetadataNew.LangID;
            SetAndSaveConfigValue("AppLanguage", selectedKey);
            LoadLocale(selectedKey);
            UpdateBindings.Update();
            PluginManager.SetPluginLocaleId(selectedKey);

            foreach (ComboBox comboBoxOthers in this.FindDescendants().OfType<ComboBox>())
            {
                if (comboBoxOthers == combobox)
                    continue;

                int lastSelected = comboBoxOthers.SelectedIndex;
                comboBoxOthers.SelectedIndex = -1;
                comboBoxOthers.SelectedIndex = lastSelected;
            }

            foreach (RadioButtons radioButtonOthers in this.FindDescendants().OfType<RadioButtons>())
            {
                int lastSelected = radioButtonOthers.SelectedIndex;
                radioButtonOthers.SelectedIndex = -1;
                radioButtonOthers.SelectedIndex = lastSelected;
            }

            InitializeSettingsSearch();
        }

        private void UpdateBindingsEvents(object sender, EventArgs e)
        {
            Bindings.Update();
            UpdateLayout();

            string switchToVer = IsPreview ? "Stable" : "Preview";
            ChangeReleaseBtnText.Text = string.Format(Lang._SettingsPage.AppChangeReleaseChannel, switchToVer);
            AppBGCustomizerNote.Text = string.Format(Lang._SettingsPage.AppBG_Note,
                string.Join("; ", BackgroundMediaUtility.SupportedImageExt),
                string.Join("; ", BackgroundMediaUtility.SupportedMediaPlayerExt)
            );

            string url = GetAppConfigValue("HttpProxyUrl");
            ValidateHttpProxyUrl(url);

            _dnsSettingsContext.ExternalDnsConnectionTypeList = null;
            _dnsSettingsContext.ExternalDnsProviderList       = null;
            CustomDnsConnectionTypeComboBox.UpdateLayout();
            CustomDnsProviderListComboBox.UpdateLayout();
        }

        private readonly List<string> _windowSizeProfilesKey = WindowSizeProfiles.Keys.ToList();
        private int SelectedWindowSizeProfile
        {
            get
            {
                string val = CurrentWindowSizeName;
                return _windowSizeProfilesKey.IndexOf(val);
            }
            set
            {
                if (value < 0) return;
                CurrentWindowSizeName     = _windowSizeProfilesKey[value];
                BGPathDisplayViewer.Width = CurrentWindowSize.SettingsPanelWidth;
                ChangeTitleDragArea.Change(DragAreaTemplate.Default);
                SaveAppConfig();
            }
        }

        [field: AllowNull, MaybeNull]
        private List<CDNURLProperty> CDNList { get => field ??= LauncherConfig.CDNList; }

        private int SelectedCDN
        {
            get
            {
                int value = GetAppConfigValue("CurrentCDN");
                return value;
            }
        }

        private void AppCDNSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.FirstOrDefault() is not CDNURLProperty asCdnUrlNew)
            {
                return;
            }

            int indexAt = CDNList.IndexOf(asCdnUrlNew);
            if (indexAt < 0)
            {
                return;
            }

            SetAppConfigValue("CurrentCDN", indexAt);
            SaveAppConfig();
        }

        private bool IsIncludeGameLogs
        {
            get => GetAppConfigValue("IncludeGameLogs");
            set => SetAndSaveConfigValue("IncludeGameLogs", value);
        }

        private bool IsShowRegionChangeWarning
        {
            get
            {
                field = LauncherConfig.IsShowRegionChangeWarning;

                PanelChangeRegionInstant.Visibility = !field ? Visibility.Visible : Visibility.Collapsed;
                return field;
            }
            set
            {
                LauncherConfig.IsShowRegionChangeWarning = value;
                IsChangeRegionWarningNeedRestart         = true;
                
                var valueConfig = field;
                ChangeRegionToggleWarning.Visibility = value != valueConfig ? Visibility.Visible : Visibility.Collapsed;
                PanelChangeRegionInstant.Visibility  = !value ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        
        private bool IsInstantRegionChange
        {
            get => field = LauncherConfig.IsInstantRegionChange;
            set
            {
                IsInstantRegionNeedRestart = true;
                var valueConfig = field;
                InstantRegionToggleWarning.Visibility = value != valueConfig ? Visibility.Visible : Visibility.Collapsed;

                LauncherConfig.IsInstantRegionChange = value;
            }
        }
        private bool IsUseDownloadChunksMerging
        {
            get => GetAppConfigValue("UseDownloadChunksMerging");
            set => SetAndSaveConfigValue("UseDownloadChunksMerging", value);
        }

        private bool IsLowerCollapsePriorityOnGameLaunch
        {
            get => GetAppConfigValue("LowerCollapsePrioOnGameLaunch");
            set => SetAndSaveConfigValue("LowerCollapsePrioOnGameLaunch", value);
        }

        private bool IsAlwaysUseExternalBrowser
        {
            get => GetAppConfigValue("UseExternalBrowser");
            set => SetAndSaveConfigValue("UseExternalBrowser", value);
        }

        private int AppGameLaunchedBehaviorIndex
        {
            get => GetAppConfigValue("GameLaunchedBehavior").Value switch
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
            get => GetAppConfigValue("MinimizeToTray");
            set => SetAndSaveConfigValue("MinimizeToTray", value);
        }

        private bool IsLaunchOnStartup
        {
            get
            {
                bool value = TaskSchedulerHelper.IsEnabled();
                StartupToTrayToggle.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                return value;
            }
            set
            {
                TaskSchedulerHelper.ToggleEnabled(value);
                StartupToTrayToggle.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private bool IsStartupToTray
        {
            get => TaskSchedulerHelper.IsOnTrayEnabled();
            set => TaskSchedulerHelper.ToggleTrayEnabled(value);
        }

        private bool IsEnableSophon
        {
            get => GetAppConfigValue("IsEnableSophon");
            set => SetAndSaveConfigValue("IsEnableSophon", value);
        }

        private int SophonDownThread
        {
            get => GetAppConfigValue("SophonCpuThread");
            set => SetAndSaveConfigValue("SophonCpuThread", value);
        }

        private int SophonHttpConn
        {
            get => GetAppConfigValue("SophonHttpConnInt");
            set => SetAndSaveConfigValue("SophonHttpConnInt", value);
        }

        private bool IsSophonPreloadPerfMode
        {
            get => GetAppConfigValue("SophonPreloadApplyPerfMode");
            set => SetAndSaveConfigValue("SophonPreloadApplyPerfMode", value);
        }

#nullable enable
        private bool IsUseProxy
        {
            get => GetAppConfigValue("IsUseProxy");
            set => SetAndSaveConfigValue("IsUseProxy", value);
        }
        private bool IsAllowHttpRedirections
        {
            get => GetAppConfigValue("IsAllowHttpRedirections");
            set => SetAndSaveConfigValue("IsAllowHttpRedirections", value);
        }
        private bool IsAllowHttpCookies
        {
            get => GetAppConfigValue("IsAllowHttpCookies");
            set => SetAndSaveConfigValue("IsAllowHttpCookies", value);
        }

        private bool IsAllowUntrustedCert
        {
            get => GetAppConfigValue("IsAllowUntrustedCert");
            set => SetAndSaveConfigValue("IsAllowUntrustedCert", value);
        }

        private double HttpClientTimeout
        {
            get => GetAppConfigValue("HttpClientTimeout");
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
                string? url = GetAppConfigValue("HttpProxyUrl");
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
            if (sender is not Button button)
            {
                return;
            }
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
                UrlStatus urlStatus = await FallbackCDNUtil.GetURLStatusCode("https://gitlab.com/bagusnl/CollapseLauncher-ReleaseRepo/-/raw/main/LICENSE", CancellationToken.None);
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
                BindingOperations.SetBinding(button, IsEnabledProperty, new Binding
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
                ProgressRing? selectedProgressRing = ProxyConnectivityTestTextChecking.Children
                    .OfType<ProgressRing>()
                    .FirstOrDefault();
                if (selectedProgressRing != null)
                    selectedProgressRing.IsIndeterminate = false;
            }
        }

        private string? HttpProxyUsername
        {
            get => GetAppConfigValue("HttpProxyUsername");
            set => SetAndSaveConfigValue("HttpProxyUsername", value);
        }

        private string? HttpProxyPassword
        {
            get
            {
                string? encData = GetAppConfigValue("HttpProxyPassword");
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
                if (value)
                    NetworkBurstDownloadModeToggle.IsOn = false;
                NetworkBurstDownloadModeToggle.IsEnabled = !value;
                return value;
            }
            set
            {
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

        private bool IsEnforceToUse7ZipOnExtract
        {
            get => IsEnforceToUse7zipOnExtract;
            set => IsEnforceToUse7zipOnExtract = value;
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

        private readonly string _dnsSettingsSeparatorList = string.Join(' ', HttpClientBuilder.DnsHostSeparators.Select(x => $"{x}"));

        private async void ValidateAndApplyDnsSettings(object sender, RoutedEventArgs e)
        {
            if (sender is not Button senderAsButton)
            {
                return;
            }

            senderAsButton.IsEnabled = false;
            NameServer[]? lastDnsSettings = HttpClientBuilder.SharedExternalDnsServers;

            try
            {
                DnsSettingsTestTextChecking.Visibility = Visibility.Visible;

                string?           dnsHost     = _dnsSettingsContext.ExternalDnsAddresses;
                DnsConnectionType connType    = (DnsConnectionType)_dnsSettingsContext.ExternalDnsConnectionType;
                string            dnsSettings = $"{dnsHost}|{connType}";

                string[]? resultHosts = await HttpClientBuilder.TryParseDnsHostsAsync(dnsSettings, true, true);
                if (resultHosts == null)
                {
                    throw new InvalidOperationException($"The current DNS host string: {dnsSettings} has malformed separator or one of the hostname's IPv4/IPv6 cannot be resolved! " + 
                                                        $"Also, make sure that you use one of these separators: {_dnsSettingsSeparatorList}");
                }


                (bool isSuccess, DnsConnectionType resultConnType) = await Task.Factory.StartNew(() =>
                {
                    bool isSuccess =
                        HttpClientBuilder.TryParseDnsConnectionType(dnsSettings, out DnsConnectionType resultConnType);
                    return (isSuccess, resultConnType);
                }, TaskCreationOptions.DenyChildAttach);

                if (!isSuccess)
                {
                    DnsConnectionType[] types       = Enum.GetValues<DnsConnectionType>();
                    string              typesInList = string.Join(", ", types);
                    throw new InvalidOperationException($"The current DNS host string: {dnsSettings} has no valid DNS Connection Type. " + 
                                                        $"The valid values are: {typesInList}");
                }

                const string testUrl = "https://gitlab.com/bagusnl/CollapseLauncher-ReleaseRepo/-/raw/main/LICENSE";

                TimeSpan timeoutSpan = TimeSpan.FromSeconds(5);
                using HttpClient httpClientWithCustomDns = new HttpClientBuilder()
                                                          .UseLauncherConfig(skipDnsInit: true)
                                                          .SetTimeout(timeoutSpan)
                                                          .Create();

                HttpClientBuilder.UseExternalDns(resultHosts, resultConnType);

                using CancellationTokenSource tokenSource = new(timeoutSpan);
                using HttpResponseMessage responseMessage =
                    await
                        httpClientWithCustomDns
                           .GetAsync(testUrl, HttpCompletionOption.ResponseContentRead, tokenSource.Token);

                if (!responseMessage.IsSuccessStatusCode)
                {
                    throw new
                        HttpRequestException($"HttpClient returns a non-successful status code while testing the request to this URL: {testUrl} (Status: {responseMessage.StatusCode})",
                                             null, responseMessage.StatusCode);
                }

                _dnsSettingsContext.SaveSettings();

                DnsSettingsTestTextSuccess.Visibility  = Visibility.Visible;
            }
            catch (Exception ex)
            {
                HttpClientBuilder.SharedExternalDnsServers = lastDnsSettings;
                DnsSettingsTestTextFailed.Visibility = Visibility.Visible;
                ErrorSender.SendException(new InvalidOperationException("DNS Settings cannot be validated due to these errors.", ex));
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
            finally
            {
                DnsSettingsTestTextChecking.Visibility = Visibility.Collapsed;
                await Task.Delay(TimeSpan.FromSeconds(2));

                DnsSettingsTestTextFailed.Visibility  = Visibility.Collapsed;
                DnsSettingsTestTextSuccess.Visibility = Visibility.Collapsed;
                senderAsButton.IsEnabled              = true;
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
                if (value) _ = DbHandler.TryInit();
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

            if (await Dialog_DbGenerateUid() != ContentDialogResult.Primary) // Send warning dialog
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
                if (Convert.ToInt32(await DbHandler.QueryKey("TestKey", true)) !=
                    r) // Query key and check if value is correct
                    throw
                        new InvalidDataException("Data validation failed!"); // Throw if value does not match (then catch), unlikely but maybe for really unstable db server

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
                                  ContentDialogTheme.Success
                                 ); // Show success dialog
            }
            catch (DllNotFoundException ex)
            {
                // No need to revert the value if fails, user is asked to restart the app
                ShowFailed(ex);
                var res = await SpawnDialog(
                                  Lang._Misc.MissingVcRedist,
                                  Lang._Misc.MissingVcRedistSubtitle,
                                  sender as UIElement,
                                  Lang._Misc.Close,
                                  Lang._Misc.Yes,
                                  null,
                                  ContentDialogButton.Primary,
                                  ContentDialogTheme.Error);
                if (res == ContentDialogResult.Primary)
                {
                    await Task.Run(() =>
                                   {
                                       ProcessStartInfo psi = new ProcessStartInfo
                                       {
                                           FileName        = _explorerPath,
                                           Arguments       = "https://aka.ms/vs/17/release/vc_redist.x64.exe",
                                           UseShellExecute = true,
                                           Verb            = "runas"
                                       };
                                       Process.Start(psi);
                                   });
                }
                else { await SentryHelper.ExceptionHandlerAsync(ex); }
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
                ErrorSender.SendException(newEx, ErrorType.Unhandled, false); // Send error with dialog
            }
            finally
            {
                // Re-enable the validation button
                senderAsButtonBase.IsEnabled = true;
            }

            return;

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
            if (await Dialog_DbGenerateUid() != ContentDialogResult.Primary)
            {
                return;
            }

            var g = Guid.CreateVersion7();
            DbUserIdTextBox.Text = g.ToString();
            _dbUserId            = g.ToString();

            ShowDbWarningStatus(Lang._SettingsPage.Database_Warning_PropertyChanged);
        }
        
        private void DbTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateDbButton.IsEnabled = !string.IsNullOrEmpty(sender.As<TextBox>().Text);
        }

        private void DbTokenPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            ValidateDbButton.IsEnabled = !string.IsNullOrEmpty(sender.As<PasswordBox>().Password);
        }
        #endregion
        #endregion

        #region Keyboard Shortcuts
        private async void ShowKbScList_Click(object sender, RoutedEventArgs e) => await KeyboardShortcuts.Dialog_ShowKbShortcuts(this);

        private async void ResetKeylist_Click(object sender, RoutedEventArgs e)
        {
            if (await Dialog_ResetKeyboardShortcuts() != ContentDialogResult.Primary)
            {
                return;
            }

            KeyboardShortcuts.ResetKeyboardShortcuts();
            KeyboardShortcutsEvent(null, AreShortcutsEnabled ? 1 : 2);
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
                KbScBtns.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                SetAndSaveConfigValue("EnableShortcuts", value);
                KeyboardShortcutsEvent(this, value ? 0 : 2);
            }
        }
        #endregion

        #region Settings Search
        private void InitializeSettingsSearch()
        {
            // Create brushes for highlighting
            _highlightBrush = new SolidColorBrush(Microsoft.UI.Colors.Yellow) { Opacity = 0.3 };
            _highlightSelectedBrush = new SolidColorBrush(Microsoft.UI.Colors.Blue) { Opacity = 0.5 };

            // Map all settings controls with their display text
            if (!(_settingsControls.Count < 1)) _settingsControls.Clear();
            ClearHighlighting();

            // Walk through all Toggle Switches, TextBlocks with headers, etc.
            CollectSearchableControls(AppSettings);

            // Initialize shortcut
            SettingsSearchBoxShortcutInit();
        }

        private void CollectSearchableControls(DependencyObject parent)
        {
            var childCount = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                switch (child)
                {
                    // Check if this is a control we want to make searchable
                    case ToggleSwitch t: // ToggleSwitch main header
                    {
                        AddControlRecursive(t.Header, t);
                        continue;
                    }
                    case RadioButtons t:
                    {
                        AddControlRecursive(t.Header, t);
                        continue;
                    }
                    case RadioButton t:
                    {
                        AddControlRecursive(t.Content, t);
                        continue;
                    }
                    case ComboBox { Header: not null } t:
                    {
                        AddControlRecursive(t.Header, t);
                        continue;
                    }
                    case ComboBoxItem t:
                    {
                        if (!string.IsNullOrEmpty(t.Content?.ToString()))
                            AddControl(t.Content.ToString(), t);
                        continue;
                    }
                    case NumberBox { Header: not null } t:
                    {
                        AddControlRecursive(t.Header, t);
                        continue;
                    }
                    case TextBlock t:
                    {
                        if (!string.IsNullOrEmpty(t.Text))
                            AddControl(t.Text, t);
                        continue;
                    }
                    case Run t when !string.IsNullOrEmpty(t.Text):
                    {
                        AddControl(t.Text, VisualTreeHelper.GetParent(t) as FrameworkElement);
                        continue;
                    }
                    case Slider t when !string.IsNullOrEmpty(t.Header?.ToString()):
                    {
                        AddControl(t.Header?.ToString(), t);
                        continue;
                    }
                }

                // Recurse into children
                CollectSearchableControls(child);
            }
        }
        
        private void AddControl(string key, FrameworkElement t)
        {
            if (t is StackPanel or Grid) 
                return;

            if (!string.IsNullOrEmpty(key) && t.IsElementVisible())
                _settingsControls.TryAdd(key, t);
        #if DEBUG
            LogWriteLine($"Got type {t.GetType()} with key {key}", LogType.Debug);
        #endif
        }

        private void AddControlRecursive(object o, FrameworkElement t)
        {
            while (true)
            {
                if (t is StackPanel or Grid) return;
                string key = null;

                switch (o)
                {
                    // Handle different content types
                    case string textContent:
                        key = textContent;
                        break;
                    case TextBlock textBlock:
                        key = textBlock.Text;
                        break;
                    case RadioButtons radioButtons:
                    {
                        AddControlRecursiveSelectible(radioButtons);
                        return;
                    }
                    case FrameworkElement element:
                    {
                        // Try to find TextBlock inside the content
                        var textBlocks = element.FindDescendants().OfType<TextBlock>();
                        var enumerable = textBlocks as TextBlock[] ?? textBlocks.ToArray();
                        if (enumerable.Length != 0)
                        {
                            key = string.Join(" ", enumerable.Select(tb => tb.Text));
                        }

                        break;
                    }
                    case null:
                    {
                        o = t;
                        continue;
                    }
                }

                if (string.IsNullOrEmpty(key))
                {
                    return;
                }

                _settingsControls.TryAdd(key, t);
            #if DEBUG
                LogWriteLine($"Got type {t.GetType()} with key {key}", LogType.Debug);
            #endif
                break;
            }
        }

    #nullable enable
        private void AddControlRecursiveSelectible(FrameworkElement element)
        {
            foreach (FrameworkElement childElement in element
                .EnumerateSelectableElementChildren()
                .OfType<FrameworkElement>())
            {
                if (childElement is not RadioButton asRadioButton)
                {
                    continue;
                }

                TextBlock? textBlock = asRadioButton.FindDescendant<TextBlock>();
                if (string.IsNullOrEmpty(textBlock?.Text))
                {
                    continue;
                }

                AddControlRecursive(textBlock.Text, asRadioButton);
            }
        }

        private void ClearHighlighting()
        {
            foreach (var control in _highlightedControls)
            {
                control.ClearHighlight();
            }

            _highlightedControls.Clear();
        }

        private void SettingsSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                PerformSearch(sender.Text);
            }
        }

        private void SettingsSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            bool isShiftPressed = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (PerformSearch(args.QueryText))
            {
                return;
            }

            if (isShiftPressed)
            {
                SettingsSearchBoxFindSelectPrevious(null, null);
                return;
            }

            SettingsSearchBoxFindSelectNext(null, null);
        }

        private bool PerformSearch(string query)
        {
            if (query == _previousSearchQuery)
            {
                return false;
            }

            _previousSearchQuery = query;

            // Clear previous highlighting
            ClearHighlighting();

            if (string.IsNullOrWhiteSpace(query))
                return false;

            // Find and highlight matching controls
            foreach (var (key, control) in _settingsControls)
            {
                int indexOfQuery;
                if ((indexOfQuery = key.IndexOf(query, StringComparison.OrdinalIgnoreCase)) < 0)
                {
                    continue;
                }
                
            #if DEBUG
                LogWriteLine($"For {query}, found key {key} with type {control.GetType()}", LogType.Debug);
            #endif

                if (control is TextBlock textBlock)
                {
                    // Create a highlighter or use an existing one (if any)
                    TextHighlighter textHighlighter    = new TextHighlighter();
                    TextRange       textHighlightRange = new TextRange(indexOfQuery, query.Length);

                    // Assign the range and its color (for background and foreground)
                    textHighlighter.Ranges.Add(textHighlightRange);

                    // Assign the highlighter (if there's none)
                    textBlock.TextHighlighters.Add(textHighlighter);
                }

                HighlightableControlProperty highlightableControl = control.CreateHighlight(_highlightBrush, _highlightSelectedBrush);
                highlightableControl.SetBrushHighlightElement();
                _highlightedControls.Add(highlightableControl);
            }

            // Find the first match and if it's null, return.
            if (_highlightedControls.Count == 0 || _highlightedControls.FirstOrDefault() is not { } firstControl)
            {
                return false;
            }

            using (_highlightLock.EnterScope())
            {
                SettingsSearchBringIntoViewAndHighlight(firstControl);
                _highlightCurrentIndex = 0;
            }
            return true;
        }

        private void SettingsSearchBringIntoViewAndHighlight(HighlightableControlProperty element)
        {
            ref IList<HighlightableControlProperty> backedList =
                ref ObservableCollectionExtension<HighlightableControlProperty>
                   .GetBackedCollectionList(_highlightedControls);

            int indexOf = backedList.IndexOf(element);
            if (indexOf < 0)
            {
                return;
            }

            foreach (var control in backedList)
            {
                if (control == element)
                {
                    continue;
                }

                control.SetBrushHighlightElement();
            }

            SettingsSearchBringIntoView(indexOf);
        }

        private void SettingsSearchBringIntoView(int atIndex)
        {
            ref IList<HighlightableControlProperty> backedList =
                ref ObservableCollectionExtension<HighlightableControlProperty>
                   .GetBackedCollectionList(_highlightedControls);

            if (backedList.Count < atIndex)
            {
                return;
            }

            HighlightableControlProperty selectedElement = backedList[atIndex];
            SettingsSearchHighlightPosText.Text = $"{atIndex + 1} / {_highlightedControls.Count}";

            // Otherwise, bring first control into view
            FrameworkElement? e = selectedElement.Element switch
            {
                RadioButton tc => VisualTreeHelper.GetParent(tc) as FrameworkElement,
                ComboBoxItem tc => VisualTreeHelper.GetParent(tc) as FrameworkElement,
                _ => selectedElement.Element
            };

            e?.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true, VerticalAlignmentRatio = 0.1f });
            selectedElement.SetBrushHighlightSelectElement();

            MainSettingsPanel.Opacity = 1f;
            AboutApp.Opacity          = 1f;
        }

        private void SettingsSearchBox_OnGettingFocus(UIElement sender, GettingFocusEventArgs args)
        {
            SettingsSearchBoxGridShadow.Translation = new Vector3(0, 0, 32);
            MainSettingsPanel.Opacity               = 0.5f;
            AboutApp.Opacity                        = 0.5f;
        }

        private void SettingsSearchBox_OnLosingFocus(UIElement sender, LosingFocusEventArgs args)
        {
            SettingsSearchBoxGridShadow.Translation = new Vector3(0, 0, 8);
            MainSettingsPanel.Opacity               = 1f;
            AboutApp.Opacity                        = 1f;
        }

        private void SettingsSearchBoxFindSelectNext(object? sender, RoutedEventArgs? e)
        {
            using (_highlightLock.EnterScope())
            {
                if (_highlightedControls.Count == 0)
                {
                    return;
                }

                ++_highlightCurrentIndex;
                if (_highlightedControls.Count <= _highlightCurrentIndex)
                {
                    _highlightCurrentIndex = -1;
                    SettingsSearchBoxFindSelectNext(sender, e);
                    return;
                }

                SettingsSearchBringIntoViewAndHighlight(_highlightedControls[_highlightCurrentIndex]);
            }
        }

        private void SettingsSearchBoxFindSelectPrevious(object? sender, RoutedEventArgs? e)
        {
            using (_highlightLock.EnterScope())
            {
                if (_highlightedControls.Count == 0)
                {
                    return;
                }

                --_highlightCurrentIndex;
                if (_highlightCurrentIndex < 0)
                {
                    _highlightCurrentIndex = _highlightedControls.Count;
                    SettingsSearchBoxFindSelectPrevious(sender, e);
                    return;
                }

                SettingsSearchBringIntoViewAndHighlight(_highlightedControls[_highlightCurrentIndex]);
            }
        }

        private void SettingsSearchBoxShortcutInit()
        {
            KeyboardAccelerator previousAccelerator = new KeyboardAccelerator
            {
                Key       = Windows.System.VirtualKey.Enter,
                Modifiers = Windows.System.VirtualKeyModifiers.Shift
            };

            KeyboardAccelerator nextAccelerator = new KeyboardAccelerator
            {
                Key       = Windows.System.VirtualKey.Enter,
                Modifiers = Windows.System.VirtualKeyModifiers.None
            };

            SettingsSearchHighlightPreviousBtn.Focus(FocusState.Keyboard);
            SettingsSearchHighlightPreviousBtn.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
            SettingsSearchHighlightPreviousBtn.KeyboardAccelerators.Add(previousAccelerator);

            SettingsSearchHighlightNextBtn.Focus(FocusState.Keyboard);
            SettingsSearchHighlightNextBtn.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
            SettingsSearchHighlightNextBtn.KeyboardAccelerators.Add(nextAccelerator);
        }

        private async void DebugCustomDialogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var method = _dialogMethods.FirstOrDefault(m => m.Name == SelectedDialogMethodName);
                if (method == null)
                {
                    LogWriteLine("[DBG-DialogSpawner] No method found.", LogType.Debug);
                    return;
                }

                LogWriteLine($"[DBG-DialogSpawner] Invoking method: {method.Name}",              LogType.Debug);
                LogWriteLine($"[DBG-DialogSpawner] Parameters: {method.GetParameters().Length}", LogType.Debug);
                LogWriteLine($"[DBG-DialogSpawner] Return type: {method.ReturnType}",            LogType.Debug);

                var parameters = method.GetParameters();
                object[]? parameterValues = null;
                if (parameters.Length > 0)
                {
                    parameterValues = await ShowDebugParameterInputDialog(method);
                    if (parameterValues == null)
                    {
                        LogWriteLine("[DBG-DialogSpawner] Parameter input dialog was cancelled.", LogType.Warning);
                        return;
                    }
                }
                LogWriteLine("[DBG-DialogSpawner] Invoking method with parameters: " + 
                             (parameterValues != null ? string.Join(", ", parameterValues) : "None"), LogType.Debug);
                var result = method.Invoke(null, parameterValues);
                if (result is Task<ContentDialogResult> task)
                {
                    await task;
                }
                else
                {
                    LogWriteLine("[DBG-DialogSpawner] Method did not return a ContentDialogResult task.", LogType.Warning);
                }
            }
            catch (Exception ex)
            {
                LogWriteLine("[DBG-DialogSpawner] Exception: " + ex, LogType.Error);
                await SpawnDialog(
                                  "Error",
                                  ex.ToString(),
                                  sender as UIElement,
                                  Lang._Misc.Close,
                                  null,
                                  null,
                                  ContentDialogButton.Close,
                                  ContentDialogTheme.Error);
            }
        }

        private async Task<object[]?> ShowDebugParameterInputDialog(MethodInfo method)
        {
            var parameters = method.GetParameters();
            var stackPanel = new StackPanel();
            var controls   = new List<Control>();

            foreach (var parameter in parameters)
            {
                var textBox = new TextBox { Header = parameter.Name, PlaceholderText = parameter.ParameterType.Name };
                stackPanel.Children.Add(textBox);
                controls.Add(textBox);
            }

            var dialog = await SpawnDialog(
                                           "Enter Parameters",
                                           stackPanel,
                                           null,
                                           Lang._Misc.Cancel,
                                           Lang._Misc.Okay,
                                           null,
                                           ContentDialogButton.Primary,
                                           ContentDialogTheme.Success);

            if (dialog != ContentDialogResult.Primary)
                return null;
            
            var values = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var text = ((TextBox)controls[i]).Text;
                values[i] = Convert.ChangeType(text, parameters[i].ParameterType);
            }

            return values;
        }

        private void DebugCustomDialogComboBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (DialogMethodNames.Contains(args.QueryText))
            {
                SelectedDialogMethodName = args.QueryText;
            }
        }

        private void DebugCustomDialogComboBox_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var filtered = DialogMethodNames
                              .Where(name => name.Contains(sender.Text, StringComparison.OrdinalIgnoreCase))
                              .ToList();
                sender.ItemsSource = filtered;
            }
        }
        #endregion

        #region Plugins
        internal static void CopyLoadedPluginInformationClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not Button { Tag: PluginInfo asPluginInfo })
                {
                    return;
                }

                string info =
                    $"""
                     Name: {asPluginInfo.Name}
                     Author: {asPluginInfo.Author}
                     Description:
                     {asPluginInfo.Description}
                     
                     =========
                     
                     Plugin Version: {asPluginInfo.Version}
                     Interface Version: {asPluginInfo.StandardVersion}
                     Creation Date: {asPluginInfo.CreationDate?.ToString(LocalFullDateTimeConverter.FullFormat)}
                     Main Library Path: {asPluginInfo.PluginFilePath}
                     Loaded Presets:
                     """;

                foreach (PluginPresetConfigWrapper wrapper in asPluginInfo.PresetConfigs)
                {
                    string name = wrapper.GameName;
                    string region = wrapper.ZoneName;

                    info += $"\r\n  • {name} - {region}";
                }

                Clipboard.CopyStringToClipboard(info);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to copy loaded plugin information: {ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(ex);
            }
        }

        private async void OpenPluginManagerClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button asButton)
            {
                return;
            }

            try
            {
                asButton.IsEnabled = false;
                FullPageOverlay overlayMenu = new FullPageOverlay(new PluginManagerPage(), XamlRoot, true)
                {
                    Size               = FullPageOverlaySize.Full,
                    OverlayTitleSource = () => Lang._PluginManagerPage.PageTitle,
                    OverlayTitleIcon   = new FontIconSource
                    {
                        Glyph = "\uE912",
                        FontSize = 16
                    }
                };

                await overlayMenu.ShowAsync();
            }
            finally
            {
                asButton.IsEnabled = true;
            }
        }
        #endregion

        #region Network Cache
        private async void NetworkCacheModeClear(object sender, RoutedEventArgs e)
        {
            if (sender is not Button asButton)
            {
                return;
            }

            try
            {
                asButton.IsEnabled = false;
                CDNCacheUtil.PerformCacheGarbageCollection(CDNCacheUtil.CurrentCacheDir, true);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Cannot clear CDN cache!\r\n{ex}", LogType.Error, true);
            }
            finally
            {
                NetworkCacheModeClearTextSuccess.Visibility = Visibility.Visible;
                await Task.Delay(TimeSpan.FromSeconds(1));
                asButton.IsEnabled                          = true;
                NetworkCacheModeClearTextSuccess.Visibility = Visibility.Collapsed;
            }
        }
        #endregion
    }
}
