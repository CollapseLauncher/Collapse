using CollapseLauncher.GamePlaytime;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher.Interfaces
{
    internal interface IGamePlaytime : IDisposable
    {
        event EventHandler<CollapsePlaytime> PlaytimeUpdated;
        CollapsePlaytime CollapsePlaytime { get; }

        void Reset();
        void Update(TimeSpan timeSpan, bool forceUpdateDb = false);
        Task StartSession(Process proc, DateTime? begin = null);
        Task StartSessionFromAwaiter(Func<CancellationToken, Task> awaiterTask, DateTime? begin = null);
        Task CheckDb(bool redirectThrow = false);
    }
}
