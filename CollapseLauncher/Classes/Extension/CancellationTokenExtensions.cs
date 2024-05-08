using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher.Extension
{
    public class CancellationTokenSourceWrapper : CancellationTokenSource
    {
        public bool IsDisposed;
        public bool IsCancelled = false;

        public new void Cancel()
        {
            base.Cancel();
            IsCancelled = true;
        }

        public new async ValueTask CancelAsync()
        {
            await base.CancelAsync();
            IsCancelled = true;
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
