﻿using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CommunityToolkit.WinUI;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Preset;
using Hi3Helper.Shared.Region;
using Hi3Helper.Sophon;
using Hi3Helper.Sophon.Helper;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Hashing;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.SentryHelper;
using Hi3Helper.Win32.TaskbarListCOM;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using CollapseUIExtension = CollapseLauncher.Extension.UIElementExtensions;
// ReSharper disable InconsistentlySynchronizedField

#nullable enable
namespace CollapseLauncher.Interfaces
{
    internal class ProgressBase<T1> : GamePropertyBase<T1> where T1 : IAssetIndexSummary
    {
        public ProgressBase(UIElement parentUI, IGameVersionCheck? gameVersionManager, IGameSettings? gameSettings, string? gamePath, string? gameRepoURL, string? versionOverride)
            : base(parentUI, gameVersionManager, gameSettings, gamePath, gameRepoURL, versionOverride)
        {
            _status = new TotalPerFileStatus() { IsIncludePerFileIndicator = true };
            _progress = new TotalPerFileProgress();
            _sophonStatus = new TotalPerFileStatus() { IsIncludePerFileIndicator = true };
            _sophonProgress = new TotalPerFileProgress();
            _assetIndex = new List<T1>();
        }

        public ProgressBase(UIElement parentUI, IGameVersionCheck? gameVersionManager, string? gamePath, string? gameRepoURL, string? versionOverride)
            : this(parentUI, gameVersionManager, null, gamePath, gameRepoURL, versionOverride) { }

        public event EventHandler<TotalPerFileProgress>? ProgressChanged;
        public event EventHandler<TotalPerFileStatus>?   StatusChanged;

        protected TotalPerFileStatus    _sophonStatus;
        protected TotalPerFileProgress  _sophonProgress;
        protected TotalPerFileStatus    _status;
        protected TotalPerFileProgress  _progress;
        protected int                   _progressAllCountCurrent;
        protected int                   _progressAllCountFound;
        protected int                   _progressAllCountTotal;
        protected long                  _progressAllSizeCurrent;
        protected long                  _progressAllSizeFound;
        protected long                  _progressAllSizeTotal;
        protected long                  _progressPerFileSizeCurrent;
        protected long                  _progressPerFileSizeTotal;

        // Extension for IGameInstallManager

        protected const int _downloadSpeedRefreshInterval = 5000;
        protected const int _refreshInterval = 100;

        protected bool _isSophonInUpdateMode { get; set; }

        #region ProgressEventHandlers - Fetch
        protected void _innerObject_ProgressAdapter(object? sender, TotalPerFileProgress e) => ProgressChanged?.Invoke(sender, e);
        protected void _innerObject_StatusAdapter(object? sender, TotalPerFileStatus e) => StatusChanged?.Invoke(sender, e);

        protected virtual void _httpClient_FetchAssetProgress(int size, DownloadProgress downloadProgress)
        {
            // Calculate the speed
            double speedAll = CalculateSpeed(size);

            if (CheckIfNeedRefreshStopwatch())
            {
                // Calculate the clamped speed and timelapse
                double speedClamped = speedAll.ClampLimitedSpeedNumber();

                TimeSpan timeLeftSpan = ConverterTool.ToTimeSpanRemain(downloadProgress.BytesTotal, downloadProgress.BytesDownloaded, speedClamped);
                double percentage = ConverterTool.ToPercentage(downloadProgress.BytesTotal, downloadProgress.BytesDownloaded);

                lock (_status)
                {
                    // Update fetch status
                    _status.IsProgressPerFileIndetermined = false;
                    _status.IsProgressAllIndetermined     = false;
                    _status.ActivityPerFile               = string.Format(Lang!._GameRepairPage!.PerProgressSubtitle3!, ConverterTool.SummarizeSizeSimple(speedClamped));
                }

                lock (_progress)
                {
                    // Update fetch progress
                    _progress.ProgressPerFilePercentage = percentage;
                    _progress.ProgressAllSizeCurrent    = downloadProgress.BytesDownloaded;
                    _progress.ProgressAllSizeTotal      = downloadProgress.BytesTotal;
                    _progress.ProgressAllSpeed          = speedClamped;
                    _progress.ProgressAllTimeLeft       = timeLeftSpan;
                }

                // Push status and progress update
                UpdateStatus();
                UpdateProgress();
            }
        }

        #endregion

        #region ProgressEventHandlers - Repair
        protected virtual void _httpClient_RepairAssetProgress(int size, DownloadProgress downloadProgress)
        {
            Interlocked.Add(ref _progressAllSizeCurrent, size);

            // Calculate the speed
            double speedAll = CalculateSpeed(size);

            if (CheckIfNeedRefreshStopwatch())
            {
                // Calculate the clamped speed and timelapse
                double speedClamped = speedAll.ClampLimitedSpeedNumber();

                TimeSpan timeLeftSpan    = ConverterTool.ToTimeSpanRemain(_progressAllSizeTotal, _progressAllSizeCurrent, speedClamped);
                double percentagePerFile = ConverterTool.ToPercentage(downloadProgress.BytesTotal, downloadProgress.BytesDownloaded);

                lock (_progress)
                {
                    _progress.ProgressPerFilePercentage  = percentagePerFile;
                    _progress.ProgressPerFileSizeCurrent = downloadProgress.BytesDownloaded;
                    _progress.ProgressPerFileSizeTotal   = downloadProgress.BytesTotal;
                    _progress.ProgressAllSizeCurrent     = _progressAllSizeCurrent;
                    _progress.ProgressAllSizeTotal       = _progressAllSizeTotal;

                    // Calculate speed
                    _progress.ProgressAllSpeed    = speedClamped;
                    _progress.ProgressAllTimeLeft = timeLeftSpan;

                    // Update current progress percentages
                    _progress.ProgressAllPercentage = _progressAllSizeCurrent != 0 ?
                        ConverterTool.ToPercentage(_progressAllSizeTotal, _progressAllSizeCurrent) :
                        0;
                }

                lock (_status)
                {
                    // Update current activity status
                    _status.IsProgressAllIndetermined     = false;
                    _status.IsProgressPerFileIndetermined = false;

                    // Set time estimation string
                    string timeLeftString = string.Format(Lang!._Misc!.TimeRemainHMSFormat!, _progress.ProgressAllTimeLeft);

                    _status.ActivityPerFile = string.Format(Lang._Misc.Speed!, ConverterTool.SummarizeSizeSimple(_progress.ProgressAllSpeed));
                    _status.ActivityAll     = string.Format(Lang._GameRepairPage!.PerProgressSubtitle2!,
                                                ConverterTool.SummarizeSizeSimple(_progressAllSizeCurrent),
                                                ConverterTool.SummarizeSizeSimple(_progressAllSizeTotal)) + $" | {timeLeftString}";

                    // Trigger update
                    UpdateAll();
                }
            }
        }

