using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Preset;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

namespace CollapseLauncher.Interfaces
{
    internal class ProgressBase<T1, T2> :
        GamePropertyBase<T1, T2> where T1 : Enum
                                 where T2 : IAssetIndexSummary
    {
        public ProgressBase(UIElement parentUI, IGameVersionCheck GameVersionManager, IGameSettings GameSettings, string gamePath, string gameRepoURL, string versionOverride)
            : base(parentUI, GameVersionManager, GameSettings, gamePath, gameRepoURL, versionOverride) => Init();

        public ProgressBase(UIElement parentUI, IGameVersionCheck GameVersionManager, string gamePath, string gameRepoURL, string versionOverride)
            : base(parentUI, GameVersionManager, gamePath, gameRepoURL, versionOverride) => Init();

        private void Init()
        {
            _status = new TotalPerfileStatus() { IsIncludePerFileIndicator = true };
            _progress = new TotalPerfileProgress();
            _stopwatch = Stopwatch.StartNew();
            _refreshStopwatch = Stopwatch.StartNew();
            _assetIndex = new List<T2>();
        }

        public event EventHandler<TotalPerfileProgress> ProgressChanged;
        public event EventHandler<TotalPerfileStatus> StatusChanged;

        protected TotalPerfileStatus _status;
        protected TotalPerfileProgress _progress;
        protected int _progressTotalCountCurrent;
        protected int _progressTotalCountFound;
        protected int _progressTotalCount;
        protected long _progressTotalSizeCurrent;
        protected long _progressTotalSizeFound;
        protected long _progressTotalSize;
        protected long _progressPerFileSizeCurrent;
        protected long _progressPerFileSize;

        // Extension for IGameInstallManager
        protected double _progressTotalReadCurrent;

        protected const int _refreshInterval = 100;

        #region ProgressEventHandlers - Fetch
        protected void _innerObject_ProgressAdapter(object sender, TotalPerfileProgress e) => ProgressChanged?.Invoke(sender, e);
        protected void _innerObject_StatusAdapter(object sender, TotalPerfileStatus e) => StatusChanged?.Invoke(sender, e);

        protected virtual void _httpClient_FetchAssetProgress(object sender, DownloadEvent e)
        {
            // Update fetch status
            _status.IsProgressPerFileIndetermined = false;
            _status.IsProgressTotalIndetermined = false;
            _status.ActivityPerFile = string.Format(Lang._GameRepairPage.PerProgressSubtitle3, ConverterTool.SummarizeSizeSimple(e.Speed));

            // Update fetch progress
            _progress.ProgressPerFilePercentage = e.ProgressPercentage;
            _progress.ProgressTotalDownload = e.SizeDownloaded;
            _progress.ProgressTotalSizeToDownload = e.SizeToBeDownloaded;
            _progress.ProgressTotalSpeed = e.Speed;
            _progress.ProgressTotalTimeLeft = e.TimeLeft;

            // Push status and progress update
            UpdateStatus();
            UpdateProgress();
        }
        #endregion

        #region ProgressEventHandlers - Repair
        protected virtual async void _httpClient_RepairAssetProgress(object sender, DownloadEvent e)
        {
            _progress.ProgressPerFilePercentage = e.ProgressPercentage;
            _progress.ProgressPerFileDownload = e.SizeDownloaded;
            _progress.ProgressPerFileSizeToDownload = e.SizeToBeDownloaded;
            _progress.ProgressTotalDownload = _progressTotalSizeCurrent;
            _progress.ProgressTotalSizeToDownload = _progressTotalSize;

            // Calculate speed
            long speed = (long)(_progressTotalSizeCurrent / _stopwatch.Elapsed.TotalSeconds);
            _progress.ProgressTotalSpeed = speed;
            _progress.ProgressTotalTimeLeft = TimeSpan.FromSeconds((_progressTotalSizeCurrent - _progressTotalSize) / ConverterTool.Unzeroed(speed));

            // Update current progress percentages
            _progress.ProgressTotalPercentage = _progressTotalSizeCurrent != 0 ?
                ConverterTool.GetPercentageNumber(_progressTotalSizeCurrent, _progressTotalSize) :
                0;

            if (e.State != DownloadState.Merging)
            {
                _progressTotalSizeCurrent += e.Read;
            }

            if (await CheckIfNeedRefreshStopwatch())
            {
                // Update current activity status
                _status.IsProgressTotalIndetermined = false;
                _status.IsProgressPerFileIndetermined = false;

                // Set time estimation string
                string timeLeftString = string.Format(Lang._Misc.TimeRemainHMSFormat, _progress.ProgressTotalTimeLeft);

                _status.ActivityPerFile = string.Format(Lang._Misc.Speed, ConverterTool.SummarizeSizeSimple(_progress.ProgressTotalSpeed));
                _status.ActivityTotal = string.Format(Lang._GameRepairPage.PerProgressSubtitle2, ConverterTool.SummarizeSizeSimple(_progressTotalSizeCurrent), ConverterTool.SummarizeSizeSimple(_progressTotalSize)) + $" | {timeLeftString}";

                // Trigger update
                UpdateAll();
            }
        }
        #endregion

        #region ProgressEventHandlers - Patch
        protected virtual async void RepairTypeActionPatching_ProgressChanged(object sender, BinaryPatchProgress e)
        {
            _progress.ProgressPerFilePercentage = e.ProgressPercentage;
            _progress.ProgressTotalSpeed = e.Speed;

            // Update current progress percentages
            _progress.ProgressTotalPercentage = _progressTotalSizeCurrent != 0 ?
                ConverterTool.GetPercentageNumber(_progressTotalSizeCurrent, _progressTotalSize) :
                0;

            if (await CheckIfNeedRefreshStopwatch())
            {
                // Update current activity status
                _status.IsProgressTotalIndetermined = false;
                _status.IsProgressPerFileIndetermined = false;
                _status.ActivityPerFile = string.Format(Lang._GameRepairPage.PerProgressSubtitle5, ConverterTool.SummarizeSizeSimple(_progress.ProgressTotalSpeed));
                _status.ActivityTotal = string.Format(Lang._GameRepairPage.PerProgressSubtitle2, ConverterTool.SummarizeSizeSimple(_progressTotalSizeCurrent), ConverterTool.SummarizeSizeSimple(_progressTotalSize));

                // Trigger update
                UpdateAll();
            }
        }
        #endregion

        #region BaseTools
        protected void TryUnassignReadOnlyFiles(string path)
        {
            // Iterate every files and set the read-only flag to false
            foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                FileInfo fileInfo = new FileInfo(file);
                if (fileInfo.IsReadOnly)
                    fileInfo.IsReadOnly = false;
            }
        }

