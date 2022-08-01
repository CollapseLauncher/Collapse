using CollapseLauncher.Dialogs;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.Win32;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
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
        public HomeMenuPanel MenuPanels { get { return regionNewsProp; } }
        CancellationTokenSource PageToken = new CancellationTokenSource();
        CancellationTokenSource InstallerDownloadTokenSource = new CancellationTokenSource();
        public HomePage()
        {
            try
            {
                MigrationWatcher.IsMigrationRunning = false;
                HomePageProp.Current = this;

                CheckIfRightSideProgress();
                LoadGameConfig();

                this.InitializeComponent();
                CheckCurrentGameState();

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
                    PostPanel.Translation += Shadow16;

                    Task.Run(() => StartCarouselAutoScroll(PageToken.Token));
                }

                if (!GetAppConfigValue("ShowEventsPanel").ToBool())
                {
                    ImageCarouselAndPostPanel.Visibility = Visibility.Collapsed;
                }

                TryLoadEventPanelImage();

                CheckRunningGameInstance();
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private async void TryLoadEventPanelImage()
        {
            if (regionNewsProp.eventPanel == null) return;

            await Task.Run(() =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ImageEventImgGrid.Visibility = Visibility.Visible;
                    ImageEventImg.Source = new BitmapImage(new Uri(regionNewsProp.eventPanel.icon));
                    ImageEventImg.Tag = regionNewsProp.eventPanel.url;
                });
            });
        }

        public async void ResetLastTimeSpan() => await Task.Run(() => LastTimeSpan = Stopwatch.StartNew());
        private void StartCarouselAutoScroll(CancellationToken token = new CancellationToken(), int delay = 5)
        {
            DispatcherQueue.TryEnqueue(async () =>
            {
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
            });
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

        private async Task HideImageCarousel(bool hide)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (!hide)
                    ImageCarouselAndPostPanel.Visibility = Visibility.Visible;
            });

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
            DispatcherQueue.TryEnqueue(() =>
            {
                ImageCarouselAndPostPanel.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
            });
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
            SpawnWebView2.SpawnWebView2Window(((Image)sender).Tag.ToString());
        }

        private void OpenButtonLinkFromTag(object sender, RoutedEventArgs e)
        {
            SpawnWebView2.SpawnWebView2Window(((Button)sender).Tag.ToString());
        }

        private void CheckIfRightSideProgress()
        {
            if (CurrentRegion.UseRightSideProgress ?? false)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    FrameGrid.ColumnDefinitions[0].Width = new GridLength(248, GridUnitType.Pixel);
                    FrameGrid.ColumnDefinitions[1].Width = new GridLength(1224, GridUnitType.Star);
                    LauncherBtn.SetValue(Grid.ColumnProperty, 0);
                    ProgressStatusGrid.SetValue(Grid.ColumnProperty, 0);
                    GameStartupSetting.SetValue(Grid.ColumnProperty, 1);
                    GameStartupSetting.HorizontalAlignment = HorizontalAlignment.Right;
                });
            }
        }

        private void CheckCurrentGameState()
        {
            if (File.Exists(
                   Path.Combine(
                       NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString()),
                       CurrentRegion.GameExecutableName)))
            {
                if (CurrentRegion.IsGenshin ?? false)
                    ConvertVersionButton.Visibility = Visibility.Collapsed;

                if (!CurrentRegion.IsConvertible ?? true)
                    ConvertVersionButton.IsEnabled = false;

                if (new FileInfo(Path.Combine(
                    NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString()),
                    CurrentRegion.GameExecutableName)).Length < 0xFFFF)
                    GameInstallationState = GameInstallStateEnum.GameBroken;
                else if (regionResourceProp.data.game.latest.version != gameIni.Config["General"]["game_version"].ToString())
                {
                    UpdateGameBtn.Visibility = Visibility.Visible;
                    StartGameBtn.Visibility = Visibility.Collapsed;
                    GameInstallationState = GameInstallStateEnum.NeedsUpdate;
                }
                else
                {
                    if (regionResourceProp.data.pre_download_game != null)
                    {
                        InstallGameBtn.Visibility = Visibility.Collapsed;
                        StartGameBtn.Visibility = Visibility.Visible;
                        NotificationBar.Translation += Shadow48;
                        NotificationBar.Closed += NotificationBar_Closed;
                        NotificationBar.IsOpen = true;

                        if (!IsPreDownloadCompleted())
                        {
                            NotificationBar.Message = string.Format(Lang._HomePage.PreloadNotifSubtitle, regionResourceProp.data.pre_download_game.latest.version);
                        }
                        else
                        {
                            NotificationBar.Title = Lang._HomePage.PreloadNotifCompleteTitle;
                            NotificationBar.Message = string.Format(Lang._HomePage.PreloadNotifCompleteSubtitle, regionResourceProp.data.pre_download_game.latest.version);
                            NotificationBar.IsClosable = true;
                            var content = new TextBlock();
                            content.Text = Lang._HomePage.PreloadNotifIntegrityCheckBtn;

                            DownloadPreBtn.Content = content;
                        }

                        GameInstallationState = GameInstallStateEnum.InstalledHavePreload;
                    }
                    else
                    {
                        InstallGameBtn.Visibility = Visibility.Collapsed;
                        StartGameBtn.Visibility = Visibility.Visible;
                        GameInstallationState = GameInstallStateEnum.Installed;
                    }
                }
                if (CurrentRegion.IsGenshin ?? false)
                    OpenCacheFolderButton.IsEnabled = false;

                if (!(CurrentRegion.IsGenshin ?? false))
                {
                    CustomStartupArgs.Visibility = Visibility.Visible;
                }
                return;
            }
            GameInstallationState = GameInstallStateEnum.NotInstalled;
            UninstallGameButton.IsEnabled = false;
            RepairGameButton.IsEnabled = false;
            OpenGameFolderButton.IsEnabled = false;
            OpenCacheFolderButton.IsEnabled = false;
            ConvertVersionButton.IsEnabled = false;
            CustomArgsTextBox.IsEnabled = false;
        }

        private void NotificationBar_Closed(InfoBar sender, InfoBarClosedEventArgs args) => sender.Translation -= Shadow48;

        private bool IsPreDownloadCompleted()
        {
            bool IsPrimaryDataExist = File.Exists(
                                        Path.Combine(gameIni.Profile["launcher"]["game_install_path"].ToString(),
                                        Path.GetFileName(GetUpdateDiffs(true).path)));
            TryAddVoicePack(GetUpdateDiffs(true));
            bool IsSecondaryDataExist = IsGameHasVoicePack ? File.Exists(
                                        Path.Combine(gameIni.Profile["launcher"]["game_install_path"].ToString(),
                                        Path.GetFileName(VoicePackFile.path))) : true;

            return IsPrimaryDataExist && IsSecondaryDataExist;
        }

        private async void CheckRunningGameInstance()
        {
            await Task.Delay(1);
            DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    TextBlock StartBtnText = new TextBlock() { FontWeight = FontWeights.Medium };
                    while (true)
                    {
                        while (App.IsGameRunning)
                        {
                            if (StartGameBtn.IsEnabled)
                                LauncherBtn.Translation -= Shadow16;

                            StartGameBtn.IsEnabled = false;
                            StartBtnText.Text = Lang._HomePage.StartBtnRunning;
                            StartGameBtn.Content = StartBtnText;
                            GameStartupSetting.IsEnabled = false;

                            await Task.Delay(100);
                        }

                        if (!StartGameBtn.IsEnabled)
                            LauncherBtn.Translation += Shadow16;

                        StartGameBtn.IsEnabled = true;
                        StartBtnText.Text = Lang._HomePage.StartBtn;
                        StartGameBtn.Content = StartBtnText;
                        GameStartupSetting.IsEnabled = true;

                        await Task.Delay(100);
                    }
                }
                catch { return; }
            });
        }

        private void AnimateGameRegSettingIcon_Start(object sender, PointerRoutedEventArgs e) => AnimatedIcon.SetState(this.GameRegionSettingIcon, "PointerOver");
        private void AnimateGameRegSettingIcon_End(object sender, PointerRoutedEventArgs e) => AnimatedIcon.SetState(this.GameRegionSettingIcon, "Normal");
        private async void InstallGameDialog(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CurrentRegion.UseRightSideProgress ?? false)
                    await HideImageCarousel(true);

                DispatcherQueue.TryEnqueue(() =>
                {
                    progressRing.Value = 0;
                    progressRing.IsIndeterminate = true;
                    ProgressStatusGrid.Visibility = Visibility.Visible;
                    InstallGameBtn.Visibility = Visibility.Collapsed;
                    CancelDownloadBtn.Visibility = Visibility.Visible;
                    ProgressTimeLeft.Visibility = Visibility.Visible;
                });

                if ((GamePathOnSteam = await Task.Run(() => CurrentRegion.GetSteamInstallationPath())) != null)
                {
                    switch (await Dialog_ExistingInstallationSteam(Content))
                    {
                        case ContentDialogResult.Primary:
                            MigrationWatcher.IsMigrationRunning = true;
                            MainFrameChanger.ChangeWindowFrame(typeof(InstallationMigrateSteam));
                            return;
                        case ContentDialogResult.Secondary:
                            await StartInstallationProcedure(await InstallGameDialogScratch());
                            break;
                        case ContentDialogResult.None:
                            MainFrameChanger.ChangeMainFrame(typeof(HomePage));
                            return;
                    }
                }
                else if (await Task.Run(() => CurrentRegion.CheckExistingGameBetterLauncher()))
                {
                    switch (await Dialog_ExistingInstallationBetterLauncher(Content))
                    {
                        case ContentDialogResult.Primary:
                            MigrationWatcher.IsMigrationRunning = true;
                            CurrentRegion.MigrateFromBetterHi3Launcher = true;
                            MainFrameChanger.ChangeWindowFrame(typeof(InstallationMigrate));
                            return;
                        case ContentDialogResult.Secondary:
                            await StartInstallationProcedure(await InstallGameDialogScratch());
                            break;
                        case ContentDialogResult.None:
                            MainFrameChanger.ChangeMainFrame(typeof(HomePage));
                            return;
                    }
                }
                else if (await Task.Run(() => CurrentRegion.CheckExistingGame()))
                {
                    switch (await Dialog_ExistingInstallation(Content))
                    {
                        case ContentDialogResult.Primary:
                            MigrationWatcher.IsMigrationRunning = true;
                            MainFrameChanger.ChangeWindowFrame(typeof(InstallationMigrate));
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
                    LogWriteLine($"Existing Installation Not Found {CurrentRegion.ZoneName}");
                    await StartInstallationProcedure(await InstallGameDialogScratch());
                }
                MainFrameChanger.ChangeMainFrame(typeof(HomePage));
            }
            catch (IOException ex)
            {
                LogWriteLine($"Installation cancelled for region {CurrentRegion.ZoneName} because of IO Error!\r\n{ex}", Hi3Helper.LogType.Warning, true);
                MainFrameChanger.ChangeMainFrame(typeof(HomePage));
            }
            catch (TaskCanceledException)
            {
                LogWriteLine($"Installation cancelled for region {CurrentRegion.ZoneName}");
            }
            catch (OperationCanceledException)
            {
                LogWriteLine($"Installation cancelled for region {CurrentRegion.ZoneName}");
                MainFrameChanger.ChangeMainFrame(typeof(HomePage));
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error while installing region {CurrentRegion.ZoneName}.\r\n{ex}", Hi3Helper.LogType.Error, true);
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
                GameZipVoiceSize = VoicePackFile.package_size;
                GameZipVoiceRequiredSize = VoicePackFile.size;
            }

            while (!await DownloadGameClient(GameDirPath))
            {
                // Always loop if something wrong happen
            }

            ApplyGameConfig(GameDirPath);

            CancelInstallationDownload();
        }

        private bool CheckExistingGame(string destinationFolder)
        {
            bool isExist;
            string targetPath = Path.Combine(destinationFolder, CurrentRegion.GameExecutableName),
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
            targetPath = Path.Combine(destinationFolder, CurrentRegion.GameDirectoryName, CurrentRegion.GameExecutableName);
            iniPath = Path.Combine(destinationFolder, CurrentRegion.GameDirectoryName, "config.ini");

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

            gameIni.Profile["launcher"]["game_install_path"] = Path.GetDirectoryName(targetPath).Replace('\\', '/');
            SaveGameProfile();
            if (CurrentRegion.IsGenshin ?? false)
                CurrentRegion.SetVoiceLanguageID(VoicePackFile.languageID ?? 2);

            return true;
        }

        private void ApplyGameConfig(string destinationFolder)
        {
            gameIni.Profile["launcher"]["game_install_path"] = destinationFolder.Replace('\\', '/');
            SaveGameProfile();
            PrepareGameConfig();
            if (IsGameHasVoicePack && (CurrentRegion.IsGenshin ?? false))
                CurrentRegion.SetVoiceLanguageID(VoicePackFile.languageID ?? 2);
        }

        private async Task<bool> DownloadGameClient(string destinationFolder)
        {
            bool returnVal = true;
            GameZipUrl = regionResourceProp.data.game.latest.path;
            GameZipRemoteHash = regionResourceProp.data.game.latest.md5.ToLower();
            GameZipPath = Path.Combine(destinationFolder, Path.GetFileName(GameZipUrl));
            GameZipRequiredSize = regionResourceProp.data.game.latest.size;

            DispatcherQueue.TryEnqueue(() =>
            {
                progressRing.Value = 0;
                progressRing.IsIndeterminate = false;
                ProgressStatusGrid.Visibility = Visibility.Visible;
                InstallGameBtn.Visibility = Visibility.Collapsed;
                CancelDownloadBtn.Visibility = Visibility.Visible;
                ProgressTimeLeft.Visibility = Visibility.Visible;
            });

            InstallerDownloadTokenSource = new CancellationTokenSource();
            CancellationToken token = InstallerDownloadTokenSource.Token;

            InstallTool = new InstallManagement(Content,
                                DownloadType.FirstInstall,
                                CurrentRegion,
                                destinationFolder,
                                appIni.Profile["app"]["DownloadThread"].ToInt(),
                                GetAppExtractConfigValue(),
                                token,
                                CurrentRegion.IsGenshin ?? false ?
                                    regionResourceProp.data.game.latest.decompressed_path :
                                    null,
                                regionResourceProp.data.game.latest.version,
                                CurrentRegion.ProtoDispatchKey,
                                CurrentRegion.GameDispatchURL,
                                CurrentRegion.GetRegServerNameID(),
                                Path.GetFileNameWithoutExtension(CurrentRegion.GameExecutableName));

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

            await InstallTool.StartInstallAsync();
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
            InstallDownloadSpeedString = SummarizeSizeSimple(e.Speed);
            InstallDownloadSizeString = SummarizeSizeSimple(e.SizeDownloaded);
            DownloadSizeString = SummarizeSizeSimple(e.SizeToBeDownloaded);
            DispatcherQueue.TryEnqueue(() =>
            {
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
            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressPrePerFileStatusFooter.Text = e.StatusTitle;
            });
        }

        Stopwatch LastTimeSpan = Stopwatch.StartNew();

        private void InstallerDownloadPreProgressChanged(object sender, InstallManagementProgress e)
        {
            if (LastTimeSpan.ElapsedMilliseconds >= RefreshTime)
            {
                InstallDownloadSpeedString = SummarizeSizeSimple(e.ProgressSpeed);
                InstallDownloadSizeString = SummarizeSizeSimple(e.ProgressDownloadedSize);
                InstallDownloadPerSizeString = SummarizeSizeSimple(e.ProgressDownloadedPerFileSize);
                DownloadSizeString = SummarizeSizeSimple(e.ProgressTotalSizeToDownload);
                DownloadPerSizeString = SummarizeSizeSimple(e.ProgressTotalSizePerFileToDownload);
                DispatcherQueue.TryEnqueue(() =>
                {
                    ProgressPreStatusSubtitle.Text = string.Format(Lang._Misc.PerFromTo, InstallDownloadSizeString, DownloadSizeString);
                    ProgressPrePerFileStatusSubtitle.Text = string.Format(Lang._Misc.PerFromTo, InstallDownloadPerSizeString, DownloadPerSizeString);
                    ProgressPreStatusFooter.Text = string.Format(Lang._Misc.Speed, InstallDownloadSpeedString);
                    ProgressPreTimeLeft.Text = string.Format(Lang._Misc.TimeRemainHMSFormat, e.TimeLeft);
                    progressPreBar.Value = Math.Round(e.ProgressPercentage, 2);
                    progressPrePerFileBar.Value = Math.Round(e.ProgressPercentagePerFile, 2);
                    progressPreBar.IsIndeterminate = false;
                    progressPrePerFileBar.IsIndeterminate = false;
                });
                ResetLastTimeSpan();
            }
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

            DispatcherQueue.TryEnqueue(() =>
            {
                PauseDownloadPreBtn.Visibility = Visibility.Collapsed;
                ResumeDownloadPreBtn.Visibility = Visibility.Visible;

                // MainFrameChanger.ChangeMainFrame(typeof(HomePage));
            });
        }

        private void CancelUpdateDownload()
        {
            InstallerDownloadTokenSource.Cancel();

            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressStatusGrid.Visibility = Visibility.Collapsed;
                UpdateGameBtn.Visibility = Visibility.Visible;
                CancelDownloadBtn.Visibility = Visibility.Collapsed;

                MainFrameChanger.ChangeMainFrame(typeof(HomePage));
            });
        }

        private void CancelInstallationDownload()
        {
            InstallerDownloadTokenSource.Cancel();

            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressStatusGrid.Visibility = Visibility.Collapsed;
                InstallGameBtn.Visibility = Visibility.Visible;
                CancelDownloadBtn.Visibility = Visibility.Collapsed;

                MainFrameChanger.ChangeMainFrame(typeof(HomePage));
            });
        }

        private async Task<string> InstallGameDialogScratch()
        {
            // StorageFolder folder;
            string folder = "";
            // string returnFolder = "";

            bool isChoosen = false;
            while (!isChoosen)
            {
                switch (await Dialog_InstallationLocation(Content))
                {
                    case ContentDialogResult.Primary:
                        // returnFolder = Path.Combine(AppGameFolder, CurrentRegion.ProfileName, CurrentRegion.GameDirectoryName);
                        folder = Path.Combine(AppGameFolder, CurrentRegion.ProfileName, CurrentRegion.GameDirectoryName);
                        isChoosen = true;
                        break;
                    case ContentDialogResult.Secondary:
                        folder = (m_window as MainWindow).GetFolderPicker();

                        if (folder != null)
                            // if (IsUserHasPermission(returnFolder = folder.Path))
                            if (IsUserHasPermission(folder))
                                    isChoosen = true;
                            else
                                // await Dialog_InsufficientWritePermission(Content, returnFolder);
                                await Dialog_InsufficientWritePermission(Content, folder);
                        else
                            isChoosen = false;
                        break;
                    case ContentDialogResult.None:
                        throw new OperationCanceledException();
                }
            }

            // return returnFolder;
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
                proc.StartInfo.FileName = Path.Combine(NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString()), CurrentRegion.GameExecutableName);
                proc.StartInfo.UseShellExecute = true;
                if (!(CurrentRegion.IsGenshin ?? false))
                {
                    proc.StartInfo.Arguments = await GetLaunchArguments();
                    LogWriteLine($"Running game with parameters:\r\n{proc.StartInfo.Arguments}");
                }
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
                LogWriteLine($"There is a problem while trying to launch Game with Region: {CurrentRegion.ZoneName}\r\nTraceback: {ex}", Hi3Helper.LogType.Error, true);
            }
        }

        public async Task<bool> CheckMediaPackInstalled()
        {
            if (CurrentRegion.IsGenshin ?? false) return true;

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
                MainFrameChanger.ChangeMainFrame(typeof(Pages.BlankPage));
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
            IntPtr _windowPtr = Hi3Helper.InvokeProp.GetProcessWindowHandle(CurrentRegion.GameExecutableName);
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
                    InnerLauncherConfig.m_presenter.Minimize();
                    string logPath = $"{GameAppDataFolder}\\{Path.GetFileName(CurrentRegion.ConfigRegistryLocation)}\\output_log.txt";

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
                                        // if (line == "asb download url:"
                                            && (CurrentRegion.IsGenshin ?? false))
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
            string GameFolder = Path.Combine(GameAppDataFolder, Path.GetFileName(CurrentRegion.ConfigRegistryLocation));
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

        private async void RepairGameButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CurrentRegion.IsGenshin ?? false)
                {
                    GameDirPath = NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString());

                    if (CurrentRegion.UseRightSideProgress ?? false)
                        await HideImageCarousel(true);

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        progressRing.Value = 0;
                        progressRing.IsIndeterminate = false;
                        ProgressStatusGrid.Visibility = Visibility.Visible;
                        InstallGameBtn.Visibility = Visibility.Collapsed;
                        CancelDownloadBtn.Visibility = Visibility.Visible;
                        ProgressTimeLeft.Visibility = Visibility.Visible;
                    });

                    InstallerDownloadTokenSource = new CancellationTokenSource();
                    CancellationToken token = InstallerDownloadTokenSource.Token;

                    InstallTool = new InstallManagement(Content,
                                        DownloadType.FirstInstall,
                                        CurrentRegion,
                                        GameDirPath,
                                        appIni.Profile["app"]["DownloadThread"].ToInt(),
                                        GetAppExtractConfigValue(),
                                        token,
                                        CurrentRegion.IsGenshin ?? false ?
                                            regionResourceProp.data.game.latest.decompressed_path :
                                            null,
                                        regionResourceProp.data.game.latest.version,
                                        CurrentRegion.ProtoDispatchKey,
                                        CurrentRegion.GameDispatchURL,
                                        CurrentRegion.GetRegServerNameID(),
                                        Path.GetFileNameWithoutExtension(CurrentRegion.GameExecutableName));

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

            switch (await Dialog_UninstallGame(Content, GameFolder, CurrentRegion.ZoneName))
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
            if (LastTimeSpan.ElapsedMilliseconds >= RefreshTime)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    progressRing.Value = e.ProgressPercentage;
                    progressRingPerFile.Value = e.ProgressPercentagePerFile;
                    ProgressStatusSubtitle.Text = string.Format(Lang._Misc.PerFromTo, SummarizeSizeSimple(e.ProgressDownloadedSize), SummarizeSizeSimple(e.ProgressTotalSizeToDownload));
                    ProgressStatusFooter.Text = string.Format(Lang._Misc.Speed, SummarizeSizeSimple(e.ProgressSpeed));
                    ProgressTimeLeft.Text = string.Format(Lang._Misc.TimeRemainHMSFormat, e.TimeLeft);
                });
                ResetLastTimeSpan();
            }
        }

        private async void UpdateGameDialog(object sender, RoutedEventArgs e)
        {
            InstallerDownloadTokenSource = new CancellationTokenSource();
            CancellationToken token = InstallerDownloadTokenSource.Token;

            if (CurrentRegion.UseRightSideProgress ?? false)
                await HideImageCarousel(true);

            GameDirPath = NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString());

            RegionResourceVersion diff = GetUpdateDiffs();
            InstallTool = new InstallManagement(Content,
                                DownloadType.Update,
                                CurrentRegion,
                                GameDirPath,
                                appIni.Profile["app"]["DownloadThread"].ToInt(),
                                GetAppExtractConfigValue(),
                                token,
                                CurrentRegion.IsGenshin ?? false ?
                                    regionResourceProp.data.game.latest.decompressed_path :
                                    CurrentRegion.ZipFileURL,
                                regionResourceProp.data.game.latest.version,
                                CurrentRegion.ProtoDispatchKey,
                                CurrentRegion.GameDispatchURL,
                                CurrentRegion.GetRegServerNameID(),
                                Path.GetFileNameWithoutExtension(CurrentRegion.GameExecutableName));

            GameZipUrl = diff.path;
            GameZipRemoteHash = diff.md5.ToLower();
            GameZipPath = Path.Combine(GameDirPath, Path.GetFileName(GameZipUrl));
            GameZipRequiredSize = diff.size;

            InstallTool.AddDownloadProperty(GameZipUrl, GameZipPath, GameDirPath, GameZipRemoteHash, GameZipRequiredSize);

            TryAddVoicePack(GetUpdateDiffs(false));

            if (IsGameHasVoicePack)
            {
                GameZipVoiceUrl = VoicePackFile.path;
                GameZipVoiceRemoteHash = VoicePackFile.md5;
                GameZipVoicePath = Path.Combine(GameDirPath, Path.GetFileName(GameZipVoiceUrl));
                GameZipVoiceSize = VoicePackFile.package_size;
                GameZipVoiceRequiredSize = VoicePackFile.size;
                InstallTool.AddDownloadProperty(GameZipVoiceUrl, GameZipVoicePath, GameDirPath, GameZipVoiceRemoteHash, GameZipVoiceRequiredSize);
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
                await InstallTool.CheckExistingDownloadAsync(Content);

                while (RetryRoutine)
                {
                    await InstallTool.StartDownloadAsync();
                    RetryRoutine = await InstallTool.StartVerificationAsync(Content);
                }

                await InstallTool.StartInstallAsync();
                await InstallTool.FinalizeInstallationAsync(Content);

                ApplyGameConfig(GameDirPath);
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
            catch (Exception ex)
            {
                LogWriteLine($"Update error! {ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void TryAddVoicePack(RegionResourceVersion diffVer)
        {
            int langID;
            if (diffVer.voice_packs != null
                && diffVer.voice_packs.Count > 0)
            {
                IsGameHasVoicePack = true;
                VoicePackFile = diffVer.voice_packs[langID = CurrentRegion.GetVoiceLanguageID()];
                VoicePackFile.languageID = langID;
                return;
            }
            LogWriteLine($"This {CurrentRegion.ProfileName} region doesn't have Voice Pack");
            IsGameHasVoicePack = false;
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
            LogWriteLine($"This {CurrentRegion.ProfileName} region doesn't have Voice Pack");
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
            PauseDownloadPreBtn.Visibility = Visibility.Visible;
            ResumeDownloadPreBtn.Visibility = Visibility.Collapsed;
            NotificationBar.IsClosable = false;

            InstallerDownloadTokenSource = new CancellationTokenSource();
            CancellationToken token = InstallerDownloadTokenSource.Token;

            GameDirPath = NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString());
            RegionResourceVersion diffVer = GetUpdateDiffs(true);

            InstallTool = new InstallManagement(Content,
                                DownloadType.PreDownload,
                                CurrentRegion,
                                GameDirPath,
                                appIni.Profile["app"]["DownloadThread"].ToInt(),
                                GetAppExtractConfigValue(),
                                token,
                                CurrentRegion.IsGenshin ?? false ?
                                    regionResourceProp.data.game.latest.decompressed_path :
                                    null,
                                regionResourceProp.data.game.latest.version,
                                CurrentRegion.ProtoDispatchKey,
                                CurrentRegion.GameDispatchURL,
                                CurrentRegion.GetRegServerNameID(),
                                Path.GetFileNameWithoutExtension(CurrentRegion.GameExecutableName));

            GameDirPath = NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString());
            GameZipUrl = diffVer.path;
            GameZipPath = Path.Combine(GameDirPath, Path.GetFileName(GameZipUrl));
            GameZipRemoteHash = diffVer.md5.ToLower();
            GameZipRequiredSize = diffVer.size;

            InstallTool.AddDownloadProperty(GameZipUrl, GameZipPath, GameDirPath, GameZipRemoteHash, GameZipRequiredSize);

            TryAddVoicePack(diffVer);

            if (IsGameHasVoicePack)
            {
                GameZipVoiceUrl = VoicePackFile.path;
                GameZipVoiceRemoteHash = VoicePackFile.md5;
                GameZipVoicePath = Path.Combine(GameDirPath, Path.GetFileName(GameZipVoiceUrl));
                GameZipVoiceSize = VoicePackFile.package_size;
                GameZipVoiceRequiredSize = VoicePackFile.size;
                InstallTool.AddDownloadProperty(GameZipVoiceUrl, GameZipVoicePath, GameDirPath, GameZipVoiceRemoteHash, GameZipVoiceRequiredSize);
            }

            bool RetryRoutine = true;

            await InstallTool.CheckDriveFreeSpace(Content);
            await InstallTool.CheckExistingDownloadAsync(Content);

            try
            {
                if (CurrentRegion.UseRightSideProgress ?? false)
                    await HideImageCarousel(true);

                InstallTool.InstallStatusChanged += InstallerDownloadPreStatusChanged;
                InstallTool.InstallProgressChanged += InstallerDownloadPreProgressChanged;

                while (RetryRoutine)
                {
                    DownloadPreBtn.Visibility = Visibility.Collapsed;
                    ProgressPreStatusGrid.Visibility = Visibility.Visible;
                    ProgressPrePerFileStatusGrid.Visibility = Visibility.Visible;
                    NotificationBar.Title = Lang._HomePage.PreloadDownloadNotifbarTitle;
                    NotificationBar.Message = Lang._HomePage.PreloadDownloadNotifbarSubtitle;

                    await InstallTool.StartDownloadAsync();

                    PauseDownloadPreBtn.IsEnabled = false;
                    NotificationBar.Title = Lang._HomePage.PreloadDownloadNotifbarVerifyTitle;

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
            if ((sender as Image).Tag == null) return;
            SpawnWebView2.SpawnWebView2Window((sender as Image).Tag.ToString());
        }
    }
}
