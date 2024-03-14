using CollapseLauncher.InstallManager;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher.Interfaces
{
    internal interface IGameInstallManager : IBackgroundActivity, IDisposable
    {
        ValueTask<int> GetInstallationPath();
        Task StartPackageDownload(bool skipDialog = false);
        ValueTask<int> StartPackageVerification(List<GameInstallPackage> gamePackage = null);
        Task StartPackageInstallation();
        void ApplyGameConfig(bool forceUpdateToLatest = false);
        bool StartAfterInstall { get; set; }

        ValueTask<bool> MoveGameLocation();
        ValueTask<bool> UninstallGame();
        void Flush();
        ValueTask<bool> IsPreloadCompleted(CancellationToken token = default);

        ValueTask<bool> TryShowFailedDeltaPatchState();
        ValueTask<bool> TryShowFailedGameConversionState();
    }
}
