using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace CollapseLauncher.Interfaces
{
    internal interface IRepair : IDisposable
    {
        ObservableCollection<AssetProperty<RepairAssetType>> AssetEntry { get; set; }
        event EventHandler<TotalPerfileProgress> ProgressChanged;
        event EventHandler<TotalPerfileStatus> StatusChanged;
        Task<bool> StartCheckRoutine();
        Task StartRepairRoutine(bool showInteractivePrompt = false);
        void CancelRoutine();
    }
}
