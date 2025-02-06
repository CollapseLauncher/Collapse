#if !DISABLEDISCORD
using CollapseLauncher.DiscordPresence;
#endif
using CollapseLauncher.CustomControls;
using CollapseLauncher.Helper.LauncherApiLoader.Legacy;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Animation;
using CollapseLauncher.Helper.Database;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Statics;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Animations;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Win32.FileDialogCOM;
using Microsoft.UI.Composition;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using PhotoSauce.MagicScaler;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using Brush = Microsoft.UI.Xaml.Media.Brush;
using Image = Microsoft.UI.Xaml.Controls.Image;
using Point = Windows.Foundation.Point;
using UIElementExtensions = CollapseLauncher.Extension.UIElementExtensions;
// ReSharper disable InconsistentNaming
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable AsyncVoidMethod
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable CheckNamespace

// HomePage class is pretty big, so it's split into multiple files.
// This file contains the main class definition and the PageMethod region and also anything related to XAML and its styling
// You can split other methods that is not directly related to XAML into other files
// Currently separated files:
// - HomePage.Playtime.cs
//   Contains method related to playtime tracking UI and its handler
// - HomePage.GameLauncher.cs
//   Contains method related to game launching and its surrounding like arguments, GLC, etc.
// - HomePage.GameManagement.cs
//   Contains method related to game management like installation, update, etc.
// - HomePage.Misc.cs
//   Contains miscelanous method that doesn't fit into other categories and is not big enough to be in its own file

namespace CollapseLauncher.Pages
{
    public sealed partial class HomePage
    {
        #region Properties
        private GamePresetProperty CurrentGameProperty { get; set; }
        private CancellationTokenSourceWrapper PageToken { get; set; }
        private CancellationTokenSourceWrapper CarouselToken { get; set; }

        private Button _lastSocMedButton;

        private int barWidth;
        private int consoleWidth;

        public static int RefreshRateDefault => 500;
        public static int RefreshRateSlow    => 1000;

        /// <summary>
        /// Holds the value for how long a checks needs to be delayed before continuing the loop in milliseconds.
        /// Default : 200 (Please set it using RefreshRateDefault instead)
        /// </summary>
        public static int RefreshRate
        {
            get;
            set
            {
            #if DEBUG
                LogWriteLine($"HomePage Refresh Rate changed to {value}", LogType.Debug, true);
            #endif
                field = value;
            }
        }

        /// <summary>
        /// Hold cached state for IsGameRunning. The state is controlled inside CheckRunningGameInstance() method.
        /// </summary>
        private static bool _cachedIsGameRunning { get; set; }
        #endregion

        #region PageMethod
        public HomePage()
        {
            RefreshRate = RefreshRateDefault;
            Loaded += StartLoadedRoutine;

            m_homePage = this;
            InitializeConsoleValues();
        }

        ~HomePage()
        {
            // HACK: Fix random crash by always unsubscribing the StartLoadedRoutine if the GC is calling the deconstructor.
            Loaded -= StartLoadedRoutine;
        }

        private void InitializeConsoleValues()
        {
            consoleWidth = 24;
            try { consoleWidth = Console.BufferWidth; }
            catch
            {
                // ignored
            }

            barWidth = (consoleWidth - 22) / 2 - 1;
        }

        private bool IsPageUnload { get; set; }

        private static bool NeedShowEventIcon => GetAppConfigValue("ShowEventsPanel").ToBool();

        private void ReturnToHomePage()
        {
            var presetConfig = GamePropertyVault.GetCurrentGameProperty().GamePreset;
            if (IsPageUnload && presetConfig.HashID != CurrentGameProperty.GamePreset.HashID)
            {
                return;
            }

            MainPage.PreviousTagString.Add(MainPage.PreviousTag);
            MainFrameChanger.ChangeMainFrame(typeof(HomePage));
        }

