using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;

using static Hi3Helper.Locale;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Shared.Region.LauncherConfig;

using static CollapseLauncher.Pages.RepairData;

namespace CollapseLauncher.Pages
{
    public sealed partial class RepairPage : Page
    {
        long RepairSingleFileSize,
             RepairSingleFileRead,
             RepairTotalFileSize,
             RepairTotalFileRead,
             FileExistingLength;

        int RepairedFilesCount;

        int DownloadThread = GetAppConfigValue("DownloadThread").ToInt();
        int MultipleDownloadSizeStart = 10 << 20;

        private async void StartGameRepair(object sender, RoutedEventArgs e)
        {
            RepairFilesBtn.IsEnabled = false;
            CancelBtn.IsEnabled = true;

            try
            {
                await CategorizeRepairAction();
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Repairing Cancelled!");
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex);
                Console.WriteLine($"{ex}");
            }
        }

        private async Task CategorizeRepairAction()
        {
            await Task.Run(() =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    RepairTotalProgressBar.Value = 0;
                });

                RepairTotalFileRead = 0;
                RepairedFilesCount = 0;
                foreach (var BrokenFile in BrokenFileIndexesProperty)
                {
                    RepairSingleFileRead = 0;
                    FilePath = Path.Combine(GameBasePath, NormalizePath(BrokenFile.N));
                    if (!Directory.Exists(Path.GetDirectoryName(FilePath)))
                        Directory.CreateDirectory(Path.GetDirectoryName(FilePath));

                    switch (BrokenFile.FT)
                    {
                        case FileType.Generic:
                        case FileType.Audio:
                            RepairGenericAudioFiles(BrokenFile);
                            break;
                        case FileType.Blocks:
                            RepairBlockFiles(BrokenFile);
                            break;
                    }
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    sw.Stop();
                    CancelBtn.IsEnabled = false;
                    CheckFilesBtn.Visibility = Visibility.Visible;
                    CheckFilesBtn.IsEnabled = true;
                    RepairFilesBtn.Visibility = Visibility.Collapsed;
                    RepairStatus.Text = Lang._GameRepairPage.Status7;
                    RepairPerFileStatus.Text = Lang._GameRepairPage.StatusNone;
                    RepairTotalStatus.Text = Lang._GameRepairPage.StatusNone;
                    RepairPerFileProgressBar.Value = 0;
                    RepairTotalProgressBar.Value = 0;
                });
            });
        }

        FileInfo RepairFileInfo;
        FileStream RepairFileStream;
        MemoryStream RepairMemoryStream;
        private void RepairGenericAudioFiles(FilePropertiesRemote input)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RepairStatus.Text = string.Format(Lang._GameRepairPage.Status8, input.N);
                RepairTotalStatus.Text = string.Format(Lang._GameRepairPage.PerProgressSubtitle2, RepairedFilesCount, BrokenFilesCount);
                RepairTotalProgressBar.Value = GetPercentageNumber(RepairedFilesCount, BrokenFilesCount, 2);
            });

            RepairFileInfo = new FileInfo(FilePath);

            if (input.FT == FileType.Generic)
                FileURL = GameBaseURL + input.N;
            else
                FileURL = GameBaseURL + Path.GetDirectoryName(input.N).Replace('\\', '/') + $"/{input.RN}";

            if (input.S == 0)
            {
                RepairFileInfo.Create();
            }
            else
            {
                http.DownloadProgress += GenericFilesDownloadProgress;

                if (input.S > MultipleDownloadSizeStart)
                    http.DownloadFile(FileURL, FilePath, DownloadThread, cancellationTokenSource.Token);
                else
                    using (RepairFileStream = RepairFileInfo.Create())
                        http.DownloadFile(FileURL, RepairFileStream, cancellationTokenSource.Token, null, null, false);

                http.DownloadProgress -= GenericFilesDownloadProgress;
            }
            DispatcherQueue.TryEnqueue(() =>
            {
                NeedRepairListUI.RemoveAt(0);
            });
            RepairedFilesCount++;
        }

        private void RepairBlockFiles(FilePropertiesRemote input)
        {
            string BlockBasePath = NormalizePath(input.N);
            foreach (var block in input.BlkC)
            {
                FileURL = GameBaseURL + input.N + $"/{block.BlockHash}.wmv";
                FilePath = Path.Combine(GameBasePath, BlockBasePath, block.BlockHash + ".wmv");
                RepairFileInfo = new FileInfo(FilePath);

                FileExistingLength = RepairFileInfo.Exists ? RepairFileInfo.Length : 0;
                
                if (RepairFileInfo.Exists && RepairFileInfo.Length != block.BlockSize)
                    RepairFileInfo.Delete();

                if (!RepairFileInfo.Exists)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        RepairStatus.Text = string.Format(Lang._GameRepairPage.Status9, block.BlockHash);
                        RepairTotalStatus.Text = string.Format(Lang._GameRepairPage.PerProgressSubtitle2, RepairedFilesCount, BrokenFilesCount);
                        RepairTotalProgressBar.Value = GetPercentageNumber(RepairedFilesCount, BrokenFilesCount, 2);
                    });

                    http.DownloadProgress += GenericFilesDownloadProgress;

                    if (block.BlockSize > MultipleDownloadSizeStart)
                        http.DownloadFile(FileURL, FilePath, DownloadThread, cancellationTokenSource.Token);
                    else
                        using (RepairFileStream = RepairFileInfo.Create())
                            http.DownloadFile(FileURL, RepairFileStream, cancellationTokenSource.Token, -1, -1, false);

                    http.DownloadProgress -= GenericFilesDownloadProgress;

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        NeedRepairListUI.RemoveAt(0);
                    });

                    RepairedFilesCount++;
                }
                else
                {
                    using (RepairFileStream = RepairFileInfo.OpenWrite())
                    {
                        foreach (var chunk in block.BlockContent)
                        {
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                RepairStatus.Text = string.Format(Lang._GameRepairPage.Status10, block.BlockHash, chunk._startoffset.ToString("x8"), chunk._filesize.ToString("x8"));
                                RepairTotalStatus.Text = string.Format(Lang._GameRepairPage.PerProgressSubtitle2, RepairedFilesCount, BrokenFilesCount);
                                RepairTotalProgressBar.Value = GetPercentageNumber(RepairedFilesCount, BrokenFilesCount, 2);
                            });

                            // RepairMemoryStream = new MemoryStream();
                            RepairFileStream.Position = chunk._startoffset;

                            http.DownloadProgress += GenericFilesDownloadProgress;
                            http.DownloadFile(FileURL, RepairFileStream, cancellationTokenSource.Token, chunk._startoffset, chunk._startoffset + chunk._filesize, false);
                            http.DownloadProgress -= GenericFilesDownloadProgress;

                            // httpClient.ProgressChanged += GenericFilesDownloadProgress;

                            // httpClient.DownloadStream(FileURL, RepairMemoryStream, cancellationTokenSource.Token,
                            // chunk._startoffset, chunk._startoffset + chunk._filesize);

                            // httpClient.ProgressChanged -= GenericFilesDownloadProgress;

                            // RepairFileStream.Write(RepairMemoryStream.GetBuffer(), 0, (int)chunk._filesize);

                            // RepairMemoryStream.Dispose();
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                NeedRepairListUI.RemoveAt(0);
                            });

                            RepairedFilesCount++;
                        }
                    }
                }
            }
        }

        Stopwatch refreshTime = Stopwatch.StartNew();

        private void GenericFilesDownloadProgress(object sender, HttpClientHelper._DownloadProgress e)
        {
            if (refreshTime.Elapsed.Milliseconds > 33)
            {
                refreshTime = Stopwatch.StartNew();
                DispatcherQueue.TryEnqueue(() =>
                {
                    RepairPerFileStatus.Text = string.Format(Lang._Misc.Speed, SummarizeSizeSimple(e.CurrentSpeed));
                    RepairPerFileProgressBar.Value = Math.Round(e.ProgressPercentage, 2);
                });
            }
        }
    }
}
