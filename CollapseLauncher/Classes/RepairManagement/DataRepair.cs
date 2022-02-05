using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

using Force.Crc32;

using Newtonsoft.Json;

using Hi3Helper.Data;
using Hi3Helper.Preset;

using static Hi3Helper.Logger;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static Hi3Helper.Shared.Region.InstallationManagement;

using static CollapseLauncher.Pages.RepairData;

namespace CollapseLauncher.Pages
{
    public sealed partial class RepairPage : Page
    {
        long RepairSingleFileSize,
             RepairSingleFileRead,
             RepairTotalFileSize,
             RepairTotalFileRead;

        int RepairedFilesCount;

        int DownloadThread = appIni.Profile["app"]["DownloadThread"].ToInt();

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
                    RepairStatus.Text = "Repair Completed!";
                    RepairPerFileStatus.Text = "None";
                    RepairTotalStatus.Text = "None";
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
            RepairedFilesCount++;
            DispatcherQueue.TryEnqueue(() =>
            {
                RepairStatus.Text = $"Repairing: {input.N}";
                RepairTotalStatus.Text = $"Progress: {RepairedFilesCount}/{BrokenFilesCount}";
                RepairTotalProgressBar.Value = GetPercentageNumber(RepairedFilesCount, BrokenFilesCount, 2);
            });

            RepairFileInfo = new FileInfo(FilePath);

            if (RepairFileInfo.Exists)
                RepairFileInfo.Delete();

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
                httpClient.ProgressChanged += GenericFilesDownloadProgress;
                httpClient.Completed += ProgressCompleted;

                Console.WriteLine(FilePath);
                httpClient.DownloadStream(FileURL, RepairFileStream = RepairFileInfo.Create(), cancellationTokenSource.Token);

                httpClient.ProgressChanged -= GenericFilesDownloadProgress;
                httpClient.Completed -= ProgressCompleted;
            }
            DispatcherQueue.TryEnqueue(() =>
            {
                NeedRepairListUI.RemoveAt(0);
            });
        }

        private void RepairBlockFiles(FilePropertiesRemote input)
        {
            string BlockBasePath = NormalizePath(input.N);
            foreach (var block in input.BlkC)
            {
                FileURL = GameBaseURL + input.N + $"/{block.BlockHash}.wmv";
                FilePath = Path.Combine(GameBasePath, BlockBasePath, block.BlockHash + ".wmv");
                RepairFileInfo = new FileInfo(FilePath);

                if (RepairFileInfo.Exists && RepairFileInfo.Length != block.BlockSize)
                    RepairFileInfo.Delete();

                if (!RepairFileInfo.Exists)
                {
                    RepairedFilesCount++;

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        RepairStatus.Text = $"Repairing Blk: {block.BlockHash}";
                        RepairTotalStatus.Text = $"Progress: {RepairedFilesCount}/{BrokenFilesCount}";
                        RepairTotalProgressBar.Value = GetPercentageNumber(RepairedFilesCount, BrokenFilesCount, 2);
                    });

                    httpClient.ProgressChanged += GenericFilesDownloadProgress;
                    httpClient.Completed += ProgressCompleted;

                    httpClient.DownloadStream(FileURL, RepairFileStream = RepairFileInfo.Create(), cancellationTokenSource.Token);

                    httpClient.ProgressChanged -= GenericFilesDownloadProgress;
                    httpClient.Completed -= ProgressCompleted;

                    RepairFileStream.Dispose();
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        NeedRepairListUI.RemoveAt(0);
                    });
                }
                else
                {
                    using (RepairFileStream = RepairFileInfo.OpenWrite())
                    {
                        foreach (var chunk in block.BlockContent)
                        {
                            RepairedFilesCount++;

                            DispatcherQueue.TryEnqueue(() =>
                            {
                                RepairStatus.Text = $"Repairing Blk: {block.BlockHash} | Offset: 0x{chunk._startoffset.ToString("x8")} - Size: 0x{chunk._filesize.ToString("x8")}";
                                RepairTotalStatus.Text = $"Progress: {RepairedFilesCount}/{BrokenFilesCount}";
                                RepairTotalProgressBar.Value = GetPercentageNumber(RepairedFilesCount, BrokenFilesCount, 2);
                            });

                            RepairMemoryStream = new MemoryStream();
                            RepairFileStream.Position = chunk._startoffset;

                            httpClient.ProgressChanged += GenericFilesDownloadProgress;
                            httpClient.Completed += ProgressCompleted;

                            httpClient.DownloadStream(FileURL, RepairMemoryStream, cancellationTokenSource.Token,
                                chunk._startoffset, chunk._startoffset + chunk._filesize);

                            httpClient.ProgressChanged -= GenericFilesDownloadProgress;
                            httpClient.Completed -= ProgressCompleted;

                            RepairFileStream.Write(RepairMemoryStream.GetBuffer(), 0, (int)chunk._filesize);

                            RepairMemoryStream.Dispose();
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                NeedRepairListUI.RemoveAt(0);
                            });
                        }
                        RepairFileStream.Dispose();
                    }
                }
            }
        }

        private void GenericFilesPartialDownloadProgress(object sender, PartialDownloadProgressChanged e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RepairPerFileStatus.Text = $"Speed: {SummarizeSizeSimple(e.CurrentSpeed)}/s";
                RepairPerFileProgressBar.Value = Math.Round(e.ProgressPercentage, 2);
            });
        }

        private void GenericFilesDownloadProgress(object sender, DownloadProgressChanged e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RepairPerFileStatus.Text = $"Speed: {SummarizeSizeSimple(e.CurrentSpeed)}/s";
                RepairPerFileProgressBar.Value = Math.Round(e.ProgressPercentage, 2);
            });
        }
    }
}
