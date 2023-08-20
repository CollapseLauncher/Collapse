using CollapseLauncher.Dialogs;
using CollapseLauncher.Interfaces;
using CollapseLauncher.Statics;
using CommunityToolkit.WinUI.UI.Controls;
using Hi3Helper;
using Hi3Helper.Preset;
using Hi3Helper.Screen;
using Hi3Helper.Shared.ClassStruct;
#if !DISABLEDISCORD
using Hi3Helper.DiscordPresence;
#endif
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Win32;
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Windows.UI.Text;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public sealed partial class HomePage : Page
    {
        #region Properties
        private GamePresetProperty CurrentGameProperty { get; set; }
        private HomeMenuPanel MenuPanels { get => regionNewsProp; }
        private CancellationTokenSource PageToken { get; init; }
        private CancellationTokenSource CarouselToken { get; set; }
        private CancellationTokenSource PlaytimeToken { get; set; }
        #endregion

        #region PageMethod
        public HomePage()
        {
            CurrentGameProperty = GamePropertyVault.GetCurrentGameProperty();
            PageToken = new CancellationTokenSource();
            CarouselToken = new CancellationTokenSource();
            PlaytimeToken = new CancellationTokenSource();
            this.InitializeComponent();
            CheckIfRightSideProgress();
            this.Loaded += StartLoadedRoutine;
        }

        private bool IsPageUnload { get; set; }
        private bool NeedShowEventIcon = true;

        private void ReturnToHomePage()
        {
            if (!IsPageUnload
             || GamePropertyVault.GetCurrentGameProperty()._GamePreset.HashID == CurrentGameProperty._GamePreset.HashID)
            {
                MainPage.PreviousTagString.Add(MainPage.PreviousTag);
                MainFrameChanger.ChangeMainFrame(typeof(HomePage));
            }
        }

        private async void StartLoadedRoutine(object sender, RoutedEventArgs e)
        {
            try
            {
                BackgroundImgChanger.ToggleBackground(false);
                GetCurrentGameState();

                if (!GetAppConfigValue("ShowEventsPanel").ToBool())
                    ImageCarouselAndPostPanel.Visibility = Visibility.Collapsed;

                if (!GetAppConfigValue("ShowSocialMediaPanel").ToBool())
                {
                    SocMedPanel.Visibility = Visibility.Collapsed;
                    ImageEventImgGrid.Visibility = Visibility.Collapsed;
                }

                TryLoadEventPanelImage();

                SocMedPanel.Translation += Shadow48;
                LauncherBtn.Translation += Shadow32;
                GameStartupSetting.Translation += Shadow32;
                CommunityToolsBtn.Translation += Shadow32;
                PlaytimeBtn.Translation += Shadow32;

                if (MenuPanels.imageCarouselPanel != null
                    && MenuPanels.articlePanel != null)
                {
                    ImageCarousel.SelectedIndex = 0;
                    ShowEventsPanelToggle.IsEnabled = true;
                    ImageCarousel.Visibility = Visibility.Visible;
                    ImageCarouselPipsPager.Visibility = Visibility.Visible;
                    PostPanel.Visibility = Visibility.Visible;
                    ImageCarousel.Translation += Shadow48;
                    ImageCarouselPipsPager.Translation += Shadow16;
                    PostPanel.Translation += Shadow48;
                }

                if (await CurrentGameProperty._GameInstall.TryShowFailedDeltaPatchState()) return;
                if (await CurrentGameProperty._GameInstall.TryShowFailedGameConversionState()) return;

                CheckRunningGameInstance(PageToken.Token);
                AutoUpdatePlaytimeCounter(false, PlaytimeToken.Token);

                StartCarouselAutoScroll(CarouselToken.Token);
            }
            catch (ArgumentNullException ex)
            {
                LogWriteLine($"The necessary section of Launcher Scope's config.ini is broken.\r\n{ex}", LogType.Error, true);
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            IsPageUnload = true;
            PageToken.Cancel();
            CarouselToken.Cancel();
            PlaytimeToken.Cancel();
        }
        #endregion

        #region EventPanel
        private void TryLoadEventPanelImage()
        {
            if (regionNewsProp.eventPanel == null) return;

            ImageEventImgGrid.Visibility = !NeedShowEventIcon ? Visibility.Collapsed : Visibility.Visible;
            ImageEventImg.Source = new BitmapImage(new Uri(regionNewsProp.eventPanel.icon));
            ImageEventImg.Tag = regionNewsProp.eventPanel.url;

            if (IsCustomBG)
            {
                ImageEventImgGrid.Margin = new Thickness(0, 0, 0, 16);
            }
        }
        #endregion

        #region Carousel
        private async void StartCarouselAutoScroll(CancellationToken token = new CancellationToken(), int delay = 5)
        {
            if (MenuPanels.imageCarouselPanel == null) return;
            try
            {
                while (true)
                {
                    await Task.Delay(delay * 1000, token);
                    if (MenuPanels.imageCarouselPanel == null) return;
                    if (ImageCarousel.SelectedIndex != MenuPanels.imageCarouselPanel.Count - 1)
                        ImageCarousel.SelectedIndex++;
                    else
                        for (int i = MenuPanels.imageCarouselPanel.Count; i > 0; i--)
                        {
                            ImageCarousel.SelectedIndex = i - 1;
                            await Task.Delay(100, token);
                        }
                }
            }
            catch (Exception) { }
        }

        private void CarouselStopScroll(object sender, PointerRoutedEventArgs e) => CarouselToken.Cancel();

        private void CarouselRestartScroll(object sender, PointerRoutedEventArgs e)
        {
            CarouselToken = new CancellationTokenSource();
            StartCarouselAutoScroll(CarouselToken.Token);
        }

        private async void HideImageCarousel(bool hide)
        {
            if (!hide)
                ImageCarouselAndPostPanel.Visibility = Visibility.Visible;

            Storyboard storyboard = new Storyboard();
            DoubleAnimation OpacityAnimation = new DoubleAnimation();
            OpacityAnimation.From = hide ? 1 : 0;
            OpacityAnimation.To = hide ? 0 : 1;
            OpacityAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.10));

            Storyboard.SetTarget(OpacityAnimation, ImageCarouselAndPostPanel);
            Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
            storyboard.Children.Add(OpacityAnimation);

            storyboard.Begin();

            await Task.Delay(100);

            ImageCarouselAndPostPanel.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
        }
        #endregion

        #region SocMed Buttons
        private void FadeInSocMedButton(object sender, PointerRoutedEventArgs e)
        {
            Storyboard sb = ((Button)sender).Resources["EnterStoryboard"] as Storyboard;
            ((Button)sender).Translation += Shadow16;
            sb.Begin();
        }

        private void FadeOutSocMedButton(object sender, PointerRoutedEventArgs e)
        {
            Storyboard sb = ((Button)sender).Resources["ExitStoryboard"] as Storyboard;
            ((Button)sender).Translation -= Shadow16;
            sb.Begin();
        }

        private async void HideSocialMediaPanel(bool hide)
        {
            HideImageEventImg(hide);
            if (!hide)
            {
                SocMedPanel.Visibility = Visibility.Visible;
            }

            Storyboard storyboard = new Storyboard();
            DoubleAnimation OpacityAnimation = new DoubleAnimation();
            OpacityAnimation.From = hide ? 1 : 0;
            OpacityAnimation.To = hide ? 0 : 1;
            OpacityAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.10));

            Storyboard.SetTarget(OpacityAnimation, SocMedPanel);
            Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
            storyboard.Children.Add(OpacityAnimation);

            storyboard.Begin();

            await Task.Delay(100);

            SocMedPanel.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OpenSocMedLink(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(((Button)sender).Tag.ToString())) return;

            new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    FileName = ((Button)sender).Tag.ToString()
                }
            }.Start();
        }
        #endregion

        #region Event Image
        private async void HideImageEventImg(bool hide)
        {
            if (!NeedShowEventIcon) return;

            if (!hide)
                ImageEventImgGrid.Visibility = Visibility.Visible;

            Storyboard storyboard = new Storyboard();
            DoubleAnimation OpacityAnimation = new DoubleAnimation();
            OpacityAnimation.From = hide ? 1 : 0;
            OpacityAnimation.To = hide ? 0 : 1;
            OpacityAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.10));

            Storyboard.SetTarget(OpacityAnimation, ImageEventImgGrid);
            Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
            storyboard.Children.Add(OpacityAnimation);

            storyboard.Begin();

            await Task.Delay(100);

            ImageEventImgGrid.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
        }
        #endregion

        #region Open Link from Tag
        private void OpenImageLinkFromTag(object sender, PointerRoutedEventArgs e)
        {
            SpawnWebView2.SpawnWebView2Window(((ImageEx)sender).Tag.ToString());
        }

        private async void OpenButtonLinkFromTag(object sender, RoutedEventArgs e)
        {
            // Get the tag string.
            string tagContent = ((ButtonBase)sender).Tag.ToString();

            // Split the tag string by $ character as separator.
            string[] tagProperty = tagContent.Split('$');

            // If the tagProperty has more than 1 array (that means it has tag action property),
            // then generate the tag action and execute it.
            if (tagProperty.Length > 1)
            {
                // Check if the tag has "OpenUrlIfCancel". This will be used to check if the
                // tag action is getting cancelled, then open the URL of the tag.
                bool isOpenUrlIfCancel = tagProperty.Contains("OpenUrlIfCancel");

                // Build the tag Task to be executed. The action will return boolean as the result.
                //    true          => Task has been executed successfully.
                //    false         => Task was cancelled or error has occurred.
                //    Task<null>    => Means the tag action property failed to be deserialized caused by
                //                     invalid tag or parameter/argument.
                Task<bool> action = TryBuildTagPropertyAction(tagProperty[1]);

                // If the action returns a null task (Task<null>), then fallback to open the URL instead.
                if (action == null)
                {
                    LogWriteLine($"Tag Property seems to be invalid or incomplete. Failback to open the URL instead!\r\nTag String: {tagProperty[1]}", LogType.Warning, true);
                    SpawnWebView2.SpawnWebView2Window(tagProperty[0]);
                    return;
                }

                // Await and run the tag action task and put the action result to isActionCompleted
                bool isActionCompleted = await action;
                // If the action is true (successfully executed), then return
                if (isActionCompleted) return;
                // If the action is false (failed/cancel) and doesn't have "OpenUrlIfCancel" tag, then return
                // Otherwise, fallback to open the URL.
                if (!isOpenUrlIfCancel) return;
            }

            // Open the URL and spawn WebView2 window
            SpawnWebView2.SpawnWebView2Window(tagProperty[0]);
        }

        private void OpenLinkFromButtonWithTag(object sender, RoutedEventArgs e)
        {
            object ImageTag = ((Button)sender).Tag;
            if (ImageTag == null) return;
            SpawnWebView2.SpawnWebView2Window((string)ImageTag);
        }

        private void ClickImageEventSpriteLink(object sender, PointerRoutedEventArgs e)
        {
            object ImageTag = ((Image)sender).Tag;
            if (ImageTag == null) return;
            SpawnWebView2.SpawnWebView2Window((string)ImageTag);
        }
        #endregion

        #region Tag Property
        private Task<bool> TryBuildTagPropertyAction(string tagProperty)
        {
            try
            {
                // Split the property string by : mark to get the tag action type and its parameter.
                string[] property = tagProperty.Split(':');
                // If the property array has less than 2, then return null to fallback (open the URL).
                if (property.Length < 2) return null;

                // Check the tag action type
                switch (property[0].ToLower())
                {
                    case "openexternalapp":
                        return TagPropertyAction_OpenExternalApp(property[1]);
                }
            }
            // If the error has occured, then return null to fallback (open the URL).
            catch (Exception ex)
            {
                LogWriteLine($"Failed while parsing Tag Property: {tagProperty}!\r\n{ex}", LogType.Warning, true);
            }
            return null;
        }

        private async Task<bool> TagPropertyAction_OpenExternalApp(string propertiesString)
        {
            // Split the properties string by , mark to get the argument string.
            string[] properties = propertiesString.Split(',');
            // Initialize the application properties
            string applicationName = "";
            string applicationExecName = "";
            bool runAsAdmin = false;

            // If the properties array is empty, then throw and back to fallback (open the URL).
            if (properties.Length == 0) throw new ArgumentNullException("Properties for OpenExternalApp can't be empty!");

            // Iterate the properties array
            for (int i = 0; i < properties.Length; i++)
            {
                // Split the property by = mark to get the argument and its value.
                string[] argumentStr = properties[i].Split("=");

                // If the argument array is empty, then throw and back to fallback (open the URL).
                if (argumentStr.Length == 0) throw new ArgumentNullException("Argument can't be empty!");

                // Check the argument type
                switch (argumentStr[0].ToLower())
                {
                    case "applicationexecname":
                        // If the value is empty, then throw and back to fallback (open the URL).
                        if (argumentStr.Length < 2) throw new ArgumentException($"Argument error on {argumentStr[0]}: Executable name must be defined!");

                        // Get the application executable 
                        applicationExecName = argumentStr[1];
# if DEBUG
                        LogWriteLine($"Got '{argumentStr[0]}' as parameter for application executable", LogType.Debug, true);
# endif
                        break;
                    case "applicationname":
                        // Get the application name. If it's empty, then fallback to default "MyApplication" name.
                        // Else, return the defined application name.
                        applicationName = argumentStr.Length < 2 || string.IsNullOrEmpty(argumentStr[1]) ? "MyApplication" : argumentStr[1];
#if DEBUG
                        LogWriteLine($"Got '{argumentStr[1]}' as parameter for application name", LogType.Debug, true);
# endif
                        break;
                    case "runasadmin":
                        // Try parse the boolean value. If it's valid and the value of runAsAdmin is true, then set
                        // runAsAdmin as true and "AND" it with isBoolValid.
                        bool isBoolValid = bool.TryParse(argumentStr[1], out runAsAdmin);
                        runAsAdmin = isBoolValid && runAsAdmin;
#if DEBUG
                        LogWriteLine($"Got '{isBoolValid}' as parameter for application executable", LogType.Debug, true);
# endif
                        break;
                    default:
                        // If the argument type is unknown, then throw and back to fallback (open the URL).
                        throw new ArgumentException($"Argument {argumentStr[0]} is unknown!");
                }
            }

            // Trim the space in the application name to be used for app config.
            string applicationNameTrimmed = applicationName.Replace(" ", "");
            // If the RunAsAdmin config key doesn't exist, then create one.
            if (!IsConfigKeyExist($"Exec_RunAsAdmin_{applicationNameTrimmed}")) SetAndSaveConfigValue($"Exec_RunAsAdmin_{applicationNameTrimmed}", runAsAdmin);

            // Check if the Application path config is exist. If not, then return empty string. Otherwise, return the actual value.
            string applicationPath = IsConfigKeyExist($"Exec_Path_{applicationNameTrimmed}") ? GetAppConfigValue($"Exec_Path_{applicationNameTrimmed}").ToString() : "";
            // Check if the applicationPath variable is empty or if the application path in applicationPath variable.
            // If the variable is empty or the path is not exist, then spawn File Picker dialog.
            if (string.IsNullOrEmpty(applicationPath) || !File.Exists(applicationPath))
            {
                // Run the loop
                while (true)
                {
                    // Set initial value to null
                    string file = null;
                    switch (await Dialog_OpenExecutable(Content))
                    {
                        case ContentDialogResult.Primary:
                            // Try get the file path
                            file = await FileDialogNative.GetFilePicker(new Dictionary<string, string> { { applicationName, applicationExecName } }, string.Format(Lang._HomePage.CommunityToolsBtn_OpenExecutableAppDialogTitle, applicationName));
                            // If the file returns null because of getting cancelled, then back to loop again.
                            if (string.IsNullOrEmpty(file)) continue;
                            // Otherwise, assign the value to applicationPath variable and save it to the app config
                            applicationPath = file;
                            SetAndSaveConfigValue($"Exec_Path_{applicationNameTrimmed}", file);
                            break;
                        case ContentDialogResult.Secondary:
                            // If the main dialog is getting cancelled, then return false (as cancel and fallback to URL [if enabled]).
                            return false;
                        case ContentDialogResult.None:
                            // Return true when cancelled
                            return true;
                    }

                    // If the file variable is not null anymore, then break from the loop and continue
                    // the call below.
                    if (!string.IsNullOrEmpty(file)) break;
                }
            }

            try
            {
                // Try run the application
                Process proc = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        Verb = runAsAdmin ? "runas" : "",
                        FileName = applicationPath,
                        WorkingDirectory = Path.GetDirectoryName(applicationPath),
                    }
                };
                proc.Start();
            }
            catch (Exception ex)
            {
                // If error happened while running the app, then log and return true as successful
                // Thoughts @Cyr0? Should we mark it as successful (true) or failed (false)?
                LogWriteLine($"Unable to start app {applicationName}! {ex}", LogType.Error, true);
                return true;
            }

            // If all above are executed successfully, then return true as "successful"
            return true;
        }
        #endregion

        #region Right Side Progress
        private void CheckIfRightSideProgress()
        {
            if (CurrentGameProperty._GameVersion.GamePreset.UseRightSideProgress ?? false)
            {
                FrameGrid.ColumnDefinitions[0].Width = new GridLength(248, GridUnitType.Pixel);
                FrameGrid.ColumnDefinitions[1].Width = new GridLength(1224, GridUnitType.Star);
                LauncherBtn.SetValue(Grid.ColumnProperty, 0);
                ProgressStatusGrid.SetValue(Grid.ColumnProperty, 0);
                GameStartupSetting.SetValue(Grid.ColumnProperty, 1);
                GameStartupSetting.HorizontalAlignment = HorizontalAlignment.Right;
            }
        }
        #endregion

        #region Game State
        private void GetCurrentGameState()
        {
            Visibility RepairGameButtonVisible = (CurrentGameProperty._GameVersion.GamePreset.IsRepairEnabled ?? false) ? Visibility.Visible : Visibility.Collapsed;

            if ((!(CurrentGameProperty._GameVersion.GamePreset.IsConvertible ?? false)) || (CurrentGameProperty._GameVersion.GameType != GameType.Honkai))
                ConvertVersionButton.Visibility = Visibility.Collapsed;

            // Clear the _CommunityToolsProperty statics
            PageStatics._CommunityToolsProperty.Clear();

            // Check if the _CommunityToolsProperty has the official tool list for current game type
            if (PageStatics._CommunityToolsProperty.OfficialToolsDictionary.ContainsKey(CurrentGameProperty._GameVersion.GameType))
            {
                // If yes, then iterate it and add it to the list, to then getting read by the
                // DataTemplate from HomePage
                foreach (CommunityToolsEntry iconProperty in PageStatics._CommunityToolsProperty.OfficialToolsDictionary[CurrentGameProperty._GameVersion.GameType])
                {
                    PageStatics._CommunityToolsProperty.OfficialToolsList.Add(iconProperty);
                }
            }

            // Check if the _CommunityToolsProperty has the community tool list for current game type
            if (PageStatics._CommunityToolsProperty.CommunityToolsDictionary.ContainsKey(CurrentGameProperty._GameVersion.GameType))
            {
                // If yes, then iterate it and add it to the list, to then getting read by the
                // DataTemplate from HomePage
                foreach (CommunityToolsEntry iconProperty in PageStatics._CommunityToolsProperty.CommunityToolsDictionary[CurrentGameProperty._GameVersion.GameType])
                {
                    PageStatics._CommunityToolsProperty.CommunityToolsList.Add(iconProperty);
                }
            }

            if (CurrentGameProperty._GameVersion.GameType == GameType.Genshin) OpenCacheFolderButton.Visibility = Visibility.Collapsed;

            GameInstallationState = CurrentGameProperty._GameVersion.GetGameState();
            switch (GameInstallationState)
            {
                case GameInstallStateEnum.Installed:
                    {
                        RepairGameButton.Visibility = RepairGameButtonVisible;
                        InstallGameBtn.Visibility = Visibility.Collapsed;
                        StartGameBtn.Visibility = Visibility.Visible;
                        CustomStartupArgs.Visibility = Visibility.Visible;
                    }
                    return;
                case GameInstallStateEnum.InstalledHavePreload:
                    {
                        RepairGameButton.Visibility = RepairGameButtonVisible;
                        CustomStartupArgs.Visibility = Visibility.Visible;
                        InstallGameBtn.Visibility = Visibility.Collapsed;
                        StartGameBtn.Visibility = Visibility.Visible;
                        NeedShowEventIcon = false;
                        SpawnPreloadBox();
                    }
                    return;
                case GameInstallStateEnum.NeedsUpdate:
                    {
                        RepairGameButton.Visibility = RepairGameButtonVisible;
                        RepairGameButton.IsEnabled = false;
                        UpdateGameBtn.Visibility = Visibility.Visible;
                        StartGameBtn.Visibility = Visibility.Collapsed;
                        InstallGameBtn.Visibility = Visibility.Collapsed;
                    }
                    return;
            }

            if ((GameInstallationState == GameInstallStateEnum.NeedsUpdate
             || GameInstallationState == GameInstallStateEnum.GameBroken
             || GameInstallationState == GameInstallStateEnum.NotInstalled)
             && CurrentGameProperty._GameInstall.IsRunning)
            {
                if (CurrentGameProperty._GameVersion.GamePreset.UseRightSideProgress ?? false)
                    HideImageCarousel(true);

                progressRing.Value = 0;
                progressRing.IsIndeterminate = true;
                ProgressStatusGrid.Visibility = Visibility.Visible;
                InstallGameBtn.Visibility = Visibility.Collapsed;
                CancelDownloadBtn.Visibility = Visibility.Visible;
                ProgressTimeLeft.Visibility = Visibility.Visible;

                CurrentGameProperty._GameInstall.ProgressChanged += GameInstall_ProgressChanged;
                CurrentGameProperty._GameInstall.StatusChanged += GameInstall_StatusChanged;
            }

            UninstallGameButton.IsEnabled = false;
            RepairGameButton.IsEnabled = false;
            OpenGameFolderButton.IsEnabled = false;
            OpenCacheFolderButton.IsEnabled = false;
            ConvertVersionButton.IsEnabled = false;
            CustomArgsTextBox.IsEnabled = false;
            OpenScreenshotFolderButton.IsEnabled = false;
        }

        private async void CheckRunningGameInstance(CancellationToken Token)
        {
            FontFamily FF = Application.Current.Resources["FontAwesomeSolid"] as FontFamily;
            VerticalAlignment TVAlign = VerticalAlignment.Center;
            Orientation SOrient = Orientation.Horizontal;
            Thickness Margin = new Thickness(0, -2, 8, 0);
            Thickness SMargin = new Thickness(16, 0, 16, 0);
            FontWeight FW = FontWeights.Medium;
            string Gl = "";

            StackPanel BtnStartGame = new StackPanel() { Orientation = SOrient, Margin = SMargin };
            BtnStartGame.Children.Add(new TextBlock() { FontWeight = FW, Margin = Margin, VerticalAlignment = TVAlign, Text = Lang._HomePage.StartBtn });
            BtnStartGame.Children.Add(new TextBlock() { FontFamily = FF, Text = Gl, FontSize = 18 });

            StackPanel BtnRunningGame = new StackPanel() { Orientation = SOrient, Margin = SMargin };
            BtnRunningGame.Children.Add(new TextBlock() { FontWeight = FW, Margin = Margin, VerticalAlignment = TVAlign, Text = Lang._HomePage.StartBtnRunning });
            BtnRunningGame.Children.Add(new TextBlock() { FontFamily = FF, Text = Gl, FontSize = 18 });

            try
            {
                while (!Token.IsCancellationRequested)
                {
                    while (App.IsGameRunning)
                    {
                        if (StartGameBtn.IsEnabled)
                            LauncherBtn.Translation -= Shadow16;

                        StartGameBtn.IsEnabled = false;
                        StartGameBtn.Content = BtnRunningGame;

                        //GameStartupSetting.IsEnabled = false;
                        RepairGameButton.IsEnabled = false;
                        UninstallGameButton.IsEnabled = false;
                        ConvertVersionButton.IsEnabled = false;
                        CustomArgsTextBox.IsEnabled = false;
                        StopGameButton.IsEnabled = true;

                        PlaytimeIdleStack.Visibility = Visibility.Collapsed;
                        PlaytimeRunningStack.Visibility = Visibility.Visible;

                        await Task.Delay(100, Token);
#if !DISABLEDISCORD
                        AppDiscordPresence.SetActivity(ActivityType.Play, 0);
#endif
                    }

                    if (!StartGameBtn.IsEnabled)
                        LauncherBtn.Translation += Shadow16;

                    StartGameBtn.IsEnabled = true;
                    StartGameBtn.Content = BtnStartGame;

                    //GameStartupSetting.IsEnabled = true;
                    RepairGameButton.IsEnabled = true;
                    UninstallGameButton.IsEnabled = true;
                    ConvertVersionButton.IsEnabled = true;
                    CustomArgsTextBox.IsEnabled = true;
                    StopGameButton.IsEnabled = false;

                    PlaytimeIdleStack.Visibility = Visibility.Visible;
                    PlaytimeRunningStack.Visibility = Visibility.Collapsed;

                    await Task.Delay(100, Token);
#if !DISABLEDISCORD
                    AppDiscordPresence.SetActivity(ActivityType.Idle, 0);
#endif
                }
            }
            catch { return; }
        }

        #endregion

        #region Community Button
        private void OpenCommunityButtonLink(object sender, RoutedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() => CommunityToolsBtn.Flyout.Hide());
            OpenButtonLinkFromTag(sender, e);
        }
        #endregion

        #region Preload
        private async void SpawnPreloadBox()
        {
            if (CurrentGameProperty._GameInstall.IsRunning)
            {
                // TODO
                PauseDownloadPreBtn.Visibility = Visibility.Visible;
                ResumeDownloadPreBtn.Visibility = Visibility.Collapsed;
                PreloadDialogBox.IsClosable = false;
                PreloadDialogBox.Margin = new Thickness(0, 0, 0, -32);

                IsSkippingUpdateCheck = true;
                DownloadPreBtn.Visibility = Visibility.Collapsed;
                ProgressPreStatusGrid.Visibility = Visibility.Visible;
                ProgressPreButtonGrid.Visibility = Visibility.Visible;
                PreloadDialogBox.Title = Lang._HomePage.PreloadDownloadNotifbarTitle;
                PreloadDialogBox.Message = Lang._HomePage.PreloadDownloadNotifbarSubtitle;

                CurrentGameProperty._GameInstall.ProgressChanged += PreloadDownloadProgress;
                CurrentGameProperty._GameInstall.StatusChanged += PreloadDownloadStatus;
                PreloadDialogBox.IsOpen = true;
                return;
            }

            PreloadDialogBox.Translation += Shadow48;
            PreloadDialogBox.Closed += PreloadDialogBox_Closed;

            string ver = CurrentGameProperty._GameVersion.GetGameVersionAPIPreload()?.VersionString;

            try
            {
                if (CurrentGameProperty._GameVersion.IsGameHasDeltaPatch())
                {
                    PreloadDialogBox.Title = string.Format(Lang._HomePage.PreloadNotifDeltaDetectTitle, ver);
                    PreloadDialogBox.Message = Lang._HomePage.PreloadNotifDeltaDetectSubtitle;
                    DownloadPreBtn.Visibility = Visibility.Collapsed;
                    PreloadDialogBox.IsOpen = true;
                    return;
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"An error occured while trying to determine delta-patch availability\r\n{ex}", LogType.Error, true);
            }

            if (!await CurrentGameProperty._GameInstall.IsPreloadCompleted())
            {
                PreloadDialogBox.Title = string.Format(Lang._HomePage.PreloadNotifTitle, ver);
            }
            else
            {
                PreloadDialogBox.Title = Lang._HomePage.PreloadNotifCompleteTitle;
                PreloadDialogBox.Message = string.Format(Lang._HomePage.PreloadNotifCompleteSubtitle, ver);
                PreloadDialogBox.IsClosable = true;

                StackPanel Text = new StackPanel { Orientation = Orientation.Horizontal };
                Text.Children.Add(
                    new FontIcon
                    {
                        Glyph = "",
                        FontFamily = (FontFamily)Application.Current.Resources["FontAwesomeSolid"],
                        FontSize = 16
                    });

                Text.Children.Add(
                    new TextBlock
                    {
                        Text = Lang._HomePage.PreloadNotifIntegrityCheckBtn,
                        FontWeight = FontWeights.Medium,
                        Margin = new Thickness(8, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    });

                DownloadPreBtn.Content = Text;
            }
            PreloadDialogBox.IsOpen = true;
        }

        private async void PredownloadDialog(object sender, RoutedEventArgs e)
        {
            ((Button)sender).IsEnabled = false;

            PauseDownloadPreBtn.Visibility = Visibility.Visible;
            ResumeDownloadPreBtn.Visibility = Visibility.Collapsed;
            PreloadDialogBox.IsClosable = false;
            PreloadDialogBox.Margin = new Thickness(0, 0, 0, -32);
            // While this fixes #191, we need to find a way to move all elements above it by at least 16

            try
            {
                IsSkippingUpdateCheck = true;
                DownloadPreBtn.Visibility = Visibility.Collapsed;
                ProgressPreStatusGrid.Visibility = Visibility.Visible;
                ProgressPreButtonGrid.Visibility = Visibility.Visible;
                PreloadDialogBox.Title = Lang._HomePage.PreloadDownloadNotifbarTitle;
                PreloadDialogBox.Message = Lang._HomePage.PreloadDownloadNotifbarSubtitle;

                CurrentGameProperty._GameInstall.ProgressChanged += PreloadDownloadProgress;
                CurrentGameProperty._GameInstall.StatusChanged += PreloadDownloadStatus;

                int verifResult = 0;
                while (verifResult != 1)
                {
                    await CurrentGameProperty._GameInstall.StartPackageDownload(true);

                    PauseDownloadPreBtn.IsEnabled = false;
                    PreloadDialogBox.Title = Lang._HomePage.PreloadDownloadNotifbarVerifyTitle;

                    verifResult = await CurrentGameProperty._GameInstall.StartPackageVerification();

                    if (verifResult == -1)
                    {
                        ReturnToHomePage();
                        return;
                    }
                    if (verifResult == 1)
                    {
                        await Dialog_PreDownloadPackageVerified(this);
                        ReturnToHomePage();
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LogWriteLine($"Pre-Download paused!", LogType.Warning);
            }
            finally
            {
                IsSkippingUpdateCheck = false;
                CurrentGameProperty._GameInstall.ProgressChanged -= PreloadDownloadProgress;
                CurrentGameProperty._GameInstall.StatusChanged -= PreloadDownloadStatus;
                CurrentGameProperty._GameInstall.Flush();
            }
        }

        private void PreloadDownloadStatus(object sender, TotalPerfileStatus e)
        {
            DispatcherQueue.TryEnqueue(() => ProgressPrePerFileStatusFooter.Text = e.ActivityStatus);
        }

        private void PreloadDownloadProgress(object sender, TotalPerfileProgress e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                string InstallDownloadSpeedString = SummarizeSizeSimple(e.ProgressTotalSpeed);
                string InstallDownloadSizeString = SummarizeSizeSimple(e.ProgressTotalDownload);
                string InstallDownloadPerSizeString = SummarizeSizeSimple(e.ProgressPerFileDownload);
                string DownloadSizeString = SummarizeSizeSimple(e.ProgressTotalSizeToDownload);
                string DownloadPerSizeString = SummarizeSizeSimple(e.ProgressPerFileSizeToDownload);

                ProgressPreStatusSubtitle.Text = string.Format(Lang._Misc.PerFromTo, InstallDownloadSizeString, DownloadSizeString);
                ProgressPrePerFileStatusSubtitle.Text = string.Format(Lang._Misc.PerFromTo, InstallDownloadPerSizeString, DownloadPerSizeString);
                ProgressPreStatusFooter.Text = string.Format(Lang._Misc.Speed, InstallDownloadSpeedString);
                ProgressPreTimeLeft.Text = string.Format(Lang._Misc.TimeRemainHMSFormat, e.ProgressTotalTimeLeft);
                progressPreBar.Value = Math.Round(e.ProgressTotalPercentage, 2);
                progressPrePerFileBar.Value = Math.Round(e.ProgressPerFilePercentage, 2);
                progressPreBar.IsIndeterminate = false;
                progressPrePerFileBar.IsIndeterminate = false;
            });
        }

        private void PreloadDialogBox_Closed(InfoBar sender, InfoBarClosedEventArgs args)
        {
            sender.Translation -= Shadow48;
            HideImageEventImg(false);
        }
        #endregion

        #region Button Animator
        private void AnimateGameRegSettingIcon_Start(object sender, PointerRoutedEventArgs e) => AnimatedIcon.SetState(this.GameRegionSettingIcon, "PointerOver");
        private void AnimateGameRegSettingIcon_End(object sender, PointerRoutedEventArgs e) => AnimatedIcon.SetState(this.GameRegionSettingIcon, "Normal");
        #endregion

        #region Game Install
        private async void InstallGameDialog(object sender, RoutedEventArgs e)
        {
            try
            {
                IsSkippingUpdateCheck = true;

                if (CurrentGameProperty._GameVersion.GamePreset.UseRightSideProgress ?? false)
                    HideImageCarousel(true);

                progressRing.Value = 0;
                progressRing.IsIndeterminate = true;
                ProgressStatusGrid.Visibility = Visibility.Visible;
                InstallGameBtn.Visibility = Visibility.Collapsed;
                CancelDownloadBtn.Visibility = Visibility.Visible;
                ProgressTimeLeft.Visibility = Visibility.Visible;

                CurrentGameProperty._GameInstall.ProgressChanged += GameInstall_ProgressChanged;
                CurrentGameProperty._GameInstall.StatusChanged += GameInstall_StatusChanged;

                int dialogResult = await CurrentGameProperty._GameInstall.GetInstallationPath();
                if (dialogResult < 0)
                {
                    return;
                }
                if (dialogResult == 0)
                {
                    CurrentGameProperty._GameInstall.ApplyGameConfig();
                    return;
                }

                int verifResult;
                bool skipDialog = false;
                while ((verifResult = await CurrentGameProperty._GameInstall.StartPackageVerification()) == 0)
                {
                    await CurrentGameProperty._GameInstall.StartPackageDownload(skipDialog);
                    skipDialog = true;
                }
                if (verifResult == -1)
                {
                    CurrentGameProperty._GameInstall.ApplyGameConfig(true);
                    return;
                }

                await CurrentGameProperty._GameInstall.StartPackageInstallation();
                await CurrentGameProperty._GameInstall.StartPostInstallVerification();
                CurrentGameProperty._GameInstall.ApplyGameConfig(true);
            }
            catch (TaskCanceledException)
            {
                LogWriteLine($"Installation cancelled for game {CurrentGameProperty._GameVersion.GamePreset.ZoneFullname}");
            }
            catch (OperationCanceledException)
            {
                LogWriteLine($"Installation cancelled for game {CurrentGameProperty._GameVersion.GamePreset.ZoneFullname}");
            }
            catch (NullReferenceException ex)
            {
                IsPageUnload = true;
                LogWriteLine($"Error while installing game {CurrentGameProperty._GameVersion.GamePreset.ZoneName}\r\n{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(new NullReferenceException("Collapse was not able to complete post-installation tasks, but your game has been successfully updated.\r\t" +
                    $"Please report this issue to our GitHub here: https://github.com/neon-nyan/CollapseLauncher/issues/new or come back to the launcher and make sure to use Repair Game in Game Settings button later.\r\nThrow: {ex}", ex));
            }
            catch (Exception ex)
            {
                IsPageUnload = true;
                LogWriteLine($"Error while installing game {CurrentGameProperty._GameVersion.GamePreset.ZoneName}.\r\n{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex, ErrorType.Unhandled);
            }
            finally
            {
                IsSkippingUpdateCheck = false;
                CurrentGameProperty._GameInstall.ProgressChanged -= GameInstall_ProgressChanged;
                CurrentGameProperty._GameInstall.StatusChanged -= GameInstall_StatusChanged;
                CurrentGameProperty._GameInstall.Flush();
                ReturnToHomePage();
            }
        }

        private void GameInstall_StatusChanged(object sender, TotalPerfileStatus e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressStatusTitle.Text = e.ActivityStatus;
                progressPerFile.Visibility = e.IsIncludePerFileIndicator ? Visibility.Visible : Visibility.Collapsed;

                progressRing.IsIndeterminate = e.IsProgressTotalIndetermined;
                progressRingPerFile.IsIndeterminate = e.IsProgressPerFileIndetermined;
            });
        }

        private void GameInstall_ProgressChanged(object sender, TotalPerfileProgress e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                progressRing.Value = e.ProgressTotalPercentage;
                progressRingPerFile.Value = e.ProgressPerFilePercentage;
                ProgressStatusSubtitle.Text = string.Format(Lang._Misc.PerFromTo, SummarizeSizeSimple(e.ProgressTotalDownload), SummarizeSizeSimple(e.ProgressTotalSizeToDownload));
                ProgressStatusFooter.Text = string.Format(Lang._Misc.Speed, SummarizeSizeSimple(e.ProgressTotalSpeed));
                ProgressTimeLeft.Text = string.Format(Lang._Misc.TimeRemainHMSFormat, e.ProgressTotalTimeLeft);
            });
        }

        private void CancelInstallationProcedure(object sender, RoutedEventArgs e)
        {
            switch (GameInstallationState)
            {
                case GameInstallStateEnum.NeedsUpdate:
                    CancelUpdateDownload();
                    break;
                case GameInstallStateEnum.InstalledHavePreload:
                    CancelPreDownload();
                    break;
                case GameInstallStateEnum.NotInstalled:
                case GameInstallStateEnum.GameBroken:
                case GameInstallStateEnum.Installed:
                    CancelInstallationDownload();
                    break;
            }
        }
        #endregion

        #region Download Cancellation
        private void CancelPreDownload()
        {
            CurrentGameProperty._GameInstall.CancelRoutine();

            PauseDownloadPreBtn.Visibility = Visibility.Collapsed;
            ResumeDownloadPreBtn.Visibility = Visibility.Visible;
            ResumeDownloadPreBtn.IsEnabled = true;
        }

        private void CancelUpdateDownload()
        {
            CurrentGameProperty._GameInstall.CancelRoutine();

            ProgressStatusGrid.Visibility = Visibility.Collapsed;
            UpdateGameBtn.Visibility = Visibility.Visible;
            CancelDownloadBtn.Visibility = Visibility.Collapsed;
        }

        private void CancelInstallationDownload()
        {
            CurrentGameProperty._GameInstall.CancelRoutine();

            ProgressStatusGrid.Visibility = Visibility.Collapsed;
            InstallGameBtn.Visibility = Visibility.Visible;
            CancelDownloadBtn.Visibility = Visibility.Collapsed;
        }

        CancellationTokenSource WatchOutputLog = new CancellationTokenSource();
        #endregion

        #region Game Start/Stop Method
        private async void StartGame(object sender, RoutedEventArgs e)
        {
            try
            {
                bool IsContinue = await CheckMediaPackInstalled();

                if (!IsContinue) return;
                Process proc = new Process();
                proc.StartInfo.FileName = Path.Combine(NormalizePath(GameDirPath), CurrentGameProperty._GameVersion.GamePreset.GameExecutableName);
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.Arguments = GetLaunchArguments();
                LogWriteLine($"Running game with parameters:\r\n{proc.StartInfo.Arguments}");
                proc.StartInfo.WorkingDirectory = Path.GetDirectoryName(NormalizePath(GameDirPath));
                proc.StartInfo.Verb = "runas";
                proc.Start();

                WatchOutputLog = new CancellationTokenSource();

                if (GetAppConfigValue("EnableConsole").ToBool())
                {
                    ReadOutputLog();
                    GameLogWatcher();
                }

                StartPlaytimeCounter(CurrentGameProperty._GameVersion.GamePreset.ConfigRegistryLocation, proc, CurrentGameProperty._GameVersion.GamePreset);
                AutoUpdatePlaytimeCounter(true, PlaytimeToken.Token);

            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                LogWriteLine($"There is a problem while trying to launch Game with Region: {CurrentGameProperty._GameVersion.GamePreset.ZoneName}\r\nTraceback: {ex}", LogType.Error, true);
            }
        }

        private void StopGame(PresetConfigV2 gamePreset)
        {
            try
            {
                var gameProcess = Process.GetProcessesByName(gamePreset.GameExecutableName.Split('.')[0]);
                foreach (var p in gameProcess)
                {
                    LogWriteLine($"Trying to stop game process {gamePreset.GameExecutableName.Split('.')[0]} at PID {p.Id}", LogType.Scheme, true);
                    p.Kill();
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                LogWriteLine($"There is a problem while trying to stop Game with Region: {CurrentGameProperty._GameVersion.GamePreset.ZoneName}\r\nTraceback: {ex}", LogType.Error, true);
            }
        }
        #endregion

        #region Game Launch Argument Builder
        bool RequireWindowExclusivePayload = false;
        public string GetLaunchArguments()
        {
            StringBuilder parameter = new StringBuilder();

            IGameSettingsUniversal _Settings = CurrentGameProperty._GameSettings.AsIGameSettingsUniversal();
            if (CurrentGameProperty._GameVersion.GameType == GameType.Honkai)
            {
                if (_Settings.SettingsCollapseScreen.UseExclusiveFullscreen)
                {
                    parameter.Append("-window-mode exclusive ");
                    RequireWindowExclusivePayload = true;
                }

                System.Drawing.Size screenSize = _Settings.SettingsScreen.sizeRes;

                byte apiID = _Settings.SettingsCollapseScreen.GameGraphicsAPI;

                if (apiID == 4)
                {
                    LogWriteLine($"You are going to use DX12 mode in your game.\r\n\tUsing CustomScreenResolution or FullscreenExclusive value may break the game!", LogType.Warning);
                    if (_Settings.SettingsCollapseScreen.UseCustomResolution && _Settings.SettingsScreen.isfullScreen)
                        parameter.AppendFormat("-screen-width {0} -screen-height {1} ", ScreenProp.GetScreenSize().Width, ScreenProp.GetScreenSize().Height);
                    else
                        parameter.AppendFormat("-screen-width {0} -screen-height {1} ", screenSize.Width, screenSize.Height);
                }
                else
                    parameter.AppendFormat("-screen-width {0} -screen-height {1} ", screenSize.Width, screenSize.Height);

                switch (apiID)
                {
                    case 0:
                        parameter.Append("-force-feature-level-10-1 ");
                        break;
                    default:
                    case 1:
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
            }
            if (CurrentGameProperty._GameVersion.GameType == GameType.StarRail)
            {
                if (_Settings.SettingsCollapseScreen.UseExclusiveFullscreen)
                {
                    parameter.Append("-window-mode exclusive -screen-fullscreen 1 ");
                    RequireWindowExclusivePayload = true;
                }

                System.Drawing.Size screenSize = _Settings.SettingsScreen.sizeRes;

                byte apiID = _Settings.SettingsCollapseScreen.GameGraphicsAPI;

                if (apiID == 4)
                {
                    LogWriteLine($"You are going to use DX12 mode in your game.\r\n\tUsing CustomScreenResolution or FullscreenExclusive value may break the game!", LogType.Warning);
                    if (_Settings.SettingsCollapseScreen.UseCustomResolution && _Settings.SettingsScreen.isfullScreen)
                        parameter.AppendFormat("-screen-width {0} -screen-height {1} ", ScreenProp.GetScreenSize().Width, ScreenProp.GetScreenSize().Height);
                    else
                        parameter.AppendFormat("-screen-width {0} -screen-height {1} ", screenSize.Width, screenSize.Height);
                }
                else
                    parameter.AppendFormat("-screen-width {0} -screen-height {1} ", screenSize.Width, screenSize.Height);
            }
            if (CurrentGameProperty._GameVersion.GameType == GameType.Genshin)
            {
                if (_Settings.SettingsCollapseScreen.UseExclusiveFullscreen)
                {
                    parameter.Append("-window-mode exclusive -screen-fullscreen 1 ");
                    RequireWindowExclusivePayload = true;
                    LogWriteLine($"Exclusive mode is enabled in Genshin Impact, stability may suffer!\r\nTry not to Alt+Tab when game is on its loading screen :)", LogType.Warning, true);
                }

                System.Drawing.Size screenSize = _Settings.SettingsScreen.sizeRes;

                byte apiID = _Settings.SettingsCollapseScreen.GameGraphicsAPI;

                if (apiID == 4)
                {
                    LogWriteLine($"You are going to use DX12 mode in your game.\r\n\tUsing CustomScreenResolution or FullscreenExclusive value may break the game!", LogType.Warning);
                    if (_Settings.SettingsCollapseScreen.UseCustomResolution && _Settings.SettingsScreen.isfullScreen)
                        parameter.AppendFormat("-screen-width {0} -screen-height {1} ", ScreenProp.GetScreenSize().Width, ScreenProp.GetScreenSize().Height);
                    else
                        parameter.AppendFormat("-screen-width {0} -screen-height {1} ", screenSize.Width, screenSize.Height);
                }
                else
                    parameter.AppendFormat("-screen-width {0} -screen-height {1} ", screenSize.Width, screenSize.Height);
            }

            if (_Settings.SettingsCollapseScreen.UseBorderlessScreen)
            {
                parameter.Append("-popupwindow ");
            }
            string customArgs = _Settings.SettingsCustomArgument.CustomArgumentValue;

            if (!string.IsNullOrEmpty(customArgs))
                parameter.Append(customArgs);

            return parameter.ToString();
        }

        public string CustomArgsValue
        {
            get => ((IGameSettingsUniversal)CurrentGameProperty._GameSettings).SettingsCustomArgument.CustomArgumentValue;
            set => ((IGameSettingsUniversal)CurrentGameProperty._GameSettings).SettingsCustomArgument.CustomArgumentValue = value;
        }
        #endregion

        #region Media Pack
        public async Task<bool> CheckMediaPackInstalled()
        {
            if (CurrentGameProperty._GameVersion.GameType != GameType.Honkai) return true;

            RegistryKey reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\WindowsFeatures\WindowsMediaVersion");
            if (reg != null)
                return true;

            switch (await Dialog_NeedInstallMediaPackage(Content))
            {
                case ContentDialogResult.Primary:
                    TryInstallMediaPack();
                    break;
                case ContentDialogResult.Secondary:
                    return true;
            }

            return false;
        }

        public async void TryInstallMediaPack()
        {
            try
            {
                Process proc = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(AppFolder, "Misc", "InstallMediaPack.cmd"),
                        UseShellExecute = true,
                        Verb = "runas"
                    }
                };

                ShowLoadingPage.ShowLoading(Lang._Dialogs.InstallingMediaPackTitle, Lang._Dialogs.InstallingMediaPackSubtitle);
                MainFrameChanger.ChangeMainFrame(typeof(BlankPage));
                proc.Start();
                await proc.WaitForExitAsync();
                ShowLoadingPage.ShowLoading(Lang._Dialogs.InstallingMediaPackTitle, Lang._Dialogs.InstallingMediaPackSubtitleFinished);
                await Dialog_InstallMediaPackageFinished(Content);
                MainFrameChanger.ChangeWindowFrame(typeof(MainPage));
            }
            catch { }
        }
        #endregion

        #region Exclusive Window Payload
        public async void StartExclusiveWindowPayload()
        {
            IntPtr _windowPtr = InvokeProp.GetProcessWindowHandle(CurrentGameProperty._GameVersion.GamePreset.GameExecutableName);
            await Task.Delay(1000);
            new InvokeProp.InvokePresence(_windowPtr).HideWindow();
            await Task.Delay(1000);
            new InvokeProp.InvokePresence(_windowPtr).ShowWindow();
        }
        #endregion

        #region Game Log Method
        public async void ReadOutputLog()
        {
            int consoleWidth = 24;
            try { consoleWidth = Console.BufferWidth; } catch { }

            string line;
            int barwidth = ((consoleWidth - 22) / 2) - 1;
            LogWriteLine($"Are Game logs getting saved to Collapse logs: {GetAppConfigValue("IncludeGameLogs").ToBool()}", LogType.Scheme, true);
            LogWriteLine($"{new string('=', barwidth)} GAME STARTED {new string('=', barwidth)}", LogType.Warning, true);
            try
            {
                m_presenter.Minimize();
                string logPath = Path.Combine(CurrentGameProperty._GameVersion.GameDirAppDataPath, CurrentGameProperty._GameVersion.GameOutputLogName);

                if (!Directory.Exists(Path.GetDirectoryName(logPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath));

                using (FileStream fs = new FileStream(logPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite))
                using (StreamReader reader = new StreamReader(fs))
                {
                    while (true)
                    {
                        while (!reader.EndOfStream)
                        {
                            line = await reader.ReadLineAsync(WatchOutputLog.Token);
                            if (RequireWindowExclusivePayload && line == "MoleMole.MonoGameEntry:Awake()")
                            {
                                StartExclusiveWindowPayload();
                                RequireWindowExclusivePayload = false;
                            }
                            LogWriteLine(line, LogType.Game, GetAppConfigValue("IncludeGameLogs").ToBool());
                        }
                        await Task.Delay(100, WatchOutputLog.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LogWriteLine($"{new string('=', barwidth)} GAME STOPPED {new string('=', barwidth)}", LogType.Warning, true);
                m_presenter.Restore();
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", LogType.Error);
            }
        }

        private async void GameLogWatcher()
        {
            await Task.Delay(5000);
            while (App.IsGameRunning)
            {
                await Task.Delay(3000);
            }

            WatchOutputLog.Cancel();
        }
        #endregion

        #region Open Button Method
        private void OpenGameFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string GameFolder = NormalizePath(GameDirPath);
            LogWriteLine($"Opening Game Folder:\r\n\t{GameFolder}");
            new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    FileName = "explorer.exe",
                    Arguments = GameFolder
                }
            }.Start();
        }

        private void OpenCacheFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string GameFolder = CurrentGameProperty._GameVersion.GameDirAppDataPath;
            LogWriteLine($"Opening Game Folder:\r\n\t{GameFolder}");
            new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    FileName = "explorer.exe",
                    Arguments = GameFolder
                }
            }.Start();
        }

        private void OpenScreenshotFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string ScreenshotFolder = Path.Combine(NormalizePath(GameDirPath), CurrentGameProperty._GameVersion.GamePreset.GameType switch
            {
                GameType.StarRail => $"{Path.GetFileNameWithoutExtension(CurrentGameProperty._GameVersion.GamePreset.GameExecutableName)}_Data\\ScreenShots",
                _ => "ScreenShot"
            });

            LogWriteLine($"Opening Screenshot Folder:\r\n\t{ScreenshotFolder}");

            if (!Directory.Exists(ScreenshotFolder))
                Directory.CreateDirectory(ScreenshotFolder);

            new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    FileName = "explorer.exe",
                    Arguments = ScreenshotFolder
                }
            }.Start();
        }
        #endregion

        #region Game Management Buttons
        private void RepairGameButton_Click(object sender, RoutedEventArgs e)
        {
            m_mainPage.InvokeMainPageNavigateByTag("repair");
        }

        private async void UninstallGameButton_Click(object sender, RoutedEventArgs e)
        {
            if (await CurrentGameProperty._GameInstall.UninstallGame())
            {
                MainFrameChanger.ChangeMainFrame(typeof(HomePage));
            }
        }

        private void ConvertVersionButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrameChanger.ChangeWindowFrame(typeof(InstallationConvert));
        }

        private async void StopGameButton_Click(object sender, RoutedEventArgs e)
        {
           if (await Dialog_StopGame(this) != ContentDialogResult.Primary) return;
           StopGame(CurrentGameProperty._GameVersion.GamePreset);
        }
        #endregion

        #region Playtime Buttons
        private void ForceUpdatePlaytimeButton_Click(object sender, RoutedEventArgs e)
        {
            if (!App.IsGameRunning)
            {
                UpdatePlaytime();
            }
        }

        private async void ChangePlaytimeButton_Click(object sender, RoutedEventArgs e)
        {
            if (await Dialog_ChangePlaytime(this) != ContentDialogResult.Primary) return;

            int playtimemins = int.Parse("0" + MinutePlaytimeTextBox.Text);
            int playtimehours = int.Parse("0" + HourPlaytimeTextBox.Text);
            int FinalPlaytimeMinutes = playtimemins % 60;
            int FinalPlaytimeHours = playtimehours + playtimemins / 60;
            if (FinalPlaytimeHours > 99999) { FinalPlaytimeHours = 99999; FinalPlaytimeMinutes = 59; }
            MinutePlaytimeTextBox.Text = FinalPlaytimeMinutes.ToString();
            HourPlaytimeTextBox.Text = FinalPlaytimeHours.ToString();

            int FinalPlaytime = FinalPlaytimeHours * 3600 + FinalPlaytimeMinutes * 60;

            SavePlaytimetoRegistry(CurrentGameProperty._GameVersion.GamePreset.ConfigRegistryLocation, FinalPlaytime);
            LogWriteLine($"Playtime counter changed to {HourPlaytimeTextBox.Text + "h " + MinutePlaytimeTextBox.Text + "m"}. (Previous value: {PlaytimeMainBtn.Text})");
            UpdatePlaytime(false, FinalPlaytime);
            PlaytimeFlyout.Hide();
        }

        private async void ResetPlaytimeButton_Click(object sender, RoutedEventArgs e)
        {
            if (await Dialog_ResetPlaytime(this) != ContentDialogResult.Primary) return;

            SavePlaytimetoRegistry(CurrentGameProperty._GameVersion.GamePreset.ConfigRegistryLocation, 0);
            LogWriteLine($"Playtime counter changed to 0h 0m 0s. (Previous value: {PlaytimeMainBtn.Text})");
            UpdatePlaytime(false, 0);
            PlaytimeFlyout.Hide();
        }

        private void NumberValidationTextBox(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            sender.MaxLength = sender == HourPlaytimeTextBox ? 5 : 3;
            args.Cancel = args.NewText.Any(c => !char.IsDigit(c));
        }
        #endregion

        #region Playtime Tracker Method
        private void UpdatePlaytime(bool reg = true, int CPtV = 0)
        {
            int CurrentPlaytimeValue = reg ? ReadPlaytimeFromRegistry(CurrentGameProperty._GameVersion.GamePreset.ConfigRegistryLocation) : CPtV;
            HourPlaytimeTextBox.Text = (CurrentPlaytimeValue / 3600).ToString();
            MinutePlaytimeTextBox.Text = (CurrentPlaytimeValue % 3600 / 60).ToString();
            PlaytimeMainBtn.Text = HourPlaytimeTextBox.Text + "h " + MinutePlaytimeTextBox.Text + "m";
        }

        private static int ReadPlaytimeFromRegistry(string RegionRegKey)
        {
            try
            {
                return (int)Registry.CurrentUser.OpenSubKey(RegionRegKey, true).GetValue("CollapseLauncher_Playtime", 0);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Playtime - There was an error reading from the registry. \n {ex}");
                return 0;
            }

        }

        private static void SavePlaytimetoRegistry(string RegionRegKey, int value)
        {
            try
            {
                Registry.CurrentUser.OpenSubKey(RegionRegKey, true).SetValue("CollapseLauncher_Playtime", value, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Playtime - There was an error writing to registry. \n {ex}");
            }
        }

        private async static void StartPlaytimeCounter(string oldRegionRegistryKey, Process proc, PresetConfigV2 gamePreset)
        {
            int saveFrequencyinSeconds = 60;

            int currentPlaytime = ReadPlaytimeFromRegistry(oldRegionRegistryKey);
            int elapsedSeconds = 0;

            var inGameTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            inGameTimer.Tick += (o, e) =>
            {
                elapsedSeconds++;

                if (elapsedSeconds % saveFrequencyinSeconds == 0)
                {
                    //LogWriteLine($"Added \"fake\" {saveFrequencyinSeconds} seconds to {OldRegionRK.Split('\\')[2]} playtime.", LogType.Default, true);
                    SavePlaytimetoRegistry(oldRegionRegistryKey, currentPlaytime + elapsedSeconds);
                }
            };
            inGameTimer.Start();

            await proc.WaitForExitAsync();
            if (gamePreset.GameType == GameType.Honkai)
            {
                try
                {
                    while (true)
                    {
                        Process[] pname = Process.GetProcessesByName(gamePreset.GameExecutableName.Split('.')[0]);
                        switch (pname.Length)
                        {
                            case 0:
                                break;
                            case 1:
                                proc = pname[0];
                                LogWriteLine($"Found the main HI3 process [{pname[0].Id}]");
                                await proc.WaitForExitAsync();
                                break;
                            default:
                                await Task.Delay(5000);
                                continue;
                        }
                        break;
                    }

                }
                catch (Exception e)
                {
                    LogWriteLine($"Failed to find the main BH3 process [{e}]");
                }
            }
            SavePlaytimetoRegistry(oldRegionRegistryKey, currentPlaytime + elapsedSeconds);
            LogWriteLine($"Added {elapsedSeconds}s [{elapsedSeconds / 60}m {elapsedSeconds % 60}s] seconds to {oldRegionRegistryKey.Split('\\')[2]} playtime.", LogType.Default, true);
            inGameTimer.Stop();
        }

        private async void AutoUpdatePlaytimeCounter(bool bootByCollapse = false, CancellationToken token = new CancellationToken())
        {
            string regionKey = CurrentGameProperty._GameVersion.GamePreset.ConfigRegistryLocation;
            int oldTime = ReadPlaytimeFromRegistry(regionKey);
            UpdatePlaytime(false, oldTime);

            bool dynamicUpdate = true;

            try
            {
                await Task.Delay(2000, token);

                if (!dynamicUpdate)
                {
                    while (App.IsGameRunning) { }
                    UpdatePlaytime();
                    return;
                }

                int elapsedSeconds = 0;

                if (bootByCollapse)
                {
                    while (App.IsGameRunning)
                    {
                        await Task.Delay(60000, token);
                        elapsedSeconds += 60;
                        UpdatePlaytime(false, oldTime + elapsedSeconds);
                    }
                    UpdatePlaytime();
                    return;
                }

                if (App.IsGameRunning)
                {
                    await Task.Delay(60000, token);
                    int newTime = ReadPlaytimeFromRegistry(regionKey);
                    if (newTime == oldTime) return;
                    //int CurrentSeconds = int.Parse(Newtime.Split(' ')[2].Split('s')[0]) * 1000;
                    //await Task.Delay(60000 - CurrentSeconds, token);

                }

                while (App.IsGameRunning)
                {
                    UpdatePlaytime(false, oldTime + elapsedSeconds);
                    elapsedSeconds += 60;
                    await Task.Delay(60000, token);
                }

                UpdatePlaytime();
            }

            catch
            {
            }
        }
        #endregion

        #region Game Update Dialog
        private async void UpdateGameDialog(object sender, RoutedEventArgs e)
        {
            CurrentGameProperty._GameInstall.ProgressChanged += GameInstall_ProgressChanged;
            CurrentGameProperty._GameInstall.StatusChanged += GameInstall_StatusChanged;

            if (CurrentGameProperty._GameVersion.GamePreset.UseRightSideProgress ?? false)
                HideImageCarousel(true);

            try
            {
                ProgressStatusGrid.Visibility = Visibility.Visible;
                UpdateGameBtn.Visibility = Visibility.Collapsed;
                CancelDownloadBtn.Visibility = Visibility.Visible;

                int verifResult;
                bool skipDialog = false;
                while ((verifResult = await CurrentGameProperty._GameInstall.StartPackageVerification()) == 0)
                {
                    await CurrentGameProperty._GameInstall.StartPackageDownload(skipDialog);
                    skipDialog = true;
                }
                if (verifResult == -1)
                {
                    return;
                }

                await CurrentGameProperty._GameInstall.StartPackageInstallation();
                await CurrentGameProperty._GameInstall.StartPostInstallVerification();
                CurrentGameProperty._GameInstall.ApplyGameConfig(true);
            }
            catch (TaskCanceledException)
            {
                LogWriteLine($"Update cancelled!", LogType.Warning);
            }
            catch (OperationCanceledException)
            {
                LogWriteLine($"Update cancelled!", LogType.Warning);
            }
            catch (NullReferenceException ex)
            {
                IsPageUnload = true;
                LogWriteLine($"Update error on {CurrentGameProperty._GameVersion.GamePreset.ZoneFullname} game!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(new NullReferenceException("Oops, the launcher cannot finalize the installation but don't worry, your game has been totally updated.\r\t" +
                    $"Please report this issue to our GitHub here: https://github.com/neon-nyan/CollapseLauncher/issues/new or come back to the launcher and make sure to use Repair Game in Game Settings button later.\r\nThrow: {ex}", ex));
            }
            catch (Exception ex)
            {
                IsPageUnload = true;
                LogWriteLine($"Update error on {CurrentGameProperty._GameVersion.GamePreset.ZoneFullname} game!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
            finally
            {
                CurrentGameProperty._GameInstall.ProgressChanged -= GameInstall_ProgressChanged;
                CurrentGameProperty._GameInstall.StatusChanged -= GameInstall_StatusChanged;
                CurrentGameProperty._GameInstall.Flush();
                ReturnToHomePage();
            }
        }
        #endregion
    }
}