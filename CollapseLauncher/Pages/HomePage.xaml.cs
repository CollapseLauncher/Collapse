using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Numerics;
using Windows.Storage;
using Windows.Storage.Pickers;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;

using Newtonsoft.Json;

using CollapseLauncher.Dialogs;

using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;

using static Hi3Helper.Logger;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static Hi3Helper.Shared.Region.InstallationManagement;
using static Hi3Helper.Shared.Region.GameSettingsManagement;

using static CollapseLauncher.Dialogs.SimpleDialogs;

namespace CollapseLauncher.Pages
{
    public static class HomePageProp
    {
        public static HomePage Current { get; set; }
    }

    public sealed partial class HomePage : Page
    {
        HttpClientHelper HttpTool = new HttpClientHelper();
        public HomeMenuPanel MenuPanels { get { return regionNewsProp; } }
        CancellationTokenSource PageToken = new CancellationTokenSource();
        CancellationTokenSource InstallerDownloadTokenSource = new CancellationTokenSource();
        bool IsCheckPreIntegrity = false;

        public HomePage()
        {
            try
            {
                this.InitializeComponent();
                MigrationWatcher.IsMigrationRunning = false;
                HomePageProp.Current = this;

                CheckIfRightSideProgress();
                LoadGameConfig();
                CheckCurrentGameState();

                SocMedPanel.Translation += Shadow32;
                LauncherBtn.Translation += Shadow32;
                GameStartupSetting.Translation += Shadow32;

                if (MenuPanels.imageCarouselPanel.Count != 0)
                {
                    ImageCarousel.SelectedIndex = 0;
                    ImageCarousel.Visibility = Visibility.Visible;
                    ImageCarouselPipsPager.Visibility = Visibility.Visible;
                    ImageCarousel.Translation += Shadow48;
                    ImageCarouselPipsPager.Translation += Shadow16;

                    Task.Run(() => StartCarouselAutoScroll(PageToken.Token));
                }

                Task.Run(() =>
                {
                    try
                    { CheckRunningGameInstance(); }
                    catch (Exception) { }
                });
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

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
                catch { }
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
            Storyboard storyboard = new Storyboard();
            Storyboard storyboard2 = new Storyboard();
            DoubleAnimation OpacityAnimation = new DoubleAnimation();
            OpacityAnimation.From = hide ? 1 : 0;
            OpacityAnimation.To = hide ? 0 : 1;
            OpacityAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.10));

            DoubleAnimation OpacityAnimation2 = new DoubleAnimation();
            OpacityAnimation2.From = hide ? 1 : 0;
            OpacityAnimation2.To = hide ? 0 : 1;
            OpacityAnimation2.Duration = new Duration(TimeSpan.FromSeconds(0.10));

            Storyboard.SetTarget(OpacityAnimation, ImageCarousel);
            Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
            Storyboard.SetTarget(OpacityAnimation2, ImageCarouselPipsPager);
            Storyboard.SetTargetProperty(OpacityAnimation2, "Opacity");
            storyboard.Children.Add(OpacityAnimation);
            storyboard2.Children.Add(OpacityAnimation2);

            storyboard.Begin();
            storyboard2.Begin();
            await Task.Delay(100);
            DispatcherQueue.TryEnqueue(() =>
            {
                ImageCarousel.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
                ImageCarouselPipsPager.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
            });
        }