        protected void TryDeleteReadOnlyFile(string path)
        {
            try
            {
                FileInfo file = new FileInfo(path);
                file.IsReadOnly = false;
                file.Delete();
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to delete file: {path}\r\n{ex}", LogType.Error, true);
            }
        }

        protected void MoveFolderContent(string SourcePath, string DestPath)
        {
            // Get the source folder path length + 1
            int DirLength = SourcePath.Length + 1;

            // Initializw paths and error status
            string destFilePath;
            string destFolderPath;
            bool ErrorOccured = false;

            // Enumerate files inside of source path
            foreach (string filePath in Directory.EnumerateFiles(SourcePath, "*", SearchOption.AllDirectories))
            {
                // Get the relative path of the file from source path
                ReadOnlySpan<char> relativePath = filePath.AsSpan().Slice(DirLength);
                // Get the absolute path for destination
                destFilePath = Path.Combine(DestPath, relativePath.ToString());
                // Get folder path for destination
                destFolderPath = Path.GetDirectoryName(destFilePath);

                // Create the destination folder if not exist
                if (!Directory.Exists(destFolderPath))
                    Directory.CreateDirectory(destFolderPath);

                try
                {
                    // Try moving the file
                    LogWriteLine($"Moving \"{relativePath.ToString()}\" to \"{destFolderPath}\"", LogType.Default, true);
                    FileInfo filePathInfo = new FileInfo(filePath);
                    filePathInfo.IsReadOnly = false;
                    filePathInfo.MoveTo(destFilePath, true);
                }
                catch (Exception ex)
                {
                    // If failed, flag ErrorOccured as true and skip to the next file 
                    LogWriteLine($"Error while moving \"{relativePath.ToString()}\" to \"{destFolderPath}\"\r\nException: {ex}", LogType.Error, true);
                    ErrorOccured = true;
                }
            }

            // If no error occurred, then delete the source folder
            if (!ErrorOccured)
                Directory.Delete(SourcePath, true);
        }

        protected virtual void ResetStatusAndProgress()
        {
            // Reset the cancellation token
            _token = new CancellationTokenSource();

            // Reset RepairAssetProperty list
            AssetEntry.Clear();

            // Reset status and progress properties
            ResetStatusAndProgressProperty();

            // Update the status and progress
            UpdateAll();
        }

