﻿using System.Threading;
using System.Threading.Tasks;
// ReSharper disable PartialTypeWithSinglePart

namespace CollapseLauncher.Extension
{
    public sealed partial class CancellationTokenSourceWrapper : CancellationTokenSource
    {
        public bool IsDisposed;
        public bool IsCancelled;

        public new void Cancel()
        {
            if (!IsCancellationRequested) base.Cancel();
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
