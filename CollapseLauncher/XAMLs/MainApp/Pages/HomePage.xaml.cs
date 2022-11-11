using CollapseLauncher.Dialogs;
using CommunityToolkit.WinUI.UI.Controls;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Text;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.FileDialogNative;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Preset.ConfigV2Store;
using static Hi3Helper.Shared.Region.GameConfig;
using static Hi3Helper.Shared.Region.InstallationManagement;
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
        public HomeMenuPanel MenuPanels => regionNewsProp;
        CancellationTokenSource PageToken = new CancellationTokenSource();
        CancellationTokenSource InstallerDownloadTokenSource = new CancellationTokenSource();
        public HomePage()
        {
            this.InitializeComponent();
            CheckIfRightSideProgress();
            this.Loaded += StartLoadedRoutine;
        }

        private bool NeedShowEventIcon = true;

        private async void StartLoadedRoutine(object sender, RoutedEventArgs e)
        {
            try
            {
                GameDirPath = NormalizePath(await LoadGameConfig());

                GetCurrentGameState();

                if (!GetAppConfigValue("ShowEventsPanel").ToBool())
                    ImageCarouselAndPostPanel.Visibility = Visibility.Collapsed;

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

                MigrationWatcher.IsMigrationRunning = false;
                HomePageProp.Current = this;

                await CheckFailedDeltaPatchState();
                await CheckFailedGameConversion();
                CheckRunningGameInstance();
                StartCarouselAutoScroll(PageToken.Token);
            }
            catch (ArgumentNullException ex)
            {
                LogWriteLine($"The necessary section of Launcher Scope's config.ini is broken.\r\n{ex}", Hi3Helper.LogType.Error, true);
                await StartGameConfigBrokenDialog();
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private async Task StartGameConfigBrokenDialog()
        {
            bool IsComplete = false;
            while (!IsComplete)
            {
                await Dialog_GameConfigBroken(Content, gameIni.ProfilePath);
#if DISABLE_COM
                string GamePath = GetFolderPicker();
#else
                string GamePath = await GetFolderPicker();
#endif

                if (IsComplete = CheckExistingGame(GamePath))
                    MainFrameChanger.ChangeWindowFrame(typeof(MainPage));
            }
        }

        private void TryLoadEventPanelImage()
        {
            if (regionNewsProp.eventPanel == null) return;

            ImageEventImgGrid.Visibility = !NeedShowEventIcon ? Visibility.Collapsed : Visibility.Visible;
            ImageEventImg.Source = new BitmapImage(new Uri(regionNewsProp.eventPanel.icon));
            ImageEventImg.Tag = regionNewsProp.eventPanel.url;
        }

        private async void StartCarouselAutoScroll(CancellationToken token = new CancellationToken(), int delay = 5)
        {
            if (MenuPanels.imageCarouselPanel == null) return;
            try
            {
                while (true)
                {
                    await Task.Delay(delay * 1000, token);
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

        private void CarouselStopScroll(object sender, PointerRoutedEventArgs e) => PageToken.Cancel();

        private void CarouselRestartScroll(object sender, PointerRoutedEventArgs e)
        {
            PageToken = new CancellationTokenSource();
            StartCarouselAutoScroll(PageToken.Token);
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

        private async void HideImageEventImg(bool hide)
        {
            if (ImageEventImgGrid.Visibility == Visibility.Collapsed && NeedShowEventIcon) return;

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
            if (CurrentConfigV2.UseRightSideProgress ?? false)
            {
                FrameGrid.ColumnDefinitions[0].Width = new GridLength(248, GridUnitType.Pixel);
                FrameGrid.ColumnDefinitions[1].Width = new GridLength(1224, GridUnitType.Star);
                LauncherBtn.SetValue(Grid.ColumnProperty, 0);
                ProgressStatusGrid.SetValue(Grid.ColumnProperty, 0);
                GameStartupSetting.SetValue(Grid.ColumnProperty, 1);
                GameStartupSetting.HorizontalAlignment = HorizontalAlignment.Right;
            }
        }

        private async Task CheckFailedDeltaPatchState()
        {
            string GamePath = NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString());
            string GamePathIngredients = GamePath + "_Ingredients";
            if (!Directory.Exists(GamePathIngredients)) return;
            LogWriteLine($"Previous failed delta patch has been detected on Game {CurrentConfigV2.ZoneFullname} ({GamePathIngredients})", Hi3Helper.LogType.Warning, true);
            try
            {
                switch (await Dialog_PreviousDeltaPatchInstallFailed(Content))
                {
                    case ContentDialogResult.Primary:
                        RollbackFileContent(GamePath, GamePathIngredients);
                        MainFrameChanger.ChangeMainFrame(typeof(HomePage));
                        break;
                }
            }
            catch
            {
                RollbackFileContent(GamePath, GamePathIngredients);
                MainFrameChanger.ChangeMainFrame(typeof(HomePage));
            }
        }

        private async Task CheckFailedGameConversion()
        {
            string GamePath = NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString());
            string GamePathIngredients = GetFailedGameConversionFolder(GamePath);
            if (GamePathIngredients is null) return;
            if (!Directory.Exists(GamePathIngredients)) return;

            long FileSize = Directory.EnumerateFiles(GamePathIngredients).Sum(x => new FileInfo(x).Length);
            if (FileSize < 1 << 20) return;

            LogWriteLine($"Previous failed game conversion has been detected on Game: {CurrentConfigV2.ZoneFullname} ({GamePathIngredients})", Hi3Helper.LogType.Warning, true);

            try
            {
                switch (await Dialog_PreviousGameConversionFailed(Content))
                {
                    case ContentDialogResult.Primary:
                        RollbackFileContent(GamePath, GamePathIngredients);
                        MainFrameChanger.ChangeMainFrame(typeof(HomePage));
                        break;
                }
            }
            catch
            {
                RollbackFileContent(GamePath, GamePathIngredients);
                MainFrameChanger.ChangeMainFrame(typeof(HomePage));
            }
        }

        private string GetFailedGameConversionFolder(string basepath)
        {
            try
            {
                string ParentPath = Path.GetDirectoryName(basepath);
                string IngredientPath = Directory.EnumerateDirectories(ParentPath, $"{CurrentConfigV2.GameDirectoryName}*_ConvertedTo-*_Ingredients", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault();
                if (IngredientPath is not null) return IngredientPath;
            }
#if DEBUG
            catch (DirectoryNotFoundException)
            {
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex, ErrorType.Unhandled);
#else
            catch
            {
#endif
            }
            return null;
        }

        private void RollbackFileContent(string OrigPath, string IngrPath)
        {
            int DirLength = IngrPath.Length + 1;
            string destFilePath;
            string destFolderPath;
            bool ErrorOccured = false;

            foreach (string filePath in Directory.EnumerateFiles(IngrPath, "*", SearchOption.AllDirectories))
            {
                ReadOnlySpan<char> relativePath = filePath.AsSpan().Slice(DirLength);
                destFilePath = Path.Combine(OrigPath, relativePath.ToString());
                destFolderPath = Path.GetDirectoryName(destFilePath);

                if (!Directory.Exists(destFolderPath))
                    Directory.CreateDirectory(destFolderPath);

                try
                {
                    LogWriteLine($"Moving \"{relativePath.ToString()}\" to \"{destFolderPath}\"", Hi3Helper.LogType.Default, true);
                    File.Move(filePath, destFilePath, true);
                }
                catch (Exception ex)
                {
                    LogWriteLine($"Error while moving \"{relativePath.ToString()}\" to \"{destFolderPath}\"\r\nException: {ex}", Hi3Helper.LogType.Error, true);
                    ErrorOccured = true;
                }
            }

            if (!ErrorOccured)
                Directory.Delete(IngrPath, true);
        }

        private void GetCurrentGameState()
        {
            Visibility RepairGameButtonVisible = (CurrentConfigV2.IsRepairEnabled ?? false) || (CurrentConfigV2.IsGenshin ?? false) ? Visibility.Visible : Visibility.Collapsed;

            if ((!(CurrentConfigV2.IsConvertible ?? false)) || (CurrentConfigV2.IsGenshin ?? false))
                ConvertVersionButton.Visibility = Visibility.Collapsed;

            if (CurrentConfigV2.IsGenshin ?? false)
            {
                OpenScreenshotFolderButton.Visibility = Visibility.Visible;
                OpenCacheFolderButton.Visibility = Visibility.Collapsed;
            }

            switch (GameInstallationState = GetGameInstallationStatus())
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

            if (!IsPreDownloadCompleted())
            {
                PreloadDialogBox.Message = string.Format(Lang._HomePage.PreloadNotifSubtitle, regionResourceProp.data.pre_download_game.latest.version);
            }
            else
            {
                PreloadDialogBox.Title = Lang._HomePage.PreloadNotifCompleteTitle;
                PreloadDialogBox.Message = string.Format(Lang._HomePage.PreloadNotifCompleteSubtitle, regionResourceProp.data.pre_download_game.latest.version);
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

        private GameInstallStateEnum GetGameInstallationStatus()
        {
            string GameVersion = gameIni.Config["General"]["game_version"].ToString();

            // If the default value is null or empty, return NotInstalled.
            if (string.IsNullOrEmpty(GameDirPath)) return GameInstallStateEnum.NotInstalled;

            // Normalize game path starts from here to avoid crash if GameDirPath is empty.
            GameDirPath = NormalizePath(GameDirPath);
            string GameExecutionPath = Path.Combine(GameDirPath, CurrentConfigV2.GameExecutableName);
            FileInfo GameExecutionInfo = new FileInfo(GameExecutionPath);

            if (GameExecutionInfo.Exists)
            {
                // If game execution has less than 64 KB in size, then declare as broken.
                if (GameExecutionInfo.Length < 0xFFFF) return GameInstallStateEnum.GameBroken;
                // Return if the game needs an update.
                if (GameVersion != regionResourceProp.data.game.latest.version) return GameInstallStateEnum.NeedsUpdate;
                // Return if the game passed checks above and pre_download_game is not null.
                if (regionResourceProp.data.pre_download_game != null) return GameInstallStateEnum.InstalledHavePreload;
                // Return if all checks passed
                return GameInstallStateEnum.Installed;
            }

            // Return if the game doesn't exist.
            return GameInstallStateEnum.NotInstalled;
        }

        private void PreloadDialogBox_Closed(InfoBar sender, InfoBarClosedEventArgs args)
        {
            sender.Translation -= Shadow48;
            HideImageEventImg(false);
        }

        private bool IsPreDownloadCompleted()
        {
            bool IsPrimaryDataExist = File.Exists(
                                        Path.Combine(GameDirPath,
                                        Path.GetFileName(GetUpdateDiffs(true).path)));

            VoicePacks = TryAddVoicePack(GetUpdateDiffs(true));

            bool IsSecondaryDataExist = IsGameHasVoicePack ? VoicePacks.All(x => File.Exists(Path.Combine(GameDirPath, Path.GetFileName(x.Value.path)))) : true;

            return IsPrimaryDataExist && IsSecondaryDataExist;
        }

        private async void CheckRunningGameInstance()
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
                while (true)
                {
                    while (App.IsGameRunning)
                    {
                        if (StartGameBtn.IsEnabled)
                            LauncherBtn.Translation -= Shadow16;

                        StartGameBtn.IsEnabled = false;
                        StartGameBtn.Content = BtnRunningGame;
                        GameStartupSetting.IsEnabled = false;

                        await Task.Delay(100);
                    }

                    if (!StartGameBtn.IsEnabled)
                        LauncherBtn.Translation += Shadow16;

                    StartGameBtn.IsEnabled = true;
                    StartGameBtn.Content = BtnStartGame;
                    GameStartupSetting.IsEnabled = true;

                    await Task.Delay(100);
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
                if (CurrentConfigV2.UseRightSideProgress ?? false)
                    HideImageCarousel(true);

                progressRing.Value = 0;
                progressRing.IsIndeterminate = true;
                ProgressStatusGrid.Visibility = Visibility.Visible;
                InstallGameBtn.Visibility = Visibility.Collapsed;
                CancelDownloadBtn.Visibility = Visibility.Visible;
                ProgressTimeLeft.Visibility = Visibility.Visible;

                if ((GamePathOnSteam = await Task.Run(CurrentConfigV2.GetSteamInstallationPath)) != null)
                {
                    switch (await Dialog_ExistingInstallationSteam(Content))
                    {
                        case ContentDialogResult.Primary:
#if DISABLEMOVEMIGRATE
                            ApplyGameConfig(GamePathOnSteam);
#else
                            MigrationWatcher.IsMigrationRunning = true;
                            MainFrameChanger.ChangeWindowFrame(typeof(InstallationMigrateSteam));
#endif
                            return;
                        case ContentDialogResult.Secondary:
                            await StartInstallationProcedure(await InstallGameDialogScratch());
                            break;
                        case ContentDialogResult.None:
                            MainFrameChanger.ChangeMainFrame(typeof(HomePage));
                            return;
                    }
                }
                else if (await Task.Run(CurrentConfigV2.CheckExistingGameBetterLauncher))
                {
                    switch (await Dialog_ExistingInstallationBetterLauncher(Content))
                    {
                        case ContentDialogResult.Primary:
#if DISABLEMOVEMIGRATE
                            gameIni.Profile["launcher"]["game_install_path"] = CurrentConfigV2.BetterHi3LauncherConfig.game_info.install_path.Replace('\\', '/');
                            SaveGameProfile();
                            MainFrameChanger.ChangeWindowFrame(typeof(MainPage));
#else
                            MigrationWatcher.IsMigrationRunning = true;
                            CurrentRegion.MigrateFromBetterHi3Launcher = true;
                            MainFrameChanger.ChangeWindowFrame(typeof(InstallationMigrate));
#endif
                            return;
                        case ContentDialogResult.Secondary:
                            await StartInstallationProcedure(await InstallGameDialogScratch());
                            break;
                        case ContentDialogResult.None:
                            MainFrameChanger.ChangeMainFrame(typeof(HomePage));
                            return;
                    }
                }
                else if (await Task.Run(CurrentConfigV2.CheckExistingGame))
                {
                    switch (await Dialog_ExistingInstallation(Content))
                    {
                        case ContentDialogResult.Primary:
#if DISABLEMOVEMIGRATE
                            gameIni.Profile["launcher"]["game_install_path"] = CurrentConfigV2.ActualGameDataLocation.Replace('\\', '/');
                            SaveGameProfile();
                            MainFrameChanger.ChangeWindowFrame(typeof(MainPage));
#else
                            MigrationWatcher.IsMigrationRunning = true;
                            MainFrameChanger.ChangeWindowFrame(typeof(InstallationMigrate));
#endif
                            return;
                        case ContentDialogResult.Secondary:
                            await StartInstallationProcedure(await InstallGameDialogScratch());
                            break;
                        case ContentDialogResult.None:
                            MainFrameChanger.ChangeMainFrame(typeof(HomePage));
                            return;
                    }
                }
                else
                {
                    LogWriteLine($"Existing Installation Not Found {CurrentConfigV2.ZoneFullname}");
                    await StartInstallationProcedure(await InstallGameDialogScratch());
                }
                MainFrameChanger.ChangeMainFrame(typeof(HomePage));
            }
            catch (IOException ex)
            {
                LogWriteLine($"Installation cancelled for game {CurrentConfigV2.ZoneFullname} because of IO Error!\r\n{ex}", Hi3Helper.LogType.Warning, true);
                MainFrameChanger.ChangeMainFrame(typeof(HomePage));
            }
            catch (TaskCanceledException)
            {
                LogWriteLine($"Installation cancelled for game {CurrentConfigV2.ZoneFullname}");
            }
            catch (OperationCanceledException)
            {
                LogWriteLine($"Installation cancelled for game {CurrentConfigV2.ZoneFullname}");
            }
            catch (NullReferenceException ex)
            {
                LogWriteLine($"Error while installing game {CurrentConfigV2.ZoneName}\r\n{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(new NullReferenceException("Oops, the launcher cannot finalize the installation but don't worry, your game has been totally updated.\r\t" +
                    $"Please report this issue to our GitHub here: https://github.com/neon-nyan/CollapseLauncher/issues/new or come back to the launcher and make sure to use Repair Game in Game Settings button later.\r\nThrow: {ex}", ex));
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error while installing game {CurrentConfigV2.ZoneName}.\r\n{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex, ErrorType.Unhandled);
            }
        }

        private async Task StartInstallationProcedure(string destinationFolder)
        {
            GameDirPath = destinationFolder;
            if (CheckExistingGame(destinationFolder))
            {
                CancelInstallationDownload();
                return;
            }

            if (!Directory.Exists(GameDirPath))
                Directory.CreateDirectory(GameDirPath);

            if (string.IsNullOrEmpty(GameDirPath))
                throw new OperationCanceledException();

            await TrySetVoicePack(GetUpdateDiffs());

            if (IsGameHasVoicePack)
            {
                GameZipVoiceUrl = VoicePackFile.path;
                GameZipVoicePath = Path.Combine(GameDirPath, Path.GetFileName(GameZipVoiceUrl));
                GameZipVoiceRemoteHash = VoicePackFile.md5.ToLower();
                GameZipVoiceRequiredSize = VoicePackFile.size;
            }

            while (!await DownloadGameClient(GameDirPath))
            {
                // Always loop if something wrong happen
            }

            CancelInstallationDownload();
        }

        private bool CheckExistingGame(string destinationFolder)
        {
            bool isExist;
            if (destinationFolder == null) return false;

            string targetPath = Path.Combine(destinationFolder, CurrentConfigV2.GameExecutableName),
                   iniPath = Path.Combine(destinationFolder, "config.ini");

            // Phase 1 Check
            if (File.Exists(targetPath) && File.Exists(iniPath))
            {
                gameIni.Config = new IniFile();
                gameIni.ConfigPath = iniPath;
                gameIni.Config.Load(gameIni.ConfigPath);
                isExist = true;

                return CheckExistingGameVerAndSet(targetPath, iniPath, isExist);
            }

            // Phase 2 Check
            targetPath = Path.Combine(destinationFolder, CurrentConfigV2.GameDirectoryName, CurrentConfigV2.GameExecutableName);
            iniPath = Path.Combine(destinationFolder, CurrentConfigV2.GameDirectoryName, "config.ini");

            if (File.Exists(targetPath) && File.Exists(iniPath))
            {
                gameIni.Config = new IniFile();
                gameIni.ConfigPath = iniPath;
                gameIni.Config.Load(gameIni.ConfigPath);
                isExist = true;

                return CheckExistingGameVerAndSet(targetPath, iniPath, isExist);
            }

            return false;
        }

        private bool CheckExistingGameVerAndSet(string targetPath, string iniPath, bool isExist)
        {
            if (!isExist)
                return false;

            string gamePath = Path.GetDirectoryName(targetPath);
            gameIni.Profile["launcher"]["game_install_path"] = gamePath.Replace('\\', '/');
            SaveGameProfile();
            if (CurrentConfigV2.IsGenshin ?? false)
                CurrentConfigV2.SetVoiceLanguageID(VoicePackFile.languageID ?? 2);

            FileInfo ExecFile = new FileInfo(Path.Combine(gamePath, CurrentConfigV2.GameExecutableName));
            if (!ExecFile.Exists)
            {
                return false;
            }

            if (ExecFile.Length < 8 << 10)
            {
                return false;
            }

            return true;
        }

        private void ApplyGameConfig(string destinationFolder)
        {
            gameIni.Profile["launcher"]["game_install_path"] = destinationFolder.Replace('\\', '/');
            SaveGameProfile();
            PrepareGameConfig();
            if (IsGameHasVoicePack && (CurrentConfigV2.IsGenshin ?? false))
                CurrentConfigV2.SetVoiceLanguageID(VoicePackFile.languageID ?? 2);
        }

        private async Task<bool> DownloadGameClient(string destinationFolder)
        {
            bool returnVal = true;
            GameZipUrl = regionResourceProp.data.game.latest.path;
            GameZipRemoteHash = regionResourceProp.data.game.latest.md5.ToLower();
            GameZipPath = Path.Combine(destinationFolder, Path.GetFileName(GameZipUrl));
            GameZipRequiredSize = regionResourceProp.data.game.latest.size;

            progressRing.Value = 0;
            progressRing.IsIndeterminate = false;
            ProgressStatusGrid.Visibility = Visibility.Visible;
            InstallGameBtn.Visibility = Visibility.Collapsed;
            CancelDownloadBtn.Visibility = Visibility.Visible;
            ProgressTimeLeft.Visibility = Visibility.Visible;

            InstallerDownloadTokenSource = new CancellationTokenSource();
            CancellationToken token = InstallerDownloadTokenSource.Token;

            InstallTool = new InstallManagement(Content,
                                DownloadType.FirstInstall,
                                CurrentConfigV2,
                                destinationFolder,
                                appIni.Profile["app"]["DownloadThread"].ToInt(),
                                GetAppExtractConfigValue(),
                                token,
                                CurrentConfigV2.IsGenshin ?? false ?
                                    regionResourceProp.data.game.latest.decompressed_path :
                                    null,
                                regionResourceProp.data.game.latest.version,
                                CurrentConfigV2.ProtoDispatchKey,
                                CurrentConfigV2.GameDispatchURL,
                                CurrentConfigV2.GetRegServerNameID(),
                                Path.GetFileNameWithoutExtension(CurrentConfigV2.GameExecutableName));

            InstallTool.AddDownloadProperty(GameZipUrl, GameZipPath, GameDirPath, GameZipRemoteHash, GameZipRequiredSize);
            if (IsGameHasVoicePack)
                InstallTool.AddDownloadProperty(GameZipVoiceUrl, GameZipVoicePath, GameDirPath, GameZipVoiceRemoteHash, GameZipVoiceRequiredSize);

            ProgressStatusGrid.Visibility = Visibility.Visible;
            UpdateGameBtn.Visibility = Visibility.Collapsed;
            CancelDownloadBtn.Visibility = Visibility.Visible;

            InstallTool.InstallStatusChanged += InstallToolStatus;
            InstallTool.InstallProgressChanged += InstallToolProgress;
            bool RetryRoutine = true;

            await InstallTool.CheckDriveFreeSpace(Content);
            await InstallTool.CheckExistingDownloadAsync(Content);

            while (RetryRoutine)
            {
                await InstallTool.StartDownloadAsync();
                RetryRoutine = await InstallTool.StartVerificationAsync(Content);
            }

            await Task.Run(InstallTool.StartInstall);

            ApplyGameConfig(GameDirPath);
            await InstallTool.FinalizeInstallationAsync(Content);

            return returnVal;
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
                LogWrite($"{e.State}: {InstallDownloadSpeedString}", Hi3Helper.LogType.Empty, false, true);
                ProgressPreStatusFooter.Text = string.Format(Lang._Misc.Speed, InstallDownloadSpeedString);
                ProgressPreTimeLeft.Text = string.Format(Lang._Misc.TimeRemainHMSFormat, e.TimeLeft);
                progressPreBar.Value = Math.Round(e.ProgressPercentage, 2);
                progressPreBar.IsIndeterminate = false;
            });
        }

        private void InstallerDownloadPreStatusChanged(object sender, InstallManagementStatus e)
        {
            DispatcherQueue.TryEnqueue(() => ProgressPrePerFileStatusFooter.Text = e.StatusTitle);
        }

        Stopwatch LastTimeSpan = Stopwatch.StartNew();

        private void InstallerDownloadPreProgressChanged(object sender, InstallManagementProgress e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (LastTimeSpan.ElapsedMilliseconds >= RefreshTime)
                {
                    InstallDownloadSpeedString = SummarizeSizeSimple(e.ProgressSpeed);
                    InstallDownloadSizeString = SummarizeSizeSimple(e.ProgressDownloadedSize);
                    InstallDownloadPerSizeString = SummarizeSizeSimple(e.ProgressDownloadedPerFileSize);
                    DownloadSizeString = SummarizeSizeSimple(e.ProgressTotalSizeToDownload);
                    DownloadPerSizeString = SummarizeSizeSimple(e.ProgressTotalSizePerFileToDownload);

                    ProgressPreStatusSubtitle.Text = string.Format(Lang._Misc.PerFromTo, InstallDownloadSizeString, DownloadSizeString);
                    ProgressPrePerFileStatusSubtitle.Text = string.Format(Lang._Misc.PerFromTo, InstallDownloadPerSizeString, DownloadPerSizeString);
                    ProgressPreStatusFooter.Text = string.Format(Lang._Misc.Speed, InstallDownloadSpeedString);
                    ProgressPreTimeLeft.Text = string.Format(Lang._Misc.TimeRemainHMSFormat, e.TimeLeft);
                    progressPreBar.Value = Math.Round(e.ProgressPercentage, 2);
                    progressPrePerFileBar.Value = Math.Round(e.ProgressPercentagePerFile, 2);
                    progressPreBar.IsIndeterminate = false;
                    progressPrePerFileBar.IsIndeterminate = false;
                    ResetLastTimeSpan();
                }
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

            InstallerDownloadTokenSource.Cancel();

            PauseDownloadPreBtn.Visibility = Visibility.Collapsed;
            ResumeDownloadPreBtn.Visibility = Visibility.Visible;
        }

        private void CancelUpdateDownload()
        {
            InstallerDownloadTokenSource.Cancel();

            ProgressStatusGrid.Visibility = Visibility.Collapsed;
            UpdateGameBtn.Visibility = Visibility.Visible;
            CancelDownloadBtn.Visibility = Visibility.Collapsed;

            MainFrameChanger.ChangeMainFrame(typeof(HomePage));
        }

        private void CancelInstallationDownload()
        {
            InstallerDownloadTokenSource.Cancel();

            ProgressStatusGrid.Visibility = Visibility.Collapsed;
            InstallGameBtn.Visibility = Visibility.Visible;
            CancelDownloadBtn.Visibility = Visibility.Collapsed;

            MainFrameChanger.ChangeMainFrame(typeof(HomePage));
        }

        private async Task<string> InstallGameDialogScratch()
        {
            string folder = "";

            bool isChoosen = false;
            while (!isChoosen)
            {
                switch (await Dialog_InstallationLocation(Content))
                {
                    case ContentDialogResult.Primary:
                        folder = Path.Combine(AppGameFolder, CurrentConfigV2.ProfileName, CurrentConfigV2.GameDirectoryName);
                        isChoosen = true;
                        break;
                    case ContentDialogResult.Secondary:
#if DISABLE_COM
                        folder = GetFolderPicker();
#else
                        folder = await GetFolderPicker();
#endif

                        if (folder != null)
                            if (IsUserHasPermission(folder))
                                isChoosen = true;
                            else
                                await Dialog_InsufficientWritePermission(Content, folder);
                        else
                            isChoosen = false;
                        break;
                    case ContentDialogResult.None:
                        MainFrameChanger.ChangeMainFrame(typeof(HomePage));
                        throw new OperationCanceledException();
                }
            }

            return folder;
        }

        CancellationTokenSource WatchOutputLog = new CancellationTokenSource();
        private async void StartGame(object sender, RoutedEventArgs e)
        {
            try
            {
                bool IsContinue = await CheckMediaPackInstalled();

                if (!IsContinue) return;
                Process proc = new Process();
                proc.StartInfo.FileName = Path.Combine(NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString()), CurrentConfigV2.GameExecutableName);
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.Arguments = await GetLaunchArguments();
                LogWriteLine($"Running game with parameters:\r\n{proc.StartInfo.Arguments}");
                proc.StartInfo.WorkingDirectory = Path.GetDirectoryName(NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString()));
                proc.StartInfo.Verb = "runas";
                proc.Start();

                WatchOutputLog = new CancellationTokenSource();

                ReadOutputLog();
                GameLogWatcher();

                await proc.WaitForExitAsync();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                LogWriteLine($"There is a problem while trying to launch Game with Region: {CurrentConfigV2.ZoneName}\r\nTraceback: {ex}", Hi3Helper.LogType.Error, true);
            }
        }

        public async Task<bool> CheckMediaPackInstalled()
        {
            if (CurrentConfigV2.IsGenshin ?? false) return true;

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
            IntPtr _windowPtr = Hi3Helper.InvokeProp.GetProcessWindowHandle(CurrentConfigV2.GameExecutableName);
            await Task.Delay(1000);
            new Hi3Helper.InvokeProp.InvokePresence(_windowPtr).HideWindow();
            await Task.Delay(1000);
            new Hi3Helper.InvokeProp.InvokePresence(_windowPtr).ShowWindow();
        }

        public async void ReadOutputLog()
        {
            await Task.Run(() =>
            {
                string line;
                int barwidth = ((Console.BufferWidth - 22) / 2) - 1;
                LogWriteLine($"{new string('=', barwidth)} GAME STARTED {new string('=', barwidth)}", Hi3Helper.LogType.Warning, true);
                try
                {
                    m_presenter.Minimize();
                    string logPath = $"{GameAppDataFolder}\\{Path.GetFileName(CurrentConfigV2.ConfigRegistryLocation)}\\output_log.txt";

                    if (!Directory.Exists(Path.GetDirectoryName(logPath)))
                        Directory.CreateDirectory(Path.GetDirectoryName(logPath));

                    if (!File.Exists(logPath))
                        File.Create(logPath).Close();

                    using (FileStream fs = new FileStream(logPath,
                            FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (StreamReader reader = new StreamReader(fs))
                        {
                            long lastMaxOffset = reader.BaseStream.Length;

                            while (true)
                            {
                                WatchOutputLog.Token.ThrowIfCancellationRequested();
                                Thread.Sleep(100);

                                reader.BaseStream.Seek(lastMaxOffset, SeekOrigin.Begin);

                                while ((line = reader.ReadLine()) != null)
                                {
                                    if (RequireWindowExclusivePayload)
                                    {
                                        if (line == "MoleMole.MonoGameEntry:Awake()"
                                            && (CurrentConfigV2.IsGenshin ?? false))
                                        {
                                            StartExclusiveWindowPayload();
                                            RequireWindowExclusivePayload = false;
                                        }
                                    }
                                    LogWriteLine(line, Hi3Helper.LogType.Game, true);
                                }

                                lastMaxOffset = reader.BaseStream.Position;
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    LogWriteLine($"{new string('=', barwidth)} GAME STOPPED {new string('=', barwidth)}", Hi3Helper.LogType.Warning, true);
                    m_presenter.Restore();
                }
                catch (Exception ex)
                {
                    LogWriteLine($"{ex}", Hi3Helper.LogType.Error);
                }
            });
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            PageToken.Cancel();
            InstallerDownloadTokenSource.Cancel();
        }

        private void OpenGameFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string GameFolder = NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString());
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
            string GameFolder = Path.Combine(GameAppDataFolder, Path.GetFileName(CurrentConfigV2.ConfigRegistryLocation));
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
            string ScreenshotFolder = Path.Combine(NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString()), "ScreenShot");
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

        private async void RepairGameButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CurrentConfigV2.IsGenshin ?? false)
                {
                    GameDirPath = NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString());

                    if (CurrentConfigV2.UseRightSideProgress ?? false)
                        HideImageCarousel(true);

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        progressRing.Value = 0;
                        progressRing.IsIndeterminate = false;
                        ProgressStatusGrid.Visibility = Visibility.Visible;
                        InstallGameBtn.Visibility = Visibility.Collapsed;
                        StartGameBtn.Visibility = Visibility.Collapsed;
                        CancelDownloadBtn.Visibility = Visibility.Visible;
                        ProgressTimeLeft.Visibility = Visibility.Visible;
                    });

                    InstallerDownloadTokenSource = new CancellationTokenSource();
                    CancellationToken token = InstallerDownloadTokenSource.Token;

                    InstallTool = new InstallManagement(Content,
                                        DownloadType.FirstInstall,
                                        CurrentConfigV2,
                                        GameDirPath,
                                        appIni.Profile["app"]["DownloadThread"].ToInt(),
                                        GetAppExtractConfigValue(),
                                        token,
                                        CurrentConfigV2.IsGenshin ?? false ?
                                            regionResourceProp.data.game.latest.decompressed_path :
                                            null,
                                        regionResourceProp.data.game.latest.version,
                                        CurrentConfigV2.ProtoDispatchKey,
                                        CurrentConfigV2.GameDispatchURL,
                                        CurrentConfigV2.GetRegServerNameID(),
                                        Path.GetFileNameWithoutExtension(CurrentConfigV2.GameExecutableName));

                    InstallTool.InstallProgressChanged += InstallToolProgress;
                    InstallTool.InstallStatusChanged += InstallToolStatus;
                    await InstallTool.PostInstallVerification(Content);
                    InstallTool.InstallProgressChanged -= InstallToolProgress;
                    InstallTool.InstallStatusChanged -= InstallToolStatus;

                    await Dialog_RepairCompleted(Content, InstallTool.GetBrokenFilesCount());
                    MainFrameChanger.ChangeMainFrame(typeof(HomePage));
                }
                else
                {
                    MainFrameChanger.ChangeMainFrame(typeof(RepairPage));
                }
            }
            catch (OperationCanceledException)
            {
                LogWriteLine($"Repair process has been cancelled!", Hi3Helper.LogType.Warning, true);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Repair process has been cancelled due an error!\r\n{ex}", Hi3Helper.LogType.Error, true);
            }
        }

        private async void UninstallGameButton_Click(object sender, RoutedEventArgs e)
        {
            string GameFolder = NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString());

            switch (await Dialog_UninstallGame(Content, GameFolder, CurrentConfigV2.ZoneFullname))
            {
                case ContentDialogResult.Primary:
                    try
                    {
                        Directory.Delete(GameFolder, true);
                    }
                    catch { }
                    MainFrameChanger.ChangeMainFrame(typeof(HomePage));
                    break;
                default:
                    break;
            }
        }

        public RegionResourceVersion GetUpdateDiffs(bool isPredownload = false)
        {
            RegionResourceVersion diff;
            try
            {
                if (isPredownload)
                    diff = regionResourceProp.data.pre_download_game.diffs
                            .Where(x => x.version == gameIni.Config["General"]["game_version"].ToString())
                            .First();
                else
                    diff = regionResourceProp.data.game.diffs
                            .Where(x => x.version == gameIni.Config["General"]["game_version"].ToString())
                            .First();
            }
            catch
            {
                if (isPredownload)
                    diff = regionResourceProp.data.pre_download_game.latest;
                else
                    diff = regionResourceProp.data.game.latest;
            }

            return diff;
        }

        private void InstallToolStatus(object sender, InstallManagementStatus e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressStatusTitle.Text = e.StatusTitle;
                progressPerFile.Visibility = e.IsPerFile ? Visibility.Visible : Visibility.Collapsed;

                progressRing.IsIndeterminate = e.IsIndetermined;
                progressRingPerFile.IsIndeterminate = e.IsIndetermined;
            });
        }

        private void InstallToolProgress(object sender, InstallManagementProgress e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (LastTimeSpan.ElapsedMilliseconds >= RefreshTime)
                {
                    progressRing.Value = e.ProgressPercentage;
                    progressRingPerFile.Value = e.ProgressPercentagePerFile;
                    ProgressStatusSubtitle.Text = string.Format(Lang._Misc.PerFromTo, SummarizeSizeSimple(e.ProgressDownloadedSize), SummarizeSizeSimple(e.ProgressTotalSizeToDownload));
                    ProgressStatusFooter.Text = string.Format(Lang._Misc.Speed, SummarizeSizeSimple(e.ProgressSpeed));
                    ProgressTimeLeft.Text = string.Format(Lang._Misc.TimeRemainHMSFormat, e.TimeLeft);
                    ResetLastTimeSpan();
                }
            });
        }

        private async void UpdateGameDialog(object sender, RoutedEventArgs e)
        {
            InstallerDownloadTokenSource = new CancellationTokenSource();
            CancellationToken token = InstallerDownloadTokenSource.Token;

            if (CurrentConfigV2.UseRightSideProgress ?? false)
                HideImageCarousel(true);

            GameDirPath = NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString());

            RegionResourceVersion diff = GetUpdateDiffs();
            InstallTool = new InstallManagement(Content,
                                DownloadType.Update,
                                CurrentConfigV2,
                                GameDirPath,
                                appIni.Profile["app"]["DownloadThread"].ToInt(),
                                GetAppExtractConfigValue(),
                                token,
                                regionResourceProp.data.game.latest.decompressed_path,
                                regionResourceProp.data.game.latest.version,
                                CurrentConfigV2.ProtoDispatchKey,
                                CurrentConfigV2.GameDispatchURL,
                                CurrentConfigV2.GetRegServerNameID(),
                                Path.GetFileNameWithoutExtension(CurrentConfigV2.GameExecutableName));

            GameZipUrl = diff.path;
            GameZipRemoteHash = diff.md5.ToLower();
            GameZipPath = Path.Combine(GameDirPath, Path.GetFileName(GameZipUrl));
            GameZipRequiredSize = diff.size;

            InstallTool.AddDownloadProperty(GameZipUrl, GameZipPath, GameDirPath, GameZipRemoteHash, GameZipRequiredSize);

            VoicePacks = TryAddVoicePack(GetUpdateDiffs(false));

            if (IsGameHasVoicePack)
            {
                foreach (KeyValuePair<string, RegionResourceVersion> a in VoicePacks)
                {
                    GameZipVoiceUrl = a.Value.path;
                    GameZipVoiceRemoteHash = a.Value.md5;
                    GameZipVoicePath = Path.Combine(GameDirPath, Path.GetFileName(GameZipVoiceUrl));
                    GameZipVoiceRequiredSize = a.Value.size;
                    InstallTool.AddDownloadProperty(GameZipVoiceUrl, GameZipVoicePath, GameDirPath, GameZipVoiceRemoteHash, GameZipVoiceRequiredSize);
                }
            }

            try
            {
                ProgressStatusGrid.Visibility = Visibility.Visible;
                UpdateGameBtn.Visibility = Visibility.Collapsed;
                CancelDownloadBtn.Visibility = Visibility.Visible;

                InstallTool.InstallStatusChanged += InstallToolStatus;
                InstallTool.InstallProgressChanged += InstallToolProgress;
                bool RetryRoutine = true;

                await InstallTool.CheckDriveFreeSpace(Content);

                if (await InstallTool.StartIfDeltaPatchAvailable())
                {
                    await InstallTool.CheckExistingDownloadAsync(Content);

                    while (RetryRoutine)
                    {
                        await InstallTool.StartDownloadAsync();
                        RetryRoutine = await InstallTool.StartVerificationAsync(Content);
                    }

                    await Task.Run(InstallTool.StartInstall);
                }

                ApplyGameConfig(GameDirPath);
                await InstallTool.FinalizeInstallationAsync(Content);

                MainFrameChanger.ChangeMainFrame(typeof(HomePage));
            }
            catch (IOException ex)
            {
                LogWriteLine($"Update cancelled because of IO Error!\r\n{ex}", Hi3Helper.LogType.Warning);
                MainFrameChanger.ChangeMainFrame(typeof(HomePage));
            }
            catch (TaskCanceledException)
            {
                LogWriteLine($"Update cancelled!", Hi3Helper.LogType.Warning);
                MainFrameChanger.ChangeMainFrame(typeof(HomePage));
            }
            catch (OperationCanceledException)
            {
                LogWriteLine($"Update cancelled!", Hi3Helper.LogType.Warning);
            }
            catch (NullReferenceException ex)
            {
                LogWriteLine($"Update error on {CurrentConfigV2.ZoneFullname} game!\r\n{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(new NullReferenceException("Oops, the launcher cannot finalize the installation but don't worry, your game has been totally updated.\r\t" +
                    $"Please report this issue to our GitHub here: https://github.com/neon-nyan/CollapseLauncher/issues/new or come back to the launcher and make sure to use Repair Game in Game Settings button later.\r\nThrow: {ex}", ex));
            }
            catch (Exception ex)
            {
                LogWriteLine($"Update error on {CurrentConfigV2.ZoneFullname} game!\r\n{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private Dictionary<string, RegionResourceVersion> TryAddVoicePack(RegionResourceVersion diffVer)
        {
            int langID;
            if (diffVer.voice_packs != null
                && diffVer.voice_packs.Count > 0)
            {
                Dictionary<string, RegionResourceVersion> VoicePacks = new Dictionary<string, RegionResourceVersion>();
                IsGameHasVoicePack = true;
                VoicePackFile = diffVer.voice_packs[langID = CurrentConfigV2.GetVoiceLanguageID()];
                VoicePackFile.languageID = langID;
                VoicePacks.Add(VoicePackFile.language, VoicePackFile);
                TryAddOtherInstalledVoicePacks(ref VoicePacks, diffVer.voice_packs);
                return VoicePacks;
            }
            LogWriteLine($"This {CurrentConfigV2.ZoneFullname} game doesn't have Voice Pack");
            IsGameHasVoicePack = false;
            return null;
        }

        private void TryAddOtherInstalledVoicePacks(ref Dictionary<string, RegionResourceVersion> VoicePacksOut, List<RegionResourceVersion> Packs)
        {
            if (File.Exists(Path.Combine(GameDirPath, "Audio_Chinese_pkg_version")))
                TryAddOtherVoicePacksDictionary(Packs[0].language, Packs[0], 0, ref VoicePacksOut);
            if (File.Exists(Path.Combine(GameDirPath, "Audio_English(US)_pkg_version")))
                TryAddOtherVoicePacksDictionary(Packs[1].language, Packs[1], 1, ref VoicePacksOut);
            if (File.Exists(Path.Combine(GameDirPath, "Audio_Japanese_pkg_version")))
                TryAddOtherVoicePacksDictionary(Packs[2].language, Packs[2], 2, ref VoicePacksOut);
            if (File.Exists(Path.Combine(GameDirPath, "Audio_Korean_pkg_version")))
                TryAddOtherVoicePacksDictionary(Packs[3].language, Packs[3], 3, ref VoicePacksOut);
        }

        private void TryAddOtherVoicePacksDictionary(string key, RegionResourceVersion value, int langID, ref Dictionary<string, RegionResourceVersion> VoicePacksOut)
        {
            if (!VoicePacksOut.ContainsKey(key))
            {
                value.languageID = langID;
                VoicePacksOut.Add(key, value);
            }
        }

        private async Task TrySetVoicePack(RegionResourceVersion diffVer)
        {
            int langID;
            if (diffVer.voice_packs != null
                && diffVer.voice_packs.Count > 0)
            {
                IsGameHasVoicePack = true;
                VoicePackFile = diffVer.voice_packs[langID = await Dialog_ChooseAudioLanguage(Content, EnumerateAudioLanguageString(diffVer))];
                VoicePackFile.languageID = langID;
                return;
            }
            LogWriteLine($"This {CurrentConfigV2.ZoneFullname} region doesn't have Voice Pack");
            IsGameHasVoicePack = false;
        }

        private void ConvertVersionButton_Click(object sender, RoutedEventArgs e) => MainFrameChanger.ChangeWindowFrame(typeof(InstallationConvert));

        private List<string> EnumerateAudioLanguageString(RegionResourceVersion diffVer)
        {
            List<string> value = new List<string>();
            foreach (RegionResourceVersion Entry in diffVer.voice_packs)
            {
                switch (Entry.language)
                {
                    case "en-us":
                        value.Add(Lang._Misc.LangNameENUS);
                        break;
                    case "ja-jp":
                        value.Add(Lang._Misc.LangNameJP);
                        break;
                    case "zh-cn":
                        value.Add(Lang._Misc.LangNameCN);
                        break;
                    case "ko-kr":
                        value.Add(Lang._Misc.LangNameKR);
                        break;
                    default:
                        value.Add(Entry.language);
                        break;
                }
            }
            return value;
        }

        private async void PredownloadDialog(object sender, RoutedEventArgs e)
        {
            ((Button)sender).IsEnabled = false;

            PauseDownloadPreBtn.Visibility = Visibility.Visible;
            ResumeDownloadPreBtn.Visibility = Visibility.Collapsed;
            PreloadDialogBox.IsClosable = false;

            InstallerDownloadTokenSource = new CancellationTokenSource();
            CancellationToken token = InstallerDownloadTokenSource.Token;

            GameDirPath = NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString());
            RegionResourceVersion diffVer = GetUpdateDiffs(true);

            InstallTool = new InstallManagement(Content,
                                DownloadType.PreDownload,
                                CurrentConfigV2,
                                GameDirPath,
                                appIni.Profile["app"]["DownloadThread"].ToInt(),
                                GetAppExtractConfigValue(),
                                token,
                                CurrentConfigV2.IsGenshin ?? false ?
                                    regionResourceProp.data.game.latest.decompressed_path :
                                    null,
                                regionResourceProp.data.game.latest.version,
                                CurrentConfigV2.ProtoDispatchKey,
                                CurrentConfigV2.GameDispatchURL,
                                CurrentConfigV2.GetRegServerNameID(),
                                Path.GetFileNameWithoutExtension(CurrentConfigV2.GameExecutableName));

            GameDirPath = NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString());
            GameZipUrl = diffVer.path;
            GameZipPath = Path.Combine(GameDirPath, Path.GetFileName(GameZipUrl));
            GameZipRemoteHash = diffVer.md5.ToLower();
            GameZipRequiredSize = diffVer.size;

            InstallTool.AddDownloadProperty(GameZipUrl, GameZipPath, GameDirPath, GameZipRemoteHash, GameZipRequiredSize);

            VoicePacks = TryAddVoicePack(diffVer);

            if (IsGameHasVoicePack)
            {
                foreach (KeyValuePair<string, RegionResourceVersion> a in VoicePacks)
                {
                    GameZipVoiceUrl = a.Value.path;
                    GameZipVoiceRemoteHash = a.Value.md5;
                    GameZipVoicePath = Path.Combine(GameDirPath, Path.GetFileName(GameZipVoiceUrl));
                    GameZipVoiceRequiredSize = a.Value.size;
                    InstallTool.AddDownloadProperty(GameZipVoiceUrl, GameZipVoicePath, GameDirPath, GameZipVoiceRemoteHash, GameZipVoiceRequiredSize);
                }
            }

            bool RetryRoutine = true;

            await InstallTool.CheckDriveFreeSpace(Content);
            await InstallTool.CheckExistingDownloadAsync(Content);

            try
            {
                if (CurrentConfigV2.UseRightSideProgress ?? false)
                    HideImageCarousel(true);

                InstallTool.InstallStatusChanged += InstallerDownloadPreStatusChanged;
                InstallTool.InstallProgressChanged += InstallerDownloadPreProgressChanged;

                while (RetryRoutine)
                {
                    DownloadPreBtn.Visibility = Visibility.Collapsed;
                    ProgressPreStatusGrid.Visibility = Visibility.Visible;
                    ProgressPreButtonGrid.Visibility = Visibility.Visible;
                    PreloadDialogBox.Title = Lang._HomePage.PreloadDownloadNotifbarTitle;
                    PreloadDialogBox.Message = Lang._HomePage.PreloadDownloadNotifbarSubtitle;
                    await InstallTool.StartDownloadAsync();

                    PauseDownloadPreBtn.IsEnabled = false;
                    PreloadDialogBox.Title = Lang._HomePage.PreloadDownloadNotifbarVerifyTitle;

                    RetryRoutine = await InstallTool.StartVerificationAsync(Content);
                }

                InstallTool.InstallProgressChanged -= InstallerDownloadPreProgressChanged;
                InstallTool.InstallStatusChanged -= InstallerDownloadPreStatusChanged;

                await Dialog_PreDownloadPackageVerified(Content);

                OverlapFrame.Navigate(typeof(HomePage), null, new DrillInNavigationTransitionInfo());
            }
            catch (OperationCanceledException)
            {
                LogWriteLine($"Pre-Download paused!", Hi3Helper.LogType.Warning);
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

        public string CustomArgsValue
        {
            get => GetGameConfigValue("CustomArgs").ToString();
            set => SetAndSaveGameConfigValue("CustomArgs", value);
        }

        private void ClickImageEventSpriteLink(object sender, PointerRoutedEventArgs e)
        {
            object ImageTag = ((Image)sender).Tag;
            if (ImageTag == null) return;
            SpawnWebView2.SpawnWebView2Window((string)ImageTag);
        }
    }
}
