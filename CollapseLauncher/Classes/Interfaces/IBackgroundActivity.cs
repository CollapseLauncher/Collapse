using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;

namespace CollapseLauncher.Interfaces
{
    internal interface IBackgroundActivity
    {
        event EventHandler<TotalPerfileProgress> ProgressChanged;
        event EventHandler<TotalPerfileStatus> StatusChanged;
        event EventHandler DisposingTrigger;

        bool IsRunning { get; }
        UIElement _parentUI { get; }
        void CancelRoutine();
        void Dispatch(DispatcherQueueHandler handler);
    }
}
