using System.Threading;
using System.Threading.Tasks;
// ReSharper disable PartialTypeWithSinglePart
#pragma warning disable IDE0130

namespace CollapseLauncher.Extension;

public sealed partial class CancellationTokenSourceWrapper : CancellationTokenSource
{
    public volatile bool IsDisposed;
    public volatile bool IsCancelled;

    public new void Cancel()
    {
        IsCancelled = true;
        if (!IsCancellationRequested)
            base.Cancel();
    }

    public new async ValueTask CancelAsync()
    {
        IsCancelled = true;
        if (!IsCancellationRequested)
            await base.CancelAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        IsDisposed = true;
        base.Dispose(true);
    }
}