        protected virtual void UpdateRepairStatus(string activityStatus, string activityAll, bool isPerFileIndetermined)
        {
            lock (_status)
            {
                // Set repair activity status
                _status.ActivityStatus = activityStatus;
                _status.ActivityAll = activityAll;
                _status.IsProgressPerFileIndetermined = isPerFileIndetermined;
            }

            // Update status
            UpdateStatus();
        }
        #endregion

        #region ProgressEventHandlers - UpdateCache
        protected virtual void _httpClient_UpdateAssetProgress(int size, DownloadProgress downloadProgress)
        {
            Interlocked.Add(ref _progressAllSizeCurrent, size);

            // Calculate the speed
            double speedAll = CalculateSpeed(size);

            if (CheckIfNeedRefreshStopwatch())
            {
                // Calculate the clamped speed and timelapse
                double speedClamped   = speedAll.ClampLimitedSpeedNumber();
                TimeSpan timeLeftSpan = ConverterTool.ToTimeSpanRemain(_progressAllSizeTotal, _progressAllSizeCurrent, speedClamped);
                double percentage     = ConverterTool.ToPercentage(_progressAllSizeTotal, _progressAllSizeCurrent);

                // Update current progress percentages and speed
                lock (_progress)
                {
                    _progress.ProgressAllPercentage = percentage;
                }

                // Update current activity status
                _status.IsProgressAllIndetermined = false;
                string timeLeftString             = string.Format(Lang._Misc.TimeRemainHMSFormat, timeLeftSpan);
                _status.ActivityAll               = string.Format(Lang._Misc.Downloading + ": {0}/{1} ", _progressAllCountCurrent,
                                                                _progressAllCountTotal)
                                                  + string.Format($"({Lang._Misc.SpeedPerSec})",
                                                                ConverterTool.SummarizeSizeSimple(speedClamped))
                                                  + $" | {timeLeftString}";

                // Trigger update
                UpdateAll();
            }
        }
        #endregion

        #region ProgressEventHandlers - Patch
        protected virtual void RepairTypeActionPatching_ProgressChanged(object? sender, BinaryPatchProgress e)
        {
            lock (_progress)
            {
                _progress.ProgressPerFilePercentage = e.ProgressPercentage;
                _progress.ProgressAllSpeed = e.Speed;

                // Update current progress percentages
                _progress.ProgressAllPercentage = _progressAllSizeCurrent != 0 ?
                    ConverterTool.ToPercentage(_progressAllSizeTotal, _progressAllSizeCurrent) :
                    0;
            }

            if (CheckIfNeedRefreshStopwatch())
            {
                lock (_status)
                {
                    // Update current activity status
                    _status.IsProgressAllIndetermined     = false;
                    _status.IsProgressPerFileIndetermined = false;
                    _status.ActivityPerFile               = string.Format(Lang!._GameRepairPage!.PerProgressSubtitle5!,
                        ConverterTool.SummarizeSizeSimple(_progress.ProgressAllSpeed));
                    _status.ActivityAll                   = string.Format(Lang._GameRepairPage.PerProgressSubtitle2!,
                        ConverterTool.SummarizeSizeSimple(_progressAllSizeCurrent),
                        ConverterTool.SummarizeSizeSimple(_progressAllSizeTotal));
                }

                // Trigger update
                UpdateAll();
            }
        }
        #endregion

        #region ProgressEventHandlers - CRC/HashCheck
        protected virtual void UpdateProgressCRC(long read)
        {
            // Calculate speed
            double speedAll = CalculateSpeed(read);

            if (!CheckIfNeedRefreshStopwatch())
            {
                return;
            }

            lock (_progress)
            {
                // Update current progress percentages
                _progress.ProgressPerFilePercentage = _progressPerFileSizeCurrent != 0 ?
                    ConverterTool.ToPercentage(_progressPerFileSizeTotal, _progressPerFileSizeCurrent) :
                    0;
                _progress.ProgressAllPercentage = _progressAllSizeCurrent != 0 ?
                    ConverterTool.ToPercentage(_progressAllSizeTotal, _progressAllSizeCurrent) :
                    0;

                // Update the progress of total size
                _progress.ProgressPerFileSizeCurrent = _progressPerFileSizeCurrent;
                _progress.ProgressPerFileSizeTotal   = _progressPerFileSizeTotal;
                _progress.ProgressAllSizeCurrent     = _progressAllSizeCurrent;
                _progress.ProgressAllSizeTotal       = _progressAllSizeTotal;

                // Calculate current speed and update the status and progress speed
                _progress.ProgressAllSpeed = speedAll;

                // Calculate the timelapse
                _progress.ProgressAllTimeLeft = ConverterTool.ToTimeSpanRemain(_progressAllSizeTotal, _progressAllSizeCurrent, speedAll);
            }

            lock (_status)
            {
                // Set time estimation string
                string timeLeftString = string.Format(Lang!._Misc!.TimeRemainHMSFormat!, _progress.ProgressAllTimeLeft);

                // Update current activity status
                _status.ActivityPerFile = string.Format(Lang._Misc.Speed!, ConverterTool.SummarizeSizeSimple(_progress.ProgressAllSpeed));
                _status.ActivityAll = string.Format(Lang._GameRepairPage!.PerProgressSubtitle2!, 
                                                    ConverterTool.SummarizeSizeSimple(_progressAllSizeCurrent), 
                                                    ConverterTool.SummarizeSizeSimple(_progressAllSizeTotal)) + $" | {timeLeftString}";
            }

            // Trigger update
            UpdateAll();
        }
        #endregion

