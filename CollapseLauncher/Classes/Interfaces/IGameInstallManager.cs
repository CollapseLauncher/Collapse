using System;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher.Interfaces
{
    internal interface IGameInstallManager : IBackgroundActivity, IDisposable
    {
        ValueTask<int> GetInstallationPath();
        Task StartPackageDownload(bool skipDialog = false);
        ValueTask<int> StartPackageVerification();
        Task StartPackageInstallation();
        Task StartPostInstallVerification();
        void ApplyGameConfig(bool forceUpdateToLatest = false);

        Task MoveGameLocation();
        ValueTask<bool> UninstallGame();
        void Flush();
        ValueTask<bool> IsPreloadCompleted(CancellationToken token = default);

        ValueTask<bool> TryShowFailedDeltaPatchState();
        ValueTask<bool> TryShowFailedGameConversionState();
    }
}
