﻿using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher
{
    internal partial class FileMigrationProcess
    {
        private const int _refreshInterval = 100; // 100ms UI refresh interval

        private string dialogTitle { get; set; }
        private string inputPath { get; set; }
        private string outputPath { get; set; }
        private bool isFileTransfer { get; set; }
        private UIElement parentUI { get; set; }
        private CancellationTokenSource tokenSource { get; set; }

        private long CurrentSizeMoved;
        private long CurrentFileCountMoved;
        private long TotalFileSize;
        private long TotalFileCount;
        private bool IsSameOutputDrive = false;
        private Stopwatch ProcessStopwatch;
        private Stopwatch EventsStopwatch;

        private FileMigrationProcess(UIElement parentUI, string dialogTitle, string inputPath, string outputPath, bool isFileTransfer, CancellationTokenSource tokenSource)
        {
            this.dialogTitle       = dialogTitle;
            this.inputPath         = inputPath;
            this.outputPath        = outputPath;
            this.isFileTransfer    = isFileTransfer;
            this.parentUI          = parentUI;
            this.tokenSource       = tokenSource;
        }

        internal async Task<string> StartRoutine()
        {
            bool isSuccess = false;
            this.CurrentSizeMoved = 0;
            this.CurrentFileCountMoved = 0;
            this.TotalFileSize = 0;
            this.TotalFileCount = 0;
            FileMigrationProcessUIRef? uiRef = null;

            try
            {
                if (!await IsOutputPathSpaceSufficient(this.inputPath, this.outputPath))
                    throw new OperationCanceledException($"Disk space is not sufficient. Cancelling!");

                uiRef = BuildMainMigrationUI();
                string _outputPath = await StartRoutineInner(uiRef.Value);
                uiRef.Value.mainDialogWindow!.Hide();
                isSuccess = true;

                return _outputPath;
            }
            catch when (!isSuccess) // Throw if the isSuccess is not set to true
            {
                if (uiRef.HasValue && uiRef.Value.mainDialogWindow != null)
                {
                    uiRef.Value.mainDialogWindow.Hide();
                    await Task.Delay(500); // Give artificial delay to give main dialog window thread to close first
                }
                throw;
            }
            finally
            {
                if (ProcessStopwatch != null) ProcessStopwatch.Stop();
                if (EventsStopwatch != null) EventsStopwatch.Stop();
            }
        }

        private async Task<string> StartRoutineInner(FileMigrationProcessUIRef uiRef)
        {
            this.ProcessStopwatch = Stopwatch.StartNew();
            this.EventsStopwatch = Stopwatch.StartNew();
            return this.isFileTransfer ? await MoveFile(uiRef) : await MoveDirectory(uiRef);
        }

        private async Task<string> MoveFile(FileMigrationProcessUIRef uiRef)
        {
            FileInfo inputPathInfo = new FileInfo(this.inputPath!);
            FileInfo outputPathInfo = new FileInfo(this.outputPath!);

            string inputPathDir = Path.GetDirectoryName(inputPathInfo.FullName);
            string outputPathDir = Path.GetDirectoryName(outputPathInfo.FullName);

            if (!Directory.Exists(outputPathDir))
                Directory.CreateDirectory(outputPathDir!);

            // Update path display
            string inputFileBasePath = inputPathInfo.FullName.Substring(inputPathDir!.Length + 1);
            UpdateCountProcessed(uiRef, inputFileBasePath);

            if (this.IsSameOutputDrive)
            {
                // Logger.LogWriteLine($"[FileMigrationProcess::MoveFile()] Moving file in the same drive from: {inputPathInfo.FullName} to {outputPathInfo.FullName}", LogType.Default, true);
                inputPathInfo.MoveTo(outputPathInfo.FullName);
                UpdateSizeProcessed(uiRef, inputPathInfo.Length);
            }
            else
            {
                // Logger.LogWriteLine($"[FileMigrationProcess::MoveFile()] Moving file across different drives from: {inputPathInfo.FullName} to {outputPathInfo.FullName}", LogType.Default, true);
                await MoveWriteFile(uiRef, inputPathInfo, outputPathInfo, this.tokenSource == null ? default : this.tokenSource.Token);
            }

            return outputPathInfo.FullName;
        }

        private async Task<string> MoveDirectory(FileMigrationProcessUIRef uiRef)
        {
            DirectoryInfo inputPathInfo = new DirectoryInfo(this.inputPath!);
            if (!Directory.Exists(this.outputPath))
                Directory.CreateDirectory(this.outputPath!);

            DirectoryInfo outputPathInfo = new DirectoryInfo(this.outputPath);

            int parentInputPathLength = inputPathInfo.Parent!.FullName.Length + 1;
            string outputDirBaseNamePath = inputPathInfo.FullName.Substring(parentInputPathLength);
            string outputDirPath = Path.Combine(this.outputPath, outputDirBaseNamePath);

            await Parallel.ForEachAsync(
                inputPathInfo.EnumerateFiles("*", SearchOption.AllDirectories),
                new ParallelOptions
                {
                    CancellationToken = this.tokenSource?.Token ?? default,
                    MaxDegreeOfParallelism = LauncherConfig.AppCurrentThread
                },
                async (inputFileInfo, cancellationToken) =>
                {
                    int _parentInputPathLength = inputPathInfo.Parent!.FullName.Length + 1;
                    string inputFileBasePath = inputFileInfo!.FullName.Substring(_parentInputPathLength);

                    // Update path display
                    UpdateCountProcessed(uiRef, inputFileBasePath);

                    string outputTargetPath = Path.Combine(outputPathInfo.FullName, inputFileBasePath);
                    string outputTargetDirPath = Path.GetDirectoryName(outputTargetPath);

                    if (!Directory.Exists(outputTargetDirPath))
                        Directory.CreateDirectory(outputTargetDirPath!);

                    if (this.IsSameOutputDrive)
                    {
                        // Logger.LogWriteLine($"[FileMigrationProcess::MoveDirectory()] Moving directory content in the same drive from: {inputFileInfo.FullName} to {outputTargetPath}", LogType.Default, true);
                        inputFileInfo.MoveTo(outputTargetPath);
                        UpdateSizeProcessed(uiRef, inputFileInfo.Length);
                    }
                    else
                    {
                        // Logger.LogWriteLine($"[FileMigrationProcess::MoveDirectory()] Moving directory content across different drives from: {inputFileInfo.FullName} to {outputTargetPath}", LogType.Default, true);
                        FileInfo outputFileInfo = new FileInfo(outputTargetPath);
                        await MoveWriteFile(uiRef, inputFileInfo, outputFileInfo, cancellationToken);
                    }
                }).ConfigureAwait(false);

            inputPathInfo.Delete(true);
            return outputDirPath;
        }
    }
}