        #region ProgressEventHandlers - DoCopyStreamProgress
        protected virtual void UpdateProgressCopyStream(long currentPosition, int read, long totalReadSize)
        {
            // Calculate the speed
            double speedAll = CalculateSpeed(read);

            if (CheckIfNeedRefreshStopwatch())
            {
                lock (_progress)
                {
                    // Update current progress percentages
                    _progress.ProgressPerFilePercentage = ConverterTool.ToPercentage(totalReadSize, currentPosition);

                    // Update the progress of total size
                    _progress.ProgressPerFileSizeCurrent = currentPosition;
                    _progress.ProgressPerFileSizeTotal   = totalReadSize;

                    // Calculate current speed and update the status and progress speed
                    _progress.ProgressAllSpeed = speedAll;

                    // Calculate the timelapse
                    _progress.ProgressAllTimeLeft = ConverterTool.ToTimeSpanRemain(totalReadSize, currentPosition, speedAll);
                }

                lock (_status)
                {
                    // Set time estimation string
                    string timeLeftString = string.Format(Lang!._Misc!.TimeRemainHMSFormat!, _progress.ProgressAllTimeLeft);

                    // Update current activity status
                    _status.ActivityPerFile = string.Format(Lang._Misc.Speed!, ConverterTool.SummarizeSizeSimple(_progress.ProgressAllSpeed));
                    _status.ActivityAll     = string.Format(Lang._GameRepairPage!.PerProgressSubtitle2!, 
                                                          ConverterTool.SummarizeSizeSimple(currentPosition), 
                                                          ConverterTool.SummarizeSizeSimple(totalReadSize)) + $" | {timeLeftString}";
                }

                // Trigger update
                UpdateAll();
            }
        }
        #endregion

        #region ProgressEventHandlers - SpeedCalculator and Refresh Interval Checker
        private const double _scOneSecond = 1000;
        private long _scLastTick = Environment.TickCount64;
        private long _scLastReceivedBytes;
        private double _scLastSpeed;

        protected double CalculateSpeed(long receivedBytes) => CalculateSpeed(receivedBytes, ref _scLastSpeed, ref _scLastReceivedBytes, ref _scLastTick);

        protected double CalculateSpeed(long receivedBytes, ref double lastSpeedToUse, ref long lastReceivedBytesToUse, ref long lastTickToUse)
        {
            long   currentTick           = Environment.TickCount64 - lastTickToUse + 1;
            long   totalReceivedInSecond = Interlocked.Add(ref lastReceivedBytesToUse, receivedBytes);
            double speed                 = totalReceivedInSecond * _scOneSecond / currentTick;

            if (!(currentTick > _scOneSecond))
            {
                return lastSpeedToUse;
            }

            lastSpeedToUse = speed;
            _ = Interlocked.Exchange(ref lastSpeedToUse,         speed);
            _ = Interlocked.Exchange(ref lastReceivedBytesToUse, 0);
            _ = Interlocked.Exchange(ref lastTickToUse,          Environment.TickCount64);
            return lastSpeedToUse;
        }

        private int _riLastTick = Environment.TickCount;

        protected bool CheckIfNeedRefreshStopwatch()
        {
            int currentTick = Environment.TickCount - _riLastTick;
            if (currentTick <= _refreshInterval)
            {
                return false;
            }

            Interlocked.Exchange(ref _riLastTick, Environment.TickCount);
            return true;

        }
        #endregion

        #region ProgressEventHandlers - SophonInstaller
        private double _sophonDownloadOnlySpeed = 1;
        private double _sophonDownloadOnlyLastSpeed;
        private long   _sophonDownloadOnlyReceivedBytes;
        private long   _sophonDownloadOnlyLastTick = Environment.TickCount64;

        private long   _sophonDownloadOnlyCurrentDownloadedBytes;
        private long   _sophonDownloadOnlyLastDownloadedBytes;

        protected void UpdateSophonFileTotalProgress(long read)
        {
            _ = Interlocked.Add(ref _progressAllSizeCurrent, read);

            // Calculate the speed
            double speedAll = CalculateSpeed(read);

            // Get last received bytes from download
            long lastReceivedDownloadBytes = _sophonDownloadOnlyCurrentDownloadedBytes - _sophonDownloadOnlyLastDownloadedBytes;
            _ = Interlocked.Exchange(ref _sophonDownloadOnlyLastDownloadedBytes, _sophonDownloadOnlyCurrentDownloadedBytes);

            // Calculate the speed for download (just use it for update only by setting receivedBytes to 0)
            _sophonDownloadOnlySpeed = CalculateSpeed(lastReceivedDownloadBytes, ref _sophonDownloadOnlyLastSpeed, ref _sophonDownloadOnlyReceivedBytes, ref _sophonDownloadOnlyLastTick);

            // Calculate the clamped speed for download and timelapse
            double speedDownloadClamped = _sophonDownloadOnlySpeed.ClampLimitedSpeedNumber();

            if (!CheckIfNeedRefreshStopwatch())
            {
                return;
            }

            // Assign local sizes to progress
            _sophonProgress.ProgressAllSizeCurrent     = _progressAllSizeCurrent;
            _sophonProgress.ProgressAllSizeTotal       = _progressAllSizeTotal;
            _sophonProgress.ProgressPerFileSizeCurrent = _progressPerFileSizeCurrent;
            _sophonProgress.ProgressPerFileSizeTotal   = _progressPerFileSizeTotal;

            _sophonProgress.ProgressAllSpeed     = speedAll;
            _sophonProgress.ProgressPerFileSpeed = speedDownloadClamped;

            // Calculate Count
            _sophonProgress.ProgressAllEntryCountCurrent = _progressAllCountCurrent;
            _sophonProgress.ProgressAllEntryCountTotal   = _progressAllCountTotal;

            // Always change the status progress to determined
            _status.IsProgressAllIndetermined     = false;
            _status.IsProgressPerFileIndetermined = false;
            StatusChanged?.Invoke(this, _status);

            // Calculate percentage
            _sophonProgress.ProgressAllPercentage     = ConverterTool.ToPercentage(_progressAllSizeTotal, _progressAllSizeCurrent);
            _sophonProgress.ProgressPerFilePercentage = ConverterTool.ToPercentage(_progressPerFileSizeTotal, _progressPerFileSizeCurrent);
            _sophonProgress.ProgressAllTimeLeft       = ConverterTool.ToTimeSpanRemain(_progressAllSizeTotal, _progressAllSizeCurrent, speedAll);

            // Update progress
            ProgressChanged?.Invoke(this, _sophonProgress);

            // Update taskbar progress
            if (_status.IsProgressAllIndetermined)
            {
                WindowUtility.SetTaskBarState(TaskbarState.Indeterminate);
            }
            else if (_status.IsCompleted || _status.IsCanceled)
            {
                WindowUtility.SetTaskBarState(TaskbarState.NoProgress);
            }
            else
            {
                WindowUtility.SetTaskBarState(TaskbarState.Normal);
                WindowUtility.SetProgressValue((ulong)(_sophonProgress.ProgressAllPercentage * 10), 1000);
            }
        }

