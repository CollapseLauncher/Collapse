using System;

namespace CollapseLauncher.Interfaces
{
    internal interface IBackgroundActivity
    {
        event EventHandler<TotalPerfileProgress> ProgressChanged;
        event EventHandler<TotalPerfileStatus> StatusChanged;
        bool IsRunning { get; }
        void CancelRoutine();
    }
}
