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
        void Update(TimeSpan      timeSpan, bool      forceUpdateDb = false);
        void StartSession(Process proc,     DateTime? begin         = null);
        void CheckDb();
    }
}
