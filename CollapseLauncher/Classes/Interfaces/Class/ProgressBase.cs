using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Http.Legacy;
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
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

namespace CollapseLauncher.Interfaces
{
    internal class ProgressBase<T1> : GamePropertyBase<T1> where T1 : IAssetIndexSummary
    {
        public ProgressBase(UIElement parentUI, IGameVersionCheck GameVersionManager, IGameSettings GameSettings, string gamePath, string gameRepoURL, string versionOverride)
            : base(parentUI, GameVersionManager, GameSettings, gamePath, gameRepoURL, versionOverride) => Init();

        public ProgressBase(UIElement parentUI, IGameVersionCheck GameVersionManager, string gamePath, string gameRepoURL, string versionOverride)
            : base(parentUI, GameVersionManager, gamePath, gameRepoURL, versionOverride) => Init();

        private void Init()
        {
            _status = new TotalPerfileStatus() { IsIncludePerFileIndicator = true };
            _progress = new TotalPerfileProgress();
            _sophonStatus = new TotalPerfileStatus() { IsIncludePerFileIndicator = true };
            _sophonProgress = new TotalPerfileProgress();
            _stopwatch = Stopwatch.StartNew();
            _refreshStopwatch = Stopwatch.StartNew();
            _downloadSpeedRefreshStopwatch = Stopwatch.StartNew();
            _assetIndex = new List<T1>();
        }

        private object _objLock = new object();

        public event EventHandler<TotalPerfileProgress> ProgressChanged;
        public event EventHandler<TotalPerfileStatus>   StatusChanged;

        protected TotalPerfileStatus    _sophonStatus;
        protected TotalPerfileProgress  _sophonProgress;
        protected TotalPerfileStatus    _status;
        protected TotalPerfileProgress  _progress;
        protected int                   _progressAllCountCurrent;
        protected int                   _progressAllCountFound;
        protected int                   _progressAllCountTotal;
        protected long                  _progressAllSizeCurrent;
        protected long                  _progressAllSizeFound;
        protected long                  _progressAllSizeTotal;
        protected double                _progressAllIOReadCurrent;
        protected long                  _progressPerFileSizeCurrent;
        protected long                  _progressPerFileSizeTotal;
        protected double                _progressPerFileIOReadCurrent;

        // Extension for IGameInstallManager

        protected const int _downloadSpeedRefreshInterval = 5000;
        protected const int _refreshInterval = 100;

        protected bool _isSophonInUpdateMode { get; set; }

        #region ProgressEventHandlers - Fetch
        protected void _innerObject_ProgressAdapter(object sender, TotalPerfileProgress e) => ProgressChanged?.Invoke(sender, e);
        protected void _innerObject_StatusAdapter(object sender, TotalPerfileStatus e) => StatusChanged?.Invoke(sender, e);

        protected virtual async void _httpClient_FetchAssetProgress(int size, DownloadProgress downloadData)
        {
            if (await CheckIfNeedRefreshStopwatch())
            {
                double speed = downloadData.BytesDownloaded / _downloadSpeedRefreshStopwatch.Elapsed.TotalSeconds;
                TimeSpan timeLeftSpan = ((downloadData.BytesTotal - downloadData.BytesDownloaded) / speed).ToTimeSpanNormalized();
                double percentage = ConverterTool.GetPercentageNumber(downloadData.BytesDownloaded, downloadData.BytesTotal, 2);

                lock (_status!)
                {
                    // Update fetch status
                    _status.IsProgressPerFileIndetermined = false;
                    _status.IsProgressAllIndetermined = false;
                    _status.ActivityPerFile = string.Format(Lang!._GameRepairPage!.PerProgressSubtitle3!, ConverterTool.SummarizeSizeSimple(speed));
                }

                lock (_progress!)
                {
                    // Update fetch progress
                    _progress.ProgressPerFilePercentage = percentage;
                    _progress.ProgressAllSizeCurrent = downloadData.BytesDownloaded;
                    _progress.ProgressAllSizeTotal = downloadData.BytesTotal;
                    _progress.ProgressAllSpeed = speed;
                    _progress.ProgressAllTimeLeft = timeLeftSpan;
                }

                // Push status and progress update
                UpdateStatus();
                UpdateProgress();
            }
        }

