using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace CollapseLauncher.Interfaces
{

    internal interface IRepair : IDisposable
    {
        ObservableCollection<RepairAssetProperty> RepairAssetEntry { get; set; }
        event EventHandler<RepairProgress> ProgressChanged;
        event EventHandler<RepairStatus> StatusChanged;
        Task<bool> StartCheckRoutine();
        Task StartRepairRoutine(bool showInteractivePrompt = false);
        void CancelRoutine();
    }
}
