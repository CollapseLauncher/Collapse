using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.InstallManager;
using CommunityToolkit.WinUI;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Hashes;
using Hi3Helper.Http;
using Hi3Helper.Http.Legacy;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Hi3Helper.SimpleZipArchiveReader;
using Hi3Helper.Sophon;
using Hi3Helper.Sophon.Helper;
using Hi3Helper.Win32.TaskbarListCOM;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using SevenZipExtractor;
using SevenZipExtractor.Event;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Logger = Hi3Helper.Logger;
// ReSharper disable AccessToDisposedClosure

// ReSharper disable InconsistentlySynchronizedField
// ReSharper disable IdentifierTypo
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBeProtected.Global
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Interfaces;

public delegate Task InstallPackageExtractorDelegate(Func<Stream>      streamFactory,
                                                     string            outputDir,
                                                     CancellationToken token);

internal abstract class ProgressBase : GamePropertyBase
{
    protected ProgressBase(
        UIElement      parentUI,
        IGameVersion   gameVersionManager,
        IGameSettings? gameSettings,
        string?        gameRepoURL,
        string?        versionOverride)
        : base(parentUI,
               gameVersionManager,
               gameSettings,
               gameRepoURL,
               versionOverride)
    {
        Progress = new TotalPerFileProgress();
        Status   = new TotalPerFileStatus
        {
            IsIncludePerFileIndicator = true
        };

        AssetEntry = [];
    }

    protected ProgressBase(
        UIElement    parentUI,
        IGameVersion gameVersionManager,
        string?      gameRepoURL,
        string?      versionOverride)
        : this(parentUI,
               gameVersionManager,
               null,
               gameRepoURL,
               versionOverride)
    { }

    public event EventHandler<TotalPerFileProgress>? ProgressChanged;
    public event EventHandler<TotalPerFileStatus>?   StatusChanged;

    public TotalPerFileStatus Status
    {
        get;
        private init
        {
            OnPropertyChanged();
            field = value;
        }
    }

    public TotalPerFileProgress Progress
    {
        get;
        private init
        {
            OnPropertyChanged();
            field = value;
        }
    }

    internal int  ProgressAllCountCurrent;
    internal int  ProgressAllCountFound;
    internal int  ProgressAllCountTotal;
    internal long ProgressAllSizeCurrent;
    internal long ProgressAllSizeFound;
    internal long ProgressAllSizeTotal;
    internal long ProgressPerFileSizeCurrent;
    internal long ProgressPerFileSizeTotal;

    /// <summary>
    /// Normalized app download thread configured within app global config.<br/>
    /// <br/>
    /// If the thread is set to less or equal to 0, it will automatically set to the thread count of your CPU.
    /// </summary>
    internal int ThreadForDownloadNormalized
    {
        get
        {
            int parallelThread = LauncherConfig.AppCurrentDownloadThread;
            if (parallelThread <= 0)
            {
                parallelThread = Environment.ProcessorCount;
            }

            return parallelThread;
        }
    }

    /// <summary>
    /// Normalized app I/O thread configured within app global config.<br/>
    /// <br/>
    /// If the thread is set to less or equal to 0, it will automatically set to the thread count of your CPU.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    internal int ThreadForIONormalized
    {
        get
        {
            int parallelThread = LauncherConfig.AppCurrentThread;
            if (parallelThread <= 0)
            {
                parallelThread = Environment.ProcessorCount;
            }

            return parallelThread;
        }
    }

    public ObservableCollection<IAssetProperty> AssetEntry
    {
        get;
        set;
    }

    internal bool IsForceHttpOverride => LauncherConfig.GetAppConfigValue("EnableHTTPRepairOverride");

    // Extension for IGameInstallManager

    private const int RefreshInterval = 100;

    public bool IsSophonInUpdateMode { get; protected set; }
    protected bool IsAllowExtractCorruptZip { get; set; }


    #region ProgressEventHandlers - Fetch
    protected void _innerObject_ProgressAdapter(object? sender, TotalPerFileProgress e) => ProgressChanged?.Invoke(sender, e);
    protected void _innerObject_StatusAdapter(object? sender, TotalPerFileStatus e) => StatusChanged?.Invoke(sender, e);

    protected virtual void _httpClient_FetchAssetProgress(int size, DownloadProgress downloadProgress)
    {
        // Calculate the speed
        double speedAll = CalculateSpeed(size);

        if (!CheckIfNeedRefreshStopwatch())
        {
            return;
        }

        // Calculate the clamped speed and timelapse
        double speedClamped = speedAll.ClampLimitedSpeedNumber();

        TimeSpan timeLeftSpan = ConverterTool.ToTimeSpanRemain(downloadProgress.BytesTotal, downloadProgress.BytesDownloaded, speedClamped);
        double   percentage   = ConverterTool.ToPercentage(downloadProgress.BytesTotal, downloadProgress.BytesDownloaded);

        lock (Status)
        {
            // Update fetch status
            Status.IsProgressPerFileIndetermined = false;
            Status.IsProgressAllIndetermined     = false;
            Status.ActivityPerFile               = string.Format(Locale.Lang._GameRepairPage.PerProgressSubtitle3, ConverterTool.SummarizeSizeSimple(speedClamped));
        }

        lock (Progress)
        {
            // Update fetch progress
            Progress.ProgressPerFilePercentage = percentage;
            Progress.ProgressAllSizeCurrent    = downloadProgress.BytesDownloaded;
            Progress.ProgressAllSizeTotal      = downloadProgress.BytesTotal;
            Progress.ProgressAllSpeed          = speedClamped;
            Progress.ProgressAllTimeLeft       = timeLeftSpan;
        }

        // Push status and progress update
        UpdateStatus();
        UpdateProgress();
    }

    #endregion

    #region ProgressEventHandlers - Repair
    protected virtual void _httpClient_RepairAssetProgress(int size, DownloadProgress downloadProgress)
    {
        Interlocked.Add(ref ProgressAllSizeCurrent, size);

        // Calculate the speed
        double speedAll = CalculateSpeed(size);

        if (!CheckIfNeedRefreshStopwatch())
        {
            return;
        }

        // Calculate the clamped speed and timelapse
        double speedClamped = speedAll.ClampLimitedSpeedNumber();

        TimeSpan timeLeftSpan      = ConverterTool.ToTimeSpanRemain(ProgressAllSizeTotal, ProgressAllSizeCurrent, speedClamped);
        double   percentagePerFile = ConverterTool.ToPercentage(downloadProgress.BytesTotal, downloadProgress.BytesDownloaded);

        lock (Progress)
        {
            Progress.ProgressPerFilePercentage  = percentagePerFile;
            Progress.ProgressPerFileSizeCurrent = downloadProgress.BytesDownloaded;
            Progress.ProgressPerFileSizeTotal   = downloadProgress.BytesTotal;
            Progress.ProgressAllSizeCurrent     = ProgressAllSizeCurrent;
            Progress.ProgressAllSizeTotal       = ProgressAllSizeTotal;

            // Calculate speed
            Progress.ProgressAllSpeed    = speedClamped;
            Progress.ProgressAllTimeLeft = timeLeftSpan;

            // Update current progress percentages
            Progress.ProgressAllPercentage = ProgressAllSizeCurrent != 0 ?
                ConverterTool.ToPercentage(ProgressAllSizeTotal, ProgressAllSizeCurrent) :
                0;
        }

        lock (Status)
        {
            // Update current activity status
            Status.IsProgressAllIndetermined     = false;
            Status.IsProgressPerFileIndetermined = false;

            // Set time estimation string
            string timeLeftString = string.Format(Locale.Lang._Misc.TimeRemainHMSFormat, Progress.ProgressAllTimeLeft);

            Status.ActivityPerFile = string.Format(Locale.Lang._Misc.Speed, ConverterTool.SummarizeSizeSimple(Progress.ProgressAllSpeed));
            Status.ActivityAll     = string.Format(Locale.Lang._GameRepairPage.PerProgressSubtitle2,
                                                   ConverterTool.SummarizeSizeSimple(ProgressAllSizeCurrent),
                                                   ConverterTool.SummarizeSizeSimple(ProgressAllSizeTotal)) + $" | {timeLeftString}";

            // Trigger update
            UpdateAll();
        }
    }

