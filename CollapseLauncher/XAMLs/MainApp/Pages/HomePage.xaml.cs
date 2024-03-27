using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.FileDialogCOM;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Interfaces;
using CollapseLauncher.Statics;
using CollapseLauncher.ShortcutUtils;
using Hi3Helper;
using Hi3Helper.Preset;
using Hi3Helper.Screen;
using Hi3Helper.Shared.ClassStruct;
#if !DISABLEDISCORD
using Hi3Helper.DiscordPresence;
#endif
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Win32;
using PhotoSauce.MagicScaler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Text;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.RegionResourceListHelper;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using Brush = Microsoft.UI.Xaml.Media.Brush;
using FontFamily = Microsoft.UI.Xaml.Media.FontFamily;
using Image = Microsoft.UI.Xaml.Controls.Image;
using Orientation = Microsoft.UI.Xaml.Controls.Orientation;
using Hi3Helper.EncTool.WindowTool;

namespace CollapseLauncher.Pages
{
    public sealed partial class HomePage : Page
    {
        #region Properties
        private GamePresetProperty CurrentGameProperty { get; set; }
        private HomeMenuPanel MenuPanels { get => regionNewsProp; }
        private CancellationTokenSource PageToken { get; set; }
        private CancellationTokenSource CarouselToken { get; set; }

        private int barwidth;
        private int consoleWidth;
        
        public static  int RefreshRateDefault { get; } = 200;
        public static  int RefreshRateSlow    { get; } = 1000;

        private static int _refreshRate;

        /// <summary>
        /// Holds the value for how long a checks needs to be delayed before continuing the loop in miliseconds.
        /// Default : 200 (Please set it using RefrehRateDefault instead)
        /// </summary>
        public static int RefreshRate
        {
            get => _refreshRate;
            set
            {
#if DEBUG
                LogWriteLine($"HomePage Refresh Rate changed to {value}", LogType.Debug, true);
#endif
                _refreshRate = value;
            }
        }

        /// <summary>
        /// Hold cached state for IsGameRunning. The state is controlled inside CheckRunningGameInstance() method.
        /// </summary>
        public static bool _cachedIsGameRunning { get; set; }
        #endregion

        #region PageMethod
        public HomePage()
        {
            RefreshRate = RefreshRateDefault;
            this.Loaded += StartLoadedRoutine;

            m_homePage  =  this;
            InitializeConsoleValues();
        }

        ~HomePage()
        {
            // HACK: Fix random crash by always unsubscribing the StartLoadedRoutine if the GC is calling the deconstructor.
            this.Loaded -= StartLoadedRoutine;
        }

        private void InitializeConsoleValues()
        {
            consoleWidth = 24;
            try { consoleWidth = Console.BufferWidth; } catch { }
            barwidth = ((consoleWidth - 22) / 2) - 1;
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
                // HACK: Fix random crash by manually load the XAML part
                //       But first, let it initialize its properties.
                CurrentGameProperty = GamePropertyVault.GetCurrentGameProperty();
                PageToken = new CancellationTokenSource();
                CarouselToken = new CancellationTokenSource();

                this.InitializeComponent();

                BackgroundImgChanger.ToggleBackground(false);
                CheckIfRightSideProgress();
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
                    || MenuPanels.articlePanel != null)
                {
                    ImageCarousel.SelectedIndex = 0;
                    ImageCarousel.Visibility = Visibility.Visible;
                    ImageCarouselPipsPager.Visibility = Visibility.Visible;
                    ImageCarousel.Translation += Shadow48;
                    ImageCarouselPipsPager.Translation += Shadow16;

                    ShowEventsPanelToggle.IsEnabled = true;
                    PostPanel.Visibility = Visibility.Visible;
                    PostPanel.Translation += Shadow48;
                }

                if (await CurrentGameProperty._GameInstall.TryShowFailedDeltaPatchState()) return;
                if (await CurrentGameProperty._GameInstall.TryShowFailedGameConversionState()) return;

                UpdatePlaytime();
                UpdateLastPlayed();
                CheckRunningGameInstance(PageToken.Token);
                StartCarouselAutoScroll(CarouselToken.Token);

                if (m_arguments.StartGame?.Play != true)
                    return;

                m_arguments.StartGame.Play = false;

                if (CurrentGameProperty.IsGameRunning)
                    return;

                if (CurrentGameProperty._GameInstall.IsRunning)
                {
                    CurrentGameProperty._GameInstall.StartAfterInstall = CurrentGameProperty._GameInstall.IsRunning;
                    return;
                }

                switch (CurrentGameProperty._GameVersion.GetGameState())
                {
                    case GameInstallStateEnum.InstalledHavePreload:
                    case GameInstallStateEnum.Installed:
                        StartGame(null, null);
                        break;
                    case GameInstallStateEnum.InstalledHavePlugin:
                    case GameInstallStateEnum.NeedsUpdate:
                        CurrentGameProperty._GameInstall.StartAfterInstall = true;
                        UpdateGameDialog(null, null);
                        break;
                    case GameInstallStateEnum.NotInstalled:
                    case GameInstallStateEnum.GameBroken:
                        CurrentGameProperty._GameInstall.StartAfterInstall = true;
                        InstallGameDialog(null, null);
                        break;
                }
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
        }
        #endregion