        protected void UpdateSophonFileDownloadProgress(long downloadedWrite, long currentWrite)
        {
            _ = Interlocked.Add(ref _progressPerFileSizeCurrent,               downloadedWrite);
            _ = Interlocked.Add(ref _sophonDownloadOnlyCurrentDownloadedBytes, currentWrite);
        }

        protected void UpdateSophonDownloadStatus(SophonAsset asset)
        {
            Interlocked.Add(ref _progressAllCountCurrent, 1);
            _status.ActivityStatus = string.Format("{0}: {1}",
                                                   _isSophonInUpdateMode
                                                       ? Lang._Misc.Updating
                                                       : Lang._Misc.Downloading,
                                                   string.Format(Lang._Misc.PerFromTo, _progressAllCountCurrent,
                                                                 _progressAllCountTotal));

            UpdateStatus();
        }

        protected void UpdateSophonLogHandler(object? sender, LogStruct e)
        {
#if !DEBUG
            if (e.LogLevel == LogLevel.Debug) return;
#endif
            (bool isNeedWriteLog, LogType logType) logPair = e.LogLevel switch
            {
                LogLevel.Warning => (true, LogType.Warning),
                LogLevel.Debug => (true, LogType.Debug),
                LogLevel.Error => (true, LogType.Error),
                _ => (true, LogType.Default)
            };
            LogWriteLine(e.Message, logPair.logType, logPair.isNeedWriteLog);
        }
        #endregion

        #region BaseTools
        protected async Task DoCopyStreamProgress(Stream source, Stream target, CancellationToken token = default, long? estimatedSize = null)
        {
            int read;
            // ReSharper disable once ConstantNullCoalescingCondition
            long inputSize = estimatedSize != null ? estimatedSize ?? 0 : source.Length;
            long currentPos = 0;

            bool isLastPerfileStateIndetermined = _status.IsProgressPerFileIndetermined;
            bool isLastTotalStateIndetermined = _status.IsProgressAllIndetermined;

            _status.IsProgressPerFileIndetermined = false;
            _status.IsProgressAllIndetermined = true;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(16 << 10);
            try
            {
                while ((read = await source.ReadAsync(buffer, token)) > 0)
                {
                    await target.WriteAsync(buffer, 0, read, token);
                    currentPos += read;
                    UpdateProgressCopyStream(currentPos, read, inputSize);
                }
            }
            finally
            {
                _status.IsProgressPerFileIndetermined = isLastPerfileStateIndetermined;
                _status.IsProgressAllIndetermined = isLastTotalStateIndetermined;
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        protected string EnsureCreationOfDirectory(string str)
            => StreamUtility.EnsureCreationOfDirectory(str).FullName;

        protected string EnsureCreationOfDirectory(FileInfo str)
            => str.EnsureCreationOfDirectory().FullName;

        protected void TryUnassignReadOnlyFiles(string dirPath)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(dirPath);
            if (!directoryInfo.Exists)
            {
                return;
            }

            // Iterate every file and set the read-only flag to false
            foreach (FileInfo file in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                _ = file.EnsureNoReadOnly();
            }
        }

        protected void TryUnassignReadOnlyFileSingle(string filePath)
        {
            FileInfo fileInfo = new FileInfo(filePath);
            _ = fileInfo.EnsureNoReadOnly();
        }

        protected void TryDeleteReadOnlyDir(string dirPath)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(dirPath);
            if (!dirInfo.Exists)
            {
                return;
            }

            foreach (FileInfo files in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                .EnumerateNoReadOnly())
            {
                try
                {
                    files.Delete();
                }
                catch (Exception ex)
                {
                    SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                    LogWriteLine($"Failed while deleting file: {files.FullName}\r\n{ex}", LogType.Warning, true);
                } // Suppress errors
            }

            try
            {
                dirInfo.Refresh();
                dirInfo.Delete(true);
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Failed while deleting parent dir: {dirPath}\r\n{ex}", LogType.Warning, true);
            } // Suppress errors
        }

        protected void TryDeleteReadOnlyFile(string filePath)
        {
            FileInfo fileInfo = new FileInfo(filePath)
                .EnsureNoReadOnly(out bool isFileExist);
            if (!isFileExist) return;

            try
            {
                fileInfo.Delete();
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Failed to delete file: {fileInfo.FullName}\r\n{ex}", LogType.Error, true);
            }
        }

