using CollapseLauncher.Interfaces;
using Microsoft.UI.Xaml;
using System.Threading.Tasks;
using WinRT;
#pragma warning disable IDE0290
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.GameManagement.WpfPackage;

[GeneratedBindableCustomProperty]
internal partial class WpfPackageContext : ProgressBase
{
    public WpfPackageContext(
        UIElement    parentUI,
        IGameVersion gameVersionManager)
        : base(parentUI,
               gameVersionManager,
               null,
               null)
    {
        ConfigAutoUpdateKey = $"{GameVersionManager.GameBiz}_EnableWpfAutoUpdate";
    }

    /// <summary>
    /// Start update check progress.
    /// </summary>
    /// <returns>Returns false if an update check or the actual update process is already in progress.</returns>
    public ValueTask<bool> StartUpdateCheckAsync()
    {
        if (ChangesInProgress)
        {
            return ValueTask.FromResult(false);
        }

        return TryRunExamineThrow(StartUpdateCheckAsyncCore());
    }

    /// <summary>
    /// Cancel all update progress routines.
    /// </summary>
    public void CancelRoutine()
    {
        _localCts.Cancel();
    }
}
