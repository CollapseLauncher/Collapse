using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using System.Collections.Generic;
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
            await FetchAssetFromGameCacheFiles(checkAssetIndex, Token.Token);
        }
        else
        {
            await FetchAssetFromSophon(checkAssetIndex, Token.Token);
            if (!IsMainAssetOnlyMode)
            {
                await FetchAssetFromGameAssetBundle(checkAssetIndex, Token.Token);
            }
        }

        return true;
    }
}
