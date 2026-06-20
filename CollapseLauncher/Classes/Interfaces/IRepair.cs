using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace CollapseLauncher.Interfaces
{
    internal interface IRepair : IBackgroundActivity, IDisposable
    {
        ObservableCollection<IAssetProperty> AssetEntry { get; set; }
        TotalPerFileStatus Status { get; }
        Task<bool> StartCheckRoutine(bool useFastCheck = false);
        Task StartRepairRoutine(bool showInteractivePrompt = false, Action actionIfInteractiveCancel = null);
    }
}
