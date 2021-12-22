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

using CollapseLauncher.Dialogs;

using Hi3Helper.Data;
using Hi3Helper.Preset;

using Newtonsoft.Json;

using SharpCompress;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.IO;

using static Hi3Helper.Logger;
using static Hi3Helper.Data.ConverterTool;
using static CollapseLauncher.LauncherConfig;
using static CollapseLauncher.Dialogs.SimpleDialogs;
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
        public HomePage()
        {
            this.InitializeComponent();
            MigrationWatcher.IsMigrationRunning = false;
            HomePageProp.Current = this;

            LoadGameConfig();
            CheckCurrentGameState();

            Task.Run(() => CheckRunningGameInstance());
        }

        private void CheckCurrentGameState()
        {
            if (File.Exists(
                   Path.Combine(
                       NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString()),
                       "BH3.exe")))
            {
                if (new FileInfo(Path.Combine(
                    NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString()),
                    "BH3.exe")).Length < 0xFFFF)
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
                        GameInstallationState = GameInstallStateEnum.InstalledHavePreload;
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
            }
            GameInstallationState = GameInstallStateEnum.NotInstalled;
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
                    switch (await Dialog_ExistingInstallation(Content))
                    {
                        case ContentDialogResult.Primary:
                            MigrationWatcher.IsMigrationRunning = true;
                            OverlapFrame.Navigate(typeof(InstallationMigrate));
                            await CheckMigrationProcess();
                            OverlapFrame.Navigate(typeof(HomePage));
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
                switch (await Dialog_InstallationDownloadAdditional(Content))
                {
                    case ContentDialogResult.Primary:
                        RequireAdditionalDataDownload = true;
                        break;
                    case ContentDialogResult.Secondary:
                        RequireAdditionalDataDownload = false;
                        break;
                }

                if (string.IsNullOrEmpty(destinationFolder))
                    throw new OperationCanceledException();

                while (!await DownloadGameClient(destinationFolder))
                {
                    // Always loop if something wrong happen
                }

                File.Delete(fileOutput);
                ApplyGameConfig(destinationFolder);

                DispatcherQueue.TryEnqueue(() => OverlapFrame.Navigate(typeof(HomePage)));
            }
            catch (OperationCanceledException)
            {

            }
        }

        private void ApplyGameConfig(string destinationFolder)
        {
            gameIni.Profile["launcher"]["game_install_path"] = Path.Combine(destinationFolder, "Games").Replace('\\', '/');
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

                await Task.Run(() => InstallerHttpClient.DownloadFileMultipleSession(fileURL, fileOutput, "", appIni.Profile["app"]["DownloadThread"].ToInt(), token));

                InstallerHttpClient.PartialProgressChanged -= InstallerDownloadStatusChanged;
                InstallerHttpClient.Completed -= InstallerDownloadStatusCompleted;
            }

            InstallerDownloadTokenSource = new CancellationTokenSource();
            token = InstallerDownloadTokenSource.Token;

            if (!await DoZipVerification(fileOutput, token))
            {
                switch (await Dialog_GameInstallationFileCorrupt(Content, regionResourceProp.data.game.latest.md5, fileHash))
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
            if (File.Exists(inputFile))
            {
                fileHash = await Task.Run(() => GetMD5FromFile(inputFile, token));
                if (fileHash == regionResourceProp.data.game.latest.md5)
                {
                    LogWriteLine();
                    LogWriteLine($"Downloaded game installation is verified and Ready to be extracted!");
                }
                else
                {
                    LogWriteLine();
                    LogWriteLine($"Downloaded game installation is corrupted!\r\n\tServer Hash: {regionResourceProp.data.game.latest.md5}\r\n\tDownloaded Hash: {fileHash}", Hi3Helper.LogType.Error);
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
            destinationFolder = Path.Combine(destinationFolder, "Games");
            byte[] buffer = new byte[131072];
            // string outputPath;

            if (!Directory.Exists(destinationFolder))
                Directory.CreateDirectory(destinationFolder);

            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressStatusTitle.Text = "Extracting";
            });

            SevenZipTool sevenZip = new SevenZipTool();

            sevenZip.Load(sourceFile);

            sevenZip.ExtractProgressChanged += ExtractProgress;
            try
            {
                sevenZip.ExtractToDirectory(destinationFolder, Environment.ProcessorCount, token);
            }
            catch (OperationCanceledException ex)
            {
                sevenZip.ExtractProgressChanged -= ExtractProgress;
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

                    InstallVerifySizeString = SummarizeSizeSimple(totalRead);
                    VerifySizeString = SummarizeSizeSimple(fileLength);
                    InstallVerifySpeedString = $"{SummarizeSizeSimple((long)(totalRead / sw.Elapsed.TotalSeconds))}/s";

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

            return BytesToHex(md5.Hash).ToLowerInvariant();
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

            foreach (string file in Directory.GetFiles(Path.GetDirectoryName(fileOutput), "*.7z.0*"))
                if ((fileInfo = new FileInfo(file)).Exists)
                    fileInfo.Delete();
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
            InstallDownloadSpeedString = $"{SummarizeSizeSimple(e.CurrentSpeed)}/s";
            InstallDownloadSizeString = SummarizeSizeSimple(e.BytesReceived);
            DownloadSizeString = SummarizeSizeSimple(e.TotalBytesToReceive);
            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressStatusSubtitle.Text = $"{InstallDownloadSizeString} / {DownloadSizeString}";
                LogWrite($"{e.Message}: {InstallDownloadSpeedString}", Hi3Helper.LogType.Empty, false, true);
                ProgressStatusFooter.Text = $"Speed: {InstallDownloadSpeedString}";
                ProgressTimeLeft.Text = string.Format("{0:%h}h{0:%m}m{0:%s}s left", e.TimeLeft);
                progressRing.Value = Math.Round(e.ProgressPercentage, 2);
                ProgressStatusTitle.Text = e.Message;
            });
        }

        private void InstallerDownloadStatusCompleted(object sender, DownloadProgressCompleted e)
        {

        }

        private void CancelInstallationProcedure(object sender, RoutedEventArgs e)
        {
            switch (GameInstallationState)
            {
                case GameInstallStateEnum.NeedsUpdate:
                    CancelUpdateDownload();
                    break;
                case GameInstallStateEnum.NotInstalled:
                    CancelInstallationDownload();
                    break;
            }
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

                OverlapFrame.Navigate(typeof(HomePage));
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

                OverlapFrame.Navigate(typeof(HomePage));
            });
        }

        private async Task<string> InstallGameDialogScratch()
        {
            FolderPicker folderPicker = new FolderPicker();
            StorageFolder folder;
            string returnFolder = "";

            try
            {
                switch (await Dialog_InstallationLocation(Content))
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
            while (MigrationWatcher.IsMigrationRunning)
            {
                // Take sleep for 250ms
                await Task.Delay(250);
            }
        }

        private async void StartGamme(object sender, RoutedEventArgs e)
        {
            try
            {
                Process proc = new Process();
                proc.StartInfo.FileName = Path.Combine(NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString()), "BH3.exe");
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.Arguments = $"";
                proc.StartInfo.WorkingDirectory = Path.GetDirectoryName(NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString()));
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
            MigrationWatcher.IsMigrationRunning = false;
            CancelInstallationDownload();
        }

        private async void UpdateGameDialog(object sender, RoutedEventArgs e)
        {
            string GamePath = NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString());
            fileURL = regionResourceProp.data.game.latest.path;
            string GameZipPath = Path.Combine(GamePath, "..\\", Path.GetFileName(fileURL));

            try
            {
                while (!await UpdateGameClient(GameZipPath, GamePath))
                {

                }

                File.Delete(GameZipPath);
                ApplyGameConfig(Path.GetDirectoryName(GamePath));
                OverlapFrame.Navigate(typeof(HomePage));
            }
            catch (OperationCanceledException)
            {
                LogWriteLine($"Update cancelled!", Hi3Helper.LogType.Warning);
                CancelUpdateDownload();
            }
        }

        private async Task<bool> UpdateGameClient(string sourceFile, string GamePath)
        {
            bool returnVal = true;

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
                await Task.Run(() => InstallerHttpClient.DownloadFileMultipleSession(fileURL, sourceFile, "", appIni.Profile["app"]["DownloadThread"].ToInt(), token));
            }

            InstallerHttpClient.PartialProgressChanged -= InstallerDownloadStatusChanged;
            InstallerHttpClient.Completed -= InstallerDownloadStatusCompleted;

            if (await DoZipVerification(sourceFile, token))
            {
                await Task.Run(() => ExtractDownloadedGame(sourceFile, Path.Combine(GamePath, "..\\"), token));
                await CleanUpAssets(GamePath);
            }
            else
            {
                switch (await Dialog_GameInstallationFileCorrupt(Content, regionResourceProp.data.game.latest.md5, fileHash))
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
    }
}
