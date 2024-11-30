using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace CollapseLauncher.Interfaces
{
    internal interface IRepair : IDisposable
    {
        ObservableCollection<IAssetProperty> AssetEntry { get; set; }
        event EventHandler<TotalPerFileProgress> ProgressChanged;
        event EventHandler<TotalPerFileStatus> StatusChanged;
        Task<bool> StartCheckRoutine(bool useFastCheck = false);
        Task StartRepairRoutine(bool showInteractivePrompt = false, Action actionIfInteractiveCancel = null);
        void CancelRoutine();
    }
}
