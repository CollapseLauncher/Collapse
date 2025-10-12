using System;
using System.Threading.Tasks;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher;

internal partial class HonkaiRepairV2
{
    public Task StartRepairRoutine(bool    showInteractivePrompt     = false,
                                   Action? actionIfInteractiveCancel = null) =>
        TryRunExamineThrow(StartRepairRoutineCoreAsync(showInteractivePrompt, actionIfInteractiveCancel));

    private async Task StartRepairRoutineCoreAsync(bool    showInteractivePrompt     = false,
                                                   Action? actionIfInteractiveCancel = null)
    {
        if (AssetIndex.Count == 0) throw new InvalidOperationException("There's no broken file being reported! You can't perform repair process!");

        if (showInteractivePrompt &&
            actionIfInteractiveCancel != null)
        {
            await SpawnRepairDialog(AssetIndex, actionIfInteractiveCancel);
        }


    }
}