        protected void MoveFolderContent(string sourcePath, string destPath)
        {
            // Get the source folder path length + 1
            int dirLength = sourcePath.Length + 1;

            // Initialize paths and error status
            string  destFilePath;
            string? destFolderPath;
            bool    errorOccured = false;

            // Enumerate files inside of source path
            DirectoryInfo directoryInfoSource = new DirectoryInfo(sourcePath);
            if (!directoryInfoSource.Exists)
            {
                throw new DirectoryNotFoundException($"Cannot find source directory on this path: {sourcePath}");
            }
            foreach (FileInfo fileInfo in directoryInfoSource.EnumerateFiles("*", SearchOption.AllDirectories)
                .EnumerateNoReadOnly())
            {
                // Get the relative path of the file from source path
                ReadOnlySpan<char> relativePath = fileInfo.FullName.AsSpan().Slice(dirLength);
                // Get the absolute path for destination
                destFilePath = Path.Combine(destPath, relativePath.ToString());
                // Get folder path for destination
                destFolderPath = Path.GetDirectoryName(destFilePath);

                // Create the destination folder if not exist
                if (!string.IsNullOrEmpty(destFolderPath))
                    _ = Directory.CreateDirectory(destFolderPath);

                try
                {
                    // Try moving the file
                    LogWriteLine($"Moving \"{relativePath.ToString()}\" to \"{destFolderPath}\"", LogType.Default, true);
                    fileInfo.MoveTo(destFilePath, true);
                }
                catch (Exception ex)
                {
                    SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                    // If failed, flag ErrorOccured as true and skip to the next file 
                    LogWriteLine($"Error while moving \"{relativePath.ToString()}\" to \"{destFolderPath}\"\r\nException: {ex}", LogType.Error, true);
                    errorOccured = true;
                }
            }

            // If no error occurred, then delete the source folder
            if (!errorOccured)
            {
                try
                {
                    directoryInfoSource.Refresh();
                    directoryInfoSource.Delete(true);
                }
                catch (Exception ex)
                {
                    SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                    // If failed, flag ErrorOccured as true and skip the source directory deletion 
                    LogWriteLine($"Error while deleting source directory \"{directoryInfoSource.FullName}\"\r\nException: {ex}", LogType.Error, true);
                }
            }
        }

        protected virtual void ResetStatusAndProgress()
        {
            // Reset the cancellation token
            _token = new CancellationTokenSourceWrapper();

            // Reset RepairAssetProperty list
            AssetEntry!.Clear();

            // Reset status and progress properties
            ResetStatusAndProgressProperty();

            // Update the status and progress
            UpdateAll();
        }
        
        protected void ResetStatusAndProgressProperty()
        {
            // Reset cancellation token
            _token = new CancellationTokenSourceWrapper();

            lock (_status)
            {
                // Show the asset entry panel
                _status.IsAssetEntryPanelShow = false;

                // Reset all total activity status
                _status.ActivityStatus            = Lang!._GameRepairPage!.StatusNone;
                _status.ActivityAll               = Lang._GameRepairPage.StatusNone;
                _status.IsProgressAllIndetermined = false;

                // Reset all per-file activity status
                _status.ActivityPerFile               = Lang._GameRepairPage.StatusNone;
                _status.IsProgressPerFileIndetermined = false;

                // Reset all status indicators
                _status.IsAssetEntryPanelShow = false;
                _status.IsCompleted           = false;
                _status.IsCanceled            = false;

                // Reset all total activity progress
                lock (_progress)
                {
                    _progress.ProgressPerFilePercentage = 0;
                    _progress.ProgressAllPercentage     = 0;
                    _progress.ProgressPerFileSpeed      = 0;
                    _progress.ProgressAllSpeed          = 0;

                    _progress.ProgressAllEntryCountCurrent     = 0;
                    _progress.ProgressAllEntryCountTotal       = 0;
                    _progress.ProgressPerFileEntryCountCurrent = 0;
                    _progress.ProgressPerFileEntryCountTotal   = 0;
                }
                // Reset all inner counter
                _progressAllCountCurrent      = 0;
                _progressAllCountTotal        = 0;
                _progressAllSizeCurrent       = 0;
                _progressAllSizeTotal         = 0;
                _progressPerFileSizeCurrent   = 0;
                _progressPerFileSizeTotal     = 0;
            }
        }

        protected async Task<MemoryStream> BufferSourceStreamToMemoryStream(Stream input, CancellationToken token)
        {
            // Initialize buffer and return stream
            int read;
            byte[] buffer = new byte[16 << 10];
            MemoryStream stream = new MemoryStream();

            // Initialize length and Stopwatch
            double sizeToDownload = input.Length;
            double downloaded = 0;
            Stopwatch sw = Stopwatch.StartNew();

            // Do read the stream
            while ((read = await input.ReadAsync(buffer, token)) > 0)
            {
                await stream.WriteAsync(buffer, 0, read, token);

                // Update the read status
                downloaded += read;
                lock (_progress)
                {
                    _progress.ProgressPerFilePercentage = Math.Round((downloaded / sizeToDownload) * 100, 2);
                }
                lock (_status)
                {
                    _status.ActivityPerFile = string.Format(Lang!._GameRepairPage!.PerProgressSubtitle3!, ConverterTool.SummarizeSizeSimple(downloaded / sw.Elapsed.TotalSeconds));
                }
                UpdateAll();
            }

            // Reset the stream position and stop the stopwatch
            stream.Position = 0;
            sw.Stop();

            // Return the return stream
            return stream;
        }

