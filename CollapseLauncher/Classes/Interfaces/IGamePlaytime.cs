using CollapseLauncher.GamePlaytime;
using System;
using System.Diagnostics;

namespace CollapseLauncher.Interfaces
{
    internal interface IGamePlaytime : IDisposable
    {
        event EventHandler<CollapsePlaytime> PlaytimeUpdated;

        void Reset();
        void ForceUpdate();
        void Update(TimeSpan timeSpan);
        void StartSession(Process proc);
    }
}
