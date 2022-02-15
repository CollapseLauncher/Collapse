using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;

using static Hi3Helper.Logger;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static Hi3Helper.Shared.Region.InstallationManagement;

using static CollapseLauncher.Pages.RepairData;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CollapseLauncher.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
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
        }
    }
}