        protected async ValueTask FetchBilibiliSDK(CancellationToken token)
        {
            // Check whether the sdk is not null, 
            if (_gameVersionManager.GameAPIProp.data?.sdk == null) return;

            // Initialize SDK DLL path variables
            string   sdkDllPath;

            // Set total activity string as "Loading Indexes..."
            _status.ActivityStatus = Lang!._GameRepairPage!.Status2;
            UpdateStatus();

            // Get the URL and get the remote stream of the zip file
            // Also buffer the stream to memory
            string?                          url            = _gameVersionManager.GameAPIProp.data.sdk.path;
            if (url == null) throw new NullReferenceException();

            HttpResponseMessage httpResponse = await FallbackCDNUtil.GetURLHttpResponse(url, token);
            await using BridgedNetworkStream httpStream     = await FallbackCDNUtil.GetHttpStreamFromResponse(httpResponse, token);
            await using MemoryStream         bufferedStream = await BufferSourceStreamToMemoryStream(httpStream, token);
            using ZipArchive                 zip            = new ZipArchive(bufferedStream, ZipArchiveMode.Read, true);
            // Iterate the Zip Entry
            foreach (var entry in zip.Entries)
            {
                // Get the filename of the entry without ext.
                string fileName = Path.GetFileNameWithoutExtension(entry.FullName);

                // If the entry is the "sdk_pkg_version", then override the info to sdk_pkg_version
                switch (fileName)
                {
                    case "PCGameSDK":
                        // Set the SDK DLL path
                        sdkDllPath = Path.Combine(_gamePath!, $"{Path.GetFileNameWithoutExtension(_gameVersionManager!.GamePreset!.GameExecutableName)}_Data", "Plugins", "PCGameSDK.dll");
                        break;
                    case "sdk_pkg_version":
                        // Set the SDK DLL path to be used for sdk_pkg_version
                        sdkDllPath = Path.Combine(_gamePath!, "sdk_pkg_version");
                        break;
                    default:
                        continue;
                }

                // Assign FileInfo to sdkDllPath
                FileInfo sdkDllFile = new FileInfo(sdkDllPath).EnsureCreationOfDirectory().EnsureNoReadOnly();

                // Do check if sdkDllFile is not null
                // Try to create the file if not exist or open an existing one
                await using Stream sdkDllStream = sdkDllFile.Open(!sdkDllFile.Exists || entry.Length < sdkDllFile.Length ? FileMode.Create : FileMode.OpenOrCreate);
                // Initiate the Crc32 hash
                Crc32 hash = new Crc32();

                // Append the SDK DLL stream to hash and get the result
                await hash.AppendAsync(sdkDllStream, token);
                byte[] hashByte = hash.GetHashAndReset();
                uint   hashInt  = BitConverter.ToUInt32(hashByte);

                // If the hash is the same, then skip
                if (hashInt == entry.Crc32) continue;
                await using Stream entryStream = entry.Open();
                // Reset the SDK DLL stream pos and write the data
                sdkDllStream.Position = 0;
                await entryStream.CopyToAsync(sdkDllStream, token);
            }
        }

        protected IEnumerable<(T1 AssetIndex, T2 AssetProperty)> PairEnumeratePropertyAndAssetIndexPackage<T2>
            (IEnumerable<T1> assetIndex, IEnumerable<T2> assetProperty)
            where T2 : IAssetProperty
        {
            using IEnumerator<T1> assetIndexEnumerator = assetIndex.GetEnumerator();
            using IEnumerator<T2> assetPropertyEnumerator = assetProperty.GetEnumerator();

            while (assetIndexEnumerator.MoveNext()
                && assetPropertyEnumerator.MoveNext())
            {
                yield return (assetIndexEnumerator.Current, assetPropertyEnumerator.Current);
            }
        }

        protected IEnumerable<T1> EnforceHTTPSchemeToAssetIndex(IEnumerable<T1> assetIndex)
        {
            const string HTTPSScheme = "https://";
            const string HTTPScheme = "http://";
            // Get the check if HTTP override is enabled
            bool isUseHttpOverride = LauncherConfig.GetAppConfigValue("EnableHTTPRepairOverride").ToBool();

            // Iterate the IAssetIndexSummary asset
            foreach (T1 asset in assetIndex)
            {
                // If the HTTP override is enabled, then start override the HTTPS scheme
                if (isUseHttpOverride)
                {
                    // Get the remote url as span
                    ReadOnlySpan<char> url = asset.GetRemoteURL().AsSpan();
                    // If the url starts with HTTPS scheme, then...
                    if (url.StartsWith(HTTPSScheme))
                    {
                        // Get the trimmed URL without HTTPS scheme as span
                        ReadOnlySpan<char> trimmedURL = url.Slice(HTTPSScheme.Length);
                        // Set the trimmed URL
                        asset.SetRemoteURL(string.Concat(HTTPScheme, trimmedURL));
                    }

                    // Yield it and continue to the next entry
                    yield return asset;
                    continue;
                }

                // If override not enabled, then just return the asset as is
                yield return asset;
            }
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
                if (_status is { IsCompleted: false })
                {
                    _assetIndex.Clear();
                }

                _status.IsRunning = false;
            }
        }

        protected void SetFoundToTotalValue()
        {
            // Assign found count and size to total count and size
            _progressAllCountTotal = _progressAllCountFound;
            _progressAllSizeTotal = _progressAllSizeFound;

            // Reset found count and size
            _progressAllCountFound = 0;
            _progressAllSizeFound = 0;
        }

        protected bool SummarizeStatusAndProgress(List<T1> assetIndex, string msgIfFound, string msgIfClear)
        {
            // Reset status and progress properties
            ResetStatusAndProgressProperty();

            // Assign found value to total value
            SetFoundToTotalValue();

            // Set check if broken asset is found or not
            bool isBrokenFound = assetIndex.Count > 0;

            // Set status
            _status.IsAssetEntryPanelShow = isBrokenFound;
            _status.IsCompleted = true;
            _status.IsCanceled = false;
            _status.ActivityStatus = isBrokenFound ? msgIfFound : msgIfClear;

            // Update status and progress
            UpdateAll();

            // Return broken asset check
            return isBrokenFound;
        }

        protected virtual bool IsArrayMatch(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target) => source.SequenceEqual(target);

        protected virtual async Task RunDownloadTask(long assetSize, FileInfo assetPath, string assetURL,
            DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, CancellationToken token, bool isOverwrite = true)
        {
            // For any instances that uses Burst Download and if the speed limiter is null when
            // _isBurstDownloadEnabled set to false, then create the speed limiter instance
            bool isUseSelfSpeedLimiter = !_isBurstDownloadEnabled;
            DownloadSpeedLimiter? downloadSpeedLimiter = null;
            if (isUseSelfSpeedLimiter)
            {
                // Create the speed limiter instance and register the listener
                downloadSpeedLimiter = DownloadSpeedLimiter.CreateInstance(LauncherConfig.DownloadSpeedLimitCached);
                LauncherConfig.DownloadSpeedLimitChanged += downloadSpeedLimiter.GetListener();
            }

            try
            {
                // Always do multi-session download with the new DownloadClient regardless of any sizes (if applicable)
                await downloadClient.DownloadAsync(
                    assetURL,
                    assetPath,
                    isOverwrite,
                    sessionChunkSize: LauncherConfig.DownloadChunkSize,
                    progressDelegateAsync: downloadProgress,
                    cancelToken: token,
                    downloadSpeedLimiter: downloadSpeedLimiter
                    );
            }
            finally
            {
                // If the self speed listener is used, then unregister the listener
                if (isUseSelfSpeedLimiter && downloadSpeedLimiter != null)
                {
                    LauncherConfig.DownloadSpeedLimitChanged -= downloadSpeedLimiter.GetListener();
                }
            }
        }

