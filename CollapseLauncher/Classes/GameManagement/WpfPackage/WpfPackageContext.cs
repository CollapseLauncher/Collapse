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
    /// Start update check process.
    /// </summary>
    /// <returns>Returns false if the routines are already in progress.</returns>
    public ValueTask<bool> StartUpdateCheckAsync()
    {
        if (ChangesInProgress)
        {
            return ValueTask.FromResult(false);
        }

        return TryRunExamineThrow(StartUpdateCheckAsyncCore());
    }

    /// <summary>
    /// Triggers tool reinstallation process.
    /// </summary>
    /// <returns>Returns false if the routines are already in progress.</returns>
    public ValueTask<bool> ReinstallToolAsync()
    {
        if (ChangesInProgress)
        {
            return ValueTask.FromResult(false);
        }

        return TryRunExamineThrow(StartUpdateCheckAsyncCore(true));
    }

    /// <summary>
    /// Cancel all update process routines.
    /// </summary>
    public void CancelRoutine()
    {
        _localCts.Cancel();
    }
}
