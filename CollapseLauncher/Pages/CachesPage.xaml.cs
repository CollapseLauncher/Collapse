using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Hi3Helper.Shared.ClassStruct;

using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public sealed partial class CachesPage : Page
    {
        public CachesPage()
        {
            this.InitializeComponent();
        }

        public async void StartCachesUpdate(object sender, RoutedEventArgs e) => await DoCachesUpdate();

        public async void StartCachesCheck(object sender, RoutedEventArgs e) => await DoCachesCheck();

        public void CancelOperation(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource.Cancel();
        }

        private void InitializeLoaded(object sender, RoutedEventArgs e)
        {
            if (GameInstallationState == GameInstallStateEnum.NotInstalled
                || GameInstallationState == GameInstallStateEnum.NeedsUpdate
                || GameInstallationState == GameInstallStateEnum.GameBroken)
            {
                Overlay.Visibility = Visibility.Visible;
                PageContent.Visibility = Visibility.Collapsed;
                OverlayTitle.Text = "You can't use this feature since the region isn't yet installed or need to be updated!";
                OverlaySubtitle.Text = "Please download/update the game first in Homepage Menu!";
            }
            else if (App.IsGameRunning)
            {
                Overlay.Visibility = Visibility.Visible;
                PageContent.Visibility = Visibility.Collapsed;
                OverlayTitle.Text = "Game is Currently Running!";
                OverlaySubtitle.Text = "Please close the game first to use this feature!";
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource?.Cancel();
        }
    }
}
