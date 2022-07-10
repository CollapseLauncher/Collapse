using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.GameConversion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.InstallationManagement;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Dialogs
{
    public partial class InstallationMigrateSteam : Page
    {
        string sourcePath;
        string targetPath;
        string endpointURL;
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        List<FilePropertiesRemote> BrokenFileIndexesProperty = new List<FilePropertiesRemote>();

        public InstallationMigrateSteam()
        {
            try
            {
                this.InitializeComponent();
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        public async Task StartMigrationProcess()
        {
            try
            {
                endpointURL = string.Format(CurrentRegion.ZipFileURL, Path.GetFileNameWithoutExtension(regionResourceProp.data.game.latest.path));
                if (await DoCheckPermission())
                {
                    await DoMigrationProcess();
                }

                targetPath = GamePathOnSteam;
                await DoCompareProcess();
                await DoConversionProcess();
                await DoVerification();

                if (BrokenFileIndexesProperty.Count > 0)
                {
                    await Dialog_SteamConversionFailedDialog(Content);
                    OperationCancelled();
                    return;
                }

                ApplyConfiguration();
                OperationCancelled(true);
            }
            catch (OperationCanceledException)
            {
                LogWriteLine($"Conversion process is cancelled for Game Region: {CurrentRegion.ZoneName}");
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex);
            }
            MainFrameChanger.ChangeWindowFrame(typeof(MainPage));
        }

        public void ApplyConfiguration()
        {
            gameIni.Profile["launcher"]["game_install_path"] = targetPath;
            gameIni.Profile.Save(gameIni.ProfilePath);

            gameIni.Config = new IniFile();
            gameIni.Config.Add("General", new Dictionary<string, IniValue>
            {
                    { "channel", new IniValue(1) },
                    { "cps", new IniValue() },
                    { "game_version", new IniValue(regionResourceProp.data.game.latest.version) },
                    { "sub_channel", new IniValue(1) },
                    { "sdk_version", new IniValue() },
            });
            gameIni.Config.Save(Path.Combine(targetPath, "config.ini"));

            File.Delete(Path.Combine(targetPath, "_conversion_unfinished"));
        }

        private async Task DoConversionProcess()
        {
            long TotalSizeOfBrokenFile = 0;
            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressSlider.Value = 3;

                Step4.Opacity = 1f;
                Step4ProgressRing.IsIndeterminate = true;
                Step4ProgressRing.Value = 0;
                Step4ProgressStatus.Text = Lang._InstallMigrateSteam.Step4Subtitle;
            });

            TotalSizeOfBrokenFile = BrokenFileIndexesProperty.Sum(x => x.S)
                                  + BrokenFileIndexesProperty.Where(x => x.BlkC != null).Sum(x => x.BlkC.Sum(x => x.BlockSize));

            LogWriteLine($"Steam to Global Version conversion will take {SummarizeSizeSimple(TotalSizeOfBrokenFile)} of file size to download!\r\n\tThe files are including:");

            foreach (var file in BrokenFileIndexesProperty)
            {
                LogWriteLine($"\t{file.N} {SummarizeSizeSimple(file.S)}", Hi3Helper.LogType.Empty);
            }

            switch (await Dialog_SteamConversionDownloadDialog(Content, SummarizeSizeSimple(TotalSizeOfBrokenFile)))
            {
                case ContentDialogResult.None:
                    OperationCancelled();
                    break;
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                Step4ProgressRing.IsIndeterminate = false;
                Step4ProgressRing.Value = 0;
            });

            await StartConversionTask();

            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressSlider.Value = 3;

                Step4.Opacity = 1f;
                Step4ProgressRing.IsIndeterminate = false;
                Step4ProgressRing.Value = 100;
                Step4ProgressStatus.Text = Lang._Misc.Completed;
            });
        }

        private async Task StartConversionTask()
        {
            SteamConversion conversionTool = new SteamConversion(targetPath, endpointURL, BrokenFileIndexesProperty, tokenSource);

            conversionTool.ProgressChanged += ConversionProgressChanged;
            await conversionTool.StartConverting();
            conversionTool.ProgressChanged -= ConversionProgressChanged;
        }

        private async Task DoCompareProcess()
        {
            await Task.Run(() =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ProgressSlider.Value = 2;

                    Step3.Opacity = 1f;
                    Step3ProgressRing.IsIndeterminate = false;
                    Step3ProgressRing.Value = 0;
                    Step3ProgressStatus.Text = Lang._InstallMigrateSteam.Step3Subtitle;
                });

                StartCheckIntegrity();

                DispatcherQueue.TryEnqueue(() =>
                {
                    ProgressSlider.Value = 2;

                    Step3.Opacity = 1f;
                    Step3ProgressRing.IsIndeterminate = false;
                    Step3ProgressRing.Value = 100;
                    Step3ProgressStatus.Text = Lang._Misc.Completed;
                });
            });
        }

        private async Task DoVerification()
        {
            await Task.Run(() =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ProgressSlider.Value = 4;

                    Step5.Opacity = 1f;
                    Step5ProgressRing.IsIndeterminate = false;
                    Step5ProgressRing.Value = 0;
                    Step5ProgressStatus.Text = Lang._InstallMigrateSteam.Step5Subtitle;
                });

                StartCheckVerification();

                DispatcherQueue.TryEnqueue(() =>
                {
                    Step5.Opacity = 1f;
                    Step5ProgressRing.IsIndeterminate = false;
                    Step5ProgressRing.Value = 100;
                    Step5ProgressStatus.Text = Lang._Misc.Completed;
                });
            });
        }

        private void StartCheckIntegrity()
        {
            CheckIntegrity integrityTool = new CheckIntegrity(targetPath, endpointURL, tokenSource);

            integrityTool.ProgressChanged += IntegrityProgressChanged;
            integrityTool.StartCheckIntegrity();
            integrityTool.ProgressChanged -= IntegrityProgressChanged;

            BrokenFileIndexesProperty = integrityTool.GetNecessaryFileList();
        }

        private void StartCheckVerification()
        {
            CheckIntegrity integrityTool = new CheckIntegrity(targetPath, endpointURL, tokenSource);

            integrityTool.ProgressChanged += VerificationProgressChanged;
            integrityTool.StartCheckIntegrity();
            integrityTool.ProgressChanged -= VerificationProgressChanged;

            BrokenFileIndexesProperty = integrityTool.GetNecessaryFileList();
        }

        private void IntegrityProgressChanged(object sender, CheckIntegrityChanged e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                Step3ProgressRing.Value = Math.Round(e.ProgressPercentage, 2);
                Step3ProgressStatus.Text = string.Format("{0} {1} ({2})...", e.Message, Math.Round(e.ProgressPercentage, 0), string.Format(Lang._Misc.Speed, SummarizeSizeSimple(e.CurrentSpeed)));
            });
        }

        private void VerificationProgressChanged(object sender, CheckIntegrityChanged e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                Step5ProgressRing.Value = Math.Round(e.ProgressPercentage, 2);
                Step5ProgressStatus.Text = string.Format("{0} {1} ({2})...", e.Message, Math.Round(e.ProgressPercentage, 0), string.Format(Lang._Misc.Speed, SummarizeSizeSimple(e.CurrentSpeed)));
            });
        }

        private void ConversionProgressChanged(object sender, ConversionTaskChanged e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                Step4ProgressRing.Value = Math.Round(e.ProgressPercentage, 2);
                Step4ProgressStatus.Text = string.Format("{0} {1} ({2})...", e.Message, Math.Round(e.ProgressPercentage, 0), string.Format(Lang._Misc.Speed, SummarizeSizeSimple(e.CurrentSpeed)));
            });
        }

        private async Task DoMigrationProcess()
        {
            try
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ProgressSlider.Value = 1;

                    Step2.Opacity = 1f;
                    Step2ProgressRing.IsIndeterminate = true;
                    Step2ProgressRing.Value = 0;
                    Step2ProgressStatus.Text = Lang._InstallMigrateSteam.Step2Subtitle1;
                });

                Process proc = new Process();
                proc.StartInfo.FileName = Path.Combine(AppFolder, "CollapseLauncher.exe");
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.Arguments = $"movesteam --input \"{sourcePath}\" --output \"{targetPath}\" --regloc \"{CurrentRegion.SteamInstallRegistryLocation}\" --keyname InstallLocation";
                proc.StartInfo.Verb = "runas";

                LogWriteLine($"Launching Invoker with Argument:\r\n\t{proc.StartInfo.Arguments}");

                await Task.Delay(5000);

                proc.Start();

                DispatcherQueue.TryEnqueue(() =>
                {
                    Step2ProgressStatus.Text = Lang._InstallMigrateSteam.Step2Subtitle2;
                });

                await Task.Run(() => proc.WaitForExit());

                DispatcherQueue.TryEnqueue(() =>
                {
                    Step2ProgressRing.IsIndeterminate = false;
                    Step2ProgressRing.Value = 100;
                    Step2ProgressStatus.Text = Lang._Misc.Completed;
                });
            }
            catch (Exception)
            {
                OperationCancelled();
            }
        }

        private async Task<bool> DoCheckPermission()
        {
            StorageFolder folder;

            DispatcherQueue.TryEnqueue(() => Step1.Opacity = 1f);

            if (IsUserHasPermission(GamePathOnSteam))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ProgressSlider.Value = 2;

                    Step1ProgressRing.IsIndeterminate = false;
                    Step1ProgressRing.Value = 100;
                    Step1ProgressStatus.Text = Lang._Misc.Completed;

                    Step2.Opacity = 1f;
                    Step2ProgressRing.IsIndeterminate = false;
                    Step2ProgressRing.Value = 100;
                    Step2ProgressStatus.Text = Lang._Misc.Skipped;
                });

                return false;
            }

            bool isChoosen = false;
            string choosenFolder = "";
            while (!isChoosen)
            {
                switch (await Dialog_SteamConversionNoPermission(Content))
                {
                    case ContentDialogResult.None:
                        OperationCancelled();
                        break;
                    case ContentDialogResult.Primary:
                        sourcePath = GamePathOnSteam;
                        choosenFolder = Path.Combine(AppGameFolder, CurrentRegion.ProfileName);
                        targetPath = Path.Combine(choosenFolder, Path.GetFileName(GamePathOnSteam));
                        break;
                    case ContentDialogResult.Secondary:
                        folder = await (m_window as MainWindow).GetFolderPicker();

                        if (folder == null)
                            OperationCancelled();

                        choosenFolder = folder.Path;

                        sourcePath = GamePathOnSteam;
                        targetPath = Path.Combine(choosenFolder, Path.GetFileName(GamePathOnSteam));
                        break;
                }

                if (!(isChoosen = IsUserHasPermission(choosenFolder)))
                    await Dialog_InsufficientWritePermission(Content, choosenFolder);
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                Step1ProgressRing.IsIndeterminate = false;
                Step1ProgressRing.Value = 100;
                Step1ProgressStatus.Text = Lang._Misc.Completed;
            });

            return true;
        }

        private void OperationCancelled(bool noException = false)
        {
            MigrationWatcher.IsMigrationRunning = false;

            if (!noException)
                throw new OperationCanceledException();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await StartMigrationProcess();
        }
    }
}