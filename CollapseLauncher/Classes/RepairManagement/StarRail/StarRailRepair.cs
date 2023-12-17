﻿using CollapseLauncher.GameVersioning;
using CollapseLauncher.Interfaces;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Hi3Helper.Locale;

namespace CollapseLauncher
{
    internal partial class StarRailRepair :
        ProgressBase<RepairAssetType, FilePropertiesRemote>, IRepair, IRepairAssetIndex
    {
        #region Properties
        private GameTypeStarRailVersion _innerGameVersionManager { get; set; }
        private bool _isOnlyRecoverMain { get; set; }
        private List<FilePropertiesRemote> _originAssetIndex { get; set; }
        #endregion

        public StarRailRepair(UIElement parentUI, IGameVersionCheck GameVersionManager, bool onlyRecoverMainAsset = false, string versionOverride = null)
            : base(parentUI, GameVersionManager, null, "", versionOverride)
        {
            // Get flag to only recover main assets
            _isOnlyRecoverMain = onlyRecoverMainAsset;
            _innerGameVersionManager = GameVersionManager as GameTypeStarRailVersion;
        }

        ~StarRailRepair() => Dispose();

        public List<FilePropertiesRemote> GetAssetIndex() => _originAssetIndex;

        public async Task<bool> StartCheckRoutine(bool useFastCheck)
        {
            _useFastMethod = useFastCheck;
            return await TryRunExamineThrow(CheckRoutine());
        }

        public async Task StartRepairRoutine(bool showInteractivePrompt = false, Action actionIfInteractiveCancel = null)
        {
            if (_assetIndex.Count == 0) throw new InvalidOperationException("There's no broken file being reported! You can't do the repair process!");

            if (showInteractivePrompt)
            {
                await SpawnRepairDialog(_assetIndex, actionIfInteractiveCancel);
            }

            _ = await TryRunExamineThrow(RepairRoutine());
        }

        private async Task<bool> CheckRoutine()
        {
            // Always clear the asset index list
            _assetIndex.Clear();

            // Reset status and progress
            ResetStatusAndProgress();

            // Step 1: Fetch asset indexes
            await Fetch(_assetIndex, _token.Token);

            // Step 2: Calculate the total size and count of the files
            CountAssetIndex(_assetIndex);

            // Step 3: Check for the asset indexes integrity
            await Check(_assetIndex, _token.Token);

            // Step 4: Summarize and returns true if the assetIndex count != 0 indicates broken file was found.
            //         either way, returns false.
            return SummarizeStatusAndProgress(
                _assetIndex,
                string.Format(Lang._GameRepairPage.Status3, _progressTotalCountFound, ConverterTool.SummarizeSizeSimple(_progressTotalSizeFound)),
                Lang._GameRepairPage.Status4);
        }

        private async Task<bool> RepairRoutine()
        {
            // Restart Stopwatch
            RestartStopwatch();

            // Assign repair task
            Task<bool> repairTask = Repair(_assetIndex, _token.Token);

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
            CancelRoutine();
        }
    }
}
