using System.Threading.Tasks;

namespace CollapseLauncher.Interfaces
{
    internal interface IGameInstallManager : IBackgroundActivity
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
        ValueTask<bool> IsPreloadCompleted();

        ValueTask<bool> TryShowFailedDeltaPatchState();
        ValueTask<bool> TryShowFailedGameConversionState();
    }
}