        protected virtual void _httpClient_FetchAssetProgress(object sender, DownloadEvent e)
        {
            lock (_status!)
            {
                // Update fetch status
                _status.IsProgressPerFileIndetermined = false;
                _status.IsProgressAllIndetermined = false;
                _status.ActivityPerFile = string.Format(Lang!._GameRepairPage!.PerProgressSubtitle3!, ConverterTool.SummarizeSizeSimple(e!.Speed));
            }

            lock (_progress!)
            {
                // Update fetch progress
                _progress.ProgressPerFilePercentage = e.ProgressPercentage;
                _progress.ProgressAllSizeCurrent = e.SizeDownloaded;
                _progress.ProgressAllSizeTotal = e.SizeToBeDownloaded;
                _progress.ProgressAllSpeed = e.Speed;
                _progress.ProgressAllTimeLeft = e.TimeLeft;
            }

            // Push status and progress update
            UpdateStatus();
            UpdateProgress();
        }
        #endregion

        #region ProgressEventHandlers - Repair
        protected virtual async void _httpClient_RepairAssetProgress(object sender, DownloadEvent e)
        {
            lock (_progress!)
            {
                _progress.ProgressPerFilePercentage     = e!.ProgressPercentage;
                _progress.ProgressPerFileSizeCurrent       = e!.SizeDownloaded;
                _progress.ProgressPerFileSizeTotal = e!.SizeToBeDownloaded;
                _progress.ProgressAllSizeCurrent         = _progressAllSizeCurrent;
                _progress.ProgressAllSizeTotal   = _progressAllSizeTotal;

                // Calculate speed
                long speed = (long)(_progressAllSizeCurrent / _stopwatch!.Elapsed.TotalSeconds);
                _progress.ProgressAllSpeed = speed;
                _progress.ProgressAllTimeLeft = ((_progressAllSizeCurrent - _progressAllSizeTotal) / ConverterTool.Unzeroed(speed))
                    .ToTimeSpanNormalized();

                // Update current progress percentages
                _progress.ProgressAllPercentage = _progressAllSizeCurrent != 0 ?
                    ConverterTool.GetPercentageNumber(_progressAllSizeCurrent, _progressAllSizeTotal) :
                    0;

                if (e.State != DownloadState.Merging)
                {
                    _progressAllSizeCurrent += e.Read;
                }
            }

            if (await CheckIfNeedRefreshStopwatch())
            {
                lock (_status!)
                {
                    // Update current activity status
                    _status.IsProgressAllIndetermined = false;
                    _status.IsProgressPerFileIndetermined = false;

                    // Set time estimation string
                    string timeLeftString = string.Format(Lang!._Misc!.TimeRemainHMSFormat!, _progress!.ProgressAllTimeLeft);

                    _status.ActivityPerFile = string.Format(Lang._Misc.Speed!, ConverterTool.SummarizeSizeSimple(_progress.ProgressAllSpeed));
                    _status.ActivityAll = string.Format(Lang._GameRepairPage!.PerProgressSubtitle2!, 
                                                          ConverterTool.SummarizeSizeSimple(_progressAllSizeCurrent), 
                                                          ConverterTool.SummarizeSizeSimple(_progressAllSizeTotal)) + $" | {timeLeftString}";

                    // Trigger update
                    UpdateAll();
                }
            }
        }

        protected virtual void UpdateRepairStatus(string activityStatus, string ActivityAll, bool isPerFileIndetermined)
        {
            lock (_status!)
            {
                // Set repair activity status
                _status.ActivityStatus = activityStatus;
                _status.ActivityAll = ActivityAll;
                _status.IsProgressPerFileIndetermined = isPerFileIndetermined;
            }

            // Update status
            UpdateStatus();
        }
        #endregion

