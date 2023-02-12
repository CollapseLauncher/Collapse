using CollapseLauncher.Interfaces;
using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;
using static Hi3Helper.Locale;

namespace CollapseLauncher
{
    internal partial class HonkaiRepair :
        ProgressBase<RepairAssetType, FilePropertiesRemote>, IRepair
    {
        private const string _blockBasePath = "BH3_Data/StreamingAssets/Asb/pc/";

        public HonkaiRepair(UIElement parentUI, string gameVersion, string gamePath,
            string gameRepoURL, PresetConfigV2 gamePreset, byte repairThread, byte downloadThread)
            : base(parentUI, gameVersion, gamePath, gameRepoURL, gamePreset, repairThread, downloadThread)
        {
        }

        ~HonkaiRepair() => Dispose();

        public async Task<bool> StartCheckRoutine()
        {
            return await TryRunExamineThrow(CheckRoutine());
        }

        public async Task StartRepairRoutine(bool showInteractivePrompt = false)
        {
            if (_assetIndex.Count == 0) throw new InvalidOperationException("There's no broken file being reported! You can't do the repair process!");

            _ = await TryRunExamineThrow(RepairRoutine());
        }

        private async Task<bool> CheckRoutine()
        {
            // Reset status and progress
            ResetStatusAndProgress();

            // Step 1: Fetch asset indexes
            _assetIndex = await Fetch();

            // Step 2: Calculate the total size and count of the files
            CountAssetIndex(_assetIndex);

            // Step 3: Check for the asset integrity
            _assetIndex = await Check(_assetIndex);

            // Step 4: Summarize and returns true if the assetIndex count != 0 indicates broken file was found.
            //         either way, returns false.
            return SummarizeStatusAndProgress(
                _assetIndex,
                string.Format(Lang._GameRepairPage.Status3, _progressTotalCountFound, ConverterTool.SummarizeSizeSimple(_progressTotalSizeFound)),
                Lang._GameRepairPage.Status4);
        }

        private async Task<bool> RepairRoutine()
        {
            // Assign repair task
            Task<bool> repairTask = Repair(_assetIndex);

            // Run repair process
            bool repairTaskSuccess = await TryRunExamineThrow(repairTask);

            // Reset status and progress
            ResetStatusAndProgress();

            // Set as completed
            _status.IsCompleted = true;
            _status.IsCanceled = false;
            _status.ActivityStatus = Lang._GameRepairPage.Status7;

            // Update status and progress
            UpdateAll();

            return repairTaskSuccess;
        }

        public void CancelRoutine()
        {
            // Trigger token cancellation
            _token.Cancel();
        }

        public void Dispose()
        {
        }
    }
}