        private async void StartLoadedRoutine(object sender, RoutedEventArgs e)
        {
            try
            {
                // HACK: Fix random crash by manually load the XAML part
                //       But first, let it initialize its properties.
                CurrentGameProperty = GamePropertyVault.GetCurrentGameProperty();
                PageToken = new CancellationTokenSourceWrapper();
                CarouselToken = new CancellationTokenSourceWrapper();

                InitializeComponent();

                BackgroundImgChanger.ToggleBackground(false);

                await GetCurrentGameState();

                if (!GetAppConfigValue("ShowEventsPanel").ToBool())
                {
                    SidePanel.Visibility = Visibility.Collapsed;
                }

                if (!GetAppConfigValue("ShowSocialMediaPanel").ToBool())
                    SocMedPanel.Visibility = Visibility.Collapsed;

                if (!GetAppConfigValue("ShowGamePlaytime").ToBool())
                    PlaytimeBtn.Visibility = Visibility.Collapsed;

                if (!DbConfig.DbEnabled)
                {
                    PlaytimeDbSyncToggle.IsEnabled = false;
                }
                
                if (!DbConfig.DbEnabled || !(CurrentGameProperty.GameSettings?.SettingsCollapseMisc.IsSyncPlaytimeToDatabase ?? false))
                    SyncDbPlaytimeBtn.IsEnabled = false;

                TryLoadEventPanelImage();

                SocMedPanel.Translation += Shadow48;
                GameStartupSetting.Translation += Shadow32;
                CommunityToolsBtn.Translation += Shadow32;

                if (IsCarouselPanelAvailable || IsPostPanelAvailable)
                {
                    ImageCarousel.SelectedIndex = 0;
                    ImageCarousel.Visibility = Visibility.Visible;
                    ImageCarouselPipsPager.Visibility = Visibility.Visible;

                    ShowEventsPanelToggle.IsEnabled = true;
                    ScaleUpEventsPanelToggle.IsEnabled = true;
                    PostPanel.Visibility = Visibility.Visible;
                    PostPanel.Translation += Shadow48;
                }

                InputSystemCursor cursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
                SophonProgressStatusGrid.SetAllControlsCursorRecursive(cursor);
                ProgressStatusGrid.SetAllControlsCursorRecursive(cursor);
                RightBottomButtons.SetAllControlsCursorRecursive(cursor);
                LeftBottomButtons.SetAllControlsCursorRecursive(cursor);
                GameStartupSettingFlyoutContainer.SetAllControlsCursorRecursive(cursor);

                if (await CurrentGameProperty.GameInstall.TryShowFailedDeltaPatchState()) return;
                if (await CurrentGameProperty.GameInstall.TryShowFailedGameConversionState()) return;
                CurrentGameProperty.GamePlaytime.PlaytimeUpdated += UpdatePlaytime;
                UpdatePlaytime(null, CurrentGameProperty.GamePlaytime.CollapsePlaytime);

                _ = StartCarouselAutoScroll();

#if !DISABLEDISCORD
                AppDiscordPresence?.SetActivity(ActivityType.Idle);
#endif

                if (IsGameStatusComingSoon || IsGameStatusPreRegister)
                {
                    LauncherBtn.Visibility = Visibility.Collapsed;
                    LauncherGameStatusPlaceholderBtn.Visibility = Visibility.Visible;

                    if (IsGameStatusComingSoon) GamePlaceholderBtnComingSoon.Visibility = Visibility.Visible;
                    if (IsGameStatusPreRegister) GamePlaceholderBtnPreRegister.Visibility = Visibility.Visible;

                    return;
                }

                if (CurrentGameProperty.IsGameRunning)
                {
                    CheckRunningGameInstance(PageToken.Token);
                    return;
                }

                // Get game state
                GameInstallStateEnum gameState = await CurrentGameProperty.GameVersion.GetGameState();

                // Start automatic scan if the game is in NotInstalled state
                // and if the return is 0 (yes), then save the config
                if (gameState == GameInstallStateEnum.NotInstalled &&
                    await CurrentGameProperty.GameInstall.GetInstallationPath(true) == 0)
                {
                    // Save the config
                    CurrentGameProperty.GameInstall.ApplyGameConfig();

                    // Refresh the Home page.
                    ReturnToHomePage();
                    return;
                }

                // Check if the game state returns NotInstalled, double-check by doing config.ini validation
                if (!await CurrentGameProperty.GameVersion
                          .EnsureGameConfigIniCorrectiveness(this))
                {
                    // If the EnsureGameConfigIniCorrectiveness() returns false,
                    // means config.ini has been changed. Then reload and return to the HomePage
                    ReturnToHomePage();
                    return;
                }

                if (!(m_arguments.StartGame?.Play ?? false))
                {
                    await CheckUserAccountControlStatus();
                    return;
                }

                m_arguments.StartGame.Play = false;

                if (CurrentGameProperty.GameInstall.IsRunning)
                {
                    CurrentGameProperty.GameInstall.StartAfterInstall = CurrentGameProperty.GameInstall.IsRunning;
                    return;
                }

                switch (gameState)
                {
                    case GameInstallStateEnum.InstalledHavePreload:
                    case GameInstallStateEnum.Installed:
                        StartGame(null, null);
                        break;
                    case GameInstallStateEnum.InstalledHavePlugin:
                    case GameInstallStateEnum.NeedsUpdate:
                        CurrentGameProperty.GameInstall.StartAfterInstall = true;
                        UpdateGameDialog(null, null);
                        break;
                    case GameInstallStateEnum.NotInstalled:
                    case GameInstallStateEnum.GameBroken:
                        CurrentGameProperty.GameInstall.StartAfterInstall = true;
                        InstallGameDialog(null, null);
                        break;
                }
            }
            catch (ArgumentNullException ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
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
            IsPageUnload                                     =  true;
            CurrentGameProperty.GamePlaytime.PlaytimeUpdated -= UpdatePlaytime;

            if (!PageToken.IsDisposed && !PageToken.IsCancelled) PageToken.Cancel();
            if (!CarouselToken.IsDisposed && !CarouselToken.IsCancelled) CarouselToken.Cancel();
        }
        #endregion

        #region EventPanel
        private readonly ConcurrentDictionary<string, byte> _eventPanelProcessing = new();
        private async void TryLoadEventPanelImage()
        {
            // Get the url and article image path
            string featuredEventArticleUrl = LauncherMetadataHelper.CurrentMetadataConfig?.GameLauncherApi?
                .LauncherGameNews?.Content?.Background?.FeaturedEventIconBtnUrl;
            string featuredEventIconImg = LauncherMetadataHelper.CurrentMetadataConfig?.GameLauncherApi?
                .LauncherGameNews?.Content?.Background?.FeaturedEventIconBtnImg;

            // If the region event panel property is null, then return
            if (string.IsNullOrEmpty(featuredEventIconImg)
             || string.IsNullOrEmpty(featuredEventArticleUrl)) return;

            if (!_eventPanelProcessing.TryAdd(featuredEventArticleUrl, 0) ||
                !_eventPanelProcessing.TryAdd(featuredEventIconImg, 0))
            {
                LogWriteLine($"[TryLoadEventPanelImage] Stopped processing {featuredEventIconImg} and {featuredEventArticleUrl} due to double processing",
                             LogType.Warning, true);
                return;
            }

            // Get the cached filename and path
            string cachedFileHash = Hash.GetHashStringFromString<Crc32>(featuredEventIconImg);
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

            try
            {
                // Using the original icon file and cached icon file streams
                if (!isCacheIconExist)
                {
                    await using FileStream cachedIconFileStream = new FileStream(cachedIconFileInfo.FullName,
                             FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
                    await using MemoryStream copyIconFileStream = new MemoryStream();
                    await using Stream iconFileStream =
                        await FallbackCDNUtil.GetHttpStreamFromResponse(featuredEventIconImg,
                                                                        PageToken.Token);
                    var scaleFactor = WindowUtility.CurrentWindowMonitorScaleFactor;
                    // Copy remote stream to memory stream
                    await iconFileStream.CopyToAsync(copyIconFileStream);
                    copyIconFileStream.Position = 0;
                    // Get the icon image information and set the resized frame size
                    var iconImageInfo = await Task.Run(() => ImageFileInfo.Load(copyIconFileStream));
                    var width         = (int)(iconImageInfo.Frames[0].Width * scaleFactor);
                    var height        = (int)(iconImageInfo.Frames[0].Height * scaleFactor);

                    copyIconFileStream.Position = 0; // Reset the original icon stream position
                    await ImageLoaderHelper.ResizeImageStream(copyIconFileStream, cachedIconFileStream,
                                                              (uint)width, (uint)height); // Start resizing
                    cachedIconFileStream.Position = 0; // Reset the cached icon stream position

                    // Set the source from cached icon stream
                    source.SetSource(cachedIconFileStream.AsRandomAccessStream());
                }
                else
                {
                    await using Stream cachedIconFileStream = cachedIconFileInfo.OpenRead();
                    // Set the source from cached icon stream
                    source.SetSource(cachedIconFileStream.AsRandomAccessStream());
                }

                // Set event icon props
                ImageEventImgGrid.Visibility = !NeedShowEventIcon ? Visibility.Collapsed : Visibility.Visible;
                ImageEventImg.Source         = source;
                ImageEventImg.Tag            = featuredEventArticleUrl;
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Failed while loading EventPanel image icon\r\n{ex}", LogType.Error, true);
            }
            finally
            {
                _eventPanelProcessing.Remove(featuredEventIconImg,    out _);
                _eventPanelProcessing.Remove(featuredEventArticleUrl, out _);
            }
        }
        #endregion

        #region Carousel

        private async Task StartCarouselAutoScroll(int delaySeconds = 5)
        {
            if (!IsCarouselPanelAvailable) return;
            if (delaySeconds < 5) delaySeconds = 5;
            
            try
            {
                while (true)
                {
                    if (CarouselToken == null || CarouselToken.IsCancellationRequested || CarouselToken.IsDisposed || CarouselToken.IsCancelled)
                    {
                        CarouselToken = new CancellationTokenSourceWrapper();
                    }
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), CarouselToken.Token);
                    if (!IsCarouselPanelAvailable) return;
                    if (ImageCarousel.SelectedIndex != GameNewsData!.NewsCarousel!.Count - 1 
                        && ImageCarousel.SelectedIndex < ImageCarousel.Items.Count - 1)
                        ImageCarousel.SelectedIndex++;
                    else
                        for (int i = GameNewsData.NewsCarousel.Count; i > 0; i--)
                        {
                            if (i - 1 >= 0 && i - 1 < ImageCarousel.Items.Count)
                            {
                                ImageCarousel.SelectedIndex = i - 1;
                            }
                            if (CarouselToken is { IsDisposed: false, IsCancellationRequested: false })
                            {
                                await Task.Delay(100, CarouselToken.Token);
                            }
                        }
                }
            }
            catch (TaskCanceledException)
            {
                // Ignore
            }
            catch (Exception ex)
            {
                LogWriteLine($"[HomePage::StartCarouselAutoScroll] Task returns error!\r\n{ex}", LogType.Error, true);
                _ = CarouselRestartScroll();
            }
        }

