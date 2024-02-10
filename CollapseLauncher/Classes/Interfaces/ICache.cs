﻿using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace CollapseLauncher.Interfaces
{
    internal interface ICache : IDisposable
    {
        ObservableCollection<IAssetProperty> AssetEntry { get; set; }
        event EventHandler<TotalPerfileProgress> ProgressChanged;
        event EventHandler<TotalPerfileStatus> StatusChanged;
        Task<bool> StartCheckRoutine(bool useFastCheck = false);
        Task StartUpdateRoutine(bool showInteractivePrompt = false);
        void CancelRoutine();
    }
}