        /// <summary>
        /// <inheritdoc cref="StreamUtility.NaivelyOpenFileStreamAsync(FileInfo, FileMode, FileAccess, FileShare, FileOptions)"/>
        /// </summary>
        internal static ValueTask<FileStream> NaivelyOpenFileStreamAsync(FileInfo info, FileMode fileMode, FileAccess fileAccess, FileShare fileShare)
            => info.NaivelyOpenFileStreamAsync(fileMode, fileAccess, fileShare);
        #endregion

        #region HashTools
        protected virtual async ValueTask<byte[]> CheckHashAsync<T>(Stream stream, T hashProvider, CancellationToken token, bool updateTotalProgress = true)
            where T : HashAlgorithm
        {
            // Create a new task from factory, assign a synchronous method to it with detached thread.
            Task<byte[]> task = Task<byte[]>
                .Factory
                .StartNew(() => CheckHash(stream, hashProvider, token, updateTotalProgress),
                          token,
                          TaskCreationOptions.DenyChildAttach,
                          TaskScheduler.Default);

            // Run the task and await, return the result
            return await task.ConfigureAwait(false);
        }

        protected virtual byte[] CheckHash<T>(Stream stream, T hashProvider, CancellationToken token, bool updateTotalProgress = true)
            where T : HashAlgorithm
        {
            // Get length based on stream length or at least if bigger, use the default one
            int bufferLen = _bufferMediumLength > stream.Length ? (int)stream.Length : _bufferMediumLength;

            // Initialize buffer
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferLen);
            bufferLen = buffer.Length;