        #region ProgressEventHandlers - Patch
        protected virtual async void RepairTypeActionPatching_ProgressChanged(object sender, BinaryPatchProgress e)
        {
            lock (_progress!)
            {
                _progress.ProgressPerFilePercentage = e!.ProgressPercentage;
                _progress.ProgressAllSpeed = e!.Speed;

                // Update current progress percentages
                _progress.ProgressAllPercentage = _progressAllSizeCurrent != 0 ?
                    ConverterTool.GetPercentageNumber(_progressAllSizeCurrent, _progressAllSizeTotal) :
                    0;
            }

            if (await CheckIfNeedRefreshStopwatch())
            {
                lock (_status!)
                {
                    // Update current activity status
                    _status.IsProgressAllIndetermined = false;
                    _status.IsProgressPerFileIndetermined = false;
                    _status.ActivityPerFile = string.Format(Lang!._GameRepairPage!.PerProgressSubtitle5!, ConverterTool.SummarizeSizeSimple(_progress!.ProgressAllSpeed));
                    _status.ActivityAll = string.Format(Lang._GameRepairPage.PerProgressSubtitle2!, ConverterTool.SummarizeSizeSimple(_progressAllSizeCurrent), ConverterTool.SummarizeSizeSimple(_progressAllSizeTotal));
                }

                // Trigger update
                UpdateAll();
            }
        }
        #endregion