        private async void CarouselPointerExited(object sender = null, PointerRoutedEventArgs e = null) =>
            await CarouselRestartScroll();

        private async void CarouselPointerEntered(object sender = null, PointerRoutedEventArgs e = null) =>
            await CarouselStopScroll();

        public async Task CarouselRestartScroll(int delaySeconds = 5)
        {
            // Don't restart carousel if game is running and LoPrio is on
            if (_cachedIsGameRunning && GetAppConfigValue("LowerCollapsePrioOnGameLaunch").ToBool()) return;
            await CarouselStopScroll();

            CarouselToken = new CancellationTokenSourceWrapper();
            _ = StartCarouselAutoScroll(delaySeconds);
        }

        public async ValueTask CarouselStopScroll()
        {
            if (CarouselToken is { IsCancellationRequested: false, IsDisposed: false, IsCancelled: false })
            {
                await CarouselToken.CancelAsync();
                CarouselToken.Dispose();
            }
        }

        private async void HideImageCarousel(bool hide)
        {
            if (!hide)
                SidePanel.Visibility = Visibility.Visible;

            HideImageEventImg(hide);

            Storyboard      storyboard       = new Storyboard();
            DoubleAnimation OpacityAnimation = new DoubleAnimation
            {
                From     = hide ? 1 : 0,
                To       = hide ? 0 : 1,
                Duration = new Duration(TimeSpan.FromSeconds(0.10))
            };

            Storyboard.SetTarget(OpacityAnimation, SidePanel);
            Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
            storyboard.Children.Add(OpacityAnimation);

            storyboard.Begin();

            await Task.Delay(100);

            SidePanel.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
        }
        #endregion