    internal virtual void UpdateRepairStatus(string activityStatus, string activityAll, bool isPerFileIndetermined)
    {
        lock (Status)
        {
            // Set repair activity status
            Status.ActivityStatus = activityStatus;
            Status.ActivityAll = activityAll;
            Status.IsProgressPerFileIndetermined = isPerFileIndetermined;
        }

        // Update status
        UpdateStatus();
    }
    #endregion

    #region ProgressEventHandlers - UpdateCache
    protected virtual void _httpClient_UpdateAssetProgress(int size, DownloadProgress downloadProgress)
    {
        Interlocked.Add(ref ProgressAllSizeCurrent, size);

        // Calculate the speed
        double speedAll = CalculateSpeed(size);

        if (!CheckIfNeedRefreshStopwatch())
        {
            return;
        }

        // Calculate the clamped speed and timelapse
        double   speedClamped = speedAll.ClampLimitedSpeedNumber();
        TimeSpan timeLeftSpan = ConverterTool.ToTimeSpanRemain(ProgressAllSizeTotal, ProgressAllSizeCurrent, speedClamped);
        double   percentage   = ConverterTool.ToPercentage(ProgressAllSizeTotal, ProgressAllSizeCurrent);

        // Update current progress percentages and speed
        lock (Progress)
        {
            Progress.ProgressAllPercentage = percentage;
        }

        // Update current activity status
        Status.IsProgressAllIndetermined = false;
        string timeLeftString             = string.Format(Locale.Lang._Misc.TimeRemainHMSFormat, timeLeftSpan);
        Status.ActivityAll               = string.Format(Locale.Lang._Misc.Downloading + ": {0}/{1} ", ProgressAllCountCurrent,
                                                         ProgressAllCountTotal)
                                           + string.Format($"({Locale.Lang._Misc.SpeedPerSec})",
                                                           ConverterTool.SummarizeSizeSimple(speedClamped))
                                           + $" | {timeLeftString}";

        // Trigger update
        UpdateAll();
    }
    #endregion

    #region ProgressEventHandlers - Patch
    protected virtual void RepairTypeActionPatching_ProgressChanged(object? sender, BinaryPatchProgress e)
    {
        lock (Progress)
        {
            Progress.ProgressPerFilePercentage = e.ProgressPercentage;
            Progress.ProgressAllSpeed = e.Speed;

            // Update current progress percentages
            Progress.ProgressAllPercentage = ProgressAllSizeCurrent != 0 ?
                ConverterTool.ToPercentage(ProgressAllSizeTotal, ProgressAllSizeCurrent) :
                0;
        }

        if (!CheckIfNeedRefreshStopwatch())
        {
            return;
        }

        lock (Status)
        {
            // Update current activity status
            Status.IsProgressAllIndetermined     = false;
            Status.IsProgressPerFileIndetermined = false;
            Status.ActivityPerFile               = string.Format(Locale.Lang._GameRepairPage.PerProgressSubtitle5,
                                                                 ConverterTool.SummarizeSizeSimple(Progress.ProgressAllSpeed));
            Status.ActivityAll                   = string.Format(Locale.Lang._GameRepairPage.PerProgressSubtitle2,
                                                                 ConverterTool.SummarizeSizeSimple(ProgressAllSizeCurrent),
                                                                 ConverterTool.SummarizeSizeSimple(ProgressAllSizeTotal));
        }

        // Trigger update
        UpdateAll();
    }
    #endregion

    #region ProgressEventHandlers - CRC/HashCheck
    protected virtual void UpdateProgressCrc(long read)
    {
        // Calculate speed
        double speedAll = CalculateSpeed(read);

        if (!CheckIfNeedRefreshStopwatch())
        {
            return;
        }

        lock (Progress)
        {
            // Update current progress percentages
            Progress.ProgressPerFilePercentage = ProgressPerFileSizeCurrent != 0 ?
                ConverterTool.ToPercentage(ProgressPerFileSizeTotal, ProgressPerFileSizeCurrent) :
                0;
            Progress.ProgressAllPercentage = ProgressAllSizeCurrent != 0 ?
                ConverterTool.ToPercentage(ProgressAllSizeTotal, ProgressAllSizeCurrent) :
                0;

            // Update the progress of total size
            Progress.ProgressPerFileSizeCurrent = ProgressPerFileSizeCurrent;
            Progress.ProgressPerFileSizeTotal   = ProgressPerFileSizeTotal;
            Progress.ProgressAllSizeCurrent     = ProgressAllSizeCurrent;
            Progress.ProgressAllSizeTotal       = ProgressAllSizeTotal;

            // Calculate current speed and update the status and progress speed
            Progress.ProgressAllSpeed = speedAll;

            // Calculate the timelapse
            Progress.ProgressAllTimeLeft = ConverterTool.ToTimeSpanRemain(ProgressAllSizeTotal, ProgressAllSizeCurrent, speedAll);
        }

        lock (Status)
        {
            // Set time estimation string
            string timeLeftString = string.Format(Locale.Lang._Misc.TimeRemainHMSFormat, Progress.ProgressAllTimeLeft);

            // Update current activity status
            Status.ActivityPerFile = string.Format(Locale.Lang._Misc.Speed, ConverterTool.SummarizeSizeSimple(Progress.ProgressAllSpeed));
            Status.ActivityAll = string.Format(Locale.Lang._GameRepairPage.PerProgressSubtitle2, 
                                                ConverterTool.SummarizeSizeSimple(ProgressAllSizeCurrent), 
                                                ConverterTool.SummarizeSizeSimple(ProgressAllSizeTotal)) + $" | {timeLeftString}";
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

        if (!CheckIfNeedRefreshStopwatch())
        {
            return;
        }

        lock (Progress)
        {
            // Update current progress percentages
            Progress.ProgressPerFilePercentage = ConverterTool.ToPercentage(totalReadSize, currentPosition);

            // Update the progress of total size
            Progress.ProgressPerFileSizeCurrent = currentPosition;
            Progress.ProgressPerFileSizeTotal   = totalReadSize;

            // Calculate current speed and update the status and progress speed
            Progress.ProgressAllSpeed = speedAll;

            // Calculate the timelapse
            Progress.ProgressAllTimeLeft = ConverterTool.ToTimeSpanRemain(totalReadSize, currentPosition, speedAll);
        }

        lock (Status)
        {
            // Set time estimation string
            string timeLeftString = string.Format(Locale.Lang._Misc.TimeRemainHMSFormat, Progress.ProgressAllTimeLeft);

            // Update current activity status
            Status.ActivityPerFile = string.Format(Locale.Lang._Misc.Speed, ConverterTool.SummarizeSizeSimple(Progress.ProgressAllSpeed));
            Status.ActivityAll     = string.Format(Locale.Lang._GameRepairPage.PerProgressSubtitle2, 
                                                   ConverterTool.SummarizeSizeSimple(currentPosition), 
                                                   ConverterTool.SummarizeSizeSimple(totalReadSize)) + $" | {timeLeftString}";
        }

        // Trigger update
        UpdateAll();
    }
    #endregion

    #region ProgressEventHandlers - SpeedCalculator and Refresh Interval Checker
    private const double ScOneSecond = 1000;
    private long _scLastTick = Environment.TickCount64;
    private long _scLastReceivedBytes;
    private double _scLastSpeed;

    protected double CalculateSpeed(long receivedBytes) => CalculateSpeed(receivedBytes, ref _scLastSpeed, ref _scLastReceivedBytes, ref _scLastTick);

    protected static double CalculateSpeed(long receivedBytes, ref double lastSpeedToUse, ref long lastReceivedBytesToUse, ref long lastTickToUse)
    {
        long   currentTick           = Environment.TickCount64 - lastTickToUse + 1;
        long   totalReceivedInSecond = Interlocked.Add(ref lastReceivedBytesToUse, receivedBytes);
        double speed                 = totalReceivedInSecond * ScOneSecond / currentTick;

        if (!(currentTick > ScOneSecond))
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
        if (currentTick <= RefreshInterval)
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
        _ = Interlocked.Add(ref ProgressAllSizeCurrent, read);

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
        Progress.ProgressAllSizeCurrent     = ProgressAllSizeCurrent;
        Progress.ProgressAllSizeTotal       = ProgressAllSizeTotal;
        Progress.ProgressPerFileSizeCurrent = ProgressPerFileSizeCurrent;
        Progress.ProgressPerFileSizeTotal   = ProgressPerFileSizeTotal;

        Progress.ProgressAllSpeed     = speedAll;
        Progress.ProgressPerFileSpeed = speedDownloadClamped;

        // Always change the status progress to determined
        Status.IsProgressAllIndetermined     = false;
        Status.IsProgressPerFileIndetermined = false;
        StatusChanged?.Invoke(this, Status);

        // Calculate percentage
        Progress.ProgressAllPercentage     = ConverterTool.ToPercentage(ProgressAllSizeTotal, ProgressAllSizeCurrent);
        Progress.ProgressPerFilePercentage = ConverterTool.ToPercentage(ProgressPerFileSizeTotal, ProgressPerFileSizeCurrent);
        Progress.ProgressAllTimeLeft       = ConverterTool.ToTimeSpanRemain(ProgressAllSizeTotal, ProgressAllSizeCurrent, speedAll);

        // Update progress
        ProgressChanged?.Invoke(this, Progress);

        // Update taskbar progress
        if (Status.IsCanceled || Status.IsCompleted)
        {
            WindowUtility.SetTaskBarState(TaskbarState.NoProgress);
        }
        else if (Status.IsProgressAllIndetermined)
        {
            WindowUtility.SetTaskBarState(TaskbarState.Indeterminate);
        }
        else if (Status.IsRunning)
        {
            WindowUtility.SetTaskBarState(TaskbarState.Normal);
            WindowUtility.SetProgressValue((ulong)(Progress.ProgressAllPercentage * 10), 1000);
        }
        else
        {
            WindowUtility.SetTaskBarState(TaskbarState.NoProgress);
        }
    }

    protected void UpdateSophonFileDownloadProgress(long downloadedWrite, long currentWrite)
    {
        _ = Interlocked.Add(ref ProgressPerFileSizeCurrent,               downloadedWrite);
        _ = Interlocked.Add(ref _sophonDownloadOnlyCurrentDownloadedBytes, currentWrite);
    }

    protected void UpdateSophonDownloadStatus(SophonAsset _)
    {
        Interlocked.Add(ref ProgressAllCountCurrent, 1);
        Status.ActivityStatus = $"{(IsSophonInUpdateMode
            ? Locale.Lang._Misc.Updating
            : Locale.Lang._Misc.Downloading)}: {string.Format(Locale.Lang._Misc.PerFromTo, ProgressAllCountCurrent,
                                                       ProgressAllCountTotal)}";