        #region EventPanel
        private async void TryLoadEventPanelImage()
        {
            // If the region event panel property is null, then return
            if (regionNewsProp.eventPanel == null) return;

            // Get the cached filename and path
            string cachedFileHash = BytesToCRC32Simple(regionNewsProp.eventPanel.icon);
            string cachedFilePath = Path.Combine(AppGameImgCachedFolder, cachedFileHash);
            if (ImageLoaderHelper.IsWaifu2XEnabled)
                cachedFilePath += "_waifu2x";

            // Create a cached image folder if not exist
            if (!Directory.Exists(AppGameImgCachedFolder))
                Directory.CreateDirectory(AppGameImgCachedFolder);

            // Init BitmapImage to load the image and the info for cached event icon file
            BitmapImage source = new BitmapImage();
            FileInfo cachedIconFileInfo = new FileInfo(cachedFilePath);

            // Determine if the cache icon exist and the file is completed (more than 1kB in size)
            bool isCacheIconExist = cachedIconFileInfo.Exists && cachedIconFileInfo.Length > 1 << 10;

            // Using the original icon file and cached icon file streams
            if (!isCacheIconExist)
            {
                using (Stream cachedIconFileStream = cachedIconFileInfo.Create())
                using (Stream copyIconFileStream = new MemoryStream())
                await using (Stream iconFileStream = await FallbackCDNUtil.GetHttpStreamFromResponse(regionNewsProp.eventPanel.icon, PageToken.Token))
                {
                    // Copy remote stream to memory stream
                    await iconFileStream.CopyToAsync(copyIconFileStream);
                    copyIconFileStream.Position = 0;
                    // Get the icon image information and set the resized frame size
                    ImageFileInfo iconImageInfo = await Task.Run(() => ImageFileInfo.Load(copyIconFileStream));
                    int width = (int)(iconImageInfo.Frames[0].Width * m_appDPIScale);
                    int height = (int)(iconImageInfo.Frames[0].Height * m_appDPIScale);

                    copyIconFileStream.Position = 0; // Reset the original icon stream position
                    await ImageLoaderHelper.ResizeImageStream(copyIconFileStream, cachedIconFileStream, (uint)width, (uint)height); // Start resizing
                    cachedIconFileStream.Position = 0; // Reset the cached icon stream position

                    // Set the source from cached icon stream
                    source.SetSource(cachedIconFileStream.AsRandomAccessStream());
                }
            }
            else
            {
                using (Stream cachedIconFileStream = cachedIconFileInfo.OpenRead())
                {
                    // Set the source from cached icon stream
                    source.SetSource(cachedIconFileStream.AsRandomAccessStream());
                }
            }

            // Set event icon props
            ImageEventImgGrid.Visibility = !NeedShowEventIcon ? Visibility.Collapsed : Visibility.Visible;
            ImageEventImg.Source = source;
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

        public void CarouselStopScroll(object sender = null, PointerRoutedEventArgs e = null) => CarouselToken.Cancel();

        public void CarouselRestartScroll(object sender = null, PointerRoutedEventArgs e = null)
        {
            // Don't restart carousel if game is running and LoPrio is on
            if (!_cachedIsGameRunning || !GetAppConfigValue("LowerCollapsePrioOnGameLaunch").ToBool())
            {
                CarouselToken = new CancellationTokenSource();
                StartCarouselAutoScroll(CarouselToken.Token);
            }
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
            Button btn = (Button)sender;
            if (btn.Translation != Vector3.Zero || e.OriginalSource is Image) return;
            Storyboard sb = btn.Resources["EnterStoryboard"] as Storyboard;
            btn.Translation += Shadow16;
            sb.Begin();
        }

        private void FadeOutSocMedButton(object sender, PointerRoutedEventArgs e)
        {
            Button btn = (Button)sender;
            if (btn.Translation == Vector3.Zero || e.OriginalSource is Image) return;
            Storyboard sb = btn.Resources["ExitStoryboard"] as Storyboard;
            btn.Translation -= Shadow16;
            sb.Begin();

            Flyout flyout = btn.Resources["SocMedFlyout"] as Flyout;
            Point pos = e.GetCurrentPoint(btn).Position;
            if (pos.Y <= 0 || pos.Y >= btn.Height || pos.X <= -8 || pos.X >= btn.Width)
            {
                flyout.Hide();
            }
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

        private void ShowSocMedFlyout(object sender, RoutedEventArgs e)
        {
            ToolTip tooltip = sender as ToolTip;
            FlyoutBase.ShowAttachedFlyout(tooltip.Tag as FrameworkElement);
        }

        private void HideSocMedFlyout(object sender, RoutedEventArgs e)
        {
            Flyout flyout = ((StackPanel)sender).Tag as Flyout;
            flyout.Hide();
        }

        private void OnLoadedSocMedFlyout(object sender, RoutedEventArgs e)
        {
            // Prevent the flyout showing when there is no content visible
            StackPanel stackPanel = sender as StackPanel;
            bool visible = false;
            foreach (var child in stackPanel.Children)
            {
                if (child.Visibility == Visibility.Visible)
                    visible = true;
            }
            if (!visible)
            {
                HideSocMedFlyout(sender, e);
            }
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
            if (!e.GetCurrentPoint((UIElement)sender).Properties.IsLeftButtonPressed) return;
            SpawnWebView2.SpawnWebView2Window(((ImageEx.ImageEx)sender).Tag.ToString(), this.Content);
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
                    SpawnWebView2.SpawnWebView2Window(tagProperty[0], this.Content);
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
            SpawnWebView2.SpawnWebView2Window(tagProperty[0], this.Content);
        }

        private void OpenLinkFromButtonWithTag(object sender, RoutedEventArgs e)
        {
            object ImageTag = ((Button)sender).Tag;
            if (ImageTag == null) return;
            SpawnWebView2.SpawnWebView2Window((string)ImageTag, this.Content);
        }

        private void ClickImageEventSpriteLink(object sender, PointerRoutedEventArgs e)
        {
            if (!e.GetCurrentPoint((UIElement)sender).Properties.IsLeftButtonPressed) return;
            object ImageTag = ((Image)sender).Tag;
            if (ImageTag == null) return;
            SpawnWebView2.SpawnWebView2Window((string)ImageTag, this.Content);
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
                    if (iconProperty.Profiles.Contains(CurrentGameProperty._GamePreset.ProfileName))
                    {
                        PageStatics._CommunityToolsProperty.OfficialToolsList.Add(iconProperty);
                    }
                }
            }

            // Check if the _CommunityToolsProperty has the community tool list for current game type
            if (PageStatics._CommunityToolsProperty.CommunityToolsDictionary.ContainsKey(CurrentGameProperty._GameVersion.GameType))
            {
                // If yes, then iterate it and add it to the list, to then getting read by the
                // DataTemplate from HomePage
                foreach (CommunityToolsEntry iconProperty in PageStatics._CommunityToolsProperty.CommunityToolsDictionary[CurrentGameProperty._GameVersion.GameType])
                {
                    if (iconProperty.Profiles.Contains(CurrentGameProperty._GamePreset.ProfileName))
                    {
                        PageStatics._CommunityToolsProperty.CommunityToolsList.Add(iconProperty);
                    }
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
                    break;
                case GameInstallStateEnum.InstalledHavePreload:
                    {
                        RepairGameButton.Visibility = RepairGameButtonVisible;
                        CustomStartupArgs.Visibility = Visibility.Visible;
                        InstallGameBtn.Visibility = Visibility.Collapsed;
                        StartGameBtn.Visibility = Visibility.Visible;
                        NeedShowEventIcon = false;
                        SpawnPreloadBox();
                    }
                    break;
                case GameInstallStateEnum.NeedsUpdate:
                case GameInstallStateEnum.InstalledHavePlugin:
                    {
                        RepairGameButton.Visibility = RepairGameButtonVisible;
                        RepairGameButton.IsEnabled = false;
                        UpdateGameBtn.Visibility = Visibility.Visible;
                        StartGameBtn.Visibility = Visibility.Collapsed;
                        InstallGameBtn.Visibility = Visibility.Collapsed;
                    }
                    break;
                default:
                    {
                        UninstallGameButton.IsEnabled = false;
                        RepairGameButton.IsEnabled = false;
                        OpenGameFolderButton.IsEnabled = false;
                        OpenCacheFolderButton.IsEnabled = false;
                        ConvertVersionButton.IsEnabled = false;
                        CustomArgsTextBox.IsEnabled = false;
                        OpenScreenshotFolderButton.IsEnabled = false;
                        ConvertVersionButton.Visibility = Visibility.Collapsed;
                        RepairGameButton.Visibility = Visibility.Collapsed;
                        UninstallGameButton.Visibility = Visibility.Collapsed;
                        MoveGameLocationButton.Visibility = Visibility.Collapsed;
                    }
                    break;
            }

            if (CurrentGameProperty._GameInstall.IsRunning)
                RaiseBackgroundInstallationStatus(GameInstallationState);
        }

        private void RaiseBackgroundInstallationStatus(GameInstallStateEnum GameInstallationState)
        {
            if (GameInstallationState == GameInstallStateEnum.NeedsUpdate
             || GameInstallationState == GameInstallStateEnum.InstalledHavePlugin
             || GameInstallationState == GameInstallStateEnum.GameBroken
             || GameInstallationState == GameInstallStateEnum.NotInstalled)
            {
                if (CurrentGameProperty._GameVersion.GamePreset.UseRightSideProgress ?? false)
                    HideImageCarousel(true);

                progressRing.Value = 0;
                progressRing.IsIndeterminate = true;
                ProgressStatusGrid.Visibility = Visibility.Visible;
                InstallGameBtn.Visibility = Visibility.Collapsed;
                UpdateGameBtn.Visibility = Visibility.Collapsed;
                CancelDownloadBtn.Visibility = Visibility.Visible;
                ProgressTimeLeft.Visibility = Visibility.Visible;

                CurrentGameProperty._GameInstall.ProgressChanged += GameInstall_ProgressChanged;
                CurrentGameProperty._GameInstall.StatusChanged += GameInstall_StatusChanged;
            }
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
                    while (CurrentGameProperty.IsGameRunning)
                    {
                        _cachedIsGameRunning = true;

                        if (StartGameBtn.IsEnabled)
                            LauncherBtn.Translation -= Shadow16;

                        StartGameBtn.IsEnabled = false;
                        StartGameBtn.Content = BtnRunningGame;

                        //GameStartupSetting.IsEnabled = false;
                        RepairGameButton.IsEnabled = false;
                        UninstallGameButton.IsEnabled = false;
                        ConvertVersionButton.IsEnabled = false;
                        CustomArgsTextBox.IsEnabled = false;
                        MoveGameLocationButton.IsEnabled = false;
                        StopGameButton.IsEnabled = true;

                        PlaytimeIdleStack.Visibility = Visibility.Collapsed;
                        PlaytimeRunningStack.Visibility = Visibility.Visible;

#if !DISABLEDISCORD
                        AppDiscordPresence.SetActivity(ActivityType.Play);
#endif
                        
                        await Task.Delay(RefreshRate, Token);
                    }

                    _cachedIsGameRunning = false;

                    if (!StartGameBtn.IsEnabled)
                        LauncherBtn.Translation += Shadow16;

                    StartGameBtn.IsEnabled = true;
                    StartGameBtn.Content = BtnStartGame;

                    //GameStartupSetting.IsEnabled = true;
                    RepairGameButton.IsEnabled = true;
                    MoveGameLocationButton.IsEnabled = true;
                    UninstallGameButton.IsEnabled = true;
                    ConvertVersionButton.IsEnabled = true;
                    CustomArgsTextBox.IsEnabled = true;
                    StopGameButton.IsEnabled = false;

                    PlaytimeIdleStack.Visibility = Visibility.Visible;
                    PlaytimeRunningStack.Visibility = Visibility.Collapsed;
                    
#if !DISABLEDISCORD
                    AppDiscordPresence.SetActivity(ActivityType.Idle);
#endif

                    await Task.Delay(RefreshRate, Token);
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

                if (!await CurrentGameProperty._GameInstall.IsPreloadCompleted(PageToken.Token))
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
            catch (Exception ex)
            {
                LogWriteLine($"An error occured while trying to determine delta-patch availability\r\n{ex}", LogType.Error, true);
            }
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
                CurrentGameProperty._GameInstall.ApplyGameConfig(true);
                if (CurrentGameProperty._GameInstall.StartAfterInstall && CurrentGameProperty._GameVersion.IsGameInstalled())
                    StartGame(null, null);
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
                    $"Please report this issue to our GitHub here: https://github.com/CollapseLauncher/Collapse/issues/new or come back to the launcher and make sure to use Repair Game in Game Settings button later.\r\nThrow: {ex}", ex));
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
                CurrentGameProperty._GameInstall.StartAfterInstall = false;
                CurrentGameProperty._GameInstall.ProgressChanged -= GameInstall_ProgressChanged;
                CurrentGameProperty._GameInstall.StatusChanged -= GameInstall_StatusChanged;
                await Task.Delay(200);
                CurrentGameProperty._GameInstall.Flush();
                ReturnToHomePage();
            }
        }

        private void GameInstall_StatusChanged(object sender, TotalPerfileStatus e)
        {
            if (DispatcherQueue.HasThreadAccess)
                GameInstall_StatusChanged_Inner(e);
            else
                DispatcherQueue.TryEnqueue(() => GameInstall_StatusChanged_Inner(e));
        }

        private void GameInstall_StatusChanged_Inner(TotalPerfileStatus e)
        {
            ProgressStatusTitle.Text = e.ActivityStatus;
            progressPerFile.Visibility = e.IsIncludePerFileIndicator ? Visibility.Visible : Visibility.Collapsed;

            progressRing.IsIndeterminate = e.IsProgressTotalIndetermined;
            progressRingPerFile.IsIndeterminate = e.IsProgressPerFileIndetermined;
        }

        private void GameInstall_ProgressChanged(object sender, TotalPerfileProgress e)
        {
            if (DispatcherQueue.HasThreadAccess)
                GameInstall_ProgressChanged_Inner(e);
            else
                DispatcherQueue.TryEnqueue(() => GameInstall_ProgressChanged_Inner(e));
        }

        private void GameInstall_ProgressChanged_Inner(TotalPerfileProgress e)
        {
            progressRing.Value = e.ProgressTotalPercentage;
            progressRingPerFile.Value = e.ProgressPerFilePercentage;
            ProgressStatusSubtitle.Text = string.Format(Lang._Misc.PerFromTo, SummarizeSizeSimple(e.ProgressTotalDownload), SummarizeSizeSimple(e.ProgressTotalSizeToDownload));
            ProgressStatusFooter.Text = string.Format(Lang._Misc.Speed, SummarizeSizeSimple(e.ProgressTotalSpeed));
            ProgressTimeLeft.Text = string.Format(Lang._Misc.TimeRemainHMSFormat, e.ProgressTotalTimeLeft);
        }

        private void CancelInstallationProcedure(object sender, RoutedEventArgs e)
        {
            switch (GameInstallationState)
            {
                case GameInstallStateEnum.NeedsUpdate:
                case GameInstallStateEnum.InstalledHavePlugin:
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
        #endregion

        #region Game Start/Stop Method
        CancellationTokenSource WatchOutputLog = new();
        CancellationTokenSource ResizableWindowHookToken;
        private async void StartGame(object sender, RoutedEventArgs e)
        {
            // Initialize values
            IGameSettingsUniversal _Settings   = CurrentGameProperty!._GameSettings!.AsIGameSettingsUniversal();
            PresetConfigV2         _gamePreset = CurrentGameProperty!._GameVersion!.GamePreset!;

            var isGenshin  = CurrentGameProperty!._GameVersion.GameType == GameType.Genshin;
            var giForceHDR = false;
            
            try
            {
                if (!await CheckMediaPackInstalled()) return;

                if (isGenshin)
                {
                    giForceHDR = GetAppConfigValue("ForceGIHDREnable").ToBool();
                    if (giForceHDR) GenshinHDREnforcer();
                }

                if (_Settings!.SettingsCollapseMisc != null &&
                    _Settings.SettingsCollapseMisc.UseAdvancedGameSettings &&
                    _Settings.SettingsCollapseMisc.UseGamePreLaunchCommand) PreLaunchCommand(_Settings);

                Process proc                    = new Process();
                proc.StartInfo.FileName         = Path.Combine(NormalizePath(GameDirPath)!, _gamePreset.GameExecutableName!);
                proc.StartInfo.UseShellExecute  = true;
                proc.StartInfo.Arguments        = GetLaunchArguments(_Settings)!;
                LogWriteLine($"[HomePage::StartGame()] Running game with parameters:\r\n{proc.StartInfo.Arguments}");
                if (File.Exists(Path.Combine(GameDirPath!, "@AltLaunchMode")))
                {
                    LogWriteLine("[HomePage::StartGame()] Using alternative launch method!", LogType.Warning, true);
                    proc.StartInfo.WorkingDirectory = (CurrentGameProperty!._GameVersion.GamePreset!.ZoneName == "Bilibili" ||
                       (isGenshin && giForceHDR) ? NormalizePath(GameDirPath) : 
                            Path.GetDirectoryName(NormalizePath(GameDirPath))!)!;
                }
                else
                {
                    proc.StartInfo.WorkingDirectory = NormalizePath(GameDirPath)!;
                }
                proc.StartInfo.UseShellExecute  = false;
                proc.StartInfo.Verb             = "runas";
                proc.Start();

                // Start the resizable window payload (also use the same token as PlaytimeToken)
                StartResizableWindowPayload(
                    _gamePreset.GameExecutableName,
                    _Settings,
                    _gamePreset.GameType);
                GameRunningWatcher(_Settings);

                if (GetAppConfigValue("EnableConsole").ToBool())
                {
                    WatchOutputLog = new CancellationTokenSource();
                    ReadOutputLog();
                }

                switch (GetAppConfigValue("GameLaunchedBehavior").ToString())
                {
                    case "Minimize":
                        (m_window as MainWindow)?.Minimize();
                        break;
                    case "ToTray":
                        (m_window as MainWindow)?.ToggleToTray_MainWindow();
                        break;
                    case "Nothing":
                        break;
                    default:
                        (m_window as MainWindow)?.Minimize();
                        break;
                }

                StartPlaytimeCounter(proc, _gamePreset);
                if (GetAppConfigValue("LowerCollapsePrioOnGameLaunch").ToBool()) CollapsePrioControl(proc);

                // Set game process priority to Above Normal when GameBoost is on
                if (_Settings.SettingsCollapseMisc != null && _Settings.SettingsCollapseMisc.UseGameBoost) GameBoost_Invoke(CurrentGameProperty);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                LogWriteLine($"There is a problem while trying to launch Game with Region: {_gamePreset.ZoneName}\r\nTraceback: {ex}", LogType.Error, true);
                ErrorSender.SendException(new System.ComponentModel.Win32Exception($"There was an error while trying to launch the game!\r\tThrow: {ex}", ex));
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

            LogWriteLine($"{new string('=', barwidth)} GAME STOPPED {new string('=', barwidth)}", LogType.Warning, true);

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
                    (m_window as MainWindow)?.Restore();
                    break;
                case "ToTray":
                    H.NotifyIcon.WindowExtensions.Show(m_window!);
                    (m_window as MainWindow)?.Restore();
                    break;
                case "Nothing":
                    break;
                default:
                    (m_window as MainWindow)?.Restore();
                    break;
            }

            // Run Post Launch Command
            if (_settings.SettingsCollapseMisc.UseAdvancedGameSettings && _settings.SettingsCollapseMisc.UseGamePostExitCommand) PostExitCommand(_settings);
        }

        private void StopGame(PresetConfigV2 gamePreset)
        {
            ArgumentNullException.ThrowIfNull(gamePreset);
            try
            {
                var gameProcess = Process.GetProcessesByName(gamePreset.GameExecutableName!.Split('.')[0]);
                foreach (var p in gameProcess)
                {
                    LogWriteLine($"Trying to stop game process {gamePreset.GameExecutableName.Split('.')[0]} at PID {p.Id}", LogType.Scheme, true);
                    p.Kill();
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                LogWriteLine($"There is a problem while trying to stop Game with Region: {gamePreset.ZoneName}\r\nTraceback: {ex}", LogType.Error, true);
            }
        }
        #endregion

        #region Game Resizable Window Payload
        internal async void StartResizableWindowPayload(string executableName, IGameSettingsUniversal settings, GameType gameType)
        {
            try
            {
                // Check if the game is using Resizable Window settings
                if (!settings.SettingsCollapseScreen.UseResizableWindow) return;
                ResizableWindowHookToken = new CancellationTokenSource();

                executableName = Path.GetFileNameWithoutExtension(executableName);
                ResizableWindowHook resizableWindowHook = new ResizableWindowHook();

                // Set the pos + size reinitialization to true if the game is Honkai: Star Rail
                // This is required for Honkai: Star Rail since the game will reset its pos + size. Making
                // it impossible to use custom resolution (but since you are using Collapse, it's now
                // possible :teriStare:)
                bool isNeedToResetPos = gameType == GameType.StarRail;
                await resizableWindowHook.StartHook(executableName, ResizableWindowHookToken.Token, isNeedToResetPos);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error while initializing Resizable Window payload!\r\n{ex}");
                ErrorSender.SendException(ex, ErrorType.GameError);
            }
        }
        #endregion

        #region Game Launch Argument Builder
        bool RequireWindowExclusivePayload = false;
        internal string GetLaunchArguments(IGameSettingsUniversal _Settings)
        {
            StringBuilder parameter = new StringBuilder();

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

            if (_Settings.SettingsCollapseMisc.UseCustomArguments)
            {
                string customArgs = _Settings.SettingsCustomArgument.CustomArgumentValue;
                if (!string.IsNullOrEmpty(customArgs))
                    parameter.Append(customArgs);
            }

            return parameter.ToString();
        }

        public string CustomArgsValue
        {
            get => ((IGameSettingsUniversal)CurrentGameProperty._GameSettings).SettingsCustomArgument.CustomArgumentValue;
            set => ((IGameSettingsUniversal)CurrentGameProperty._GameSettings).SettingsCustomArgument.CustomArgumentValue = value;
        }

        public bool UseCustomArgs
        {
            get => ((IGameSettingsUniversal)CurrentGameProperty._GameSettings).SettingsCollapseMisc.UseCustomArguments;
            set => ((IGameSettingsUniversal)CurrentGameProperty._GameSettings).SettingsCollapseMisc.UseCustomArguments = value;
        }
        #endregion

        #region Media Pack
        public async Task<bool> CheckMediaPackInstalled()
        {
            if (CurrentGameProperty._GameVersion.GameType != GameType.Honkai) return true;

            RegistryKey reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\WindowsFeatures\WindowsMediaVersion");
            if (reg != null)
                return true;

            LogWriteLine($"Media pack is not installed!\r\n\t" +
                        $"If you encounter the 'cry_ware_unity' error, run this script as an administrator:\r\n\t" +
                        $"{Path.Combine(AppFolder, "Misc", "InstallMediaPack.cmd")}", LogType.Warning, true);

            // Skip dialog if user asked before
            if (GetAppConfigValue("HI3IgnoreMediaPack").ToBool())
                return true;

            switch (await Dialog_NeedInstallMediaPackage(Content))
            {
                case ContentDialogResult.Primary:
                    TryInstallMediaPack();
                    break;
                case ContentDialogResult.Secondary:
                    SetAndSaveConfigValue("HI3IgnoreMediaPack", true);
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
            InitializeConsoleValues();

            LogWriteLine($"Are Game logs getting saved to Collapse logs: {GetAppConfigValue("IncludeGameLogs").ToBool()}", LogType.Scheme, true);
            LogWriteLine($"{new string('=', barwidth)} GAME STARTED {new string('=', barwidth)}", LogType.Warning, true);
            try
            {
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
                            string line;
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
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LogWriteLine($"There were a problem in Game Log Reader\r\n{ex}", LogType.Error);
            }
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

        private async void MoveGameLocationButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (await CurrentGameProperty._GameInstall.MoveGameLocation())
                {
                    CurrentGameProperty._GameInstall.ApplyGameConfig();
                    ReturnToHomePage();
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error has occurred while running Move Game Location tool!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex, ErrorType.Unhandled);
            }
        }
        #endregion

        #region Playtime Buttons
        private void ForceUpdatePlaytimeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_cachedIsGameRunning)
                return;

            UpdatePlaytime();
        }

        private async void ChangePlaytimeButton_Click(object sender, RoutedEventArgs e)
        {
            if (await Dialog_ChangePlaytime(this) != ContentDialogResult.Primary) return;

            int playtimeMins = int.Parse("0" + MinutePlaytimeTextBox.Text);
            int playtimeHours = int.Parse("0" + HourPlaytimeTextBox.Text);
            int finalPlaytimeMinutes = playtimeMins % 60;
            int finalPlaytimeHours = playtimeHours + playtimeMins / 60;
            if (finalPlaytimeHours > 99999) { finalPlaytimeHours = 99999; finalPlaytimeMinutes = 59; }
            MinutePlaytimeTextBox.Text = finalPlaytimeMinutes.ToString();
            HourPlaytimeTextBox.Text = finalPlaytimeHours.ToString();

            int finalPlaytime = finalPlaytimeHours * 3600 + finalPlaytimeMinutes * 60;

            SavePlaytimeToRegistry(true, CurrentGameProperty._GameVersion.GamePreset.ConfigRegistryLocation, finalPlaytime);
            LogWriteLine($"Playtime counter changed to {HourPlaytimeTextBox.Text + "h " + MinutePlaytimeTextBox.Text + "m"}. (Previous value: {PlaytimeMainBtn.Text})");
            UpdatePlaytime(false, finalPlaytime);
            PlaytimeFlyout.Hide();
        }

        private async void ResetPlaytimeButton_Click(object sender, RoutedEventArgs e)
        {
            if (await Dialog_ResetPlaytime(this) != ContentDialogResult.Primary) return;

            SavePlaytimeToRegistry(true, CurrentGameProperty._GameVersion.GamePreset.ConfigRegistryLocation, 0);
            LogWriteLine($"Playtime counter changed to 0h 0m. (Previous value: {PlaytimeMainBtn.Text})");
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
        private void UpdatePlaytime(bool readRegistry = true, int value = 0)
        {
            if (readRegistry)
                value = ReadPlaytimeFromRegistry(true, CurrentGameProperty._GameVersion.GamePreset.ConfigRegistryLocation);

            HourPlaytimeTextBox.Text = (value / 3600).ToString();
            MinutePlaytimeTextBox.Text = (value % 3600 / 60).ToString();
            PlaytimeMainBtn.Text = string.Format(Lang._HomePage.GamePlaytime_Display, (value / 3600), (value % 3600 / 60));
        }

        private DateTime Hoyoception => new(2012, 2, 13, 0, 0, 0, DateTimeKind.Utc);
        private void UpdateLastPlayed(bool readRegistry = true, int value = 0)
        {
            if (readRegistry)
                value = ReadPlaytimeFromRegistry(false, CurrentGameProperty._GameVersion.GamePreset.ConfigRegistryLocation);
            
            DateTime last = Hoyoception.AddSeconds(value).ToLocalTime();

            if (value == 0)
            {
                PlaytimeLastOpen.Visibility = Visibility.Collapsed;
                return;
            }

            PlaytimeLastOpen.Visibility = Visibility.Visible;
            string formattedText = string.Format(Lang._HomePage.GamePlaytime_ToolTipDisplay, last.Day,
                last.Month, last.Year, last.Hour, last.Minute);
            ToolTipService.SetToolTip(PlaytimeBtn, formattedText);
        }

        private async void StartPlaytimeCounter(Process proc, PresetConfigV2 gamePreset)
        {
            int currentPlaytime = ReadPlaytimeFromRegistry(true, gamePreset.ConfigRegistryLocation);

            DateTime begin = DateTime.Now;
            int lastPlayed = (int)(begin.ToUniversalTime() - Hoyoception).TotalSeconds;
            SavePlaytimeToRegistry(false, gamePreset.ConfigRegistryLocation, lastPlayed);
            UpdateLastPlayed(false, lastPlayed);
            int numOfLoops = 0;

#if DEBUG
            LogWriteLine($"{gamePreset.ProfileName} - Started session at {begin.ToLongTimeString()}.");
#endif

            using (var inGameTimer = new System.Timers.Timer())
            {
                inGameTimer.Interval = 60000;
                inGameTimer.Elapsed += (o, e) =>
                {
                    numOfLoops++;

                    DateTime now = DateTime.Now;
                    int elapsedSeconds = (int)(now - begin).TotalSeconds;
                    if (elapsedSeconds < 0)
                        elapsedSeconds = numOfLoops * 60;

                    if (GamePropertyVault.GetCurrentGameProperty()._GamePreset.ProfileName == gamePreset.ProfileName)
                        m_homePage?.DispatcherQueue.TryEnqueue(() =>
                        {
                            m_homePage.UpdatePlaytime(false, currentPlaytime + elapsedSeconds);
                        });
#if DEBUG
                    LogWriteLine($"{gamePreset.ProfileName} - {elapsedSeconds}s elapsed. ({now.ToLongTimeString()})");
#endif
                    SavePlaytimeToRegistry(true, gamePreset.ConfigRegistryLocation, currentPlaytime + elapsedSeconds);
                };

                inGameTimer.Start();
                await proc.WaitForExitAsync();
                inGameTimer.Stop();
            }

            DateTime end = DateTime.Now;
            int elapsedSeconds = (int)(end - begin).TotalSeconds;
            if (elapsedSeconds < 0)
            {
                LogWriteLine($"[HomePage::StartPlaytimeCounter] Date difference cannot be lower than 0. ({elapsedSeconds}s)", LogType.Error);
                elapsedSeconds = numOfLoops * 60;
                Dialog_InvalidPlaytime(m_mainPage?.Content, elapsedSeconds);
            }

            SavePlaytimeToRegistry(true, gamePreset.ConfigRegistryLocation, currentPlaytime + elapsedSeconds);
            LogWriteLine($"Added {elapsedSeconds}s [{elapsedSeconds / 3600}h {elapsedSeconds % 3600 / 60}m {elapsedSeconds % 3600 % 60}s] to {gamePreset.ProfileName} playtime.", LogType.Default, true);
            if (GamePropertyVault.GetCurrentGameProperty()._GamePreset.ProfileName == gamePreset.ProfileName)
                m_homePage?.DispatcherQueue.TryEnqueue(() =>
                {
                    m_homePage.UpdatePlaytime(false, currentPlaytime + elapsedSeconds);
                });
        }

        private const string _playtimeRegName = "CollapseLauncher_Playtime";
        private const string _playtimeLastPlayedRegName = "CollapseLauncher_LastPlayed";
        private static int ReadPlaytimeFromRegistry(bool isPlaytime, string regionRegistryKey)
        {
            try
            {
                return (int)Registry.CurrentUser.OpenSubKey(regionRegistryKey, true)!.GetValue(isPlaytime ? _playtimeRegName : _playtimeLastPlayedRegName, 0);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Playtime - There was an error reading from the registry. \n {ex}");
                return 0;
            }
        }

        private static void SavePlaytimeToRegistry(bool isPlaytime, string regionRegistryKey, int value)
        {
            try
            {
                Registry.CurrentUser.OpenSubKey(regionRegistryKey, true)!.SetValue(isPlaytime ? _playtimeRegName : _playtimeLastPlayedRegName, value, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Playtime - There was an error writing to registry. \n {ex}");
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
                IsSkippingUpdateCheck = true;

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
                CurrentGameProperty._GameInstall.ApplyGameConfig(true);
                if (CurrentGameProperty._GameInstall.StartAfterInstall && CurrentGameProperty._GameVersion.IsGameInstalled()) 
                    StartGame(null, null);
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
                    $"Please report this issue to our GitHub here: https://github.com/CollapseLauncher/Collapse/issues/new or come back to the launcher and make sure to use Repair Game in Game Settings button later.\r\nThrow: {ex}", ex));
            }
            catch (Exception ex)
            {
                IsPageUnload = true;
                LogWriteLine($"Update error on {CurrentGameProperty._GameVersion.GamePreset.ZoneFullname} game!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
            finally
            {
                IsSkippingUpdateCheck = false;
                CurrentGameProperty._GameInstall.StartAfterInstall = false;
                CurrentGameProperty._GameInstall.ProgressChanged -= GameInstall_ProgressChanged;
                CurrentGameProperty._GameInstall.StatusChanged -= GameInstall_StatusChanged;
                await Task.Delay(200);
                CurrentGameProperty._GameInstall.Flush();
                ReturnToHomePage();
            }
        }
        #endregion

        #region Set Hand Cursor
        private static void ChangeCursor(UIElement element, InputCursor cursor)
        {
            typeof(UIElement).InvokeMember("ProtectedCursor", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.SetProperty | BindingFlags.Instance, null, element, new object[] { cursor });
        }

        private void SetHandCursor(object sender, RoutedEventArgs e = null)
        {
            ChangeCursor((UIElement)sender, InputSystemCursor.Create(InputSystemCursorShape.Hand));
        }
        #endregion

        #region Hyper Link Color
        private void HyperLink_OnPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            TextBlock textBlock = null;
            if (sender is Grid grid)
            {
                if (grid.Children[0] is TextBlock)
                    textBlock = (TextBlock)grid.Children[0];
                else if (grid.Children[0] is CompressedTextBlock compressedTextBlock)
                {
                    compressedTextBlock.Foreground = (Brush)Application.Current.Resources["AccentColor"];
                    return;
                }
            }
            else if (sender is TextBlock block)
                textBlock = block;
            if (textBlock != null)
                textBlock.Foreground = (Brush)Application.Current.Resources["AccentColor"];
        }

        private void HyperLink_OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            TextBlock textBlock = null;
            if (sender is Grid grid)
            {
                if (grid.Children[0] is TextBlock)
                    textBlock = (TextBlock)grid.Children[0];
                else if (grid.Children[0] is CompressedTextBlock compressedTextBlock)
                {
                    compressedTextBlock.Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
                    return;
                }
            }
            else if (sender is TextBlock block)
                textBlock = block;
            if (textBlock != null)
                textBlock.Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
        }
        #endregion

        #region Misc Methods
        private async void CollapsePrioControl(Process proc)
        {
            try
            {
                using (Process collapseProcess = Process.GetCurrentProcess())
                {
                    collapseProcess.PriorityBoostEnabled = false;
                    collapseProcess.PriorityClass = ProcessPriorityClass.BelowNormal;
                    LogWriteLine($"Collapse process [PID {collapseProcess.Id}] priority is set to Below Normal, PriorityBoost is off, carousel is temporarily stopped", LogType.Default, true);
                }

                CarouselStopScroll();
                await proc.WaitForExitAsync();

                using (Process collapseProcess = Process.GetCurrentProcess())
                {
                    collapseProcess.PriorityBoostEnabled = true;
                    collapseProcess.PriorityClass = ProcessPriorityClass.Normal;
                    LogWriteLine($"Collapse process [PID {collapseProcess.Id}] priority is set to Normal, PriorityBoost is on, carousel is started", LogType.Default, true);
                }
                CarouselRestartScroll();
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error in Collapse Priority Control module!\r\n{ex}", LogType.Error, true);
            }
        }

        private void GenshinHDREnforcer()
        {
            GameSettings.Genshin.WindowsHDR GenshinHDR = new GameSettings.Genshin.WindowsHDR();
            try
            {
                GameSettings.Genshin.WindowsHDR.Load();
                GenshinHDR.isHDR = true;
                GenshinHDR.Save();
                LogWriteLine("Successfully forced Genshin HDR settings on!", LogType.Scheme, true);
            }
            catch (Exception ex)
            {
                LogWriteLine($"There was an error trying to force enable HDR on Genshin!\r\n{ex}", LogType.Error, true);
            }
        }

        private async void GameBoost_Invoke(GamePresetProperty gameProp)
        {
            // Init new target process
            Process toTargetProc = null;
            try
            {
                // Try catching the non-zero MainWindowHandle pointer and assign it to "toTargetProc" variable by using GetGameProcessWithActiveWindow()
                while ((toTargetProc = gameProp.GetGameProcessWithActiveWindow()) == null)
                {
                    await Task.Delay(1000); // Waiting the process to be found and assigned to "toTargetProc" variable.
                    // This is where the magic happen. When the "toTargetProc" doesn't meet the comparison to be compared as null,
                    // it will instead returns a non-null value and assign it to "toTargetProc" variable,
                    // which it will break the loop and execute the next code below it.
                }
                LogWriteLine($"[HomePage::GameBoost_Invoke] Found target process! Waiting 10 seconds for process initialization...\r\n\t" +
                             $"Target Process : {toTargetProc?.ProcessName} [{toTargetProc?.Id}]", LogType.Default, true);

                // Wait 10 seconds before applying
                await Task.Delay(10000);

                // Check early exit
                if (toTargetProc.HasExited)
                {
                    LogWriteLine($"[HomePage::GameBoost_Invoke] Game process {toTargetProc.ProcessName} [{toTargetProc.Id}] has exited!", LogType.Warning, true);
                    return;
                }

                // Assign the priority to the process and write a log (just for displaying any info)
                toTargetProc.PriorityClass = ProcessPriorityClass.AboveNormal;
                LogWriteLine($"[HomePage::GameBoost_Invoke] Game process {toTargetProc.ProcessName} [{toTargetProc.Id}] priority is boosted to above normal!", LogType.Warning, true);
            }
            catch ( Exception ex )
            {
                LogWriteLine($"[HomePage::GameBoost_Invoke] There has been error while boosting game priority to Above Normal!\r\n" +
                             $"\tTarget Process : {toTargetProc?.ProcessName} [{toTargetProc?.Id}]\r\n{ex}", LogType.Error, true);
            }
        }
        #endregion

        #region Pre/Post Game Launch Command
        private Process _procPreGLC;

        private async void PreLaunchCommand(IGameSettingsUniversal _settings)
        {
            try
            {
                string preGameLaunchCommand = _settings?.SettingsCollapseMisc?.GamePreLaunchCommand;
                if (string.IsNullOrEmpty(preGameLaunchCommand)) return;

                LogWriteLine($"Using Pre-launch command : {preGameLaunchCommand}\r\n\t" +
                             $"BY USING THIS, NO SUPPORT IS PROVIDED IF SOMETHING HAPPENED TO YOUR ACCOUNT, GAME, OR SYSTEM!",
                             LogType.Warning, true);

                _procPreGLC = new Process();

                _procPreGLC.StartInfo.FileName               = "cmd.exe";
                _procPreGLC.StartInfo.Arguments              = "/S /C " + "\"" + preGameLaunchCommand + "\"";
                _procPreGLC.StartInfo.CreateNoWindow         = true;
                _procPreGLC.StartInfo.UseShellExecute        = false;
                _procPreGLC.StartInfo.RedirectStandardOutput = true;
                _procPreGLC.StartInfo.RedirectStandardError  = true;

                _procPreGLC.OutputDataReceived += (sender, e) =>
                                                  {
                                                      if (!string.IsNullOrEmpty(e.Data)) LogWriteLine(e.Data, LogType.GLC, true);
                                                  };

                _procPreGLC.ErrorDataReceived += (sender, e) =>
                                                 {
                                                     if (!string.IsNullOrEmpty(e.Data)) LogWriteLine($"ERROR RECEIVED!\r\n\t{e.Data}", LogType.GLC, true);
                                                 };

                _procPreGLC.Start();

                _procPreGLC.BeginOutputReadLine();
                _procPreGLC.BeginErrorReadLine();

                await _procPreGLC.WaitForExitAsync();
            }
            catch ( System.ComponentModel.Win32Exception ex )
            {
                LogWriteLine($"There is a problem while trying to launch Pre-Game Command with Region: {CurrentGameProperty._GameVersion.GamePreset.ZoneName}\r\nTraceback: {ex}", LogType.Error, true);
                ErrorSender.SendException(new System.ComponentModel.Win32Exception($"There was an error while trying to launch Pre-Launch command!\r\tThrow: {ex}", ex));
            }
            finally
            {
                if (_procPreGLC != null) _procPreGLC.Dispose();
            }
        }

        private void PreLaunchCommand_ForceClose()
        {
            try
            {
                if (_procPreGLC != null && !_procPreGLC.HasExited)
                {
                    // Kill main and child processes
                    Process taskKill = new Process();
                    taskKill.StartInfo.FileName  = "taskkill";
                    taskKill.StartInfo.Arguments = $"/F /T /PID {_procPreGLC.Id}";
                    taskKill.Start();
                    taskKill.WaitForExit();

                    LogWriteLine("Pre-launch command has been forced to close!", LogType.Warning, true);
                }
            }
            // Ignore external errors
            catch ( InvalidOperationException ) {}
            catch (System.ComponentModel.Win32Exception) {}
        }

        private async void PostExitCommand(IGameSettingsUniversal _settings)
        {
            try
            {
                string postGameExitCommand = _settings?.SettingsCollapseMisc?.GamePostExitCommand ?? null;
                if (string.IsNullOrEmpty(postGameExitCommand)) return;

                LogWriteLine($"Using Post-launch command : {postGameExitCommand}\r\n\t" +
                             $"BY USING THIS, NO SUPPORT IS PROVIDED IF SOMETHING HAPPENED TO YOUR ACCOUNT, GAME, OR SYSTEM!",
                             LogType.Warning, true);

                Process procPostGLC = new Process();

                procPostGLC.StartInfo.FileName               = "cmd.exe";
                procPostGLC.StartInfo.Arguments              = "/S /C " + "\"" + postGameExitCommand + "\"";
                procPostGLC.StartInfo.CreateNoWindow         = true;
                procPostGLC.StartInfo.UseShellExecute        = false;
                procPostGLC.StartInfo.RedirectStandardOutput = true;
                procPostGLC.StartInfo.RedirectStandardError  = true;

                procPostGLC.OutputDataReceived += (sender, e) =>
                                                  {
                                                      if (!string.IsNullOrEmpty(e.Data)) LogWriteLine(e.Data, LogType.GLC, true);
                                                  };

                procPostGLC.ErrorDataReceived += (sender, e) =>
                                                 {
                                                     if (!string.IsNullOrEmpty(e.Data)) LogWriteLine($"ERROR RECEIVED!\r\n\t{e.Data}", LogType.GLC, true);
                                                 };

                procPostGLC.Start();
                procPostGLC.BeginOutputReadLine();
                procPostGLC.BeginErrorReadLine();

                await procPostGLC.WaitForExitAsync();
            }
            catch ( System.ComponentModel.Win32Exception ex )
            {
                LogWriteLine($"There is a problem while trying to launch Post-Game Command with Region: {CurrentGameProperty._GameVersion.GamePreset.ZoneName}\r\nTraceback: {ex}", LogType.Error, true);
                ErrorSender.SendException(new System.ComponentModel.Win32Exception($"There was an error while trying to launch Post-Exit command\r\tThrow: {ex}", ex));
            }
        }
        #endregion

        #region Shortcut Creation
        private async void AddToSteamButton_Click(object sender, RoutedEventArgs e)
        {
            Tuple<ContentDialogResult, bool> result = await Dialog_SteamShortcutCreationConfirm(this);

            if (result.Item1 != ContentDialogResult.Primary)
                return;

            if (ShortcutCreator.AddToSteam(GamePropertyVault.GetCurrentGameProperty()._GamePreset, result.Item2))
            {
                await Dialog_SteamShortcutCreationSuccess(this, result.Item2);
                return;
            }

            await Dialog_SteamShortcutCreationFailure(this);
        }

        private async void ShortcutButton_Click(object sender, RoutedEventArgs e)
        {
            string folder = await FileDialogNative.GetFolderPicker(Lang._HomePage.CreateShortcut_FolderPicker);

            if (string.IsNullOrEmpty(folder))
                return;

            if (!IsUserHasPermission(folder))
            {
                await Dialog_InsufficientWritePermission(sender as UIElement, folder);
                return;
            }

            Tuple<ContentDialogResult, bool> result = await Dialog_ShortcutCreationConfirm(this, folder);

            if (result.Item1 != ContentDialogResult.Primary)
                return;

            ShortcutCreator.CreateShortcut(folder, GamePropertyVault.GetCurrentGameProperty()._GamePreset, result.Item2);
            await Dialog_ShortcutCreationSuccess(this, folder, result.Item2);
        }
        #endregion

        private async void ProgressSettingsButton_OnClick(object sender, RoutedEventArgs e) => await Dialog_DownloadSettings(this, CurrentGameProperty);
    }
}