        #region SocMed Buttons
        private void FadeInSocMedButton(object sender, PointerRoutedEventArgs e)
        {
            Button btn = (Button)sender;
            btn.Translation = Shadow16;

            Grid             iconGrid   = btn.FindDescendant<Grid>();
            FrameworkElement iconFirst  = iconGrid!.FindDescendant("Icon");
            FrameworkElement iconSecond = iconGrid!.FindDescendant("IconHover");

            ElementScaleOutHoveredPointerEnteredInner(iconGrid, 0, -2);

            TimeSpan dur = TimeSpan.FromSeconds(0.25f);
            iconFirst.StartAnimationDetached(dur, iconFirst.GetElementCompositor().CreateScalarKeyFrameAnimation("Opacity", 0.0f, delay: TimeSpan.FromSeconds(0.08f)));
            iconSecond.StartAnimationDetached(dur, iconFirst.GetElementCompositor().CreateScalarKeyFrameAnimation("Opacity", 1.0f));
        }

        private void FadeOutSocMedButton(object sender, PointerRoutedEventArgs e)
        {
            Button btn = (Button)sender;
            btn.Translation = new Vector3(0);

            FlyoutBase flyout = btn.Flyout;
            Point pos = e.GetCurrentPoint(btn).Position;
            if (pos.Y <= 0 || pos.Y >= btn.Height || pos.X <= -8 || pos.X >= btn.Width)
            {
                flyout?.Hide();
            }

            Grid             iconGrid   = btn.FindDescendant<Grid>();
            FrameworkElement iconFirst  = iconGrid!.FindDescendant("Icon");
            FrameworkElement iconSecond = iconGrid!.FindDescendant("IconHover");

            ElementScaleInHoveredPointerExitedInner(iconGrid, 0, -2);

            TimeSpan dur = TimeSpan.FromSeconds(0.25f);
            iconFirst.StartAnimationDetached(dur, iconFirst.GetElementCompositor().CreateScalarKeyFrameAnimation("Opacity", 1.0f));
            iconSecond.StartAnimationDetached(dur, iconFirst.GetElementCompositor().CreateScalarKeyFrameAnimation("Opacity", 0.0f, delay: TimeSpan.FromSeconds(0.08f)));
        }