        UpdateStatus();
    }

    protected static void UpdateSophonLogHandler(object? sender, LogStruct e)
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
        Logger.LogWriteLine(e.Message, logPair.logType, logPair.isNeedWriteLog);
    }
    #endregion

    #region ProgressEventHandlers - Zip Extractor
    
    private void ZipProgressAdapter(object? sender, ExtractProgressProp e)
    {
        // Calculate the speed
        long read  = (long)e.Read;
        double speed = CalculateSpeed(read);
        Interlocked.Add(ref ProgressAllSizeCurrent, read);

        if (!CheckIfNeedRefreshStopwatch())
        {
            return;
        }

        // Increment current total size
        lock (Progress)
        {
            // Assign per file size
            ProgressPerFileSizeCurrent = (long)e.TotalRead;
            ProgressPerFileSizeTotal   = (long)e.TotalSize;

            lock (Progress)
            {
                // Assign local sizes to progress
                Progress.ProgressPerFileSizeCurrent = ProgressPerFileSizeCurrent;
                Progress.ProgressPerFileSizeTotal   = ProgressPerFileSizeTotal;
                Progress.ProgressAllSizeCurrent     = ProgressAllSizeCurrent;
                Progress.ProgressAllSizeTotal       = ProgressAllSizeTotal;

                // Calculate percentage and timelapse
                double percentageAll = ConverterTool.ToPercentage(ProgressAllSizeTotal, ProgressAllSizeCurrent);
                double percentagePerFile = ConverterTool.ToPercentage(ProgressPerFileSizeTotal, ProgressPerFileSizeCurrent);
                TimeSpan timeSpan = ConverterTool.ToTimeSpanRemain(ProgressAllSizeTotal, ProgressAllSizeCurrent, speed);

                Progress.ProgressPerFilePercentage = percentagePerFile;
                Progress.ProgressAllPercentage     = percentageAll;
                Progress.ProgressAllTimeLeft       = timeSpan;
                Progress.ProgressAllSpeed          = speed;
            }

            UpdateAll();
        }
    }

    #endregion

    #region ProgressEventHandlers - Download

    protected virtual void HttpClientDownloadProgressAdapter(int read, DownloadProgress downloadProgress)
    {
        // Set the progress bar not undetermined
        Status.IsProgressPerFileIndetermined = false;
        Status.IsProgressAllIndetermined = false;

        // Increment the total current size if status is not merging
        Interlocked.Add(ref ProgressAllSizeCurrent, read);

        // Calculate the speed
        double speedAll = CalculateSpeed(read);

        if (!CheckIfNeedRefreshStopwatch())
        {
            return;
        }

        lock (Progress)
        {
            // Assign speed with clamped value
            double speedClamped = speedAll.ClampLimitedSpeedNumber();

            // Assign local sizes to progress
            Progress.ProgressAllSizeCurrent = ProgressAllSizeCurrent;
            Progress.ProgressAllSizeTotal = ProgressAllSizeTotal;
            Progress.ProgressAllSpeed = speedClamped;
            Progress.ProgressAllPercentage = ConverterTool.ToPercentage(ProgressAllSizeTotal, ProgressAllSizeCurrent);
            Progress.ProgressAllTimeLeft = ConverterTool.ToTimeSpanRemain(ProgressAllSizeTotal, ProgressAllSizeCurrent, speedClamped);

            // Update the status of per file size and current progress from Http client
            Progress.ProgressPerFileSizeCurrent = downloadProgress.BytesDownloaded;
            Progress.ProgressPerFileSizeTotal = downloadProgress.BytesTotal;
            Progress.ProgressPerFileSpeed = speedClamped;
            Progress.ProgressPerFilePercentage = ConverterTool.ToPercentage(downloadProgress.BytesTotal, downloadProgress.BytesDownloaded);
        }
        // Update the status
        UpdateAll();
    }

    protected virtual void HttpClientDownloadProgressAdapter(object sender, DownloadEvent e)
    {
        // Set the progress bar not undetermined
        Status.IsProgressPerFileIndetermined = false;
        Status.IsProgressAllIndetermined = false;

        if (e.State != DownloadState.Merging)
        {
            // Increment the total current size if status is not merging
            Interlocked.Add(ref ProgressAllSizeCurrent, e.Read);
        }

        // Calculate the speed
        double speedAll = CalculateSpeed(e.Read);

        if (!CheckIfNeedRefreshStopwatch())
        {
            return;
        }

        if (e.State != DownloadState.Merging)
        {
            lock (Progress)
            {
                // Assign local sizes to progress
                Progress.ProgressPerFileSizeCurrent = ProgressPerFileSizeCurrent;
                Progress.ProgressPerFileSizeTotal = ProgressPerFileSizeTotal;
                Progress.ProgressAllSizeCurrent = ProgressAllSizeCurrent;
                Progress.ProgressAllSizeTotal = ProgressAllSizeTotal;

                // Calculate the speed
                Progress.ProgressAllSpeed = speedAll;

                // Calculate percentage
                Progress.ProgressPerFilePercentage = ConverterTool.ToPercentage(ProgressPerFileSizeTotal, ProgressPerFileSizeCurrent);
                Progress.ProgressAllPercentage = ConverterTool.ToPercentage(ProgressAllSizeTotal, ProgressAllSizeCurrent);
                // Calculate the timelapse
                Progress.ProgressAllTimeLeft = ConverterTool.ToTimeSpanRemain(ProgressAllSizeTotal, ProgressAllSizeCurrent, speedAll);
            }
        }
        else
        {
            // If merging, show per file indicator explicitly
            // and then update the normal progress
            Status.IsIncludePerFileIndicator = true;

            // If status is merging, then use progress for speed and timelapse from Http client
            // and set the rest from the base class
            lock (Progress)
            {
                Progress.ProgressAllTimeLeft = e.TimeLeft;
                Progress.ProgressAllSpeed = speedAll;

                Progress.ProgressPerFileSizeCurrent = ProgressPerFileSizeCurrent;
                Progress.ProgressPerFileSizeTotal = ProgressPerFileSizeTotal;
                Progress.ProgressAllSizeCurrent = ProgressAllSizeCurrent;
                Progress.ProgressAllSizeTotal = ProgressAllSizeTotal;
                Progress.ProgressAllPercentage = ConverterTool.ToPercentage(ProgressAllSizeTotal, ProgressAllSizeCurrent);
            }
        }

        // Update the status of per file size and current progress from Http client
        ProgressPerFileSizeCurrent = e.SizeDownloaded;
        ProgressPerFileSizeTotal = e.SizeToBeDownloaded;
        lock (Progress) Progress.ProgressPerFilePercentage = e.ProgressPercentage;

        // Update the status
        UpdateAll();
    }
    #endregion

    #region BaseTools
    internal async Task DoCopyStreamProgress(Stream source, Stream target, long? estimatedSize = null, CancellationToken token = default)
    {
        // ReSharper disable once ConstantNullCoalescingCondition
        long inputSize = estimatedSize != null ? estimatedSize ?? 0 : source.Length;
        long currentPos = 0;

        bool isLastPerfileStateIndetermined = Status.IsProgressPerFileIndetermined;
        bool isLastTotalStateIndetermined = Status.IsProgressAllIndetermined;

        Status.IsProgressPerFileIndetermined = false;
        Status.IsProgressAllIndetermined = true;

        byte[] buffer = ArrayPool<byte>.Shared.Rent(16 << 10);
        try
        {
            int read;
            while ((read = await source.ReadAsync(buffer, token)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), token);
                currentPos += read;
                UpdateProgressCopyStream(currentPos, read, inputSize);
            }
        }
        finally
        {
            Status.IsProgressPerFileIndetermined = isLastPerfileStateIndetermined;
            Status.IsProgressAllIndetermined = isLastTotalStateIndetermined;
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    protected static string EnsureCreationOfDirectory(string str)
        => StreamExtension.EnsureCreationOfDirectory(str).FullName;

    protected static string EnsureCreationOfDirectory(FileInfo str)
        => str.EnsureCreationOfDirectory().FullName;

    protected static void TryUnassignReadOnlyFiles(string dirPath)
    {
        DirectoryInfo directoryInfo = new(dirPath);
        if (!directoryInfo.Exists)
        {
            return;
        }

        // Iterate every file and set the read-only flag to false
        foreach (FileInfo file in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            _ = file.StripAlternateDataStream().EnsureNoReadOnly();
        }
    }

    protected static void TryUnassignReadOnlyFileSingle(string filePath)
    {
        FileInfo fileInfo = new(filePath);
        _ = fileInfo.EnsureNoReadOnly();
    }

    protected static void TryDeleteReadOnlyDir(string dirPath)
    {
        DirectoryInfo dirInfo = new DirectoryInfo(dirPath).EnsureNoReadOnly(out bool isDirExist);
        if (!isDirExist)
            return;

        try
        {
            // Remove read-only attribute from all files and subdirectories
            foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                file.StripAlternateDataStream().EnsureNoReadOnly();
            }
    
            foreach (var subDir in dirInfo.EnumerateDirectories("*", SearchOption.AllDirectories))
            {
                subDir.EnsureNoReadOnly();
            }
    
            // Delete the directory and all its contents
            dirInfo.Refresh();
            dirInfo.Delete(true);
        }
        catch (Exception ex)
        {
            SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
            Logger.LogWriteLine($"Failed while deleting parent dir: {dirPath}\r\n{ex}", LogType.Warning, true);
        } // Suppress errors
    }

    protected static void TryDeleteReadOnlyFile(string filePath)
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
            Logger.LogWriteLine($"Failed to delete file: {fileInfo.FullName}\r\n{ex}", LogType.Error, true);
        }
    }

    protected static void MoveFolderContent(string sourcePath, string destPath)
    {
        // Get the source folder path length + 1
        int dirLength = sourcePath.Length + 1;

        // Initialize paths and error status
        bool errorOccured = false;

        // Enumerate files inside of source path
        DirectoryInfo directoryInfoSource = new(sourcePath);
        if (!directoryInfoSource.Exists)
        {
            throw new DirectoryNotFoundException($"Cannot find source directory on this path: {sourcePath}");
        }
        foreach (FileInfo fileInfo in directoryInfoSource.EnumerateFiles("*", SearchOption.AllDirectories)
            .EnumerateNoReadOnly())
        {
            // Get the relative path of the file from source path
            ReadOnlySpan<char> relativePath = fileInfo.FullName.AsSpan()[dirLength..];
            // Get the absolute path for destination
            var destFilePath = Path.Combine(destPath, relativePath.ToString());
            // Get folder path for destination
            var destFolderPath = Path.GetDirectoryName(destFilePath);

            // Create the destination folder if not exist
            if (!string.IsNullOrEmpty(destFolderPath))
                _ = Directory.CreateDirectory(destFolderPath);

            try
            {
                // Try moving the file
                Logger.LogWriteLine($"Moving \"{relativePath.ToString()}\" to \"{destFolderPath}\"", LogType.Default, true);
                fileInfo.MoveTo(destFilePath, true);
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                // If failed, flag ErrorOccured as true and skip to the next file 
                Logger.LogWriteLine($"Error while moving \"{relativePath.ToString()}\" to \"{destFolderPath}\"\r\nException: {ex}", LogType.Error, true);
                errorOccured = true;
            }
        }

        if (errorOccured)
        {
            return;
        }

        try
        {
            // If no error occurred, then delete the source folder
            directoryInfoSource.Refresh();
            directoryInfoSource.Delete(true);
        }
        catch (Exception ex)
        {
            SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
            // If failed, flag ErrorOccured as true and skip the source directory deletion 
            Logger.LogWriteLine($"Error while deleting source directory \"{directoryInfoSource.FullName}\"\r\nException: {ex}", LogType.Error, true);
        }
    }

    protected virtual void ResetStatusAndProgress()
    {
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
        Token?.Dispose();
        Token = new CancellationTokenSourceWrapper();

        lock (Status)
        {
            // Show the asset entry panel
            Status.IsAssetEntryPanelShow = false;

            // Reset all total activity status
            Status.ActivityStatus            = Locale.Lang._GameRepairPage.StatusNone;
            Status.ActivityAll               = Locale.Lang._GameRepairPage.StatusNone;
            Status.IsProgressAllIndetermined = false;

            // Reset all per-file activity status
            Status.ActivityPerFile               = Locale.Lang._GameRepairPage.StatusNone;
            Status.IsProgressPerFileIndetermined = false;

            // Reset all status indicators
            Status.IsAssetEntryPanelShow = false;
            Status.IsCompleted           = false;
            Status.IsCanceled            = false;

            // Reset all total activity progress
            lock (Progress)
            {
                Progress.ProgressPerFilePercentage = 0;
                Progress.ProgressAllPercentage     = 0;
                Progress.ProgressPerFileSpeed      = 0;
                Progress.ProgressAllSpeed          = 0;
            }
            // Reset all inner counter
            ProgressAllCountCurrent      = 0;
            ProgressAllCountTotal        = 0;
            ProgressAllSizeCurrent       = 0;
            ProgressAllSizeTotal         = 0;
            ProgressPerFileSizeCurrent   = 0;
            ProgressPerFileSizeTotal     = 0;
        }
    }

    protected async Task FetchBilibiliSdk(CancellationToken token)
    {
        // Check whether the sdk is not null
        string gameBiz = GameVersionManager.LauncherApi?.GameBiz ?? "";
        string gameId  = GameVersionManager.LauncherApi?.GameId ?? "";
        if (!(GameVersionManager
             .LauncherApi?
             .LauncherGameResourceSdk?
             .Data?
             .TryFindByBizOrId(gameBiz, gameId, out HypChannelSdkData? sdkData) ?? false))
        {
            return;
        }

        // Set total activity string as "Loading Indexes..."
        Status.ActivityStatus = Locale.Lang._GameRepairPage.Status2;
        UpdateStatus();

        string gamePath = GamePath;
        string url      = sdkData.SdkPackageDetail?.Url ?? throw new NullReferenceException();

        // Create ZipArchiveReader and get the remote stream of the zip file
        ZipArchiveReader reader = await ZipArchiveReader.CreateFromAsync(url, token);
        HttpClient       client = FallbackCDNUtil.GetGlobalHttpClient(true);

        await Parallel.ForEachAsync(reader.Where(x => !x.IsDirectory),
                                    token,
                                    Impl);

        return;

        async ValueTask Impl(ZipArchiveEntry entry, CancellationToken innerToken)
        {
            // If the entry is the "sdk_pkg_version", then override the info to sdk_pkg_version
            string sdkDllPath = Path.Combine(gamePath, entry.Filename)
                                    .NormalizePath();

            // Assign FileInfo to sdkDllPath
            FileInfo sdkDllFile = new FileInfo(sdkDllPath)
                                 .EnsureCreationOfDirectory()
                                 .StripAlternateDataStream()
                                 .EnsureNoReadOnly();

            // Do check if sdkDllFile is not null
            // Try to create the file if not exist or open an existing one
            await using Stream sdkDllStream = sdkDllFile.Open(FileMode.OpenOrCreate);
            // Get the hash from the stream
            byte[] hashByte = await HashUtility<Crc32>.ThreadSafe.GetHashFromStreamAsync(sdkDllStream, token: innerToken);
            uint   hashInt  = BitConverter.ToUInt32(hashByte);

            // If the hash is the same, then skip
            if (hashInt == entry.Crc32)
            {
                return;
            }

            await using Stream entryStream = await entry.OpenStreamFromAsync(CreateStreamFromPosUrl, innerToken);
            // Reset the SDK DLL stream pos and write the data
            sdkDllStream.Position = 0;
            await entryStream.CopyToAsync(sdkDllStream, innerToken);
        }

        async Task<Stream> CreateStreamFromPosUrl(long? offset, long? length, CancellationToken innerToken)
        {
            HttpRequestMessage requestMessage = new(HttpMethod.Get, url);
            requestMessage.Headers.Range = new RangeHeaderValue(offset, length);

            HttpResponseMessage response = await client.SendAsync(requestMessage,
                                                                  HttpCompletionOption.ResponseHeadersRead,
                                                                  innerToken);

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync(innerToken);
        }
    }

    protected virtual void EliminatePluginAssetIndex<T>(List<T>          assetIndex,
                                                        Func<T, string?> localNameSelector,
                                                        Func<T, string>  remoteNameSelector)
    {
        string gameBiz = GameVersionManager.LauncherApi?.GameBiz ?? "";
        string gameId  = GameVersionManager.LauncherApi?.GameId ?? "";

        if (!(GameVersionManager
             .LauncherApi?
             .LauncherGameResourcePlugin?
             .Data?
             .TryFindByBizOrId(gameBiz, gameId, out HypResourcePluginData? data) ?? false))
        {
            return;
        }

        data.Plugins.ForEach(Impl);

        return;

        void Impl(HypPluginPackageInfo plugin)
        {
            if (plugin.PluginPackage?.PackageAssetValidationList == null)
            {
                return;
            }

            assetIndex.RemoveAll(asset =>
                                 {
                                     bool r = plugin
                                             .PluginPackage
                                             .PackageAssetValidationList
                                             .Any(validate => validate.FilePath != null &&
                                                              (localNameSelector(asset)?.Contains(validate.FilePath) ??
                                                               remoteNameSelector(asset).Contains(validate.FilePath)));
                                     if (r)
                                     {
                                         Logger.LogWriteLine($"[EliminatePluginAssetIndex] Removed: {localNameSelector(asset)}", LogType.Warning,
                                                      true);
                                     }
                                     return r;
                                 });
        }
    }

    protected virtual async Task<T> TryRunExamineThrow<T>(Task<T> action)
    {
        await TryRunExamineThrow((Task)action);

        if (action.IsCompletedSuccessfully)
        {
            return action.Result;
        }

        if ((action.IsFaulted ||
             action.IsCanceled) &&
            action.Exception != null)
        {
            throw action.Exception;
        }

        throw new InvalidOperationException();
    }

    protected virtual async Task TryRunExamineThrow(Task task)
    {
        // Define if the status is still running
        Status.IsRunning   = true;
        Status.IsCompleted = false;
        Status.IsCanceled  = false;

        try
        {
            // Run the task
            await task;

            Status.IsCompleted = true;
        }
        catch (TaskCanceledException)
        {
            // If a cancellation was thrown, then set IsCanceled as true
            Status.IsCompleted = false;
            Status.IsCanceled  = true;
            throw;
        }
        catch (OperationCanceledException)
        {
            // If a cancellation was thrown, then set IsCanceled as true
            Status.IsCompleted = false;
            Status.IsCanceled  = true;
            throw;
        }
        catch (Exception)
        {
            // Except, if the other exception was thrown, then set both IsCompleted
            // and IsCanceled as false.
            Status.IsCompleted = false;
            Status.IsCanceled  = false;
            throw;
        }
        finally
        {
            if (Status is { IsCompleted: false })
            {
                WindowUtility.SetTaskBarState(TaskbarState.Error);
            }
            else
            {
                WindowUtility.SetTaskBarState(TaskbarState.NoProgress);
            }

            Status.IsRunning = false;
        }
    }

    protected virtual async ValueTask<T> TryRunExamineThrow<T>(ValueTask<T> task)
    {
        // Define if the status is still running
        Status.IsRunning   = true;
        Status.IsCompleted = false;
        Status.IsCanceled  = false;

        try
        {
            // Run the task
            T result = await task;

            Status.IsCompleted = true;
            return result;
        }
        catch (TaskCanceledException)
        {
            // If a cancellation was thrown, then set IsCanceled as true
            Status.IsCompleted = false;
            Status.IsCanceled  = true;
            throw;
        }
        catch (OperationCanceledException)
        {
            // If a cancellation was thrown, then set IsCanceled as true
            Status.IsCompleted = false;
            Status.IsCanceled  = true;
            throw;
        }
        catch (Exception)
        {
            // Except, if the other exception was thrown, then set both IsCompleted
            // and IsCanceled as false.
            Status.IsCompleted = false;
            Status.IsCanceled  = false;
            throw;
        }
        finally
        {
            if (Status is { IsCompleted: false })
            {
                WindowUtility.SetTaskBarState(TaskbarState.Error);
            }
            else
            {
                WindowUtility.SetTaskBarState(TaskbarState.NoProgress);
            }

            Status.IsRunning = false;
        }
    }

    private void SetFoundToTotalValue()
    {
        // Assign found count and size to total count and size
        ProgressAllCountTotal = ProgressAllCountFound;
        ProgressAllSizeTotal = ProgressAllSizeFound;

        // Reset found count and size
        ProgressAllCountFound = 0;
        ProgressAllSizeFound = 0;
    }

    protected bool SummarizeStatusAndProgress(IList assetIndex, string msgIfFound, string msgIfClear)
    {
        // Reset status and progress properties
        ResetStatusAndProgressProperty();

        // Assign found value to total value
        SetFoundToTotalValue();

        // Set check if broken asset is found or not
        bool isBrokenFound = assetIndex.Count > 0;

        // Set status
        Status.IsAssetEntryPanelShow = isBrokenFound;
        Status.IsCompleted           = true;
        Status.IsCanceled            = false;
        Status.ActivityStatus        = isBrokenFound ? msgIfFound : msgIfClear;

        // Update status and progress
        UpdateAll();

        // Return broken asset check
        return isBrokenFound;
    }

    protected virtual bool IsArrayMatch(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target) => source.SequenceEqual(target);

    protected virtual Task RunDownloadTask(long assetSize,
                                           FileInfo assetPath,
                                           string? assetURL,
                                           DownloadClient downloadClient,
                                           DownloadProgressDelegate downloadProgress,
                                           CancellationToken token,
                                           bool isOverwrite = true)
        => RunDownloadTask(assetSize,
                           assetPath,
                           assetURL,
                           null,
                           downloadClient,
                           downloadProgress,
                           token,
                           isOverwrite);

    protected virtual async Task RunDownloadTask(long                     assetSize,
                                                 FileInfo                 assetPath,
                                                 string?                  assetURL,
                                                 string?                  secondaryURL,
                                                 DownloadClient           downloadClient,
                                                 DownloadProgressDelegate downloadProgress,
                                                 CancellationToken        token,
                                                 bool                     isOverwrite = true)
    {
        bool retrySecondary = false;
    StartOver:
        // Assign secondary URL if primaryAsset is null
        assetURL ??= secondaryURL ?? "";

        // Throw if both assetURL and secondaryURL are null
        if (string.IsNullOrEmpty(assetURL))
        {
            throw new InvalidOperationException("Both assetURL and secondaryURL cannot be empty! You must define one of them!");
        }

        // For any instances that uses Burst Download and if the speed limiter is null when
        // _isBurstDownloadEnabled set to false, then create the speed limiter instance
        bool isUseSelfSpeedLimiter = !IsBurstDownloadEnabled;
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
            await downloadClient.DownloadAsync(assetURL,
                                               assetPath,
                                               isOverwrite,
                                               sessionChunkSize: LauncherConfig.DownloadChunkSize,
                                               progressDelegateAsync: downloadProgress,
                                               cancelToken: token,
                                               downloadSpeedLimiter: downloadSpeedLimiter
                                              );
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode != HttpStatusCode.NotFound ||
                retrySecondary ||
                string.IsNullOrEmpty(secondaryURL))
            {
                throw;
            }

            retrySecondary = true;
            assetURL       = null;
            goto StartOver;
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
    #endregion

    #region Stream and Archive Tools
    /// <summary>
    /// <inheritdoc cref="StreamExtension.NaivelyOpenFileStreamAsync(FileInfo, FileMode, FileAccess, FileShare, FileOptions)"/>
    /// </summary>
    internal static ValueTask<FileStream> NaivelyOpenFileStreamAsync(FileInfo info, FileMode fileMode, FileAccess fileAccess, FileShare fileShare)
        => info.NaivelyOpenFileStreamAsync(fileMode, fileAccess, fileShare);

