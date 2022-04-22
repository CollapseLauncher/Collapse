using System.IO;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Hi3Helper.Shared.ClassStruct;

using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static Hi3Helper.Shared.Region.InstallationManagement;

using static CollapseLauncher.Pages.RepairData;

namespace CollapseLauncher.Pages
{
    public sealed partial class RepairPage : Page
    {
        public RepairPage()
        {
            this.InitializeComponent();
            GameBasePath = NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString());
            GameBaseURL = string.Format(CurrentRegion.ZipFileURL, Path.GetFileNameWithoutExtension(regionResourceProp.data.game.latest.path));
        }

        private void CancelOperation(object sender, RoutedEventArgs e)
        {
            sw.Stop();
            CancelBtn.IsEnabled = false;
            CheckFilesBtn.Visibility = Visibility.Visible;
            CheckFilesBtn.IsEnabled = true;
            RepairFilesBtn.Visibility = Visibility.Collapsed;

            httpClient.ProgressChanged -= DataFetchingProgress;

            cancellationTokenSource.Cancel();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            sw.Stop();
            cancellationTokenSource.Cancel();
        }

        private void InitializeLoaded(object sender, RoutedEventArgs e)
        {
            if (GameInstallationState == GameInstallStateEnum.NotInstalled
                || GameInstallationState == GameInstallStateEnum.NeedsUpdate
                || GameInstallationState == GameInstallStateEnum.GameBroken)
            {
                Overlay.Visibility = Visibility.Visible;
                OverlayTitle.Text = "You can't use this feature since the region isn't yet installed or need to be updated!";
                OverlaySubtitle.Text = "Please download/update the game first in Homepage Menu!";
            }
            else if (App.IsGameRunning)
            {
                Overlay.Visibility = Visibility.Visible;
                OverlayTitle.Text = "Game is Currently Running!";
                OverlaySubtitle.Text = "Please close the game first to use this feature!";
            }
        }
    }
}