        private async void HideSocialMediaPanel(bool hide)
        {
            if (!hide)
            {
                SocMedPanel.Visibility = Visibility.Visible;
            }

            Storyboard      storyboard       = new Storyboard();
            DoubleAnimation OpacityAnimation = new DoubleAnimation
            {
                From     = hide ? 1 : 0,
                To       = hide ? 0 : 1,
                Duration = new Duration(TimeSpan.FromSeconds(0.10))
            };

            Storyboard.SetTarget(OpacityAnimation, SocMedPanel);
            Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
            storyboard.Children.Add(OpacityAnimation);

            storyboard.Begin();

            await Task.Delay(100);

            SocMedPanel.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
        }

        private async void HidePlaytimeButton(bool hide)
        {
            if (!hide) PlaytimeBtn.Visibility = Visibility.Visible;

            Storyboard      storyboard       = new Storyboard();
            DoubleAnimation OpacityAnimation = new DoubleAnimation
            {
                From     = hide ? 1 : 0,
                To       = hide ? 0 : 1,
                Duration = new Duration(TimeSpan.FromSeconds(0.10))
            };

            Storyboard.SetTarget(OpacityAnimation, PlaytimeBtn);
            Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
            storyboard.Children.Add(OpacityAnimation);

            storyboard.Begin();

            await Task.Delay(100);

            PlaytimeBtn.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OpenSocMedLink(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty((sender as Button)?.Tag as string)) return;

            new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = ((Button)sender).Tag.ToString()!
                }
            }.Start();
        }

        private void ShowSocMedFlyout(object sender, RoutedEventArgs e)
        {
            if (sender is not ToolTip { Tag: Button button })
            {
                return;
            }

            if (!button.IsPointerOver && _lastSocMedButton == button)
                return;
            _lastSocMedButton = button;

            Flyout flyout = button.Flyout as Flyout;
            if (flyout != null)
            {
                Panel contextPanel = flyout.Content as Panel;
                if (contextPanel != null && contextPanel.Tag is LauncherGameNewsSocialMedia socMedData)
                {
                    if (!socMedData.IsHasDescription && !socMedData.IsHasLinks && !socMedData.IsHasQr)
                    {
                        return;
                    }
                }
            }

            FlyoutBase.ShowAttachedFlyout(button);
        }

        private void HideSocMedFlyout(object sender, RoutedEventArgs e)
        {
            Grid dummyGrid = (sender as Panel ?? throw new InvalidOperationException()).FindChild<Grid>();
            if (dummyGrid == null)
            {
                return;
            }

            Flyout flyout = dummyGrid.Tag as Flyout;
            flyout?.Hide();
        }

        private void OnLoadedSocMedFlyout(object sender, RoutedEventArgs e)
        {
            // Prevent the flyout showing when there is no content visible
            StackPanel stackPanel = sender as StackPanel;

            if (stackPanel == null)
            {
                return;
            }

            ApplySocialMediaBinding(stackPanel);

            bool visible = false;
            foreach (var child in stackPanel!.Children)
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
            //if (!NeedShowEventIcon) return;

            if (!hide)
                ImageEventImgGrid.Visibility = Visibility.Visible;

            Storyboard      storyboard       = new Storyboard();
            DoubleAnimation OpacityAnimation = new DoubleAnimation
            {
                From     = hide ? 1 : 0,
                To       = hide ? 0 : 1,
                Duration = new Duration(TimeSpan.FromSeconds(0.10))
            };

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
            SpawnWebView2.SpawnWebView2Window(((ImageEx.ImageEx)sender).Tag.ToString(), Content);
        }

        private async void OpenButtonLinkFromTag(object sender, RoutedEventArgs e)
        {
            // Get the tag string.
            string tagContent = ((ButtonBase)sender).Tag.ToString();

            // Split the tag string by $ character as separator.
            string[] tagProperty = tagContent!.Split('$');

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
                    LogWriteLine($"Tag Property seems to be invalid or incomplete. Fallback to open the URL instead!\r\nTag String: {tagProperty[1]}", LogType.Warning, true);
                    SpawnWebView2.SpawnWebView2Window(tagProperty[0], Content);
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
            SpawnWebView2.SpawnWebView2Window(tagProperty[0], Content);
        }

        private void ClickImageEventSpriteLink(object sender, PointerRoutedEventArgs e)
        {
            if (!e.GetCurrentPoint((UIElement)sender).Properties.IsLeftButtonPressed) return;
            object ImageTag = ((Image)sender).Tag;
            if (ImageTag == null) return;
            SpawnWebView2.SpawnWebView2Window((string)ImageTag, Content);
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
                SentryHelper.ExceptionHandler(ex);
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
            if (properties.Length == 0) throw new ArgumentNullException(properties.Length.ToString(),
                                                                        "Properties for OpenExternalApp can't be empty!");

            // Iterate the properties array
            for (int i = properties.Length - 1; i >= 0; i--)
            {
                // Split the property by = mark to get the argument and its value.
                string[] argumentStr = properties[i].Split("=");

                // If the argument array is empty, then throw and back to fallback (open the URL).
                if (argumentStr.Length == 0) throw new ArgumentNullException(argumentStr.Length.ToString(),
                                                                             "Argument can't be empty!");

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

            // Check if the Application path config exists. If not, then return empty string. Otherwise, return the actual value.
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
                            // Try to get the file path
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
                // Try to run the application
                Process proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        Verb = runAsAdmin ? "runas" : "",
                        FileName = applicationPath,
                        WorkingDirectory = Path.GetDirectoryName(applicationPath)!
                    }
                };
                proc.Start();
            }
            catch (Exception ex)
            {
                // If error happened while running the app, then log and return true as successful
                // Thoughts @Cry0? Should we mark it as successful (true) or failed (false)?
                // Mark is as true, since the app still ran, but it's an error outside our scope.
                await SentryHelper.ExceptionHandlerAsync(ex);
                LogWriteLine($"Unable to start app {applicationName}! {ex}", LogType.Error, true);
                return true;
            }

            // If all above are executed successfully, then return true as "successful"
            return true;
        }
        #endregion

        #region Community Button
        private void OpenCommunityButtonLink(object sender, RoutedEventArgs e)
        {
            CommunityToolsBtn.Flyout.Hide();
            OpenButtonLinkFromTag(sender, e);
        }
        #endregion

        #region Open Button Method
        private async void OpenGameFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string gameFolder = NormalizePath(GameDirPath);
                LogWriteLine($"Opening Game Folder:\r\n\t{gameFolder}");

                await Task.Run(() =>
                    new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            UseShellExecute = true,
                            FileName = "explorer.exe",
                            Arguments = gameFolder
                        }
                    }.Start());
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed when trying to open game folder!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private async void OpenCacheFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string cacheFolder = CurrentGameProperty.GameVersion.GameDirAppDataPath;
            LogWriteLine($"Opening Game Folder:\r\n\t{cacheFolder}");
            try
            {
                await Task.Run(() =>
                    new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            UseShellExecute = true,
                            FileName = "explorer.exe",
                            Arguments = cacheFolder
                        }
                    }.Start());
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed when trying to open game cache folder!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private async void OpenScreenshotFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string ScreenshotFolder = Path.Combine(NormalizePath(GameDirPath), CurrentGameProperty.GameVersion.GamePreset.GameType switch
            {
                GameNameType.StarRail => $"{Path.GetFileNameWithoutExtension(CurrentGameProperty.GameVersion.GamePreset.GameExecutableName)}_Data\\ScreenShots",
                _ => "ScreenShot"
            });

            LogWriteLine($"Opening Screenshot Folder:\r\n\t{ScreenshotFolder}");

            if (!Directory.Exists(ScreenshotFolder))
                Directory.CreateDirectory(ScreenshotFolder);

            try
            {
                await Task.Run(() => 
                    new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            UseShellExecute = true,
                            FileName = "explorer.exe",
                            Arguments = ScreenshotFolder
                        }
                    }.Start());
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed when trying to open game screenshot folder!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private async void CleanupFilesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GameStartupSetting.Flyout.Hide();
                if (CurrentGameProperty is not null)
                    await CurrentGameProperty.GameInstall.CleanUpGameFiles();
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex);
            }
        }
        #endregion
        #region Set Hand Cursor
        private void SetHandCursor(object sender, RoutedEventArgs e = null) =>
            (sender as UIElement)?.SetCursor(InputSystemCursor.Create(InputSystemCursorShape.Hand));
        #endregion

        #region Hyper Link Color
        private void HyperLink_OnPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            TextBlock textBlock = null;
            switch (sender)
            {
                case Grid grid when grid.Children[0] is TextBlock:
                    textBlock = (TextBlock)grid.Children[0];
                    break;
                case Grid grid when grid.Children[0] is CompressedTextBlock compressedTextBlock:
                    compressedTextBlock.Foreground = (Brush)Application.Current.Resources["AccentColor"];
                    return;
                case TextBlock block:
                    textBlock = block;
                    break;
            }
            if (textBlock != null)
                textBlock.Foreground = UIElementExtensions.GetApplicationResource<Brush>("AccentColor");
        }

        private void HyperLink_OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            TextBlock textBlock = null;
            switch (sender)
            {
                case Grid grid when grid.Children[0] is TextBlock:
                    textBlock = (TextBlock)grid.Children[0];
                    break;
                case Grid grid when grid.Children[0] is CompressedTextBlock compressedTextBlock:
                    compressedTextBlock.Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
                    return;
                case TextBlock block:
                    textBlock = block;
                    break;
            }
            if (textBlock != null)
                textBlock.Foreground = UIElementExtensions.GetApplicationResource<Brush>("TextFillColorPrimaryBrush");
        }
        #endregion
        
        #region Side Panel
        private bool IsPointerInsideSidePanel;
        private bool IsSidePanelCurrentlyScaledOut;

        private async void SidePanelScaleOutHoveredPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (!IsEventsPanelScaleUp) return;

            IsPointerInsideSidePanel = true;
            if (sender is not FrameworkElement elementPanel)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(0.2));
            if (IsSidePanelCurrentlyScaledOut) return;
            if (!IsPointerInsideSidePanel) return;

            var toScale    = WindowSize.WindowSize.CurrentWindowSize.PostEventPanelScaleFactor;
            var storyboard = new Storyboard();
            var transform  = (CompositeTransform)elementPanel.RenderTransform;
            transform.CenterY = elementPanel.ActualHeight + 8;
            var cubicEaseOut = new CubicEase
            {
                EasingMode = EasingMode.EaseOut
            };

            var scaleXAnim = new DoubleAnimation
            {
                From           = transform.ScaleX,
                To             = toScale,
                Duration       = new Duration(TimeSpan.FromSeconds(0.2)),
                EasingFunction = cubicEaseOut
            };
            Storyboard.SetTarget(scaleXAnim, transform);
            Storyboard.SetTargetProperty(scaleXAnim, "ScaleX");
            storyboard.Children.Add(scaleXAnim);

            var scaleYAnim = new DoubleAnimation
            {
                From           = transform.ScaleY,
                To             = toScale,
                Duration       = new Duration(TimeSpan.FromSeconds(0.2)),
                EasingFunction = cubicEaseOut
            };
            Storyboard.SetTarget(scaleYAnim, transform);
            Storyboard.SetTargetProperty(scaleYAnim, "ScaleY");
            storyboard.Children.Add(scaleYAnim);

            MainPage.CurrentBackgroundHandler?.Dimm();
            HideImageEventImg(true);

            IsSidePanelCurrentlyScaledOut = true;
            await storyboard.BeginAsync();
        }

        private async void SidePanelScaleInHoveredPointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (!IsEventsPanelScaleUp) return;

            IsPointerInsideSidePanel = false;
            if (sender is not FrameworkElement elementPanel)
            {
                return;
            }

            if (!IsSidePanelCurrentlyScaledOut) return;

            MainPage.CurrentBackgroundHandler?.Undimm();
            HideImageEventImg(false);

            var storyboard = new Storyboard();
            var transform  = (CompositeTransform)elementPanel.RenderTransform;
            transform.CenterY = elementPanel.ActualHeight + 8;
            var cubicEaseOut = new CubicEase
            {
                EasingMode = EasingMode.EaseOut
            };

            var scaleXAnim = new DoubleAnimation
            {
                From           = transform.ScaleX,
                To             = 1,
                Duration       = new Duration(TimeSpan.FromSeconds(0.25)),
                EasingFunction = cubicEaseOut
            };
            Storyboard.SetTarget(scaleXAnim, transform);
            Storyboard.SetTargetProperty(scaleXAnim, "ScaleX");
            storyboard.Children.Add(scaleXAnim);

            var scaleYAnim = new DoubleAnimation
            {
                From           = transform.ScaleY,
                To             = 1,
                Duration       = new Duration(TimeSpan.FromSeconds(0.25)),
                EasingFunction = cubicEaseOut
            };
            Storyboard.SetTarget(scaleYAnim, transform);
            Storyboard.SetTargetProperty(scaleYAnim, "ScaleY");
            storyboard.Children.Add(scaleYAnim);

            await storyboard.BeginAsync();
            IsSidePanelCurrentlyScaledOut = false;
        }
        
        #endregion

        #region Element Scale
        private void ElementScaleOutHoveredPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement elementPanel)
            {
                ElementScaleOutHoveredPointerEnteredInner(elementPanel);
            }
        }

        private async void ElementScaleOutHoveredPointerEnteredInner(FrameworkElement element,
            float xElevation = 0, float yElevation = -4)
        {
            Compositor compositor = this.GetElementCompositor();

            const float toScale       = 1.05f;
            Vector3     fromTranslate = new Vector3(0, 0, element.Translation.Z);
            // ReSharper disable ConstantConditionalAccessQualifier
            // ReSharper disable ConstantNullCoalescingCondition
            Vector3 toTranslate = new Vector3(-((float)(element?.ActualWidth ?? 0) * (toScale - 1f) / 2) + xElevation,
                                              -((float)(element?.ActualHeight ?? 0) * (toScale - 1f)) + yElevation,
                                              element.Translation.Z);
            // ReSharper restore ConstantConditionalAccessQualifier
            // ReSharper restore ConstantNullCoalescingCondition
            await element.StartAnimation(
                TimeSpan.FromSeconds(0.25),
                compositor.CreateVector3KeyFrameAnimation("Translation", toTranslate, fromTranslate),
                compositor.CreateVector3KeyFrameAnimation("Scale", new Vector3(toScale))
                );
        }

        private void ElementScaleInHoveredPointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement elementPanel)
            {
                ElementScaleInHoveredPointerExitedInner(elementPanel);
            }
        }

        private async void ElementScaleInHoveredPointerExitedInner(FrameworkElement element,
            float xElevation = 0, float yElevation = -4)
        {
            Compositor compositor = this.GetElementCompositor();

            const float toScale = 1.05f;
            // ReSharper disable ConstantConditionalAccessQualifier
            // ReSharper disable ConstantNullCoalescingCondition
            Vector3 fromTranslate = new Vector3(0, 0, element.Translation.Z);
            Vector3 toTranslate = new Vector3(-((float)(element?.ActualWidth ?? 0) * (toScale - 1f) / 2) + xElevation,
                                              -((float)(element?.ActualHeight ?? 0) * (toScale - 1f)) + yElevation,
                                              element.Translation.Z);
            // ReSharper restore ConstantConditionalAccessQualifier
            // ReSharper restore ConstantNullCoalescingCondition
            await element.StartAnimation(
                TimeSpan.FromSeconds(0.25),
                compositor.CreateVector3KeyFrameAnimation("Translation", fromTranslate, toTranslate),
                compositor.CreateVector3KeyFrameAnimation("Scale", new Vector3(1.0f))
                );
        }
        
        #endregion

        private void ApplyShadowToImageElement(object sender, RoutedEventArgs e)
        {
            if (sender is not ButtonBase { Content: Panel panel })
            {
                return;
            }

            bool isStart = true;
            foreach (Image imageElement in panel.Children.OfType<Image>())
            {
                imageElement.ApplyDropShadow(opacity: 0.5f);
                if (!isStart)
                {
                    continue;
                }

                imageElement.Opacity = 0.0f;
                imageElement.Loaded += (_, _) =>
                                       {
                                           Compositor compositor = imageElement.GetElementCompositor();
                                           imageElement.StartAnimationDetached(TimeSpan.FromSeconds(0.25f),
                                                                               compositor.CreateScalarKeyFrameAnimation("Opacity", 1.0f));
                                       };
                isStart = false;
            }

            foreach (ImageEx.ImageEx imageElement in panel.Children.OfType<ImageEx.ImageEx>())
            {
                imageElement.ApplyDropShadow(opacity: 0.5f);
                if (!isStart)
                {
                    continue;
                }

                imageElement.Opacity = 0.0f;
                imageElement.Loaded += (_, _) =>
                                       {
                                           Compositor compositor = imageElement.GetElementCompositor();
                                           imageElement.StartAnimationDetached(TimeSpan.FromSeconds(0.25f),
                                                                               compositor.CreateScalarKeyFrameAnimation("Opacity", 1.0f));
                                       };
                isStart = false;
            }
        }
    }
}
