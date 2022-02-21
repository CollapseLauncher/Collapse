using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

using Hi3Helper.Shared.ClassStruct;

using static Hi3Helper.Shared.Region.LauncherConfig;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CollapseLauncher.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
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

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource?.Cancel();
        }
    }
}