            try
            {
                // Do read activity
                int read;
                while ((read = stream.Read(buffer, 0, bufferLen)) > 0)
                {
                    // Throw Cancellation exception if detected
                    token.ThrowIfCancellationRequested();

                    // Append buffer into hash block
                    hashProvider.TransformBlock(buffer, 0, read, buffer, 0);

                    // Increment total size counter
                    if (updateTotalProgress)
                        Interlocked.Add(ref _progressAllSizeCurrent, read);

                    // Increment per file size counter
                    Interlocked.Add(ref _progressPerFileSizeCurrent, read);

                    // Update status and progress for MD5 calculation
                    UpdateProgressCRC(read);
                }

                // Finalize the hash calculation
                hashProvider.TransformFinalBlock(buffer, 0, read);

                // Return computed hash byte
                return hashProvider.Hash ?? [];
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        protected virtual async ValueTask<byte[]> CheckNonCryptoHashAsync<T>(Stream stream, T hashProvider, CancellationToken token, bool updateTotalProgress = true)
            where T : NonCryptographicHashAlgorithm
        {
            // Create a new task from factory, assign a synchronous method to it with detached thread.
            Task<byte[]> task = Task<byte[]>
                .Factory
                .StartNew(() => CheckNonCryptoHash(stream, hashProvider, token, updateTotalProgress),
                          token,
                          TaskCreationOptions.DenyChildAttach,
                          TaskScheduler.Default);

            // Run the task and await, return the result
            return await task.ConfigureAwait(false);
        }

        protected virtual byte[] CheckNonCryptoHash<T>(Stream stream, T hashProvider, CancellationToken token, bool updateTotalProgress = true)
            where T : NonCryptographicHashAlgorithm
        {
            // Get length based on stream length or at least if bigger, use the default one
            int bufferLen = _bufferMediumLength > stream.Length ? (int)stream.Length : _bufferMediumLength;

            // Initialize buffer
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferLen);
            bufferLen = buffer.Length;

            try
            {
                // Do read activity
                int read;
                while ((read = stream.Read(buffer, 0, bufferLen)) > 0)
                {
                    // Throw Cancellation exception if detected
                    token.ThrowIfCancellationRequested();

                    // Append buffer into hash block
                    hashProvider.Append(buffer.AsSpan(0, read));

                    // Increment total size counter
                    if (updateTotalProgress)
                        Interlocked.Add(ref _progressAllSizeCurrent, read);

                    // Increment per file size counter
                    Interlocked.Add(ref _progressPerFileSizeCurrent, read);

                    // Update status and progress for Xxh64 calculation
                    UpdateProgressCRC(read);
                }

                // Return computed hash byte
                return hashProvider.GetHashAndReset();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        #endregion

        #region PatchTools
        protected virtual async ValueTask RunPatchTask(DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, CancellationToken token, long patchSize, Memory<byte> patchHash,
                                                       string patchURL, string patchOutputFile, string inputFile, string outputFile, bool isNeedRename = false)
            => await RunPatchTask(downloadClient, downloadProgress, token, patchSize,
                patchHash, patchURL, new FileInfo(patchOutputFile).EnsureNoReadOnly(), new FileInfo(inputFile).EnsureNoReadOnly(), new FileInfo(outputFile).EnsureCreationOfDirectory().EnsureNoReadOnly(), isNeedRename);

        protected virtual async ValueTask RunPatchTask(DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, CancellationToken token, long patchSize, Memory<byte> patchHash,
                                                       string patchURL, FileInfo patchOutputFile, FileInfo inputFile, FileInfo outputFile, bool isNeedRename = false)
        {
            ArgumentNullException.ThrowIfNull(patchOutputFile);
            ArgumentNullException.ThrowIfNull(inputFile);
            ArgumentNullException.ThrowIfNull(outputFile);

            // If file doesn't exist, then download the patch first
            if (!patchOutputFile.Exists || patchOutputFile.Length != patchSize)
            {
                // Download patch File first
                await RunDownloadTask(patchSize, patchOutputFile, patchURL, downloadClient, downloadProgress, token);
            }

            // Always do loop if patch doesn't get downloaded properly
            while (true)
            {
                await using FileStream patchFileStream = await patchOutputFile.NaivelyOpenFileStreamAsync(FileMode.Open, FileAccess.Read, FileShare.None);
                // Verify the patch file and if it doesn't match, then re-download it
                byte[] patchCrc = await CheckHashAsync(patchFileStream, MD5.Create(), token, false);
                if (!IsArrayMatch(patchCrc, patchHash.Span))
                {
                    // Revert back the total size
                    Interlocked.Add(ref _progressAllSizeCurrent, -patchSize);

                    // Redownload the patch file
                    await RunDownloadTask(patchSize, patchOutputFile, patchURL, downloadClient, downloadProgress, token);
                    continue;
                }

                // else, break and quit from loop
                break;
            }

            // Start patching process
            BinaryPatchUtility patchUtil = new BinaryPatchUtility();
            try
            {
                string inputFilePath = inputFile.FullName;
                string patchFilePath = patchOutputFile.FullName;
                string outputFilePath = outputFile.FullName;

                // Subscribe patching progress and start applying patch
                patchUtil.ProgressChanged += RepairTypeActionPatching_ProgressChanged;
                patchUtil.Initialize(inputFilePath, patchFilePath, outputFilePath);
                await Task.Run(() => patchUtil.Apply(token), token);

                // Delete old block
                inputFile.Refresh();
                inputFile.Delete();
                if (isNeedRename)
                {
                    // Rename to the original filename
                    outputFile.Refresh();
                    outputFile.MoveTo(inputFile.FullName, true);
                }
            }
            finally
            {
                // Delete the patch file and unsubscribe the patching progress
                patchOutputFile.Refresh();
                if (patchOutputFile.Exists)
                    patchOutputFile.Delete();

                patchUtil.ProgressChanged -= RepairTypeActionPatching_ProgressChanged;
            }
        }
        #endregion

        #region DialogTools
        protected async Task SpawnRepairDialog(List<T1> assetIndex, Action? actionIfInteractiveCancel)
        {
            ArgumentNullException.ThrowIfNull(assetIndex);
            long totalSize = assetIndex.Sum(x => x.GetAssetSize());
            StackPanel content = CollapseUIExtension.CreateStackPanel();

            content.AddElementToStackPanel(new TextBlock()
            {
                Text = string.Format(Lang._InstallMgmt.RepairFilesRequiredSubtitle!, assetIndex.Count, ConverterTool.SummarizeSizeSimple(totalSize)),
                Margin = new Thickness(0, 0, 0, 16),
                TextWrapping = TextWrapping.Wrap
            });
            Button showBrokenFilesButton = content.AddElementToStackPanel(
                CollapseUIExtension.CreateButtonWithIcon<Button>(
                    Lang._InstallMgmt!.RepairFilesRequiredShowFilesBtn,
                    "\uf550",
                    "FontAwesomeSolid",
                    "AccentButtonStyle"
                )
                .WithHorizontalAlignment(HorizontalAlignment.Center));

            showBrokenFilesButton.Click += async (_, _) =>
            {
                string tempPath = Path.GetTempFileName() + ".log";

                await using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                {
                    await using (StreamWriter sw = new StreamWriter(fs))
                    {
                        await sw.WriteLineAsync($"Original Path: {_gamePath}");
                        await sw.WriteLineAsync($"Total size to download: {ConverterTool.SummarizeSizeSimple(totalSize)} ({totalSize} bytes)");
                        await sw.WriteLineAsync();

                        foreach (T1 fileList in assetIndex)
                        {
                            await sw.WriteLineAsync(fileList.PrintSummary());
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
                catch
                { 
                    // piped to parent
                }
            };

            if (totalSize == 0) return;

            ContentDialogResult result = await SimpleDialogs.SpawnDialog(
                string.Format(Lang._InstallMgmt.RepairFilesRequiredTitle!, assetIndex.Count),
                content,
                _parentUI,
                Lang._Misc!.Cancel,
                Lang._Misc.YesResume,
                null,
                ContentDialogButton.Primary,
                ContentDialogTheme.Warning);

            if (result == ContentDialogResult.None)
            {
                actionIfInteractiveCancel?.Invoke();
                throw new OperationCanceledException();
            }
        }
        #endregion

        #region HandlerUpdaters
        public void Dispatch(DispatcherQueueHandler handler, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            EnsureParentUINotNull();
            _parentUI.DispatcherQueue.TryEnqueue(priority, handler);
        }

        public Task DispatchAsync(Action handler, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            EnsureParentUINotNull();
            return _parentUI.DispatcherQueue.EnqueueAsync(handler, priority);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void EnsureParentUINotNull()
        {
            if (_parentUI == null)
            {
                throw new NullReferenceException("_parentUI cannot be null when the method is being called!");
            }
        }

        #nullable enable
        protected virtual void PopRepairAssetEntry(IAssetProperty? assetProperty = null)
        {
            try
            {
                if (_parentUI!.DispatcherQueue!.HasThreadAccess)
                {
                    if (assetProperty == null)
                    {
                        if (AssetEntry!.Count > 0) AssetEntry.RemoveAt(0);
                    }
                    else
                    {
                        AssetEntry.Remove(assetProperty);
                    }
                    return;
                }

                Dispatch(() =>
                {
                    if (assetProperty == null)
                    {
                        if (AssetEntry!.Count > 0) AssetEntry.RemoveAt(0);
                    }
                    else
                    {
                        AssetEntry.Remove(assetProperty);
                    }
                });
            }
            catch
            {
                // pipe to parent
            }
        }
        #nullable restore

        protected void UpdateAll()
        {
            UpdateStatus();
            UpdateProgress();
        }

        protected virtual void UpdateProgress()
        {
            ProgressChanged?.Invoke(this, _progress);

            if (_status is {IsProgressAllIndetermined: false, IsCompleted: false, IsCanceled: false})
            {
                WindowUtility.SetProgressValue((ulong)(_progress.ProgressAllPercentage * 10), 1000);
            }
        }

        protected virtual void UpdateStatus()
        {
            StatusChanged?.Invoke(this, _status);

            if (_status.IsProgressAllIndetermined)
            {
                WindowUtility.SetTaskBarState(TaskbarState.Indeterminate);
            }
            else if (_status.IsCompleted || _status.IsCanceled)
            {
                WindowUtility.SetTaskBarState(TaskbarState.NoProgress);
            }
            else
            {
                WindowUtility.SetTaskBarState(TaskbarState.Normal);
            }
        }

        #endregion
    }
}
