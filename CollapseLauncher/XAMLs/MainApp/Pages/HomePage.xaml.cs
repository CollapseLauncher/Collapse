using CollapseLauncher.Dialogs;
using CollapseLauncher.Interfaces;
using CollapseLauncher.Statics;
using CommunityToolkit.WinUI.UI.Controls;
using Hi3Helper;
using Hi3Helper.Http;
using Hi3Helper.Preset;
using Hi3Helper.Screen;
using Hi3Helper.Shared.ClassStruct;
#if !DISABLEDISCORD
using Hi3Helper.DiscordPresence;
#endif
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Text;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public static class HomePageProp
    {
        public static HomePage Current { get; set; }
    }

    public sealed partial class HomePage : Page
    {
        Http HttpTool = new Http();
        public RegionResourceProp GameAPIProp { get => PageStatics._GameVersion.GameAPIProp; }
        public HomeMenuPanel MenuPanels => regionNewsProp;
        CancellationTokenSource PageToken = new CancellationTokenSource();
        CancellationTokenSource CarouselToken = new CancellationTokenSource();
        public HomePage()
        {
            this.InitializeComponent();
            CheckIfRightSideProgress();
            this.Loaded += StartLoadedRoutine;
        }

        private bool IsPageUnload = false;
        private bool NeedShowEventIcon = true;

        private void ReturnToHomePage()
        {
            if (!IsPageUnload)
            {
                MainFrameChanger.ChangeMainFrame(typeof(HomePage));
            }
        }

        private async void StartLoadedRoutine(object sender, RoutedEventArgs e)
        {
            try
            {
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

                HomePageProp.Current = this;

                if (await PageStatics._GameInstall.TryShowFailedDeltaPatchState()) return;
                if (await PageStatics._GameInstall.TryShowFailedGameConversionState()) return;

                CheckRunningGameInstance(PageToken.Token);
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

        private void OpenImageLinkFromTag(object sender, PointerRoutedEventArgs e)
        {
            SpawnWebView2.SpawnWebView2Window(((ImageEx)sender).Tag.ToString());
        }

        private void OpenButtonLinkFromTag(object sender, RoutedEventArgs e)
        {
            SpawnWebView2.SpawnWebView2Window(((Button)sender).Tag.ToString());
        }

        private void CheckIfRightSideProgress()
        {
            if (PageStatics._GameVersion.GamePreset.UseRightSideProgress ?? false)
            {
                FrameGrid.ColumnDefinitions[0].Width = new GridLength(248, GridUnitType.Pixel);
                FrameGrid.ColumnDefinitions[1].Width = new GridLength(1224, GridUnitType.Star);
                LauncherBtn.SetValue(Grid.ColumnProperty, 0);
                ProgressStatusGrid.SetValue(Grid.ColumnProperty, 0);
                GameStartupSetting.SetValue(Grid.ColumnProperty, 1);
                GameStartupSetting.HorizontalAlignment = HorizontalAlignment.Right;
            }
        }

        private void GetCurrentGameState()
        {
            Visibility RepairGameButtonVisible = (PageStatics._GameVersion.GamePreset.IsRepairEnabled ?? false) ? Visibility.Visible : Visibility.Collapsed;

            if ((!(PageStatics._GameVersion.GamePreset.IsConvertible ?? false)) || (PageStatics._GameVersion.GameType != GameType.Honkai))
                ConvertVersionButton.Visibility = Visibility.Collapsed;

            if (PageStatics._GameVersion.GameType == GameType.Genshin)
            {
                OpenCacheFolderButton.Visibility = Visibility.Collapsed;
            }

            GameInstallationState = PageStatics._GameVersion.GetGameState();
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

            UninstallGameButton.IsEnabled = false;
            RepairGameButton.IsEnabled = false;
            OpenGameFolderButton.IsEnabled = false;
            OpenCacheFolderButton.IsEnabled = false;
            ConvertVersionButton.IsEnabled = false;
            CustomArgsTextBox.IsEnabled = false;
            OpenScreenshotFolderButton.IsEnabled = false;
        }

        private void SpawnPreloadBox()
        {
            PreloadDialogBox.Translation += Shadow48;
            PreloadDialogBox.Closed += PreloadDialogBox_Closed;
            PreloadDialogBox.IsOpen = true;

            string ver = PageStatics._GameVersion.GetGameVersionAPIPreload()?.VersionString;

            try
            {
                if (PageStatics._GameVersion.IsGameHasDeltaPatch())
                {
                    PreloadDialogBox.Title = string.Format(Lang._HomePage.PreloadNotifDeltaDetectTitle, ver);
                    PreloadDialogBox.Message = Lang._HomePage.PreloadNotifDeltaDetectSubtitle;
                    DownloadPreBtn.Visibility = Visibility.Collapsed;
                    return;
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"An error occured while trying to determine delta-patch availability\r\n{ex}", LogType.Error, true);
            }

            if (!PageStatics._GameInstall.IsPreloadCompleted())
            {
                PreloadDialogBox.Message = string.Format(Lang._HomePage.PreloadNotifSubtitle, ver);
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
        }

        private void PreloadDialogBox_Closed(InfoBar sender, InfoBarClosedEventArgs args)
        {
            sender.Translation -= Shadow48;
            HideImageEventImg(false);
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
                        GameStartupSetting.IsEnabled = false;

                        await Task.Delay(100, Token);
#if !DISABLEDISCORD
                        AppDiscordPresence.SetActivity(ActivityType.Play, 0);
#endif
                    }

                    if (!StartGameBtn.IsEnabled)
                        LauncherBtn.Translation += Shadow16;

                    StartGameBtn.IsEnabled = true;
                    StartGameBtn.Content = BtnStartGame;
                    GameStartupSetting.IsEnabled = true;

                    await Task.Delay(100, Token);
#if !DISABLEDISCORD
                    AppDiscordPresence.SetActivity(ActivityType.Idle, 0);
#endif
                }
            }
            catch { return; }
        }

        private void AnimateGameRegSettingIcon_Start(object sender, PointerRoutedEventArgs e) => AnimatedIcon.SetState(this.GameRegionSettingIcon, "PointerOver");
        private void AnimateGameRegSettingIcon_End(object sender, PointerRoutedEventArgs e) => AnimatedIcon.SetState(this.GameRegionSettingIcon, "Normal");
        private async void InstallGameDialog(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PageStatics._GameVersion.GamePreset.UseRightSideProgress ?? false)
                    HideImageCarousel(true);

                progressRing.Value = 0;
                progressRing.IsIndeterminate = true;
                ProgressStatusGrid.Visibility = Visibility.Visible;
                InstallGameBtn.Visibility = Visibility.Collapsed;
                CancelDownloadBtn.Visibility = Visibility.Visible;
                ProgressTimeLeft.Visibility = Visibility.Visible;

                PageStatics._GameInstall.ProgressChanged += GameInstall_ProgressChanged;
                PageStatics._GameInstall.StatusChanged += GameInstall_StatusChanged;

                int dialogResult = await PageStatics._GameInstall.GetInstallationPath();
                if (dialogResult < 0)
                {
                    return;
                }
                if (dialogResult == 0)
                {
                    PageStatics._GameInstall.ApplyGameConfig();
                    return;
                }

                int verifResult;
                bool skipDialog = false;
                while ((verifResult = await PageStatics._GameInstall.StartPackageVerification()) == 0)
                {
                    await PageStatics._GameInstall.StartPackageDownload(skipDialog);
                    skipDialog = true;
                }
                if (verifResult == -1)
                {
                    PageStatics._GameInstall.ApplyGameConfig(true);
                    return;
                }

                await PageStatics._GameInstall.StartPackageInstallation();
                await PageStatics._GameInstall.StartPostInstallVerification();
                PageStatics._GameInstall.ApplyGameConfig(true);
            }
            catch (TaskCanceledException)
            {
                LogWriteLine($"Installation cancelled for game {PageStatics._GameVersion.GamePreset.ZoneFullname}");
            }
            catch (OperationCanceledException)
            {
                LogWriteLine($"Installation cancelled for game {PageStatics._GameVersion.GamePreset.ZoneFullname}");
            }
            catch (NullReferenceException ex)
            {
                IsPageUnload = true;
                LogWriteLine($"Error while installing game {PageStatics._GameVersion.GamePreset.ZoneName}\r\n{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(new NullReferenceException("Oops, the launcher cannot finalize the installation but don't worry, your game has been totally updated.\r\t" +
                    $"Please report this issue to our GitHub here: https://github.com/neon-nyan/CollapseLauncher/issues/new or come back to the launcher and make sure to use Repair Game in Game Settings button later.\r\nThrow: {ex}", ex));
            }
            catch (Exception ex)
            {
                IsPageUnload = true;
                LogWriteLine($"Error while installing game {PageStatics._GameVersion.GamePreset.ZoneName}.\r\n{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex, ErrorType.Unhandled);
            }
            finally
            {
                PageStatics._GameInstall.ProgressChanged -= GameInstall_ProgressChanged;
                PageStatics._GameInstall.StatusChanged -= GameInstall_StatusChanged;
                PageStatics._GameInstall.Flush();
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

        string InstallDownloadSpeedString;
        string InstallDownloadSizeString;
        string InstallDownloadPerSizeString;
        string DownloadSizeString;
        string DownloadPerSizeString;

        private void InstallerDownloadPreStatusChanged(object sender, DownloadEvent e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                InstallDownloadSpeedString = SummarizeSizeSimple(e.Speed);
                InstallDownloadSizeString = SummarizeSizeSimple(e.SizeDownloaded);
                DownloadSizeString = SummarizeSizeSimple(e.SizeToBeDownloaded);
                ProgressPreStatusSubtitle.Text = string.Format(Lang._Misc.PerFromTo, InstallDownloadSizeString, DownloadSizeString);
                ProgressPreStatusFooter.Text = string.Format(Lang._Misc.Speed, InstallDownloadSpeedString);
                ProgressPreTimeLeft.Text = string.Format(Lang._Misc.TimeRemainHMSFormat, e.TimeLeft);
                progressPreBar.Value = Math.Round(e.ProgressPercentage, 2);
                progressPreBar.IsIndeterminate = false;
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

        private void CancelPreDownload()
        {
            HttpTool.DownloadProgress -= InstallerDownloadPreStatusChanged;

            PageStatics._GameInstall.CancelRoutine();

            PauseDownloadPreBtn.Visibility = Visibility.Collapsed;
            ResumeDownloadPreBtn.Visibility = Visibility.Visible;
            ResumeDownloadPreBtn.IsEnabled = true;
        }

        private void CancelUpdateDownload()
        {
            PageStatics._GameInstall.CancelRoutine();

            ProgressStatusGrid.Visibility = Visibility.Collapsed;
            UpdateGameBtn.Visibility = Visibility.Visible;
            CancelDownloadBtn.Visibility = Visibility.Collapsed;
        }

        private void CancelInstallationDownload()
        {
            PageStatics._GameInstall.CancelRoutine();

            ProgressStatusGrid.Visibility = Visibility.Collapsed;
            InstallGameBtn.Visibility = Visibility.Visible;
            CancelDownloadBtn.Visibility = Visibility.Collapsed;
        }

        CancellationTokenSource WatchOutputLog = new CancellationTokenSource();
        private async void StartGame(object sender, RoutedEventArgs e)
        {
            try
            {
                bool IsContinue = await CheckMediaPackInstalled();

                if (!IsContinue) return;
                Process proc = new Process();
                proc.StartInfo.FileName = Path.Combine(NormalizePath(GameDirPath), PageStatics._GameVersion.GamePreset.GameExecutableName);
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.Arguments = GetLaunchArguments();
                LogWriteLine($"Running game with parameters:\r\n{proc.StartInfo.Arguments}");
                proc.StartInfo.WorkingDirectory = Path.GetDirectoryName(NormalizePath(GameDirPath));
                proc.StartInfo.Verb = "runas";
                proc.Start();

                WatchOutputLog = new CancellationTokenSource();

                ReadOutputLog();
                GameLogWatcher();

                await proc.WaitForExitAsync();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                LogWriteLine($"There is a problem while trying to launch Game with Region: {PageStatics._GameVersion.GamePreset.ZoneName}\r\nTraceback: {ex}", LogType.Error, true);
            }
        }


        #region LaunchArgumentBuilder
        bool RequireWindowExclusivePayload = false;
        public string GetLaunchArguments()
        {
            StringBuilder parameter = new StringBuilder();

            IGameSettingsUniversal _Settings = PageStatics._GameSettings.AsIGameSettingsUniversal();
            if (PageStatics._GameVersion.GameType == GameType.Honkai)
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
            if (PageStatics._GameVersion.GameType == GameType.StarRail)
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
            if (!GetAppConfigValue("EnableConsole").ToBool())
                parameter.Append("-nolog ");

            string customArgs = _Settings.SettingsCustomArgument.CustomArgumentValue;

            if (!string.IsNullOrEmpty(customArgs))
                parameter.Append(customArgs);

            return parameter.ToString();
        }

        #endregion


        public async Task<bool> CheckMediaPackInstalled()
        {
            if (PageStatics._GameVersion.GamePreset.IsGenshin ?? false) return true;

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

        public async void StartExclusiveWindowPayload()
        {
            IntPtr _windowPtr = InvokeProp.GetProcessWindowHandle(PageStatics._GameVersion.GamePreset.GameExecutableName);
            await Task.Delay(1000);
            new InvokeProp.InvokePresence(_windowPtr).HideWindow();
            await Task.Delay(1000);
            new InvokeProp.InvokePresence(_windowPtr).ShowWindow();
        }

        public async void ReadOutputLog()
        {
            int consoleWidth = 24;
            try { consoleWidth = Console.BufferWidth; } catch { }

            string line;
            int barwidth = ((consoleWidth - 22) / 2) - 1;
            LogWriteLine($"{new string('=', barwidth)} GAME STARTED {new string('=', barwidth)}", LogType.Warning, true);
            try
            {
                m_presenter.Minimize();
                string logPath = Path.Combine(PageStatics._GameVersion.GameDirAppDataPath, PageStatics._GameVersion.GameOutputLogName);

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
                            LogWriteLine(line, LogType.Game, true);
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

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            IsPageUnload = true;
            PageToken.Cancel();
            CarouselToken.Cancel();
            PageStatics._GameInstall.CancelRoutine();
            GC.Collect();
        }

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
            string GameFolder = PageStatics._GameVersion.GameDirAppDataPath;
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
            string ScreenshotFolder = Path.Combine(NormalizePath(GameDirPath), "ScreenShot");
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

        private void RepairGameButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrameChanger.ChangeMainFrame(typeof(RepairPage));
        }

        private async void UninstallGameButton_Click(object sender, RoutedEventArgs e)
        {
            if (await PageStatics._GameInstall.UninstallGame())
            {
                MainFrameChanger.ChangeMainFrame(typeof(HomePage));
            }
        }

        private async void UpdateGameDialog(object sender, RoutedEventArgs e)
        {
            PageStatics._GameInstall.ProgressChanged += GameInstall_ProgressChanged;
            PageStatics._GameInstall.StatusChanged += GameInstall_StatusChanged;

            if (PageStatics._GameVersion.GamePreset.UseRightSideProgress ?? false)
                HideImageCarousel(true);

            try
            {
                ProgressStatusGrid.Visibility = Visibility.Visible;
                UpdateGameBtn.Visibility = Visibility.Collapsed;
                CancelDownloadBtn.Visibility = Visibility.Visible;

                int verifResult;
                bool skipDialog = false;
                while ((verifResult = await PageStatics._GameInstall.StartPackageVerification()) == 0)
                {
                    await PageStatics._GameInstall.StartPackageDownload(skipDialog);
                    skipDialog = true;
                }
                if (verifResult == -1)
                {
                    return;
                }

                await PageStatics._GameInstall.StartPackageInstallation();
                await PageStatics._GameInstall.StartPostInstallVerification();
                PageStatics._GameInstall.ApplyGameConfig(true);
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
                LogWriteLine($"Update error on {PageStatics._GameVersion.GamePreset.ZoneFullname} game!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(new NullReferenceException("Oops, the launcher cannot finalize the installation but don't worry, your game has been totally updated.\r\t" +
                    $"Please report this issue to our GitHub here: https://github.com/neon-nyan/CollapseLauncher/issues/new or come back to the launcher and make sure to use Repair Game in Game Settings button later.\r\nThrow: {ex}", ex));
            }
            catch (Exception ex)
            {
                IsPageUnload = true;
                LogWriteLine($"Update error on {PageStatics._GameVersion.GamePreset.ZoneFullname} game!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
            finally
            {
                PageStatics._GameInstall.ProgressChanged -= GameInstall_ProgressChanged;
                PageStatics._GameInstall.StatusChanged -= GameInstall_StatusChanged;
                PageStatics._GameInstall.Flush();
                ReturnToHomePage();
            }
        }

        private void ConvertVersionButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrameChanger.ChangeWindowFrame(typeof(InstallationConvert));
        }

        private async void PredownloadDialog(object sender, RoutedEventArgs e)
        {
            ((Button)sender).IsEnabled = false;

            PauseDownloadPreBtn.Visibility = Visibility.Visible;
            ResumeDownloadPreBtn.Visibility = Visibility.Collapsed;
            PreloadDialogBox.IsClosable = false;

            try
            {
                DownloadPreBtn.Visibility = Visibility.Collapsed;
                ProgressPreStatusGrid.Visibility = Visibility.Visible;
                ProgressPreButtonGrid.Visibility = Visibility.Visible;
                PreloadDialogBox.Title = Lang._HomePage.PreloadDownloadNotifbarTitle;
                PreloadDialogBox.Message = Lang._HomePage.PreloadDownloadNotifbarSubtitle;

                PageStatics._GameInstall.ProgressChanged += PreloadDownloadProgress;
                PageStatics._GameInstall.StatusChanged += PreloadDownloadStatus;

                int verifResult = 0;
                while (verifResult != 1)
                {
                    await PageStatics._GameInstall.StartPackageDownload(true);

                    PauseDownloadPreBtn.IsEnabled = false;
                    PreloadDialogBox.Title = Lang._HomePage.PreloadDownloadNotifbarVerifyTitle;

                    verifResult = await PageStatics._GameInstall.StartPackageVerification();

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
                PageStatics._GameInstall.ProgressChanged -= PreloadDownloadProgress;
                PageStatics._GameInstall.StatusChanged -= PreloadDownloadStatus;
                PageStatics._GameInstall.Flush();
            }
        }

        private void PreloadDownloadStatus(object sender, TotalPerfileStatus e)
        {
            ProgressPrePerFileStatusFooter.Text = e.ActivityStatus;
        }

        private void PreloadDownloadProgress(object sender, TotalPerfileProgress e)
        {
            InstallDownloadSpeedString = SummarizeSizeSimple(e.ProgressTotalSpeed);
            InstallDownloadSizeString = SummarizeSizeSimple(e.ProgressTotalDownload);
            InstallDownloadPerSizeString = SummarizeSizeSimple(e.DownloadEvent.SizeDownloaded);
            DownloadSizeString = SummarizeSizeSimple(e.ProgressTotalSizeToDownload);
            DownloadPerSizeString = SummarizeSizeSimple(e.DownloadEvent.SizeToBeDownloaded);

            ProgressPreStatusSubtitle.Text = string.Format(Lang._Misc.PerFromTo, InstallDownloadSizeString, DownloadSizeString);
            ProgressPrePerFileStatusSubtitle.Text = string.Format(Lang._Misc.PerFromTo, InstallDownloadPerSizeString, DownloadPerSizeString);
            ProgressPreStatusFooter.Text = string.Format(Lang._Misc.Speed, InstallDownloadSpeedString);
            ProgressPreTimeLeft.Text = string.Format(Lang._Misc.TimeRemainHMSFormat, e.ProgressTotalTimeLeft);
            progressPreBar.Value = Math.Round(e.ProgressTotalPercentage, 2);
            progressPrePerFileBar.Value = Math.Round(e.ProgressPerFilePercentage, 2);
            progressPreBar.IsIndeterminate = false;
            progressPrePerFileBar.IsIndeterminate = false;
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

        public string CustomArgsValue
        {
            get => ((IGameSettingsUniversal)PageStatics._GameSettings).SettingsCustomArgument.CustomArgumentValue;
            set => ((IGameSettingsUniversal)PageStatics._GameSettings).SettingsCustomArgument.CustomArgumentValue = value;
        }

        private void ClickImageEventSpriteLink(object sender, PointerRoutedEventArgs e)
        {
            object ImageTag = ((Image)sender).Tag;
            if (ImageTag == null) return;
            SpawnWebView2.SpawnWebView2Window((string)ImageTag);
        }
    }
}
