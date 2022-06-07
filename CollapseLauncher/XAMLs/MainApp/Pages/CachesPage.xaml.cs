using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Hi3Helper.Locale;
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
                OverlayTitle.Text = Lang._CachesPage.OverlayNotInstalledTitle;
                OverlaySubtitle.Text = Lang._CachesPage.OverlayNotInstalledSubtitle;
            }
            else if (App.IsGameRunning)
            {
                Overlay.Visibility = Visibility.Visible;
                PageContent.Visibility = Visibility.Collapsed;
                OverlayTitle.Text = Lang._CachesPage.OverlayGameRunningTitle;
                OverlaySubtitle.Text = Lang._CachesPage.OverlayGameRunningSubtitle;
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource?.Cancel();
        }
    }
}