#if USENEWZIPDECOMPRESS
    protected virtual long GetArchiveUncompressedSizeManaged(Stream archiveStream)
    {
        ZipArchiveReader archive = ZipArchiveReader.CreateFrom(archiveStream);
        return archive.Sum(x => x.Size);
    }
#endif

    protected virtual long GetArchiveUncompressedSizeNative7Zip(Stream archiveStream)
    {
        using ArchiveFile archive = ArchiveFile.Create(archiveStream, true);
        if (archive.Entries.Count > 1 << 10)
        {
            return archive.Entries.Select(x => (long)x.Size).ToArray().Sum();
        }

        return archive.Entries.Sum(x => (long)x.Size);
    }

    protected virtual long GetSingleOrSegmentedUncompressedSize(GameInstallPackage asset)
    {
        using Stream stream = GetSingleOrSegmentedDownloadStream(asset);

#if USENEWZIPDECOMPRESS
        if (LauncherConfig.IsEnforceToUse7zipOnExtract)
        {
            return GetArchiveUncompressedSizeNative7Zip(stream);
        }

        if ((asset.PathOutput.EndsWith(".zip",     StringComparison.OrdinalIgnoreCase) ||
             asset.PathOutput.EndsWith(".zip.001", StringComparison.OrdinalIgnoreCase)) &&
            !IsAllowExtractCorruptZip)
        {
            return GetArchiveUncompressedSizeManaged(stream);
        }

        return GetArchiveUncompressedSizeNative7Zip(stream);
#else
        return GetArchiveUncompressedSizeNative7Zip(stream);
#endif
    }

    protected virtual Stream GetSingleOrSegmentedDownloadStream(GameInstallPackage asset)
    {
        return asset.GetReadStream(DownloadThreadCount);
    }

    protected virtual void DeleteSingleOrSegmentedDownloadStream(GameInstallPackage asset)
    {
        asset.DeleteFile(DownloadThreadCount);
    }


