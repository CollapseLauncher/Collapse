using CollapseLauncher.Extension;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Sophon;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher;

internal partial class HonkaiRepairV2
{
    public async Task StartRepairRoutine(
        bool    showInteractivePrompt     = false,
        Action? actionIfInteractiveCancel = null)
    {
        await TryRunExamineThrow(StartRepairRoutineCoreAsync(showInteractivePrompt, actionIfInteractiveCancel));

        // Reset status and progress
        ResetStatusAndProgress();

        // Set as completed
        Status.ActivityStatus = Locale.Lang._GameRepairPage.Status7;

        // Update status and progress
        UpdateAll();
    }

    private async Task StartRepairRoutineCoreAsync(bool    showInteractivePrompt     = false,
                                                   Action? actionIfInteractiveCancel = null)
    {
        if (AssetIndex.Count == 0) throw new InvalidOperationException("There's no broken file being reported! You can't perform repair process!");

        // Swap current found all size to per file size
        ProgressPerFileSizeTotal = ProgressAllSizeTotal;
        ProgressAllSizeTotal     = AssetIndex.Where(x => x.FT != FileType.Unused).Sum(x => x.S);

        // Reset progress counter
        ResetProgressCounter();

        if (showInteractivePrompt &&
            actionIfInteractiveCancel != null)
        {
            await SpawnRepairDialog(AssetIndex, actionIfInteractiveCancel);
        }

        int threadNum = IsBurstDownloadEnabled
            ? 1
            : ThreadForIONormalized;

        await Parallel.ForEachAsync(AssetIndex,
                                    new ParallelOptions
                                    {
                                        CancellationToken      = Token!.Token,
                                        MaxDegreeOfParallelism = threadNum
                                    },
                                    Impl);

        return;

        ValueTask Impl(FilePropertiesRemote asset, CancellationToken token)
        {
            return asset switch
            {
                { AssociatedObject: SophonAsset }    => RepairAssetGenericSophonType(asset, token),
                { FT              : FileType.Audio } => RepairAssetAudioType(asset, token),
                { FT              : FileType.Block } => RepairAssetBlockType(asset, token),
                _                                    => RepairAssetGenericType(GetHttpClientFromFilename(asset), asset, token)
            };
        }

        HttpClient GetHttpClientFromFilename(FilePropertiesRemote asset)
        {
            const StringComparison comp = StringComparison.OrdinalIgnoreCase;

            ReadOnlySpan<char> filename = Path.GetFileName(asset.N);
            if (filename.EndsWith(".xmf", comp) ||
                (filename.StartsWith("manifest", comp) && filename.EndsWith(".m", comp)))
            {
                return HttpClientAssetBundle;
            }

            return HttpClientGeneric;
        }
    }

    private double _downloadReadLastSpeed;
    private long   _downloadReadLastReceivedBytes;
    private long   _downloadReadLastTick;

    private double _dataWriteLastSpeed;
    private long   _dataWriteLastReceivedBytes;
    private long   _dataWriteLastTick;

    private void UpdateProgressCounter(long dataWrite, long downloadRead)
    {
        double speedAll = CalculateSpeed(dataWrite, // dataWrite used as All Progress overall speed.
                                         ref _dataWriteLastSpeed,
                                         ref _dataWriteLastReceivedBytes,
                                         ref _dataWriteLastTick);

        double speedPerFile = CalculateSpeed(downloadRead, // downloadRead used as Per File Progress overall speed.
                                             ref _downloadReadLastSpeed,
                                             ref _downloadReadLastReceivedBytes,
                                             ref _downloadReadLastTick);

        Interlocked.Add(ref ProgressAllSizeCurrent,     dataWrite);
        Interlocked.Add(ref ProgressPerFileSizeCurrent, downloadRead);

        if (!CheckIfNeedRefreshStopwatch())
        {
            return;
        }

        double speedClamped = speedAll.ClampLimitedSpeedNumber();
        TimeSpan timeLeftSpan = ConverterTool.ToTimeSpanRemain(ProgressAllSizeTotal,
                                                               ProgressAllSizeCurrent,
                                                               speedClamped);

        double percentPerFile = ProgressPerFileSizeCurrent != 0
            ? ConverterTool.ToPercentage(ProgressPerFileSizeTotal, ProgressPerFileSizeCurrent)
            : 0;
        double percentAll = ProgressAllSizeCurrent != 0
            ? ConverterTool.ToPercentage(ProgressAllSizeTotal, ProgressAllSizeCurrent)
            : 0;

        lock (Progress)
        {
            Progress.ProgressPerFilePercentage  = percentPerFile;
            Progress.ProgressPerFileSizeCurrent = ProgressPerFileSizeCurrent;
            Progress.ProgressPerFileSizeTotal   = ProgressPerFileSizeTotal;
            Progress.ProgressAllSizeCurrent     = ProgressAllSizeCurrent;
            Progress.ProgressAllSizeTotal       = ProgressAllSizeTotal;

            // Calculate speed
            Progress.ProgressAllSpeed    = speedClamped;
            Progress.ProgressAllTimeLeft = timeLeftSpan;

            // Update current progress percentages
            Progress.ProgressAllPercentage = percentAll;
        }

        lock (Status)
        {
            // Update current activity status
            Status.IsProgressAllIndetermined     = false;
            Status.IsProgressPerFileIndetermined = false;

            // Set time estimation string
            string timeLeftString = string.Format(Locale.Lang._Misc.TimeRemainHMSFormat, Progress.ProgressAllTimeLeft);

            Status.ActivityPerFile = string.Format(Locale.Lang._Misc.Speed, ConverterTool.SummarizeSizeSimple(speedPerFile));
            Status.ActivityAll = string.Format(Locale.Lang._GameRepairPage.PerProgressSubtitle2,
                                               ConverterTool.SummarizeSizeSimple(ProgressAllSizeCurrent),
                                               ConverterTool.SummarizeSizeSimple(ProgressAllSizeTotal))
                                 + $" | {timeLeftString}"
                                 + $" ({string.Format(Locale.Lang._Misc.Speed, ConverterTool.SummarizeSizeSimple(speedAll))})";

            // Trigger update
            UpdateAll();
        }
    }

    private void ResetProgressCounter()
    {
        _dataWriteLastSpeed         = 0;
        _dataWriteLastReceivedBytes = 0;
        _dataWriteLastTick          = 0;

        _downloadReadLastSpeed         = 0;
        _downloadReadLastReceivedBytes = 0;
        _downloadReadLastTick          = 0;

        ProgressAllSizeCurrent     = 0;
        ProgressPerFileSizeCurrent = 0;
    }
}
