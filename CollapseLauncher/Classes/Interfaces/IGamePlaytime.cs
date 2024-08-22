using CollapseLauncher.GamePlaytime;
using System;
using System.Diagnostics;

namespace CollapseLauncher.Interfaces
{
    internal interface IGamePlaytime : IDisposable
    {
        event EventHandler<CollapsePlaytime> PlaytimeUpdated;
        CollapsePlaytime CollapsePlaytime { get; }

        void Reset();
        void Reload();
        void Update(TimeSpan timeSpan);
        void StartSession(Process proc);
    }
}