        protected void ResetStatusAndProgressProperty()
        {
            // Reset cancellation token
            _token = new CancellationTokenSource();

            // Show the asset entry panel
            _status.IsAssetEntryPanelShow = false;

            // Reset all total activity status
            _status.ActivityStatus = Lang._GameRepairPage.StatusNone;
            _status.ActivityTotal = Lang._GameRepairPage.StatusNone;
            _status.IsProgressTotalIndetermined = false;

            // Reset all per-file activity status
            _status.ActivityPerFile = Lang._GameRepairPage.StatusNone;
            _status.IsProgressPerFileIndetermined = false;

            // Reset all status indicators
            _status.IsAssetEntryPanelShow = false;
            _status.IsCompleted = false;
            _status.IsCanceled = false;

            // Reset all total activity progress
            _progress.ProgressPerFilePercentage = 0;
            _progress.ProgressTotalPercentage = 0;
            _progress.ProgressTotalEntryCount = 0;
            _progress.ProgressTotalSpeed = 0;

            // Reset all inner counter
            _progressTotalCountCurrent = 0;
            _progressTotalCount = 0;
            _progressTotalSizeCurrent = 0;
            _progressTotalSize = 0;
            _progressPerFileSizeCurrent = 0;
            _progressPerFileSize = 0;
        }

        protected async Task<bool> TryRunExamineThrow(Task<bool> action)
        {
            try
            {
                // Define if the status is still running
                _status.IsRunning = true;

                // Run the task
                return await action;
            }
            catch (TaskCanceledException)
            {
                // If a cancellation was thrown, then set IsCanceled as true
                _status.IsCompleted = false;
                _status.IsCanceled = true;
                throw;
            }
            catch (OperationCanceledException)
            {
                // If a cancellation was thrown, then set IsCanceled as true
                _status.IsCompleted = false;
                _status.IsCanceled = true;
                throw;
            }
            catch (Exception)
            {
                // Except, if the other exception was thrown, then set both IsCompleted
                // and IsCanceled as false.
                _status.IsCompleted = false;
                _status.IsCanceled = false;
                throw;
            }
            finally
            {
                // Clear the _assetIndex after that
                if (!_status.IsCompleted)
                {
                    _assetIndex.Clear();
                }

                // Define that the status is not running
                _status.IsRunning = false;
            }
        }

        protected void SetFoundToTotalValue()
        {
            // Assign found count and size to total count and size
            _progressTotalCount = _progressTotalCountFound;
            _progressTotalSize = _progressTotalSizeFound;

            // Reset found count and size
            _progressTotalCountFound = 0;
            _progressTotalSizeFound = 0;
        }

        protected bool SummarizeStatusAndProgress(List<T2> assetIndex, string msgIfFound, string msgIfClear)
        {
            // Reset status and progress properties
            ResetStatusAndProgressProperty();

            // Assign found value to total value
            SetFoundToTotalValue();

            // Set check if broken asset is found or not
            bool IsBrokenFound = assetIndex.Count > 0;

            // Set status
            _status.IsAssetEntryPanelShow = IsBrokenFound;
            _status.IsCompleted = true;
            _status.IsCanceled = false;
            _status.ActivityStatus = IsBrokenFound ? msgIfFound : msgIfClear;

            // Update status and progress
            UpdateAll();

            // Return broken asset check
            return IsBrokenFound;
        }

        protected virtual bool IsArrayMatch(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target) => source.SequenceEqual(target);