        private void OpenSocMedLink(object sender, RoutedEventArgs e) =>
            new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    FileName = ((Button)sender).Tag.ToString()
                }
            }.Start();

        private void OpenImageLinkFromTag(object sender, PointerRoutedEventArgs e) =>
            new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    FileName = ((Image)sender).Tag.ToString()
                }
            }.Start();

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
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        UpdateGameBtn.Visibility = Visibility.Visible;
                        StartGameBtn.Visibility = Visibility.Collapsed;
                    });
                    GameInstallationState = GameInstallStateEnum.NeedsUpdate;
                }
                else
                {
                    if (regionResourceProp.data.pre_download_game != null)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            InstallGameBtn.Visibility = Visibility.Collapsed;
                            StartGameBtn.Visibility = Visibility.Visible;
                            NotificationBar.IsOpen = true;

                            if (!File.Exists(
                                Path.Combine(gameIni.Profile["launcher"]["game_install_path"].ToString(),
                                Path.GetFileName(GetUpdateDiffs(true).path))))
                            {
                                NotificationBar.Message = $"Pre-Download package for v{regionResourceProp.data.pre_download_game.latest.version} is available! You can download the package while playing the game at the same time.";
                            }
                            else
                            {
                                NotificationBar.Title = "Pre-Download Package has been Downloaded!";
                                NotificationBar.Message = $"You have downloaded Pre-Download package for v{regionResourceProp.data.pre_download_game.latest.version}!";
                                NotificationBar.IsClosable = true;
                                // DownloadPreBtn.Visibility = Visibility.Collapsed;
                                var content = new TextBlock();
                                content.Text = "Check Integrity";

                                DownloadPreBtn.Content = content;
                                IsCheckPreIntegrity = true;
                            }
                        });

                        GameInstallationState = GameInstallStateEnum.InstalledHavePreload;
                    }
                    else
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            InstallGameBtn.Visibility = Visibility.Collapsed;
                            StartGameBtn.Visibility = Visibility.Visible;
                        });
                        GameInstallationState = GameInstallStateEnum.Installed;
                    }
                }
                if (CurrentRegion.IsGenshin ?? false)
                    OpenCacheFolderButton.IsEnabled = false;
                return;
            }
            GameInstallationState = GameInstallStateEnum.NotInstalled;
            UninstallGameButton.IsEnabled = false;
            RepairGameButton.IsEnabled = false;
            OpenGameFolderButton.IsEnabled = false;
            OpenCacheFolderButton.IsEnabled = false;
            ConvertVersionButton.IsEnabled = false;
        }

        private async void CheckRunningGameInstance()
        {
            while (true && !App.IsAppKilled)
            {
                while (App.IsGameRunning)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (StartGameBtn.IsEnabled)
                            LauncherBtn.Translation -= Shadow16;

                        StartGameBtn.IsEnabled = false;
                        StartGameBtn.Content = "Game is Running";
                        GameStartupSetting.IsEnabled = false;
                    });

                    await Task.Delay(3000);
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    if (!StartGameBtn.IsEnabled)
                        LauncherBtn.Translation += Shadow16;

                    StartGameBtn.IsEnabled = true;
                    StartGameBtn.Content = "Start Game";
                    GameStartupSetting.IsEnabled = true;
                });

                await Task.Delay(3000);
            }
        }

        private void AnimateGameRegSettingIcon_Start(object sender, PointerRoutedEventArgs e) => AnimatedIcon.SetState(this.GameRegionSettingIcon, "PointerOver");
        private void AnimateGameRegSettingIcon_End(object sender, PointerRoutedEventArgs e) => AnimatedIcon.SetState(this.GameRegionSettingIcon, "Normal");
        private async void InstallGameDialog(object sender, RoutedEventArgs e)
        {
            try
            {
                if ((GamePathOnSteam = await Task.Run(() => CurrentRegion.GetSteamInstallationPath())) != null)
                {
                    switch (await Dialog_ExistingInstallationSteam(Content))
                    {
                        case ContentDialogResult.Primary:
                            MigrationWatcher.IsMigrationRunning = true;
                            MainFrameChanger.ChangeMainFrame(typeof(InstallationMigrateSteam));
                            await CheckMigrationProcess();
                            break;
                        case ContentDialogResult.Secondary:
                            LogWriteLine($"TODO: Do Install for CollapseLauncher");
                            await StartInstallationProcedure(await InstallGameDialogScratch());
                            break;
                        case ContentDialogResult.None:
                            return;
                    }
                }
                else if (await Task.Run(() => CurrentRegion.CheckExistingGameBetterLauncher()))
                {
                    switch (await Dialog_ExistingInstallationBetterLauncher(Content))
                    {
                        case ContentDialogResult.Primary:
                            MigrationWatcher.IsMigrationRunning = true;
                            OverlapFrame.Navigate(typeof(InstallationMigrate), null, new DrillInNavigationTransitionInfo());
                            CurrentRegion.MigrateFromBetterHi3Launcher = true;
                            await CheckMigrationProcess();
                            MainFrameChanger.ChangeMainFrame(typeof(HomePage));
                            break;
                        case ContentDialogResult.Secondary:
                            await StartInstallationProcedure(await InstallGameDialogScratch());
                            break;
                        case ContentDialogResult.None:
                            return;
                    }
                }
                else if (await Task.Run(() => CurrentRegion.CheckExistingGame()))
                {
                    switch (await Dialog_ExistingInstallation(Content))
                    {
                        case ContentDialogResult.Primary:
                            MigrationWatcher.IsMigrationRunning = true;
                            OverlapFrame.Navigate(typeof(InstallationMigrate), null, new DrillInNavigationTransitionInfo());
                            await CheckMigrationProcess();
                            OverlapFrame.Navigate(typeof(HomePage), null, new DrillInNavigationTransitionInfo());
                            break;
                        case ContentDialogResult.Secondary:
                            LogWriteLine($"TODO: Do Install for CollapseLauncher");
                            await StartInstallationProcedure(await InstallGameDialogScratch());
                            break;
                        case ContentDialogResult.None:
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
            if (!Directory.Exists(GameDirPath))
                Directory.CreateDirectory(GameDirPath);

            if (CurrentRegion.UseRightSideProgress ?? false)
                await HideImageCarousel(true);

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

            InstallTool = new InstallManagement(DownloadType.FirstInstall,
                                destinationFolder,
                                appIni.Profile["app"]["DownloadThread"].ToInt(),
                                GetAppExtractConfigValue(),
                                token,
                                CurrentRegion.IsGenshin ?? false ?
                                    regionResourceProp.data.game.latest.decompressed_path :
                                    null,
                                regionResourceProp.data.game.latest.version,
                                CurrentRegion.ProtoDispatchKey,
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

        async Task<bool> TryCheckZipVerification(string ZipPath, string ZipHash, CancellationToken token)
        {
            if (await DoZipVerification(ZipPath, ZipHash.ToLower(), token))
                return true;

            switch (await Dialog_GameInstallationFileCorrupt(Content, ZipHash, GameZipLocalHash))
            {
                case ContentDialogResult.Primary:
                    new FileInfo(ZipPath).Delete();
                    return false;
                case ContentDialogResult.None:
                    CancelInstallationDownload();
                    throw new OperationCanceledException();
            }

            return false;
        }

        private async Task<bool> DoZipVerification(string inputFile, string inputHash, CancellationToken token)
        {
            DispatcherQueue.TryEnqueue(() => ProgressTimeLeft.Visibility = Visibility.Collapsed);

            if (File.Exists(Path.Combine(Path.GetDirectoryName(inputFile), "_noverification"))) return true;

            if (File.Exists(inputFile))
            {
                GameZipLocalHash = await Task.Run(() => GetMD5FromFile(inputFile, token));
                if (GameZipLocalHash == inputHash)
                {
                    LogWriteLine();
                    LogWriteLine($"Downloaded game installation is verified and Ready to be extracted!");
                }
                else
                {
                    LogWriteLine();
                    LogWriteLine($"Downloaded game installation is corrupted!\r\n\tServer Hash: {inputHash.ToLower()}\r\n\tDownloaded Hash: {GameZipLocalHash}", Hi3Helper.LogType.Error);
                    return false;
                }
            }
            else
            {
                LogWriteLine();
                LogWriteLine($"Downloaded game installation is Missing!", Hi3Helper.LogType.Error);
                return false;
            }

            return true;
        }

        string GetMD5FromFile(string fileOutput, CancellationToken token)
        {
            MD5 md5 = MD5.Create();
            FileStream stream;
            byte[] buffer = new byte[8388608];

            int read = 0;
            long totalRead = 0,
                 fileLength = 0;

            string InstallVerifySizeString = string.Empty,
                VerifySizeString = string.Empty, InstallVerifySpeedString = string.Empty;

            var sw = Stopwatch.StartNew();

            if (!(GameInstallationState == GameInstallStateEnum.InstalledHavePreload))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ProgressStatusTitle.Text = "Verifying Package";
                });
            }

            using (stream = new FileStream(fileOutput, FileMode.Open, FileAccess.Read))
            {
                LogWriteLine(string.Format("Verifying downloaded file"));

                fileLength = stream.Length;
                while ((read = stream.Read(buffer, 0, buffer.Length)) >= buffer.Length)
                {
                    token.ThrowIfCancellationRequested();
                    md5.TransformBlock(buffer, 0, buffer.Length, buffer, 0);
                    totalRead += read;

                    GetMD5EventStatus(sw, totalRead, fileLength);
                }
            }

            md5.TransformFinalBlock(buffer, 0, read);
            totalRead += read;

            GetMD5EventStatus(sw, totalRead, fileLength);
            sw.Stop();

            return BytesToHex(md5.Hash).ToLower();
        }


        string InstallVerifySizeString, VerifySizeString, InstallVerifySpeedString;
        private void GetMD5EventStatus(Stopwatch sw, long totalRead, long fileLength)
        {
            long speed = (long)(totalRead / sw.Elapsed.TotalSeconds);
            InstallVerifySizeString = SummarizeSizeSimple(totalRead);
            VerifySizeString = SummarizeSizeSimple(fileLength);
            InstallVerifySpeedString = $"{SummarizeSizeSimple(speed)}/s";

            if ((GameInstallationState == GameInstallStateEnum.InstalledHavePreload))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ProgressPreStatusSubtitle.Text = $"{InstallVerifySizeString} / {VerifySizeString}";
                    LogWrite($"Verifying: {InstallVerifySizeString}", Hi3Helper.LogType.Empty, false, true);
                    ProgressPreStatusFooter.Text = $"Speed: {InstallVerifySpeedString}";
                    progressPreBar.Value = Math.Round(((float)totalRead / (float)fileLength) * 100, 2);
                    ProgressPreTimeLeft.Text = string.Format("{0:%h}h{0:%m}m{0:%s}s left", TimeSpan.FromSeconds((totalRead - fileLength) / speed));
                });
            }
            else
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ProgressStatusSubtitle.Text = $"{InstallVerifySizeString} / {VerifySizeString}";
                    LogWrite($"Verifying: {InstallVerifySizeString}", Hi3Helper.LogType.Empty, false, true);
                    ProgressStatusFooter.Text = $"Speed: {InstallVerifySpeedString}";
                    progressRing.Value = Math.Round(((float)totalRead / (float)fileLength) * 100, 2);
                    ProgressTimeLeft.Text = string.Format("{0:%h}h{0:%m}m{0:%s}s left", TimeSpan.FromSeconds((totalRead - fileLength) / speed));
                });
            }
        }

        private bool IsPackageDownloadCompleted(string filePath, long fileSize)
        {
            FileInfo fileInfo = new FileInfo($"{filePath}");
            FileInfo fileInfoPartial = new FileInfo($"{filePath}.001");

            if (fileInfo.Exists)
                if ((DownloadedSize += fileInfo.Length) != fileSize)
                    return false;
                else
                    return true;

            if (fileInfoPartial.Exists)
                if ((DownloadedSize += GetExistingPartialDownloadLength(filePath)) != fileSize)
                    return false;
                else
                    return true;

            return false;
        }

        private async Task<bool> CheckExistingDownload(string fileOutput, string fileUrl)
        {
            TotalPackageDownloadSize = 0;
            DownloadedSize = 0;

            bool case1 = IsPackageDownloadCompleted(fileOutput, TotalPackageDownloadSize += new HttpClientHelper().GetContentLength(fileUrl) ?? 0);
            bool case2 = IsGameHasVoicePack ? IsPackageDownloadCompleted(GameZipVoicePath, TotalPackageDownloadSize += GameZipVoiceSize) : true;

            if (!(case1 && case2))
            {
                if (DownloadedSize == 0)
                    return true;

                switch (await Dialog_ExistingDownload(Content, DownloadedSize, TotalPackageDownloadSize))
                {
                    case ContentDialogResult.Primary:
                        break;
                    case ContentDialogResult.Secondary:
                        RemoveExistingPartialDownload(fileOutput);
                        if (File.Exists(fileOutput))
                            File.Delete(fileOutput);

                        if (IsGameHasVoicePack)
                        {
                            RemoveExistingPartialDownload(GameZipVoicePath);
                            if (File.Exists(GameZipVoicePath))
                                File.Delete(GameZipVoicePath);
                        }
                        break;
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        void RemoveExistingPartialDownload(string fileOutput)
        {
            FileInfo fileInfo;

            foreach (string file in Directory.GetFiles(Path.GetDirectoryName(fileOutput), $"{Path.GetFileName(fileOutput)}.0*"))
                if ((fileInfo = new FileInfo(file)).Exists)
                    fileInfo.Delete();
        }

        long GetExistingPartialDownloadLength(string fileOutput)
        {
            long output = 0;

            FileInfo fileInfo;

            foreach (string file in Directory.GetFiles(Path.GetDirectoryName(fileOutput), $"{Path.GetFileName(fileOutput)}.0*"))
                if ((fileInfo = new FileInfo(file)).Exists)
                    output += fileInfo.Length;

            return output;
        }

        string InstallDownloadSpeedString;
        string InstallDownloadSizeString;
        string DownloadSizeString;

        private void InstallerDownloadPreStatusChanged(object sender, HttpClientHelper._DownloadProgress e)
        {
            InstallDownloadSpeedString = $"{SummarizeSizeSimple(e.CurrentSpeed)}/s";
            InstallDownloadSizeString = SummarizeSizeSimple(e.DownloadedSize);
            DownloadSizeString = SummarizeSizeSimple(e.TotalSizeToDownload);
            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressPreStatusSubtitle.Text = $"{InstallDownloadSizeString} / {DownloadSizeString}";
                LogWrite($"{e.DownloadState}: {InstallDownloadSpeedString}", Hi3Helper.LogType.Empty, false, true);
                ProgressPreStatusFooter.Text = $"Speed: {InstallDownloadSpeedString}";
                ProgressPreTimeLeft.Text = string.Format("{0:%h}h{0:%m}m{0:%s}s left", e.TimeLeft);
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

            InstallerDownloadTokenSource.Cancel();

            DispatcherQueue.TryEnqueue(() =>
            {
                PauseDownloadPreBtn.Visibility = Visibility.Collapsed;
                ResumeDownloadPreBtn.Visibility = Visibility.Visible;

                MainFrameChanger.ChangeMainFrame(typeof(HomePage));
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
            FolderPicker folderPicker = new FolderPicker();
            StorageFolder folder;
            string returnFolder = "";

            try
            {
                bool isChoosen = false;
                while (!isChoosen)
                {
                    switch (await Dialog_InstallationLocation(Content))
                    {
                        case ContentDialogResult.Primary:
                            returnFolder = Path.Combine(AppGameFolder, CurrentRegion.ProfileName, CurrentRegion.GameDirectoryName);
                            isChoosen = true;
                            break;
                        case ContentDialogResult.Secondary:
                            folder = null;
                            folderPicker.FileTypeFilter.Add("*");
                            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, AppConfig.m_windowHandle);
                            folder = await folderPicker.PickSingleFolderAsync();

                            if (folder != null)
                                if (IsUserHasPermission(returnFolder = folder.Path))
                                    isChoosen = true;
                                else
                                    await Dialog_InsufficientWritePermission(Content, returnFolder);
                            else
                                isChoosen = false;
                            break;
                        case ContentDialogResult.None:
                            throw new TaskCanceledException();
                    }
                }
            }
            catch (TaskCanceledException)
            {
                throw new TaskCanceledException();
            }

            return returnFolder;
        }

        private async Task CheckMigrationProcess()
        {
            while (MigrationWatcher.IsMigrationRunning)
            {
                // Take sleep for 250ms
                await Task.Delay(250);
            }
        }

        CancellationTokenSource WatchOutputLog = new CancellationTokenSource();
        private async void StartGamme(object sender, RoutedEventArgs e)
        {
            try
            {
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
                    AppConfig.m_presenter.Minimize();
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
                    AppConfig.m_presenter.Restore();
                }
                catch (Exception ex)
                {
                    LogWriteLine($"{ex}", Hi3Helper.LogType.Error);
                }
            });
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            // MigrationWatcher.IsMigrationRunning = false;
            PageToken.Cancel();
            // CancelInstallationDownload();
            InstallerDownloadTokenSource.Cancel();
            // WatchOutputLog.Cancel();
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

                    InstallTool = new InstallManagement(DownloadType.FirstInstall,
                                        GameDirPath,
                                        appIni.Profile["app"]["DownloadThread"].ToInt(),
                                        GetAppExtractConfigValue(),
                                        token,
                                        CurrentRegion.IsGenshin ?? false ?
                                            regionResourceProp.data.game.latest.decompressed_path :
                                            null,
                                        regionResourceProp.data.game.latest.version,
                                        CurrentRegion.ProtoDispatchKey,
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
            DispatcherQueue.TryEnqueue(() =>
            {
                progressRing.Value = e.ProgressPercentage;
                progressRingPerFile.Value = e.ProgressPercentagePerFile;
                ProgressStatusSubtitle.Text = $"{SummarizeSizeSimple(e.ProgressDownloadedSize)} / {SummarizeSizeSimple(e.ProgressTotalSizeToDownload)}";
                ProgressStatusFooter.Text = $"Speed: {SummarizeSizeSimple(e.ProgressSpeed)}/s";
                ProgressTimeLeft.Text = string.Format("{0:%h}h{0:%m}m{0:%s}s left", e.TimeLeft);
            });
        }

        private async void UpdateGameDialog(object sender, RoutedEventArgs e)
        {
            InstallerDownloadTokenSource = new CancellationTokenSource();
            CancellationToken token = InstallerDownloadTokenSource.Token;

            if (CurrentRegion.UseRightSideProgress ?? false)
                await HideImageCarousel(true);

            GameDirPath = NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString());

            RegionResourceVersion diff = GetUpdateDiffs();
            InstallTool = new InstallManagement(DownloadType.Update,
                                GameDirPath,
                                appIni.Profile["app"]["DownloadThread"].ToInt(),
                                GetAppExtractConfigValue(),
                                token,
                                CurrentRegion.IsGenshin ?? false ?
                                    regionResourceProp.data.game.latest.decompressed_path :
                                    null,
                                regionResourceProp.data.game.latest.version,
                                CurrentRegion.ProtoDispatchKey,
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
                // OverlapFrame.Navigate(typeof(HomePage), null, new DrillInNavigationTransitionInfo());
            }
            catch (IOException ex)
            {
                MainFrameChanger.ChangeMainFrame(typeof(HomePage));
                LogWriteLine($"Update cancelled because of IO Error!\r\n{ex}", Hi3Helper.LogType.Warning);
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

        void BuildManifestList(string manifestPath, in List<PkgVersionProperties> listInput)
        {
            Span<string> _data = File.ReadAllLines(manifestPath);
            foreach (string data in _data)
                listInput.Add(JsonConvert.DeserializeObject<PkgVersionProperties>(data));
        }

        private void InstallCheckTool_PostInstallCheckChanged(object sender, PostInstallCheckProp e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                progressRing.Value = GetPercentageNumber(e.TotalReadSize, e.TotalCheckSize);
                ProgressStatusSubtitle.Text = string.Format("{0} / {1}", SummarizeSizeSimple(e.TotalReadSize), SummarizeSizeSimple(e.TotalCheckSize));
                ProgressStatusFooter.Text = string.Format("Speed: {0}/s", SummarizeSizeSimple(e.CurrentSpeed));
                ProgressTimeLeft.Text = string.Format("{0:%h}h{0:%m}m{0:%s}s left", TimeSpan.FromSeconds((e.TotalReadSize - e.TotalCheckSize) / e.CurrentSpeed));
            });
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
                        value.Add("English (US)");
                        break;
                    case "ja-jp":
                        value.Add("Japanese");
                        break;
                    case "zh-cn":
                        value.Add("Chinese (Simplified)");
                        break;
                    case "ko-kr":
                        value.Add("Korean");
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
            RegionResourceVersion diffVer = GetUpdateDiffs(true);

            GameDirPath = NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString());
            GameZipUrl = diffVer.path;
            GameZipRemoteHash = diffVer.md5.ToLower();

            TryAddVoicePack(diffVer);

            if (IsGameHasVoicePack)
            {
                GameZipVoiceUrl = VoicePackFile.path;
                GameZipVoicePath = Path.Combine(GameDirPath, Path.GetFileName(GameZipVoiceUrl));
                GameZipVoiceRemoteHash = VoicePackFile.md5.ToLower();
                GameZipVoiceSize = VoicePackFile.package_size;
            }

            string GameZipPath = Path.Combine(GameDirPath, Path.GetFileName(GameZipUrl));

            try
            {
                while (!await DownloadPredownload(GameZipPath, GameDirPath)) { }

                if (IsCheckPreIntegrity)
                    await Dialog_PreDownloadPackageVerified(Content, GameZipLocalHash);

                OverlapFrame.Navigate(typeof(HomePage), null, new DrillInNavigationTransitionInfo());
            }
            catch (OperationCanceledException)
            {
                LogWriteLine($"Pre-Download paused!", Hi3Helper.LogType.Warning);
            }
        }

        private async Task<bool> DownloadPredownload(string sourceFile, string GamePath)
        {
            bool returnVal = true;
            int DowwnloadThread = GetAppConfigValue("DownloadThread").ToInt();

            DispatcherQueue.TryEnqueue(() =>
            {
                DownloadPreBtn.Visibility = Visibility.Collapsed;
                ProgressPreStatusGrid.Visibility = Visibility.Visible;
                NotificationBar.Title = "Downloading Pre-Download";
                NotificationBar.Message = "Necessary Package";
            });

            InstallerDownloadTokenSource = new CancellationTokenSource();
            CancellationToken token = InstallerDownloadTokenSource.Token;

            HttpTool = new HttpClientHelper();

            HttpTool.DownloadProgress += InstallerDownloadPreStatusChanged;

            if (await CheckExistingDownload(sourceFile, GameZipUrl))
            {
                LogWriteLine($"Pre-download Link: {GameZipUrl}");
                if (!File.Exists(sourceFile))
                    await HttpTool.DownloadFileAsync(GameZipUrl, sourceFile, DowwnloadThread, token);

                if (IsGameHasVoicePack)
                {
                    LogWriteLine($"Download Voice Pack URL: {GameZipVoiceUrl}");
                    DispatcherQueue.TryEnqueue(() => ProgressStatusTitle.Text = "Updating Voice Pack");
                    if (!File.Exists(GameZipVoicePath))
                        await HttpTool.DownloadFileAsync(GameZipVoiceUrl, GameZipVoicePath, DowwnloadThread, token);
                }
            }

            HttpTool.DownloadProgress -= InstallerDownloadPreStatusChanged;

            progressPreBar.IsIndeterminate = false;

            DispatcherQueue.TryEnqueue(() =>
            {
                PauseDownloadPreBtn.IsEnabled = false;
            });

            if (await TryCheckZipVerification(sourceFile, GameZipRemoteHash.ToLower(), token)
                && IsGameHasVoicePack ? await TryCheckZipVerification(GameZipVoicePath, GameZipVoiceRemoteHash.ToLower(), token) : true)
                returnVal = true;

            return returnVal;
        }

        private void CleanUpAssets(string GamePath)
        {
            List<string> unusedFiles = new List<string>();
            BlockData blockUtil = new BlockData();

            string xmfPath = Path.Combine(GamePath, @"BH3_Data\StreamingAssets\Asb\pc\Blocks.xmf");

            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressStatusTitle.Text = "Clean up";
                ProgressStatusFooter.Visibility = Visibility.Collapsed;
                ProgressTimeLeft.Visibility = Visibility.Collapsed;
            });

            int i = 0;
            blockUtil.Init(new FileStream(xmfPath, FileMode.Open, FileAccess.Read), XMFFileFormat.XMF);
            blockUtil.CheckForUnusedBlocks(Path.GetDirectoryName(xmfPath));
            unusedFiles = blockUtil.GetListOfBrokenBlocks(Path.GetDirectoryName(xmfPath));
            unusedFiles.AddRange(Directory.EnumerateFiles(Path.GetDirectoryName(xmfPath), "Blocks_*.xmf"));

            foreach (string unusedFile in unusedFiles)
            {
                i++;
                DispatcherQueue.TryEnqueue(() =>
                {
                    ProgressStatusSubtitle.Text = $"{i} / {unusedFiles.Count}";
                    progressRing.Maximum = unusedFiles.Count;
                    progressRing.Value = i;
                });
                File.Delete(unusedFile);
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
    }
}
