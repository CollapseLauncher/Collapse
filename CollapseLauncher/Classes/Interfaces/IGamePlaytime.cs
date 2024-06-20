using CollapseLauncher.GamePlaytime.Universal;
using System;
using System.Diagnostics;

namespace CollapseLauncher.Interfaces
{
    internal interface IGamePlaytime
    {
        event EventHandler<Playtime> PlaytimeUpdated;

        void Reset();
        void ForceUpdate();
        void Update(TimeSpan timeSpan);
        void StartSession(Process proc);
    }
}