        #region ProgressEventHandlers - CRC/HashCheck
        protected virtual async void UpdateProgressCRC()
        {
            if (await CheckIfNeedRefreshStopwatch())
            {
                lock (_progress!)
                {
                    // Update current progress percentages
                    _progress.ProgressPerFilePercentage = _progressPerFileSizeCurrent != 0 ?
                        ConverterTool.GetPercentageNumber(_progressPerFileSizeCurrent, _progressPerFileSizeTotal) :
                        0;
                    _progress.ProgressAllPercentage = _progressAllSizeCurrent != 0 ?
                        ConverterTool.GetPercentageNumber(_progressAllSizeCurrent, _progressAllSizeTotal) :
                        0;

                    // Update the progress of total size
                    _progress.ProgressPerFileSizeCurrent = _progressPerFileSizeCurrent;
                    _progress.ProgressPerFileSizeTotal = _progressPerFileSizeTotal;
                    _progress.ProgressAllSizeCurrent = _progressAllSizeCurrent;
                    _progress.ProgressAllSizeTotal = _progressAllSizeTotal;

                    // Calculate current speed and update the status and progress speed
                    _progress.ProgressAllSpeed = _progressAllSizeCurrent / _stopwatch!.Elapsed.TotalSeconds;

                    // Calculate the timelapse
                    _progress.ProgressAllTimeLeft = ((_progressAllSizeTotal - _progressAllSizeCurrent) / ConverterTool.Unzeroed(_progress.ProgressAllSpeed)).ToTimeSpanNormalized();
                }

                lock (_status!)
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
        }
        #endregion

        #region ProgressEventHandlers - DoCopyStreamProgress
        protected virtual async void UpdateProgressCopyStream(long currentPosition, int read, long totalReadSize)
        {
            if (await CheckIfNeedRefreshStopwatch())
            {
                lock (_progress!)
                {
                    // Update current progress percentages
                    _progress.ProgressPerFilePercentage = ConverterTool.GetPercentageNumber(currentPosition, totalReadSize);

                    // Update the progress of total size
                    _progress.ProgressPerFileSizeCurrent = currentPosition;
                    _progress.ProgressPerFileSizeTotal = totalReadSize;

                    // Calculate current speed and update the status and progress speed
                    _progress.ProgressAllSpeed = currentPosition / _stopwatch!.Elapsed.TotalSeconds;

                    // Calculate the timelapse
                    _progress.ProgressAllTimeLeft = ((totalReadSize - currentPosition) / ConverterTool.Unzeroed(_progress.ProgressAllSpeed)).ToTimeSpanNormalized();
                }

                lock (_status!)
                {
                    // Set time estimation string
                    string timeLeftString = string.Format(Lang!._Misc!.TimeRemainHMSFormat!, _progress.ProgressAllTimeLeft);

                    // Update current activity status
                    _status.ActivityPerFile = string.Format(Lang._Misc.Speed!, ConverterTool.SummarizeSizeSimple(_progress.ProgressAllSpeed));
                    _status.ActivityAll = string.Format(Lang._GameRepairPage!.PerProgressSubtitle2!, 
                                                          ConverterTool.SummarizeSizeSimple(currentPosition), 
                                                          ConverterTool.SummarizeSizeSimple(totalReadSize)) + $" | {timeLeftString}";
                }

                // Trigger update
                UpdateAll();
            }
        }
        #endregion

        #region ProgressEventHandlers - SophonInstaller
        protected async void UpdateSophonFileTotalProgress(long read)
        {
            Interlocked.Add(ref _progressAllSizeCurrent, read);
            _progressAllIOReadCurrent += read;

            if (_refreshStopwatch!.ElapsedMilliseconds > _refreshInterval)
            {
                // Assign local sizes to progress
                _sophonProgress.ProgressAllSizeCurrent      = _progressAllSizeCurrent;
                _sophonProgress.ProgressAllSizeTotal        = _progressAllSizeTotal;
                _sophonProgress.ProgressPerFileSizeCurrent  = _progressPerFileSizeCurrent;
                _sophonProgress.ProgressPerFileSizeTotal    = _progressPerFileSizeTotal;

                // Calculate the speed
                double speedAll                         = _progressAllIOReadCurrent / _downloadSpeedRefreshStopwatch.Elapsed.TotalSeconds;
                double speedPerFile                     = _progressPerFileIOReadCurrent / _downloadSpeedRefreshStopwatch.Elapsed.TotalSeconds;
                double speedAllNoReset                  = _progressAllSizeCurrent / _stopwatch.Elapsed.TotalSeconds;
                _sophonProgress.ProgressAllSpeed        = speedAll;
                _sophonProgress.ProgressPerFileSpeed    = speedPerFile;

                // Calculate Count
                _sophonProgress.ProgressAllEntryCountCurrent    = _progressAllCountCurrent;
                _sophonProgress.ProgressAllEntryCountTotal      = _progressAllCountTotal;

                // Calculate percentage
                _sophonProgress.ProgressAllPercentage       =
                    Math.Round((double)_progressAllSizeCurrent / _progressAllSizeTotal * 100, 2);
                _sophonProgress.ProgressPerFilePercentage   =
                    Math.Round((double)_progressPerFileSizeCurrent / _progressPerFileSizeTotal * 100, 2);

                // Calculate the timelapse
                double progressTimeAvg = (_progressAllSizeTotal - _progressAllSizeCurrent) / speedAllNoReset;
                _sophonProgress.ProgressAllTimeLeft = progressTimeAvg.ToTimeSpanNormalized();

                // Update progress
                ProgressChanged?.Invoke(this, _sophonProgress);

                if (_downloadSpeedRefreshInterval < _downloadSpeedRefreshStopwatch!.ElapsedMilliseconds)
                {
                    _progressAllIOReadCurrent = 0;
                    _progressPerFileIOReadCurrent = 0;
                    _downloadSpeedRefreshStopwatch.Restart();
                }

                _refreshStopwatch.Restart();
                await Task.Delay(_refreshInterval);
            }
        }

        protected void UpdateSophonFileDownloadProgress(long downloadedWrite, long currentWrite)
        {
            Interlocked.Add(ref _progressPerFileSizeCurrent, downloadedWrite);
            lock (_objLock)
            {
                _progressPerFileIOReadCurrent += currentWrite;
            }
        }

        protected void UpdateSophonDownloadStatus(SophonAsset asset)
        {
            Interlocked.Add(ref _progressAllCountCurrent, 1);
            _status.ActivityStatus = string.Format("{0}: {1}",
                _isSophonInUpdateMode ? Lang._Misc.Updating : Lang._Misc.Downloading,
                string.Format(Lang._Misc.PerFromTo, _progressAllCountCurrent, _progressAllCountTotal));
            UpdateStatus();
        }

        protected void UpdateSophonLogHandler(object sender, LogStruct e)
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
            long inputSize = estimatedSize != null ? estimatedSize ?? 0 : source!.Length;
            long currentPos = 0;
            RestartStopwatch();

            bool isLastPerfileStateIndetermined = _status!.IsProgressPerFileIndetermined;
            bool isLastTotalStateIndetermined = _status!.IsProgressAllIndetermined;

            _status.IsProgressPerFileIndetermined = false;
            _status.IsProgressAllIndetermined = true;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(16 << 10);
            try
            {
                while ((read = await source!.ReadAsync(buffer, token)) > 0)
                {
                    await target!.WriteAsync(buffer, 0, read, token);
                    currentPos += read;
                    UpdateProgressCopyStream(currentPos, read, inputSize);
                }
            }
            catch { throw; }
            finally
            {
                _status!.IsProgressPerFileIndetermined = isLastPerfileStateIndetermined;
                _status!.IsProgressAllIndetermined = isLastTotalStateIndetermined;
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        protected string EnsureCreationOfDirectory(string str)
        {
            string dir = Path.GetDirectoryName(str);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir!);

            return str;
        }

        protected void TryUnassignReadOnlyFiles(string path)
        {
            // Iterate every files and set the read-only flag to false
            foreach (string file in Directory.EnumerateFiles(path!, "*", SearchOption.AllDirectories))
            {
                FileInfo fileInfo = new FileInfo(file);
                if (fileInfo.IsReadOnly)
                    fileInfo.IsReadOnly = false;
            }
        }

        protected void TryUnassignReadOnlyFileSingle(string path)
        {
            FileInfo fileInfo = new FileInfo(path);
            if (fileInfo.Exists && fileInfo.IsReadOnly)
                fileInfo.IsReadOnly = false;
        }

        protected void TryDeleteReadOnlyDir(string dirPath)
        {
            if (!Directory.Exists(dirPath)) return;
            DirectoryInfo dirInfo = new DirectoryInfo(dirPath);
            foreach (FileInfo files in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try
                {
                    files.IsReadOnly = false;
                    files.Delete();
                }
                catch (Exception ex)
                {
                    LogWriteLine($"Failed while deleting file: {files.FullName}\r\n{ex}", LogType.Warning, true);
                } // Suppress errors
            }
            try
            {
                dirInfo.Delete(true);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while deleting parent dir: {dirPath}\r\n{ex}", LogType.Warning, true);
            } // Suppress errors
        }

        protected void TryDeleteReadOnlyFile(string path)
        {
            if (!File.Exists(path)) return;
            try
            {
                FileInfo file = new FileInfo(path!);
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
            int DirLength = SourcePath!.Length + 1;

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
                destFilePath = Path.Combine(DestPath!, relativePath.ToString());
                // Get folder path for destination
                destFolderPath = Path.GetDirectoryName(destFilePath);

                // Create the destination folder if not exist
                if (!Directory.Exists(destFolderPath))
                    Directory.CreateDirectory(destFolderPath!);

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

            lock (_status!)
            {
                // Show the asset entry panel
                _status!.IsAssetEntryPanelShow = false;

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
                _progress!.ProgressPerFilePercentage    = 0;
                _progress.ProgressAllPercentage         = 0;
                _progress.ProgressPerFileSpeed          = 0;
                _progress.ProgressAllSpeed              = 0;

                _progress.ProgressAllEntryCountCurrent      = 0;
                _progress.ProgressAllEntryCountTotal        = 0;
                _progress.ProgressPerFileEntryCountCurrent  = 0;
                _progress.ProgressPerFileEntryCountTotal    = 0;

                // Reset all inner counter
                _progressAllCountCurrent    = 0;
                _progressAllCountTotal      = 0;
                _progressAllSizeCurrent     = 0;
                _progressAllSizeTotal       = 0;
                _progressPerFileSizeCurrent = 0;
                _progressPerFileSizeTotal   = 0;
            }
        }

        protected async Task<MemoryStream> BufferSourceStreamToMemoryStream(Stream input, CancellationToken token)
        {
            // Initialize buffer and return stream
            int read;
            byte[] buffer = new byte[16 << 10];
            MemoryStream stream = new MemoryStream();

            // Initialize length and Stopwatch
            double sizeToDownload = input!.Length;
            double downloaded = 0;
            Stopwatch sw = Stopwatch.StartNew();

            // Do read the stream
            while ((read = await input.ReadAsync(buffer, token)) > 0)
            {
                await stream.WriteAsync(buffer, 0, read, token);

                // Update the read status
                downloaded += read;
                _progress!.ProgressPerFilePercentage = Math.Round((downloaded / sizeToDownload) * 100, 2);
                lock (_status!)
                {
                    _status!.ActivityPerFile = string.Format(Lang!._GameRepairPage!.PerProgressSubtitle3!, ConverterTool.SummarizeSizeSimple(downloaded / sw.Elapsed.TotalSeconds));
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
            if (_gameVersionManager!.GameAPIProp!.data!.sdk == null) return;

            // Initialize SDK DLL path variables
            string sdkDllPath;
            string sdkDllDir;
            FileInfo sdkDllFile;

            // Set total activity string as "Loading Indexes..."
            _status!.ActivityStatus = Lang!._GameRepairPage!.Status2;
            UpdateStatus();

            // Get the URL and get the remote stream of the zip file
            // Also buffer the stream to memory
            string url = _gameVersionManager!.GameAPIProp.data.sdk.path;
            using (HttpResponseMessage httpResponse = await FallbackCDNUtil.GetURLHttpResponse(url, token))
                await using (BridgedNetworkStream httpStream = await FallbackCDNUtil.GetHttpStreamFromResponse(httpResponse, token))
                    await using (MemoryStream bufferedStream = await BufferSourceStreamToMemoryStream(httpStream, token))
                        using (ZipArchive zip = new ZipArchive(bufferedStream!, ZipArchiveMode.Read, true))
            {
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
                            sdkDllDir = Path.GetDirectoryName(sdkDllPath);
                            sdkDllFile = new FileInfo(sdkDllPath);

                            // Create the folder of the SDK DLL if doesn't exist
                            if (!Directory.Exists(sdkDllDir)) Directory.CreateDirectory(sdkDllDir!);
                            break;
                        case "sdk_pkg_version":
                            // Set the SDK DLL path to be used for sdk_pkg_version
                            sdkDllPath = Path.Combine(_gamePath!, "sdk_pkg_version");
                            sdkDllFile = new FileInfo(sdkDllPath);
                            break;
                        default:
                            continue;
                    }

                    // Do check if sdkDllFile is not null
                    // Try create the file if not exist or open an existing one
                    await using (Stream sdkDllStream = sdkDllFile.Open(!sdkDllFile.Exists || entry.Length < sdkDllFile.Length ? FileMode.Create : FileMode.OpenOrCreate))
                    {
                        // Initiate the Crc32 hash
                        Crc32 hash = new Crc32();

                        // Append the SDK DLL stream to hash and get the result
                        await hash.AppendAsync(sdkDllStream, token);
                        byte[] hashByte = hash.GetHashAndReset();
                        uint   hashInt  = BitConverter.ToUInt32(hashByte);

                        // If the hash is the same, then skip
                        if (hashInt == entry.Crc32) continue;
                        await using (Stream entryStream = entry.Open())
                        {
                            // Reset the SDK DLL stream pos and write the data
                            sdkDllStream.Position = 0;
                            await entryStream.CopyToAsync(sdkDllStream, token);
                        }
                    }
                }
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
            bool IsUseHTTPOverride = LauncherConfig.GetAppConfigValue("EnableHTTPRepairOverride").ToBool();

            // Iterate the IAssetIndexSummary asset
            foreach (T1 asset in assetIndex!)
            {
                // If the HTTP override is enabled, then start override the HTTPS scheme
                if (IsUseHTTPOverride)
                {
                    // Get the remote url as span
                    ReadOnlySpan<char> url = asset!.GetRemoteURL().AsSpan();
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
                _status!.IsRunning = true;

                // Run the task
                return await action!;
            }
            catch (TaskCanceledException)
            {
                // If a cancellation was thrown, then set IsCanceled as true
                _status!.IsCompleted = false;
                _status.IsCanceled = true;
                throw;
            }
            catch (OperationCanceledException)
            {
                // If a cancellation was thrown, then set IsCanceled as true
                _status!.IsCompleted = false;
                _status.IsCanceled = true;
                throw;
            }
            catch (Exception)
            {
                // Except, if the other exception was thrown, then set both IsCompleted
                // and IsCanceled as false.
                _status!.IsCompleted = false;
                _status.IsCanceled = false;
                throw;
            }
            finally
            {
                // Clear the _assetIndex after that
                if (!_status!.IsCompleted)
                {
                    _assetIndex!.Clear();
                }

                // Define that the status is not running
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
            bool IsBrokenFound = assetIndex!.Count > 0;

            // Set status
            _status!.IsAssetEntryPanelShow = IsBrokenFound;
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
            string dirPath = Path.GetDirectoryName(assetPath);
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            // Start downloading asset
            if (assetSize >= _sizeForMultiDownload && !_isBurstDownloadEnabled)
            {
                await _httpClient!.Download(assetURL, assetPath, _downloadThreadCount, true, token);
                await _httpClient.Merge(token);
            }
            else
            {
                await _httpClient!.Download(assetURL, assetPath, true, null, null, token);
            }
        }

        /// <summary>
        /// IDK what Microsoft is smoking but for some reason, the file were throwing IO_SharingViolation_File error,
        /// or sometimes "File is being used by another process" error even though the file HAS NEVER BEEN OPENED LIKE, WTFFF????!>!!!!!!
        /// </summary>
        internal static async ValueTask<FileStream> NaivelyOpenFileStreamAsync(FileInfo info, FileMode fileMode, FileAccess fileAccess, FileShare fileShare)
        {
            const int MaxTry = 10;
            int currentTry = 1;
            while (true)
            {
                try
                {
                    return info.Open(fileMode, fileAccess, fileShare);
                }
                catch
                {
                    if (currentTry <= MaxTry)
                    {
                        LogWriteLine($"Failed while trying to open: {info.FullName}. Retry attempt: {++currentTry} / {MaxTry}", LogType.Warning, true);
                        await Task.Delay(50); // Adding 50ms delay
                        continue;
                    }
                    throw; // Throw this MFs
                }
            }
        }
        #endregion

        #region HashTools
        protected virtual async ValueTask<byte[]> CheckHashAsync(Stream stream, HashAlgorithm hashProvider, CancellationToken token, bool updateTotalProgress = true)
        {
            // Initialize MD5 instance and assign buffer
            byte[] buffer = new byte[_bufferBigLength];

            // Do read activity
            int read;
            while ((read = await stream!.ReadAsync(buffer, token)) > 0)
            {
                // Throw Cancellation exception if detected
                token.ThrowIfCancellationRequested();

                // Append buffer into hash block
                hashProvider!.TransformBlock(buffer, 0, read, buffer, 0);

                lock (this)
                {
                    // Increment total size counter
                    if (updateTotalProgress) _progressAllSizeCurrent += read;
                    // Increment per file size counter
                    _progressPerFileSizeCurrent += read;
                }

                // Update status and progress for MD5 calculation
                UpdateProgressCRC();
            }

            // Finalize the hash calculation
            hashProvider!.TransformFinalBlock(buffer, 0, read);

            // Return computed hash byte
            return hashProvider.Hash;
        }
        #endregion

        #region PatchTools
        protected virtual async ValueTask RunPatchTask(Http _httpClient, CancellationToken token, long patchSize, Memory<byte> patchHash,
            string patchURL, string patchOutputFile, string inputFile, string outputFile, bool isNeedRename = false)
        {
            ArgumentNullException.ThrowIfNull(patchOutputFile);
            ArgumentNullException.ThrowIfNull(inputFile);
            ArgumentNullException.ThrowIfNull(outputFile);
            
            // Get info about patch file
            FileInfo patchInfo = new FileInfo(patchOutputFile);

            // If file doesn't exist, then download the patch first
            if (!patchInfo.Exists || patchInfo.Length != patchSize)
            {
                // Download patch File first
                await RunDownloadTask(patchSize, patchOutputFile, patchURL, _httpClient, token)!;
            }

            // Always do loop if patch doesn't get downloaded properly
            while (true)
            {
                using (FileStream patchfs = new FileStream(patchOutputFile, FileMode.Open, FileAccess.Read, FileShare.None, _bufferBigLength))
                {
                    // Verify the patch file and if it doesn't match, then redownload it
                    byte[] patchCRC = await CheckHashAsync(patchfs, MD5.Create(), token, false);
                    if (!IsArrayMatch(patchCRC, patchHash.Span))
                    {
                        // Revert back the total size
                        _progressAllSizeCurrent -= patchSize;

                        // Redownload the patch file
                        await RunDownloadTask(patchSize, patchOutputFile, patchURL, _httpClient, token)!;
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
                await Task.Run(() => patchUtil.Apply(token));

                // Delete old block
                File.Delete(inputFile);
                if (isNeedRename)
                {
                    // Rename to the original filename
                    File.Move(outputFile, inputFile, true);
                }
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
        protected async Task SpawnRepairDialog(List<T1> assetIndex, Action actionIfInteractiveCancel)
        {
            ArgumentNullException.ThrowIfNull(assetIndex);
            long totalSize = assetIndex.Sum(x => x!.GetAssetSize());
            StackPanel Content = UIElementExtensions.CreateStackPanel();

            Content.AddElementToStackPanel(new TextBlock()
            {
                Text = string.Format(Lang._InstallMgmt.RepairFilesRequiredSubtitle!, assetIndex.Count, ConverterTool.SummarizeSizeSimple(totalSize)),
                Margin = new Thickness(0, 0, 0, 16),
                TextWrapping = TextWrapping.Wrap
            });
            Button ShowBrokenFilesButton = Content.AddElementToStackPanel(
                UIElementExtensions.CreateButtonWithIcon<Button>(
                    Lang._InstallMgmt!.RepairFilesRequiredShowFilesBtn,
                    "\uf550",
                    "FontAwesomeSolid",
                    "AccentButtonStyle"
                )
                .WithHorizontalAlignment(HorizontalAlignment.Center));

            ShowBrokenFilesButton.Click += async (_, _) =>
            {
                string tempPath = Path.GetTempFileName() + ".log";

                using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                {
                    using (StreamWriter sw = new StreamWriter(fs))
                    {
                        sw.WriteLine($"Original Path: {_gamePath}");
                        sw.WriteLine($"Total size to download: {ConverterTool.SummarizeSizeSimple(totalSize)} ({totalSize} bytes)");
                        sw.WriteLine();

                        foreach (T1 fileList in assetIndex)
                        {
                            sw.WriteLine(fileList!.PrintSummary());
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
                Content,
                _parentUI,
                Lang._Misc!.Cancel,
                Lang._Misc.YesResume,
                null,
                ContentDialogButton.Primary,
                ContentDialogTheme.Warning);

            if (result == ContentDialogResult.None)
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
        public void Dispatch(DispatcherQueueHandler handler) => _parentUI!.DispatcherQueue!.TryEnqueue(handler);

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

        protected async Task<bool> CheckIfNeedRefreshStopwatch()
        {
            lock (_objLock)
            {
                if (_refreshStopwatch!.ElapsedMilliseconds > _refreshInterval)
                {
                    _refreshStopwatch.Restart();
                    return true;
                }
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
        protected virtual void RestartStopwatch() => _stopwatch!.Restart();
        #endregion
    }
}
