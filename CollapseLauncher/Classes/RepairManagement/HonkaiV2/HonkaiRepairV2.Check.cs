using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher;

internal partial class HonkaiRepairV2
{
    public Task<bool> StartCheckRoutine(bool useFastCheck = false) =>
        TryRunExamineThrow(StartCheckRoutineCoreAsync(useFastCheck));

    private async Task<bool> StartCheckRoutineCoreAsync(bool useFastCheck)
    {
        // Reset status and progress
        ResetStatusAndProgress();

        // Set total activity string as "Loading Indexes..."
        Status.ActivityStatus            = Locale.Lang._GameRepairPage.Status2;
        Status.IsProgressAllIndetermined = true;
        UpdateStatus();

        List<FilePropertiesRemote> checkAssetIndex = [];

        // Fetch assets
        if (IsCacheMode)
        {
            await FetchAssetFromGameCacheFiles(checkAssetIndex, Token!.Token);
        }
        else
        {
            await FetchAssetFromSophon(checkAssetIndex, Token!.Token);
            if (!IsMainAssetOnlyMode)
            {
                RemoveBlockAssetFromList(checkAssetIndex);
                await FetchAssetFromGameAssetBundle(checkAssetIndex, Token.Token);
            }
        }

        // Restore progress bar indeterminate state
        Status.IsProgressAllIndetermined     = false;
        Status.IsProgressPerFileIndetermined = false;
        UpdateStatus();

        // Reset the counter on the UI
        ProgressAllSizeTotal  = checkAssetIndex.Sum(x => x.S); // Add for generic size
        ProgressAllCountTotal = checkAssetIndex.Count;

        await Parallel.ForEachAsync(checkAssetIndex,
                                    new ParallelOptions
                                    {
                                        CancellationToken      = Token.Token,
                                        MaxDegreeOfParallelism = ThreadForIONormalized
                                    },
                                    Impl);

        if (!IsMainAssetOnlyMode)
        {
            CheckAssetUnusedType(AssetIndex, checkAssetIndex);
        }

        return SummarizeStatusAndProgress(AssetIndex,
                                          string.Format(Locale.Lang._GameRepairPage.Status3,
                                                        ProgressAllCountFound,
                                                        ConverterTool.SummarizeSizeSimple(ProgressAllSizeFound)),
                                          Locale.Lang._GameRepairPage.Status4);

        ValueTask Impl(FilePropertiesRemote asset, CancellationToken token) =>
            asset.FT switch
            {
                FileType.Audio => CheckAssetAudioType(asset, useFastCheck, token),
                FileType.Block => CheckAssetBlockType(asset, useFastCheck, token),
                _              => CheckAssetGenericType(asset, useFastCheck, token)
            };
    }
}