        protected virtual async Task RunDownloadTask(long assetSize, string assetPath, string assetURL, Http _httpClient, CancellationToken token)
        {
            // Check for directory availability
            if (!Directory.Exists(Path.GetDirectoryName(assetPath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            }

            // Start downloading asset
            if (assetSize >= _sizeForMultiDownload)
            {
                await _httpClient.Download(assetURL, assetPath, _downloadThreadCount, true, token);
                await _httpClient.Merge();
            }
            else
            {
                await _httpClient.Download(assetURL, assetPath, true, null, null, token);
            }
        }
        #endregion

        #region HashTools
        protected virtual byte[] CheckHash(Stream stream, HashAlgorithm hashProvider, CancellationToken token, bool updateTotalProgress = true)
        {
            // Initialize MD5 instance and assign buffer
            byte[] buffer = new byte[_bufferBigLength];

            // Do read activity
            int read;
            while ((read = stream.Read(buffer)) > 0)
            {
                // Throw Cancellation exception if detected
                token.ThrowIfCancellationRequested();

                // Append buffer into hash block
                hashProvider.TransformBlock(buffer, 0, read, buffer, 0);

                lock (this)
                {
                    // Increment total size counter
                    if (updateTotalProgress) { _progressTotalSizeCurrent += read; }
                    // Increment per file size counter
                    _progressPerFileSizeCurrent += read;
                }

                // Update status and progress for MD5 calculation
                UpdateProgressCRC();
            }

            // Finalize the hash calculation
            hashProvider.TransformFinalBlock(buffer, 0, read);

            // Return computed hash byte
            return hashProvider.Hash;
        }
        #endregion

        #region PatchTools
        protected virtual async ValueTask RunPatchTask(Http _httpClient, CancellationToken token, long patchSize, Memory<byte> patchHash,
            string patchURL, string patchOutputFile, string inputFile, string outputFile)
        {
            // Get info about patch file
            FileInfo patchInfo = new FileInfo(patchOutputFile);

            // If file doesn't exist, then download the patch first
            if (!patchInfo.Exists || patchInfo.Length != patchSize)
            {
                // Download patch File first
                await RunDownloadTask(patchSize, patchOutputFile, patchURL, _httpClient, token);
            }

            // Always do loop if patch doesn't get downloaded properly
            while (true)
            {
                using (FileStream patchfs = new FileStream(patchOutputFile, FileMode.Open, FileAccess.Read, FileShare.None, _bufferBigLength))
                {
                    // Verify the patch file and if it doesn't match, then redownload it
                    byte[] patchCRC = await Task.Run(() => CheckHash(patchfs, MD5.Create(), token, false)).ConfigureAwait(false);
                    if (!IsArrayMatch(patchCRC, patchHash.Span))
                    {
                        // Revert back the total size
                        _progressTotalSizeCurrent -= patchSize;

                        // Redownload the patch file
                        await RunDownloadTask(patchSize, patchOutputFile, patchURL, _httpClient, token);
                        continue;
                    }
                }

                // else, break and quit from loop
                break;
            }

            // Start patching process
            BinaryPatchUtility patchUtil = new BinaryPatchUtility();
            try
            {
                // Subscribe patching progress and start applying patch
                patchUtil.ProgressChanged += RepairTypeActionPatching_ProgressChanged;
                patchUtil.Initialize(inputFile, patchOutputFile, outputFile);
                await Task.Run(() => patchUtil.Apply(token)).ConfigureAwait(false);

                // Delete old block
                File.Delete(inputFile);
                // Rename to the original filename
                File.Move(outputFile, inputFile, true);
            }
            catch { throw; }
            finally
            {
                // Delete the patch file and unsubscribe the patching progress
                FileInfo fileInfo = new FileInfo(patchOutputFile);
                if (fileInfo.Exists)
                {
                    fileInfo.IsReadOnly = false;
                    fileInfo.Delete();
                }
                patchUtil.ProgressChanged -= RepairTypeActionPatching_ProgressChanged;
            }
        }
        #endregion

        #region DialogTools
        protected async Task SpawnRepairDialog(List<T2> assetIndex, Action actionIfInteractiveCancel)
        {
            long totalSize = assetIndex.Sum(x => x.GetAssetSize());
            StackPanel Content = new StackPanel();
            Button ShowBrokenFilesButton = new Button()
            {
                Content = Lang._InstallMgmt.RepairFilesRequiredShowFilesBtn,
                Style = Application.Current.Resources["AccentButtonStyle"] as Style,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            ShowBrokenFilesButton.Click += (async (a, b) =>
            {
                string tempPath = Path.GetTempFileName() + ".log";

                using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                {
                    using (StreamWriter sw = new StreamWriter(fs))
                    {
                        sw.WriteLine($"Original Path: {_gamePath}");
                        sw.WriteLine($"Total size to download: {ConverterTool.SummarizeSizeSimple(totalSize)} ({totalSize} bytes)");
                        sw.WriteLine();

                        foreach (T2 fileList in assetIndex)
                        {
                            sw.WriteLine(fileList.PrintSummary());
                        }
                    }
                }

                Process proc = new Process()
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = tempPath,
                        UseShellExecute = true
                    }
                };
                proc.Start();
                await proc.WaitForExitAsync();

                try
                {
                    File.Delete(tempPath);
                }
                catch { }
            });

            Content.Children.Add(new TextBlock()
            {
                Text = string.Format(Lang._InstallMgmt.RepairFilesRequiredSubtitle, assetIndex.Count, ConverterTool.SummarizeSizeSimple(totalSize)),
                Margin = new Thickness(0, 0, 0, 16),
                TextWrapping = TextWrapping.Wrap
            });
            Content.Children.Add(ShowBrokenFilesButton);

            ContentDialog dialog1 = new ContentDialog
            {
                Title = string.Format(Lang._InstallMgmt.RepairFilesRequiredTitle, assetIndex.Count),
                Content = Content,
                CloseButtonText = Lang._Misc.Cancel,
                PrimaryButtonText = Lang._Misc.YesResume,
                SecondaryButtonText = null,
                DefaultButton = ContentDialogButton.Primary,
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["DialogAcrylicBrush"],
                XamlRoot = _parentUI.XamlRoot
            };

            if (totalSize == 0) return;

            if (await dialog1.ShowAsync() == ContentDialogResult.None)
            {
                if (actionIfInteractiveCancel != null)
                {
                    actionIfInteractiveCancel();
                }
                throw new OperationCanceledException();
            }
        }
        #endregion

        #region HandlerUpdaters
        public void Dispatch(DispatcherQueueHandler handler) => _parentUI.DispatcherQueue.TryEnqueue(handler);

        protected virtual void PopRepairAssetEntry() => Dispatch(() =>
        {
            try
            {
                AssetEntry.RemoveAt(0);
            }
            catch { }
        });

        protected virtual async void UpdateProgressCRC()
        {
            if (await CheckIfNeedRefreshStopwatch())
            {
                // Update current progress percentages
                _progress.ProgressPerFilePercentage = _progressPerFileSizeCurrent != 0 ?
                    ConverterTool.GetPercentageNumber(_progressPerFileSizeCurrent, _progressPerFileSize) :
                    0;
                _progress.ProgressTotalPercentage = _progressTotalSizeCurrent != 0 ?
                    ConverterTool.GetPercentageNumber(_progressTotalSizeCurrent, _progressTotalSize) :
                    0;

                // Update the progress of total size
                _progress.ProgressPerFileDownload = _progressPerFileSizeCurrent;
                _progress.ProgressPerFileSizeToDownload = _progressPerFileSize;
                _progress.ProgressTotalDownload = _progressTotalSizeCurrent;
                _progress.ProgressTotalSizeToDownload = _progressTotalSize;

                // Calculate current speed and update the status and progress speed
                _progress.ProgressTotalSpeed = _progressTotalSizeCurrent / _stopwatch.Elapsed.TotalSeconds;
                _status.ActivityPerFile = string.Format(Lang._Misc.Speed, ConverterTool.SummarizeSizeSimple(_progress.ProgressTotalSpeed));

                // Calculate the timelapse
                _progress.ProgressTotalTimeLeft = TimeSpan.FromSeconds((_progressTotalSize - _progressTotalSizeCurrent) / ConverterTool.Unzeroed(_progress.ProgressTotalSpeed));

                // Set time estimation string
                string timeLeftString = string.Format(Lang._Misc.TimeRemainHMSFormat, _progress.ProgressTotalTimeLeft);

                // Update current activity status
                _status.ActivityTotal = string.Format(Lang._GameRepairPage.PerProgressSubtitle2, ConverterTool.SummarizeSizeSimple(_progressTotalSizeCurrent), ConverterTool.SummarizeSizeSimple(_progressTotalSize)) + $" | {timeLeftString}";

                // Trigger update
                UpdateAll();
            }
        }

        protected virtual void UpdateRepairStatus(string activityStatus, string activityTotal, bool isPerFileIndetermined)
        {
            // Set repair activity status
            _status.ActivityStatus = activityStatus;
            _status.ActivityTotal = activityTotal;
            _status.IsProgressPerFileIndetermined = isPerFileIndetermined;

            // Update status
            UpdateStatus();
        }

        protected async Task<bool> CheckIfNeedRefreshStopwatch()
        {
            if (_refreshStopwatch.ElapsedMilliseconds > _refreshInterval)
            {
                _refreshStopwatch.Restart();
                return true;
            }

            await Task.Delay(_refreshInterval);
            return false;
        }
        protected void UpdateAll()
        {
            UpdateStatus();
            UpdateProgress();
        }
        protected virtual void UpdateProgress() => ProgressChanged?.Invoke(this, _progress);
        protected virtual void UpdateStatus() => StatusChanged?.Invoke(this, _status);
        protected virtual void RestartStopwatch() => _stopwatch.Restart();
        #endregion
    }
}
