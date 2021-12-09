using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;

using Hi3Helper.Data;

using static Hi3Helper.Logger;
using static CollapseLauncher.LauncherConfig;
using static CollapseLauncher.Region.InstallationManagement;

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
        CancellationTokenSource InstallerDownloadTokenSource = new CancellationTokenSource();
        HttpClientTool InstallerHttpClient = new HttpClientTool();
        readonly int DownloadThread = 8;
        public HomePage()
        {
            this.InitializeComponent();
            Dialogs.MigrationWatcher.IsMigrationRunning = false;
            HomePageProp.Current = this;

            CheckCollapseLauncherGame();
            Task.Run(() => CheckRunningGameInstance());
        }

        public static bool TryGoBack()
        {
            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame.CanGoBack)
            {
                rootFrame.GoBack();
                return true;
            }
            return false;
        }

        private async void CheckRunningGameInstance()
        {

            while (true)
            {
                while (Process.GetProcessesByName("BH3").Length != 0)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        StartGameBtn.IsEnabled = false;
                        StartGameBtn.Content = "Game is Running";
                        GameStartupSetting.IsEnabled = false;
                    });

                    await Task.Delay(3000);
                }

                DispatcherQueue.TryEnqueue(() =>
                {
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
                if (CurrentRegion.CheckExistingGame())
                {
                    LogWriteLine($"Existing Installation Has Been Found {CurrentRegion.ZoneName}");
                    var cd = new ContentDialog
                    {
                        Title = "Existing Installation is Detected!",
                        Content = $"Game is already been installed on this location:\r\n\r\n{CurrentRegion.ActualGameDataLocation}\r\n\r\n"
                                + "It's recommended to migrate the game to CollapseLauncher.\r\nHowever, you can still use Official Launcher to start the game."
                                + "\r\n\r\nDo you want to continue?",
                        CloseButtonText = "Cancel",
                        PrimaryButtonText = "Yes, Migrate it",
                        SecondaryButtonText = "No, Keep Install it",
                        DefaultButton = ContentDialogButton.Primary,
                        Background = (Brush)Application.Current.Resources["DialogAcrylicBrush"]
                    };

                    cd.XamlRoot = Content.XamlRoot;

                    switch (await cd.ShowAsync())
                    {
                        case ContentDialogResult.Primary:
                            Dialogs.MigrationWatcher.IsMigrationRunning = true;
                            OverlapFrame.Navigate(typeof(Dialogs.InstallationMigrate));
                            await CheckMigrationProcess();
                            OverlapFrame.Navigate(typeof(Pages.HomePage));
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

        string fileURL, fileOutput, fileHash;

        private async Task StartInstallationProcedure(string destinationFolder)
        {
            try
            {
                while (!await DownloadGameClient(destinationFolder))
                {

                }
            }
            catch (OperationCanceledException)
            {

            }
        }

        private async Task<bool> DownloadGameClient(string destinationFolder)
        {
            bool returnVal = true,
                 gameCorrupted = false;
            fileURL = regionResourceProp.data.game.latest.path;
            fileOutput = Path.Combine(destinationFolder, Path.GetFileName(fileURL));

            DispatcherQueue.TryEnqueue(() =>
            {
                progressRing.Value = 0;
                ProgressStatusGrid.Visibility = Visibility.Visible;
                InstallGameBtn.Visibility = Visibility.Collapsed;
                CancelDownloadBtn.Visibility = Visibility.Visible;
                ProgressStatusTitle.Text = "Downloading";
            });

            InstallerDownloadTokenSource = new CancellationTokenSource();
            CancellationToken token = InstallerDownloadTokenSource.Token;

            InstallerHttpClient = new HttpClientTool();

            if (await CheckExistingDownload(fileOutput))
            {
                InstallerHttpClient.PartialProgressChanged += InstallerDownloadStatusChanged;
                InstallerHttpClient.Completed += InstallerDownloadStatusCompleted;

                // await Task.Run(() => InstallerHttpClient.DownloadFile(fileURL, fileOutput, "", -1, -1, token));
                await Task.Run(() => InstallerHttpClient.DownloadFileMultipleSession(fileURL, fileOutput, "", DownloadThread, token));

                InstallerHttpClient.PartialProgressChanged -= InstallerDownloadStatusChanged;
                InstallerHttpClient.Completed -= InstallerDownloadStatusCompleted;
            }

            InstallerDownloadTokenSource = new CancellationTokenSource();
            token = InstallerDownloadTokenSource.Token;

            if (File.Exists(fileOutput))
                await Task.Run(() =>
                {
                    fileHash = DoVerifyAndExtractInstallation(fileOutput, token);
                    if (fileHash == regionResourceProp.data.game.latest.md5)
                    {
                        LogWriteLine();
                        LogWriteLine($"Downloaded game installation is verified and Ready to be extracted!");
                    }
                    else
                    {
                        LogWriteLine();
                        LogWriteLine($"Downloaded game installation is corrupted!\r\n\tServer Hash: {regionResourceProp.data.game.latest.md5}\r\n\tDownloaded Hash: {fileHash}", Hi3Helper.LogType.Error);
                        gameCorrupted = true;
                    }
                });

            if (gameCorrupted)
            {
                var cd = new ContentDialog
                {
                    Title = "Oops! Game Installation is Corrupted",
                    Content = $"Sorry but seems your Game Installation that you've downloaded is broken."
                       + $"\r\n\r\nServer Hash: {regionResourceProp.data.game.latest.md5}\r\nDownloaded Hash: {fileHash}"
                       + "\r\n\r\nDo you want to rerdownload the file?",
                    CloseButtonText = "No, Cancel",
                    PrimaryButtonText = "Yes, Redownload",
                    DefaultButton = ContentDialogButton.Primary,
                    Background = (Brush)Application.Current.Resources["DialogAcrylicBrush"]
                };

                cd.XamlRoot = Content.XamlRoot;

                switch (await cd.ShowAsync())
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

            CancelInstallationDownload();
            return returnVal;
        }

        string DoVerifyAndExtractInstallation(string fileOutput, CancellationToken token)
        {
            return VerifyPartialMD5(fileOutput, token).ToLowerInvariant();
        }

        string VerifyPartialMD5(string fileOutput, CancellationToken token)
        {
            MD5 md5 = MD5.Create();
            FileStream stream;
            byte[] buffer = new byte[16777216];

            int read = 0;
            long totalRead = 0;

            string InstallVerifySizeString,
                VerifySizeString, InstallVerifySpeedString;

            var sw = Stopwatch.StartNew();


            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressStatusTitle.Text = "Verifying";
            });

            using (stream = new FileStream(fileOutput, FileMode.Open, FileAccess.Read))
            {
                LogWriteLine(string.Format("Verifying downloaded file"));

                long fileLength = stream.Length;
                while ((read = stream.Read(buffer, 0, buffer.Length)) >= buffer.Length)
                {
                    token.ThrowIfCancellationRequested();
                    md5.TransformBlock(buffer, 0, buffer.Length, buffer, 0);
                    totalRead += read;

                    InstallVerifySizeString = ConverterTool.SummarizeSizeSimple(totalRead);
                    VerifySizeString = ConverterTool.SummarizeSizeSimple(fileLength);
                    InstallVerifySpeedString = $"{ConverterTool.SummarizeSizeSimple((long)(totalRead / sw.Elapsed.TotalSeconds))}/s";

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        ProgressStatusSubtitle.Text = $"{InstallVerifySizeString} / {VerifySizeString}";
                        LogWrite($"Verifying: {InstallVerifySizeString}", Hi3Helper.LogType.Empty, false, true);
                        ProgressStatusFooter.Text = $"Speed: {InstallVerifySpeedString}";
                        progressRing.Value = Math.Round(((float)totalRead / (float)fileLength) * 100, 2);
                    });
                }
            }

            md5.TransformFinalBlock(buffer, 0, read);
            sw.Stop();

            return ConverterTool.BytesToHex(md5.Hash);
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
                    var cd = new ContentDialog
                    {
                        Title = "Resume Download?",
                        Content = $"You have downloaded {ConverterTool.SummarizeSizeSimple(partialLength)} of {ConverterTool.SummarizeSizeSimple(contentLength)} size"
                                  + " of the game previously."
                                  + "\r\n\r\nDo you want to resume the previous download?",
                        PrimaryButtonText = "Yes, Resume",
                        SecondaryButtonText = "No, Start from Beginning",
                        DefaultButton = ContentDialogButton.Primary,
                        Background = (Brush)Application.Current.Resources["DialogAcrylicBrush"]
                    };

                    cd.XamlRoot = Content.XamlRoot;

                    switch (await cd.ShowAsync())
                    {
                        case ContentDialogResult.Primary:
                            break;
                        case ContentDialogResult.Secondary:
                            fileInfo.Delete();
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

        long GetExistingPartialDownloadLength(string fileOutput)
        {
            long output = 0;

            FileInfo fileInfo;

            foreach (string file in Directory.GetFiles(Path.GetDirectoryName(fileOutput), "*.7z.0*"))
                if ((fileInfo = new FileInfo(file)).Exists)
                    output += fileInfo.Length;

            return output;
        }

        string InstallDownloadSpeedString;
        string InstallDownloadSizeString;
        string DownloadSizeString;
        private void InstallerDownloadStatusChanged(object sender, PartialDownloadProgressChanged e)
        {
            InstallDownloadSpeedString = $"{ConverterTool.SummarizeSizeSimple(e.CurrentSpeed)}/s";
            InstallDownloadSizeString = ConverterTool.SummarizeSizeSimple(e.BytesReceived);
            DownloadSizeString = ConverterTool.SummarizeSizeSimple(e.TotalBytesToReceive);
            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressStatusSubtitle.Text = $"{InstallDownloadSizeString} / {DownloadSizeString}";
                LogWrite($"{e.Message}: {InstallDownloadSpeedString}", Hi3Helper.LogType.Empty, false, true);
                ProgressStatusFooter.Text = $"Speed: {InstallDownloadSpeedString}";
                progressRing.Value = Math.Round(e.ProgressPercentage, 2);
                ProgressStatusTitle.Text = e.Message;
            });
        }

        private void InstallerDownloadStatusCompleted(object sender, DownloadProgressCompleted e)
        {

        }

        private void CancelInstallationProcedure(object sender, RoutedEventArgs e)
        {
            CancelInstallationDownload();
        }

        private void CancelInstallationDownload()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressStatusGrid.Visibility = Visibility.Collapsed;
                InstallGameBtn.Visibility = Visibility.Visible;
                CancelDownloadBtn.Visibility = Visibility.Collapsed;
            });

            InstallerHttpClient.PartialProgressChanged -= InstallerDownloadStatusChanged;
            InstallerHttpClient.Completed -= InstallerDownloadStatusCompleted;


            InstallerDownloadTokenSource.Cancel();
        }

        private async Task<string> InstallGameDialogScratch()
        {
            FolderPicker folderPicker = new FolderPicker();
            StorageFolder folder;
            string returnFolder = "";

            var cd = new ContentDialog
            {
                Title = "Locating Installation Folder",
                Content = $"Before Installing the Game, Do you want to specify the location of the game?",
                CloseButtonText = "Cancel",
                PrimaryButtonText = "Use default directory",
                SecondaryButtonText = "Yes, Change location",
                DefaultButton = ContentDialogButton.Primary,
                Background = (Brush)Application.Current.Resources["DialogAcrylicBrush"]
            };

            cd.XamlRoot = Content.XamlRoot;

            try
            {
                switch (await cd.ShowAsync())
                {
                    case ContentDialogResult.Primary:
                        LogWriteLine($"TODO: Do Install to Default Folder");
                        returnFolder = Path.Combine(AppGameFolder, CurrentRegion.ProfileName);
                        break;
                    case ContentDialogResult.Secondary:
                        LogWriteLine($"TODO: Do Install to Custom Folder");
                        folderPicker.FileTypeFilter.Add("*");
                        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, InvokeProp.m_windowHandle);
                        folder = await folderPicker.PickSingleFolderAsync();

                        returnFolder = folder.Path;
                        break;
                    case ContentDialogResult.None:
                        throw new TaskCanceledException();
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
            while (Dialogs.MigrationWatcher.IsMigrationRunning)
            {
                // Take sleep for 250ms
                await Task.Delay(250);
            }
        }

        private void CheckCollapseLauncherGame()
        {
            if (File.Exists(
                Path.Combine(
                    ConverterTool.NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString()),
                    "BH3.exe")))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    InstallGameBtn.Visibility = Visibility.Collapsed;
                    StartGameBtn.Visibility = Visibility.Visible;
                });
            }
        }

        private async void StartGamme(object sender, RoutedEventArgs e)
        {
            try
            {
                Process proc = new Process();
                proc.StartInfo.FileName = Path.Combine(ConverterTool.NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString()), "BH3.exe");
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.Arguments = $"-window-mode exclusive";
                proc.StartInfo.WorkingDirectory = Path.GetDirectoryName(ConverterTool.NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString()));
                proc.StartInfo.Verb = "runas";
                proc.Start();

                await proc.WaitForExitAsync();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                LogWriteLine($"There is a problem while trying to launch Game with Region: {CurrentRegion.ZoneName}\r\nTraceback: {ex}", Hi3Helper.LogType.Error, true);
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            Dialogs.MigrationWatcher.IsMigrationRunning = false;
            CancelInstallationDownload();
        }
    }
}