#if USENEWZIPDECOMPRESS
    protected virtual async Task ExtractUsingManagedZip(
        Func<Stream>      streamFactory,
        string            outputDir,
        CancellationToken token)
    {
        // Use ThreadObjectPool to cache the Streams and re-using it.
        using ThreadObjectPool<Stream> streamPool = new(
            () => CreateStreamWithPos(0, 0, token),
            capacity: ThreadCount * 2, // Double the thread count for spare capacity
            isDisposeObjects: true);

        int threadCounts = ThreadCount;
        Stream zipInitialStream = await streamPool.GetOrCreateObjectAsync(token);
        ZipArchiveReader archive;
        
        try
        {
            archive = await ZipArchiveReader.CreateFromAsync(zipInitialStream, token);
        }
        finally
        {
            streamPool.Return(zipInitialStream);
        }

        // Run the workers
        await Parallel.ForEachAsync(archive,
                                    new ParallelOptions
                                    {
                                        CancellationToken      = token,
                                        MaxDegreeOfParallelism = threadCounts
                                    },
                                    Impl);

        return;

        async ValueTask Impl(ZipArchiveEntry entry, CancellationToken innerToken)
        {
            string outputPath = Path.Combine(outputDir, entry.Filename);
            if (entry.IsDirectory)
            {
                _ = Directory.CreateDirectory(outputPath);
                return;
            }

            Stream reusableStream = await streamPool.GetOrCreateObjectAsync(innerToken);
            try
            {
                await ExtractUsingManagedZipWorker(entry,
                                                   reusableStream,
                                                   outputDir,
                                                   innerToken);
            }
            finally
            {
                streamPool.Return(reusableStream);
            }
        }

        Stream CreateStreamWithPos(long? start, long? _, CancellationToken innerToken)
        {
            Stream stream = streamFactory();
            stream.Position = start ?? 0;
            return stream;
        }
    }

    protected virtual async ValueTask ExtractUsingManagedZipWorker(
        ZipArchiveEntry   entry,
        Stream            sourceStream,
        string            outputDir,
        CancellationToken token)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(128 << 10);

        try
        {
            string outputPath = Path.Combine(outputDir, entry.Filename);
            FileInfo outputFile = new FileInfo(outputPath).EnsureCreationOfDirectory()
                                                          .StripAlternateDataStream()
                                                          .EnsureNoReadOnly();

            await using Stream deflateStream = await entry.OpenStreamFromAsync(sourceStream, false, false, token);
            await using FileStream fileStream = outputFile.Open(FileMode.Create,
                                                                  FileAccess.Write,
                                                                  FileShare.Write);

            Task runningTask = Task.Factory.StartNew(() => StartWriteInner(buffer, fileStream, deflateStream, token),
                                                     token,
                                                     TaskCreationOptions.DenyChildAttach,
                                                     TaskScheduler.Default);

            await runningTask.ConfigureAwait(false);
            return;

            void StartWriteInner(Span<byte> bufferInner, FileStream outputStream, Stream inputStream, CancellationToken cancellationTokenInner)
            {
                int read;

                // Perform async read
                while ((read = inputStream.Read(bufferInner)) > 0)
                {
                    // Throw if cancellation requested
                    cancellationTokenInner.ThrowIfCancellationRequested();

                    // Perform sync write
                    outputStream.Write(bufferInner[..read]);

                    // Increment total size
                    Interlocked.Add(ref ProgressAllSizeCurrent, read);
                    Interlocked.Add(ref ProgressPerFileSizeCurrent, read);

                    // Calculate the speed
                    double speed = CalculateSpeed(read);

                    if (!CheckIfNeedRefreshStopwatch())
                    {
                        continue;
                    }

                    // Assign local sizes to progress
                    lock (Progress)
                    {
                        Progress.ProgressPerFileSizeCurrent = ProgressPerFileSizeCurrent;
                        Progress.ProgressPerFileSizeTotal = ProgressPerFileSizeTotal;
                        Progress.ProgressAllSizeCurrent = ProgressAllSizeCurrent;
                        Progress.ProgressAllSizeTotal = ProgressAllSizeTotal;

                        // Calculate percentage and timelapse
                        double percentageAll = ConverterTool.ToPercentage(ProgressAllSizeTotal, ProgressAllSizeCurrent);
                        double percentagePerFile = ConverterTool.ToPercentage(ProgressPerFileSizeTotal, ProgressPerFileSizeCurrent);
                        TimeSpan timeSpan = ConverterTool.ToTimeSpanRemain(ProgressAllSizeTotal, ProgressAllSizeCurrent, speed);

                        Progress.ProgressPerFilePercentage = percentagePerFile;
                        Progress.ProgressAllPercentage = percentageAll;
                        Progress.ProgressAllTimeLeft = timeSpan;
                        Progress.ProgressAllSpeed = speed;
                    }

                    UpdateAll();
                }
            }

        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
#endif

    protected virtual async Task ExtractUsingNative7Zip(
        Func<Stream>      streamFactory,
        string            outputDir,
        CancellationToken token)
    {
        // Start Async Thread
        // Since the ArchiveFile (especially with the callbacks) can't run under
        // different thread, so the async call will be called at the start
        Stream?      stream      = null;
        ArchiveFile? archiveFile = null;

        try
        {
            // Load the zip
            stream      = streamFactory();
            archiveFile = ArchiveFile.Create(stream, true);

            // Start extraction
            archiveFile.ExtractProgress += ZipProgressAdapter;
            await archiveFile.ExtractAsync(e => Path.Combine(outputDir, e.FileName!),
                                           true,
                                           BufferMediumLength,
                                           token);
        }
        finally
        {
            if (archiveFile != null)
            {
                archiveFile.ExtractProgress -= ZipProgressAdapter;
                archiveFile.Dispose();
            }

            if (stream != null)
            {
                await stream.DisposeAsync();
            }
        }
    }
    #endregion

    #region HashTools
    protected virtual Task<byte[]> GetCryptoHashAsync<T>(
        string            filePath,
        byte[]?           hmacKey             = null,
        bool              updateProgress      = true,
        bool              updateTotalProgress = true,
        CancellationToken token               = default)
        where T : HashAlgorithm =>
        GetCryptoHashAsync<T>(new FileInfo(filePath),
                              hmacKey,
                              updateProgress,
                              updateTotalProgress,
                              token);

    protected virtual async Task<byte[]> GetCryptoHashAsync<T>(
        FileInfo          fileInfo,
        byte[]?           hmacKey             = null,
        bool              updateProgress      = true,
        bool              updateTotalProgress = true,
        CancellationToken token               = default)
        where T : HashAlgorithm
    {
        await using FileStream fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        return await GetCryptoHashAsync<T>(fileStream, hmacKey, updateProgress, updateTotalProgress, token);
    }

    protected virtual Task<byte[]> GetCryptoHashAsync<T>(
        Stream            stream,
        byte[]?           hmacKey             = null,
        bool              updateProgress      = true,
        bool              updateTotalProgress = true,
        CancellationToken token               = default)
        where T : HashAlgorithm =>
        CryptoHashUtility<T>.ThreadSafe
                            .GetHashFromStreamAsync(stream,
                                                    read =>
                                                        UpdateHashReadProgress(
                                                         read,
                                                         updateProgress,
                                                         updateTotalProgress),
                                                    hmacKey,
                                                    stream.Length.GetFileStreamBufferSize(),
                                                    token);

    protected virtual byte[] GetCryptoHash<T>(
        string            filePath,
        byte[]?           hmacKey             = null,
        bool              updateProgress      = true,
        bool              updateTotalProgress = true,
        CancellationToken token               = default)
        where T : HashAlgorithm =>
        GetCryptoHash<T>(new FileInfo(filePath), hmacKey, updateProgress, updateTotalProgress, token);

    protected virtual byte[] GetCryptoHash<T>(
        FileInfo          fileInfo,
        byte[]?           hmacKey             = null,
        bool              updateProgress      = true,
        bool              updateTotalProgress = true,
        CancellationToken token               = default)
        where T : HashAlgorithm
    {
        using FileStream fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        return GetCryptoHash<T>(fileStream, hmacKey, updateProgress, updateTotalProgress, token);
    }

    protected virtual byte[] GetCryptoHash<T>(
        Stream            stream,
        byte[]?           hmacKey             = null,
        bool              updateProgress      = true,
        bool              updateTotalProgress = true,
        CancellationToken token               = default)
        where T : HashAlgorithm =>
        CryptoHashUtility<T>.ThreadSafe
                            .GetHashFromStream(stream,
                                               read =>
                                                   UpdateHashReadProgress(read,
                                                                          updateProgress,
                                                                          updateTotalProgress),
                                               hmacKey,
                                               stream.Length.GetFileStreamBufferSize(),
                                               false,
                                               token);

    protected virtual Task<byte[]> GetHashAsync<T>(
        string            filePath,
        bool              updateProgress      = true,
        bool              updateTotalProgress = true,
        CancellationToken token               = default)
        where T : NonCryptographicHashAlgorithm, new() =>
        GetHashAsync<T>(new FileInfo(filePath),
                        updateProgress,
                        updateTotalProgress,
                        token);

    protected virtual async Task<byte[]> GetHashAsync<T>(
        FileInfo          fileInfo,
        bool              updateProgress      = true,
        bool              updateTotalProgress = true,
        CancellationToken token               = default)
        where T : NonCryptographicHashAlgorithm, new()
    {
        await using FileStream fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        return await GetHashAsync<T>(fileStream, updateProgress, updateTotalProgress, token);
    }

    protected virtual Task<byte[]> GetHashAsync<T>(
        Stream            stream,
        bool              updateProgress      = true,
        bool              updateTotalProgress = true,
        CancellationToken token               = default)
        where T : NonCryptographicHashAlgorithm, new() =>
        HashUtility<T>.ThreadSafe
                      .GetHashFromStreamAsync(stream,
                                              read =>
                                                  UpdateHashReadProgress(read,
                                                                         updateProgress,
                                                                         updateTotalProgress),
                                              stream.Length.GetFileStreamBufferSize(),
                                              token);

    protected virtual byte[] GetHash<T>(
        string            filePath,
        bool              updateProgress      = true,
        bool              updateTotalProgress = true,
        CancellationToken token               = default)
        where T : NonCryptographicHashAlgorithm, new()
        => GetHash<T>(new FileInfo(filePath),
                      updateProgress,
                      updateTotalProgress,
                      token);

    protected virtual byte[] GetHash<T>(
        FileInfo          fileInfo,
        bool              updateProgress      = true,
        bool              updateTotalProgress = true,
        CancellationToken token               = default)
        where T : NonCryptographicHashAlgorithm, new()
    {
        using FileStream fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        return GetHash<T>(fileStream, updateProgress, updateTotalProgress, token);
    }

    protected virtual byte[] GetHash<T>(
        Stream            stream,
        bool              updateProgress      = true,
        bool              updateTotalProgress = true,
        CancellationToken token               = default)
        where T : NonCryptographicHashAlgorithm, new()
        => HashUtility<T>.ThreadSafe
                         .GetHashFromStream(stream,
                                            read =>
                                                UpdateHashReadProgress(read,
                                                                       updateProgress,
                                                                       updateTotalProgress),
                                            stream.Length.GetFileStreamBufferSize(),
                                            token);

    protected void UpdateHashReadProgress(int read, bool updateProgress, bool updateTotalProgress)
    {
        // If progress update is not allowed, then return
        if (!updateProgress)
        {
            return;
        }

        // Otherwise, perform the progress update
        // Increment total size counter if allowed
        if (updateTotalProgress)
            Interlocked.Add(ref ProgressAllSizeCurrent, read);

        // Increment per file size counter
        Interlocked.Add(ref ProgressPerFileSizeCurrent, read);

        // Update status and progress for Xxh64 calculation
        UpdateProgressCrc(read);
    }
    #endregion

    #region PatchTools
    protected virtual async ValueTask RunPatchTask(DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, long patchSize, Memory<byte> patchHash,
                                                   string patchURL, string patchOutputFile, string inputFile, string outputFile, bool isNeedRename = false, CancellationToken token = default)
        => await RunPatchTask(downloadClient, downloadProgress, patchSize,
            patchHash, patchURL, new FileInfo(patchOutputFile).StripAlternateDataStream().EnsureNoReadOnly(), new FileInfo(inputFile).EnsureNoReadOnly(),
            new FileInfo(outputFile).EnsureCreationOfDirectory().EnsureNoReadOnly(), isNeedRename, token);

    protected virtual async ValueTask RunPatchTask(DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, long patchSize, Memory<byte> patchHash,
                                                   string patchURL, FileInfo patchOutputFile, FileInfo inputFile, FileInfo outputFile, bool isNeedRename = false, CancellationToken token = default)
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
            FileStream patchFileStream = await patchOutputFile.NaivelyOpenFileStreamAsync(FileMode.Open, FileAccess.Read, FileShare.None);
            try
            {
                // Verify the patch file and if it doesn't match, then re-download it
                byte[] patchCrc = await GetCryptoHashAsync<MD5>(patchFileStream, null, true, false, token);
                Array.Reverse(patchCrc);
                if (!IsArrayMatch(patchCrc, patchHash.Span))
                {
                    // Revert back the total size
                    Interlocked.Add(ref ProgressAllSizeCurrent, -patchSize);

                    // Dispose patch stream before re-downloading
                    await patchFileStream.DisposeAsync();

                    // Re-download the patch file
                    await RunDownloadTask(patchSize, patchOutputFile, patchURL, downloadClient, downloadProgress, token);
                    continue;
                }

                // else, break and quit from loop
                break;
            }
            finally
            {
                await patchFileStream.DisposeAsync();
            }
        }

        // Start patching process
        BinaryPatchUtility patchUtil = new();
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
        catch (Exception ex)
        {
            Logger.LogWriteLine($"Failed while patching file: {inputFile.FullName} -> {outputFile.FullName}\r\n{ex}", LogType.Error, true);
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

    #region HandlerUpdaters
    public void Dispatch(DispatcherQueueHandler handler, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        EnsureParentUINotNull();
        ParentUI.DispatcherQueue.TryEnqueue(priority, handler);
    }

    public Task DispatchAsync(Action handler, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        EnsureParentUINotNull();
        return ParentUI.DispatcherQueue.EnqueueAsync(handler, priority);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void EnsureParentUINotNull()
    {
        if (ParentUI == null)
        {
            throw new NullReferenceException("_parentUI cannot be null when the method is being called!");
        }
    }

    internal virtual void PopRepairAssetEntry(IAssetProperty? assetProperty = null)
    {
        try
        {
            if (ParentUI.DispatcherQueue.HasThreadAccessSafe())
            {
                ImplDelete();
                return;
            }

            Dispatch(ImplDelete);
        }
        catch
        {
            // pipe to parent
        }

        return;

        void ImplDelete()
        {
            if (assetProperty == null)
            {
                if (AssetEntry.Count > 0) AssetEntry.RemoveAt(0);
            }
            else
            {
                AssetEntry.Remove(assetProperty);
            }
        }
    }

    protected void UpdateAll()
    {
        UpdateStatus();
        UpdateProgress();
    }

    internal virtual void UpdateProgress() => UpdateProgress(Progress);

    internal virtual void UpdateProgress(TotalPerFileProgress progress)
    {
        ProgressChanged?.Invoke(this, progress);

        if (Status is { IsProgressAllIndetermined: false, IsRunning: true })
        {
            WindowUtility.SetProgressValue((ulong)(progress.ProgressAllPercentage * 10), 1000);
        }
    }

    internal virtual void UpdateStatus() => UpdateStatus(Status);

    internal virtual void UpdateStatus(TotalPerFileStatus status)
    {
        StatusChanged?.Invoke(this, status);

        if (status.IsCanceled || status.IsCompleted)
        {
            WindowUtility.SetTaskBarState(TaskbarState.NoProgress);
        }
        else if (status.IsProgressAllIndetermined)
        {
            WindowUtility.SetTaskBarState(TaskbarState.Indeterminate);
        }
        else if (status.IsRunning)
        {
            WindowUtility.SetTaskBarState(TaskbarState.Normal);
        }
        else
        {
            WindowUtility.SetTaskBarState(TaskbarState.NoProgress);
        }
    }
    #endregion
}
