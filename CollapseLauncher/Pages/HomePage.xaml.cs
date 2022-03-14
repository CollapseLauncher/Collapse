using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
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

using CollapseLauncher.Dialogs;

using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;

using Newtonsoft.Json;

using static Hi3Helper.Logger;
using static Hi3Helper.InvokeProp;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static Hi3Helper.Shared.Region.InstallationManagement;

using static CollapseLauncher.Dialogs.SimpleDialogs;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CollapseLauncher.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public static class HomePageProp
    {
        public static HomePage Current { get; set; }
    }

    public sealed partial class HomePage : Page
    {
        public HomeMenuPanel MenuPanels { get { return regionNewsProp; } }
        CancellationTokenSource PageToken = new CancellationTokenSource();
        CancellationTokenSource InstallerDownloadTokenSource = new CancellationTokenSource();
        HttpClientTool InstallerHttpClient = new HttpClientTool();
        bool IsCheckPreIntegrity = false;

        Vector3 Shadow16 = new Vector3(0, 0, 16);
        Vector3 Shadow32 = new Vector3(0, 0, 32);
        Vector3 Shadow48 = new Vector3(0, 0, 48);

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
                LauncherBtn.Translation += Shadow16;

                if (MenuPanels.imageCarouselPanel.Count != 0)
                {
                    ImageCarousel.SelectedIndex = 0;
                    ImageCarousel.Visibility = Visibility.Visible;
                    ImageCarousel.Translation += Shadow48;
                    StartCarouselAutoScroll(PageToken.Token);
                }

                Task.Run(() => CheckRunningGameInstance());
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private async void StartCarouselAutoScroll(CancellationToken token = new CancellationToken(), int delay = 5)
        {
            try
            {
                while (true)
                {
                    await Task.Delay(delay * 1000, token);
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        if (ImageCarousel.SelectedIndex != MenuPanels.imageCarouselPanel.Count - 1)
                        {
                            ImageCarousel.SelectedIndex++;
                        }
                        else
                        {
                            for (int i = MenuPanels.imageCarouselPanel.Count; i > 0; i--)
                            {
                                ImageCarousel.SelectedIndex = i - 1;
                                await Task.Delay(50, token);
                            }
                        }
                    });
                }
            }
            catch (OperationCanceledException) { }
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
            await Task.Run(() => { });
            Storyboard storyboard = new Storyboard();
            DoubleAnimation OpacityAnimation = new DoubleAnimation();
            OpacityAnimation.From = hide ? 1 : 0;
            OpacityAnimation.To = hide ? 0 : 1;
            OpacityAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.10));

            Storyboard.SetTarget(OpacityAnimation, ImageCarousel);
            Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
            storyboard.Children.Add(OpacityAnimation);

            storyboard.Begin();
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
                    FrameGrid.ColumnDefinitions[0].Width = new GridLength(227, GridUnitType.Pixel);
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
                                "..\\",
                                Path.GetFileName(regionResourceProp.data.pre_download_game.latest.path))))
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
            OpenGameFolderButton.IsEnabled = false;
            OpenCacheFolderButton.IsEnabled = false;
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
                if ((GamePathOnSteam = CurrentRegion.GetSteamInstallationPath()) != null)
                {
                    switch (await Dialog_ExistingInstallationSteam(Content))
                    {
                        case ContentDialogResult.Primary:
                            MigrationWatcher.IsMigrationRunning = true;
                            OverlapFrame.Navigate(typeof(InstallationMigrateSteam), null, new DrillInNavigationTransitionInfo());
                            await CheckMigrationProcess();
                            MainFrameChanger.ChangeMainFrame(typeof(HomePage));
                            // OverlapFrame.Navigate(typeof(HomePage), null, new DrillInNavigationTransitionInfo());
                            break;
                        case ContentDialogResult.Secondary:
                            LogWriteLine($"TODO: Do Install for CollapseLauncher");
                            await StartInstallationProcedure(await InstallGameDialogScratch());
                            break;
                        case ContentDialogResult.None:
                            return;
                    }
                }
                else if (CurrentRegion.CheckExistingGameBetterLauncher())
                {
                    switch (await Dialog_ExistingInstallationBetterLauncher(Content))
                    {
                        case ContentDialogResult.Primary:
                            MigrationWatcher.IsMigrationRunning = true;
                            OverlapFrame.Navigate(typeof(InstallationMigrate), null, new DrillInNavigationTransitionInfo());
                            CurrentRegion.MigrateFromBetterHi3Launcher = true;
                            await CheckMigrationProcess();
                            MainFrameChanger.ChangeMainFrame(typeof(HomePage));
                            //OverlapFrame.Navigate(typeof(HomePage), null, new DrillInNavigationTransitionInfo());
                            break;
                        case ContentDialogResult.Secondary:
                            await StartInstallationProcedure(await InstallGameDialogScratch());
                            break;
                        case ContentDialogResult.None:
                            return;
                    }
                }
                else if (CurrentRegion.CheckExistingGame())
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
            }
            catch (TaskCanceledException)
            {
                LogWriteLine($"Installation cancelled for region {CurrentRegion.ZoneName}");
            }
        }

        string fileURL, fileOutput, fileHash, fileDownloadHash;

        private async Task StartInstallationProcedure(string destinationFolder)
        {
            try
            {
                /*
                string ExistingPath = ScanForExistingGameLocation(destinationFolder);
                if (ExistingPath != null)
                {

                    switch (await Dialog_InstallationLocateExisting(Content))
                    {
                        case ContentDialogResult.Primary:
                            RequireAdditionalDataDownload = true;
                            break;
                        case ContentDialogResult.Secondary:
                            RequireAdditionalDataDownload = false;
                            break;
                    }

                    DispatcherQueue.TryEnqueue(() => OverlapFrame.Navigate(typeof(HomePage), null, new DrillInNavigationTransitionInfo()));
                    return;
                }

                switch (await Dialog_InstallationDownloadAdditional(Content))
                {
                    case ContentDialogResult.Primary:
                        RequireAdditionalDataDownload = true;
                        break;
                    case ContentDialogResult.Secondary:
                        RequireAdditionalDataDownload = false;
                        break;
                }
                */

                if (CurrentRegion.UseRightSideProgress ?? false)
                    await HideImageCarousel(true);

                if (string.IsNullOrEmpty(destinationFolder))
                    throw new OperationCanceledException();

                while (!await DownloadGameClient(destinationFolder))
                {
                    // Always loop if something wrong happen
                }

                if (!File.Exists(Path.Combine(Path.GetDirectoryName(fileOutput), "_test")))
                    File.Delete(fileOutput);

                ApplyGameConfig(destinationFolder);

                DispatcherQueue.TryEnqueue(() => OverlapFrame.Navigate(typeof(HomePage), null, new DrillInNavigationTransitionInfo()));
            }
            catch (OperationCanceledException)
            {
                LogWriteLine($"Installation cancelled!", Hi3Helper.LogType.Warning);
            }
        }

        private string ScanForExistingGameLocation(string destinationFolder)
        {
            string locatePath = Directory.GetFiles(destinationFolder, "*.exe", SearchOption.AllDirectories)
                .Where(x => x.Contains(CurrentRegion.GameDirectoryName)
                         && x.Contains(CurrentRegion.GameExecutableName)).First();

            if (string.IsNullOrEmpty(locatePath))
                return null;

            locatePath = Path.GetDirectoryName(locatePath);
            return locatePath;
        }

        private void ApplyGameConfig(string destinationFolder)
        {
            gameIni.Profile["launcher"]["game_install_path"] = Path.Combine(destinationFolder, CurrentRegion.GameDirectoryName).Replace('\\', '/');
            SaveGameProfile();
            PrepareGameConfig();
            // GamePkgVersionVerification();
        }

        private void GamePkgVersionVerification()
        {
            string GamePath = NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString());
            string PkgVersionPath = Path.Combine(GamePath, "pkg_version");

            string[] PkgVersionData = File.ReadAllLines(PkgVersionPath);
            int PkgVersionDataCount = PkgVersionData.Length - 1;

            PkgVersionProperties pkgVersionProperties;

            for (int i = 0; i < PkgVersionDataCount; i++)
            {
                pkgVersionProperties = JsonConvert.DeserializeObject<PkgVersionProperties>(PkgVersionData[i]);
            }
        }

        private async Task<bool> DownloadGameClient(string destinationFolder)
        {
            bool returnVal = true;
            fileURL = regionResourceProp.data.game.latest.path;
            fileDownloadHash = regionResourceProp.data.game.latest.md5;
            fileOutput = Path.Combine(destinationFolder, Path.GetFileName(fileURL));

            DispatcherQueue.TryEnqueue(() =>
            {
                progressRing.Value = 0;
                ProgressStatusGrid.Visibility = Visibility.Visible;
                InstallGameBtn.Visibility = Visibility.Collapsed;
                CancelDownloadBtn.Visibility = Visibility.Visible;
                ProgressTimeLeft.Visibility = Visibility.Visible;
                ProgressStatusTitle.Text = "Downloading";
            });

            InstallerDownloadTokenSource = new CancellationTokenSource();
            CancellationToken token = InstallerDownloadTokenSource.Token;

            InstallerHttpClient = new HttpClientTool();

            if (await CheckExistingDownload(fileOutput))
            {
                InstallerHttpClient.PartialProgressChanged += InstallerDownloadStatusChanged;
                InstallerHttpClient.Completed += InstallerDownloadStatusCompleted;

                LogWriteLine($"Download URL: {fileURL}");
                await Task.Run(() => InstallerHttpClient.DownloadFileMultipleSession(fileURL, fileOutput, "", appIni.Profile["app"]["DownloadThread"].ToInt(), token));

                InstallerHttpClient.PartialProgressChanged -= InstallerDownloadStatusChanged;
                InstallerHttpClient.Completed -= InstallerDownloadStatusCompleted;
            }

            InstallerDownloadTokenSource = new CancellationTokenSource();
            token = InstallerDownloadTokenSource.Token;

            if (!await DoZipVerification(fileOutput, token))
            {
                switch (await Dialog_GameInstallationFileCorrupt(Content, fileDownloadHash, fileHash))
                {
                    case ContentDialogResult.Primary:
                        new FileInfo(fileOutput).Delete();
                        returnVal = false;
                        break;
                    case ContentDialogResult.None:
                        CancelInstallationDownload();
                        throw new OperationCanceledException();
                }
            }
            else
            {
                await Task.Run(() => ExtractDownloadedGame(fileOutput, destinationFolder, token), token);
            }

            CancelInstallationDownload();
            return returnVal;
        }

        private async Task<bool> DoZipVerification(string inputFile, CancellationToken token)
        {
            DispatcherQueue.TryEnqueue(() => ProgressTimeLeft.Visibility = Visibility.Collapsed);

            if (File.Exists(Path.Combine(Path.GetDirectoryName(fileOutput), "_noverification"))) return true;

            if (File.Exists(inputFile))
            {
                fileHash = await Task.Run(() => GetMD5FromFile(inputFile, token));
                if (fileHash == fileDownloadHash.ToLower())
                {
                    LogWriteLine();
                    LogWriteLine($"Downloaded game installation is verified and Ready to be extracted!");
                }
                else
                {
                    LogWriteLine();
                    LogWriteLine($"Downloaded game installation is corrupted!\r\n\tServer Hash: {fileDownloadHash}\r\n\tDownloaded Hash: {fileHash}", Hi3Helper.LogType.Error);
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

        string InstallExtractSizeString, ExtractSizeString, InstallExtractSpeedString;
        private void ExtractDownloadedGame(string sourceFile, string destinationFolder, CancellationToken token)
        {
            destinationFolder = Path.Combine(destinationFolder, CurrentRegion.GameDirectoryName);

            if (!Directory.Exists(destinationFolder))
                Directory.CreateDirectory(destinationFolder);

            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressStatusTitle.Text = "Extracting";
            });

            SevenZipTool sevenZip = new SevenZipTool();
            try
            {
                switch (Path.GetExtension(sourceFile).ToLower())
                {
                    case ".7z":
                        sevenZip.Load(sourceFile);

                        sevenZip.ExtractProgressChanged += ExtractProgress;
                        sevenZip.ExtractToDirectory(destinationFolder, Environment.ProcessorCount, token);
                        break;
                    case ".zip":
                        sevenZip.LoadZip(sourceFile);

                        sevenZip.ExtractProgressChanged += ExtractProgress;
                        sevenZip.ExtractToDirectory(destinationFolder, Environment.ProcessorCount, token);
                        break;
                }
            }
            catch (OperationCanceledException ex)
            {
                sevenZip.ExtractProgressChanged -= ExtractProgress;
                sevenZip.Dispose();
                throw new OperationCanceledException("Operation cancelled", ex);
            }

            sevenZip.ExtractProgressChanged -= ExtractProgress;

            sevenZip.Dispose();
        }

        private void ExtractProgress(object sender, ExtractProgress e)
        {
            InstallExtractSizeString = SummarizeSizeSimple(e.totalExtractedSize);
            ExtractSizeString = SummarizeSizeSimple(e.totalUncompressedSize);
            InstallExtractSpeedString = $"{SummarizeSizeSimple(e.CurrentSpeed)}/s";

            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressStatusSubtitle.Text = $"{InstallExtractSizeString} / {ExtractSizeString}";
                ProgressStatusFooter.Text = $"Speed: {InstallExtractSpeedString}";
                progressRing.Value = Math.Round(e.ProgressPercentage, 2);
            });
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
                    ProgressStatusTitle.Text = "Verifying";
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

        private async Task<bool> CheckExistingDownload(string fileOutput)
        {
            FileInfo fileInfo = new FileInfo($"{fileOutput}.001");

            if (fileInfo.Exists)
            {
                long contentLength = new HttpClientTool().GetContentLength(fileURL) ?? 0;
                long partialLength = GetExistingPartialDownloadLength(fileOutput);
                if (partialLength != contentLength)
                {
                    switch (await Dialog_ExistingDownload(Content, partialLength, contentLength))
                    {
                        case ContentDialogResult.Primary:
                            break;
                        case ContentDialogResult.Secondary:
                            RemoveExistingPartialDownload(fileOutput);
                            break;
                    }
                    return true;
                }
                else if (partialLength == contentLength && !File.Exists(fileOutput))
                {
                    return true;
                }
            }
            else if (File.Exists(fileOutput))
            {
                return false;
            }

            return true;
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
        private void InstallerDownloadStatusChanged(object sender, PartialDownloadProgressChanged e)
        {
            InstallDownloadSpeedString = $"{SummarizeSizeSimple(e.CurrentSpeed)}/s";
            InstallDownloadSizeString = SummarizeSizeSimple(e.BytesReceived);
            DownloadSizeString = SummarizeSizeSimple(e.TotalBytesToReceive);
            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressStatusSubtitle.Text = $"{InstallDownloadSizeString} / {DownloadSizeString}";
                LogWrite($"{e.DownloadState}: {InstallDownloadSpeedString}", Hi3Helper.LogType.Empty, false, true);
                ProgressStatusFooter.Text = $"Speed: {InstallDownloadSpeedString}";
                ProgressTimeLeft.Text = string.Format("{0:%h}h{0:%m}m{0:%s}s left", e.TimeLeft);
                progressRing.Value = Math.Round(e.ProgressPercentage, 2);
                ProgressStatusTitle.Text = e.DownloadState.ToString();
            });
        }

        private void InstallerDownloadStatusCompleted(object sender, DownloadProgressCompleted e)
        {

        }

        private void InstallerDownloadPreStatusChanged(object sender, PartialDownloadProgressChanged e)
        {
            InstallDownloadSpeedString = $"{SummarizeSizeSimple(e.CurrentSpeed)}/s";
            InstallDownloadSizeString = SummarizeSizeSimple(e.BytesReceived);
            DownloadSizeString = SummarizeSizeSimple(e.TotalBytesToReceive);
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

        private void InstallerDownloadPreStatusCompleted(object sender, DownloadProgressCompleted e)
        {

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
                    CancelInstallationDownload();
                    break;
            }
        }

        private void CancelPreDownload()
        {
            InstallerHttpClient.PartialProgressChanged -= InstallerDownloadPreStatusChanged;
            InstallerHttpClient.Completed -= InstallerDownloadPreStatusCompleted;

            InstallerDownloadTokenSource.Cancel();

            DispatcherQueue.TryEnqueue(() =>
            {
                PauseDownloadPreBtn.Visibility = Visibility.Collapsed;
                ResumeDownloadPreBtn.Visibility = Visibility.Visible;
            });
        }

        private void CancelUpdateDownload()
        {
            InstallerHttpClient.PartialProgressChanged -= InstallerDownloadStatusChanged;
            InstallerHttpClient.Completed -= InstallerDownloadStatusCompleted;

            InstallerDownloadTokenSource.Cancel();

            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressStatusGrid.Visibility = Visibility.Collapsed;
                UpdateGameBtn.Visibility = Visibility.Visible;
                CancelDownloadBtn.Visibility = Visibility.Collapsed;

                OverlapFrame.Navigate(typeof(HomePage), null, new DrillInNavigationTransitionInfo());
            });
        }

        private void CancelInstallationDownload()
        {
            InstallerHttpClient.PartialProgressChanged -= InstallerDownloadStatusChanged;
            InstallerHttpClient.Completed -= InstallerDownloadStatusCompleted;

            InstallerDownloadTokenSource.Cancel();

            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressStatusGrid.Visibility = Visibility.Collapsed;
                InstallGameBtn.Visibility = Visibility.Visible;
                CancelDownloadBtn.Visibility = Visibility.Collapsed;

                OverlapFrame.Navigate(typeof(HomePage), null, new DrillInNavigationTransitionInfo());
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
                            returnFolder = Path.Combine(AppGameFolder, CurrentRegion.ProfileName);
                            isChoosen = true;
                            break;
                        case ContentDialogResult.Secondary:
                            folder = null;
                            folderPicker.FileTypeFilter.Add("*");
                            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, m_windowHandle);
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
            catch
            { }

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
                if (!CurrentRegion.IsGenshin ?? false)
                {
                    proc.StartInfo.Arguments = await Hi3Helper.Shared.Region.GameSettingsManagement.GetLaunchArguments();
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

        public async void ReadOutputLog()
        {
            await Task.Run(() =>
            {
                string line;
                int barwidth = ((Console.BufferWidth - 22) / 2) - 1;
                LogWriteLine($"{new string('=', barwidth)} GAME STARTED {new string('=', barwidth)}", Hi3Helper.LogType.Warning, true);
                try
                {
                    AppConfig._presenter.Minimize();
                    using (FileStream fs = new FileStream($"{GameAppDataFolder}\\{Path.GetFileName(CurrentRegion.ConfigRegistryLocation)}\\output_log.txt",
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
                    AppConfig._presenter.Restore();
                }
                catch (Exception ex)
                {
                    LogWriteLine($"{ex}", Hi3Helper.LogType.Error);
                }
            });
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            MigrationWatcher.IsMigrationRunning = false;
            PageToken.Cancel();
            CancelInstallationDownload();
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

        public string GetUpdateDiffs()
        {
            if (CurrentRegion.IsGenshin ?? false)
            {
                string oldVer;
                if ((oldVer = gameIni.Config["General"]["game_version"].ToString()) == null)
                {
                    fileDownloadHash = regionResourceProp.data.game.latest.md5;
                    return regionResourceProp.data.game.latest.path;
                }
                else
                {
                    fileDownloadHash = regionResourceProp.data.game.diffs.Where(x => x.version == oldVer).ToList()[0].md5;
                    return regionResourceProp.data.game.diffs.Where(x => x.version == oldVer).ToList()[0].path;
                }
            }

            fileDownloadHash = regionResourceProp.data.game.latest.md5;
            return regionResourceProp.data.game.latest.path;
        }

        private async void UpdateGameDialog(object sender, RoutedEventArgs e)
        {
            if (CurrentRegion.UseRightSideProgress ?? false)
                await HideImageCarousel(true);

            string GamePath = NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString());
            fileURL = GetUpdateDiffs();
            fileOutput = Path.Combine(GamePath, "..\\", Path.GetFileName(fileURL));

            GetUpdateDiffs();

            try
            {
                while (!await UpdateGameClient(fileOutput, GamePath))
                {

                }

                if (!File.Exists(Path.Combine(Path.GetDirectoryName(fileOutput), "_test")))
                    File.Delete(fileOutput);

                ApplyGameConfig(Path.GetDirectoryName(GamePath));
                OverlapFrame.Navigate(typeof(HomePage), null, new DrillInNavigationTransitionInfo());
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

        private async Task<bool> UpdateGameClient(string sourceFile, string GamePath)
        {
            bool returnVal = true;
            fileOutput = Path.Combine(gamePath, Path.GetFileName(fileURL));
            DispatcherQueue.TryEnqueue(() =>
            {
                progressRing.Value = 0;
                ProgressStatusGrid.Visibility = Visibility.Visible;
                UpdateGameBtn.Visibility = Visibility.Collapsed;
                CancelDownloadBtn.Visibility = Visibility.Visible;
                ProgressTimeLeft.Visibility = Visibility.Visible;
                ProgressStatusTitle.Text = "Updating";
            });

            InstallerDownloadTokenSource = new CancellationTokenSource();
            CancellationToken token = InstallerDownloadTokenSource.Token;

            InstallerHttpClient = new HttpClientTool();

            InstallerHttpClient.PartialProgressChanged += InstallerDownloadStatusChanged;
            InstallerHttpClient.Completed += InstallerDownloadStatusCompleted;

            if (await CheckExistingDownload(sourceFile))
            {
                LogWriteLine($"Download URL: {fileURL}");
                await Task.Run(() => InstallerHttpClient.DownloadFileMultipleSession(fileURL, sourceFile, "", appIni.Profile["app"]["DownloadThread"].ToInt(), token));
            }

            InstallerHttpClient.PartialProgressChanged -= InstallerDownloadStatusChanged;
            InstallerHttpClient.Completed -= InstallerDownloadStatusCompleted;

            if (await DoZipVerification(sourceFile, token))
            {
                await Task.Run(() => ExtractDownloadedGame(sourceFile, Path.Combine(GamePath, "..\\"), token));
                if (CurrentRegion.IsGenshin ?? false)
                {
                    await Task.Run(() => ApplyDeltaPatch(GamePath));
                    await Task.Run(() => CleanUpAssetsGenshin(GamePath));
                }
                else
                {
                    await CleanUpAssets(GamePath);
                }
            }
            else
            {
                switch (await Dialog_GameInstallationFileCorrupt(Content, fileDownloadHash, fileHash))
                {
                    case ContentDialogResult.Primary:
                        new FileInfo(fileOutput).Delete();
                        returnVal = false;
                        break;
                    case ContentDialogResult.None:
                        CancelInstallationDownload();
                        throw new OperationCanceledException();
                }
            }

            return returnVal;
        }

        class PatchEntryProp
        {
            public string remoteName { get; set; }
        }

        public void CleanUpAssetsGenshin(string GamePath)
        {
            string listPath = Path.Combine(GamePath, "deletefiles.txt"),
                   filePath;

            string[] list = File.ReadAllLines(listPath);
            foreach (string line in list)
            {
                filePath = Path.Combine(GamePath, NormalizePath(line));
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }

            File.Delete(listPath);
        }

        public void ApplyDeltaPatch(string GamePath)
        {
            string patchListPath = Path.Combine(GamePath, "hdifffiles.txt"),
                   patchFilePath,
                   inputFilePath,
                   outputFilePath;

            DispatcherQueue.TryEnqueue(() =>
            {
                progressRing.Value = 0;
                ProgressStatusGrid.Visibility = Visibility.Visible;
                ProgressTimeLeft.Visibility = Visibility.Collapsed;
                ProgressStatusFooter.Visibility = Visibility.Collapsed;
                ProgressStatusTitle.Text = "Applying Delta Patches";
            });

            HPatchUtil patchUtil = new HPatchUtil();

            PatchEntryProp entry;

            if (File.Exists(patchListPath))
            {
                string[] list = File.ReadAllLines(patchListPath);
                int count = 0;
                foreach (string line in list)
                {
                    entry = JsonConvert.DeserializeObject<PatchEntryProp>(line);
                    inputFilePath = Path.Combine(GamePath, NormalizePath(entry.remoteName));
                    patchFilePath = inputFilePath + ".hdiff";
                    outputFilePath = inputFilePath + "_tmp";

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        progressRing.Value = GetPercentageNumber(count, list.Length);
                        ProgressStatusSubtitle.Text = $"{count} / {list.Length} files";
                    });

                    if (File.Exists(inputFilePath) && File.Exists(outputFilePath) || File.Exists(inputFilePath))
                    {
                        if (File.Exists(patchFilePath))
                        {
                            patchUtil.HPatchFile(inputFilePath, patchFilePath, outputFilePath);
                            File.Delete(patchFilePath);
                            File.Delete(inputFilePath);
                            File.Move(outputFilePath, inputFilePath);
                        }
                    }

                    count++;
                }
            }

            File.Delete(patchListPath);
        }

        private async void PredownloadDialog(object sender, RoutedEventArgs e)
        {
            PauseDownloadPreBtn.Visibility = Visibility.Visible;
            ResumeDownloadPreBtn.Visibility = Visibility.Collapsed;
            NotificationBar.IsClosable = false;

            string GamePath = NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString());
            fileURL = regionResourceProp.data.pre_download_game.latest.path;
            fileDownloadHash = regionResourceProp.data.pre_download_game.latest.md5;
            string GameZipPath = Path.Combine(GamePath, "..\\", Path.GetFileName(fileURL));

            try
            {
                while (!await DownloadPredownload(GameZipPath, GamePath))
                {

                }

                if (IsCheckPreIntegrity)
                {
                    await Dialog_PreDownloadPackageVerified(Content, fileHash);
                }

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

            DispatcherQueue.TryEnqueue(() =>
            {
                DownloadPreBtn.Visibility = Visibility.Collapsed;
                ProgressPreStatusGrid.Visibility = Visibility.Visible;
            });

            InstallerDownloadTokenSource = new CancellationTokenSource();
            CancellationToken token = InstallerDownloadTokenSource.Token;

            InstallerHttpClient = new HttpClientTool();

            InstallerHttpClient.PartialProgressChanged += InstallerDownloadPreStatusChanged;
            InstallerHttpClient.Completed += InstallerDownloadPreStatusCompleted;

            if (await CheckExistingDownload(sourceFile))
            {
                LogWriteLine($"Pre-download Link: {fileURL}");
                await Task.Run(() => InstallerHttpClient.DownloadFileMultipleSession(fileURL, sourceFile, "", appIni.Profile["app"]["DownloadThread"].ToInt(), token));
            }

            InstallerHttpClient.PartialProgressChanged -= InstallerDownloadPreStatusChanged;
            InstallerHttpClient.Completed -= InstallerDownloadPreStatusCompleted;

            progressPreBar.IsIndeterminate = false;

            DispatcherQueue.TryEnqueue(() =>
            {
                PauseDownloadPreBtn.IsEnabled = false;
            });

            if (!await DoZipVerification(sourceFile, token))
            {
                switch (await Dialog_GameInstallationFileCorrupt(Content, fileDownloadHash, fileHash))
                {
                    case ContentDialogResult.Primary:
                        new FileInfo(fileOutput).Delete();
                        returnVal = false;
                        break;
                    case ContentDialogResult.None:
                        CancelInstallationDownload();
                        throw new OperationCanceledException();
                }
            }

            return returnVal;
        }

        private async Task CleanUpAssets(string GamePath)
        {
            List<string> unusedFiles = new List<string>();
            BlockData blockUtil = new BlockData();

            PresetConfigClasses gameProp = new PresetConfigClasses
            {
                ActualGameDataLocation = GamePath
            };

            string xmfPath = Path.Combine(GamePath, @"BH3_Data\StreamingAssets\Asb\pc\Blocks.xmf");

            DispatcherQueue.TryEnqueue(() => {
                ProgressStatusTitle.Text = "Clean up";
                ProgressStatusFooter.Visibility = Visibility.Collapsed;
                ProgressTimeLeft.Visibility = Visibility.Collapsed;
            });

            int i = 0;

            await Task.Run(() =>
            {
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
            });
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
