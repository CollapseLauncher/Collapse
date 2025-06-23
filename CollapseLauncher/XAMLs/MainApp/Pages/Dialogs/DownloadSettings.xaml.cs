using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;

namespace CollapseLauncher.Dialogs
{
    public partial class DownloadSettings : UserControl
    {
        #region Properties
        private GamePresetProperty CurrentGameProperty { get; }
        private PostInstallBehaviour CurrentPostInstallBehaviour =>
            CurrentGameProperty.GameInstall.PostInstallBehaviour;

        private int PostIntallShutdownTimeout
        {
            get => LauncherConfig.PostIntallShutdownTimeout;
            set => LauncherConfig.PostIntallShutdownTimeout = value;
        }
        #endregion

        internal DownloadSettings(GamePresetProperty currentGameProperty)
        {
            CurrentGameProperty = currentGameProperty;
            InitializeComponent();
        }

        private void Control_Loaded(object sender, RoutedEventArgs e)
        {
            ShutdownTimeout.Visibility = CurrentPostInstallBehaviour is
                    PostInstallBehaviour.Shutdown or PostInstallBehaviour.Restart
                ? Visibility.Visible : Visibility.Collapsed;
            PostInstallBox.SelectedIndex = (int)CurrentPostInstallBehaviour;
            if (MainPage.PreviousTag == "settings")
                NetworkSettings.Visibility = Visibility.Collapsed;
        }

        private void OnPostInstallBehaviourChange(object sender, SelectionChangedEventArgs e)
        {
            CurrentGameProperty.GameInstall.PostInstallBehaviour =
                (PostInstallBehaviour)(PostInstallBox.SelectedIndex);

            ShutdownTimeout.Visibility = CurrentPostInstallBehaviour is
                    PostInstallBehaviour.Shutdown or PostInstallBehaviour.Restart
                ? Visibility.Visible : Visibility.Collapsed;
        }

        #region Download Speed Limiter Properties
        private bool IsUseDownloadSpeedLimiter
        {
            get
            {
                bool value = LauncherConfig.IsUseDownloadSpeedLimiter;
                NetworkDownloadSpeedLimitGrid.Opacity = value ? 1 : 0.45;
                if (value)
                    LauncherConfig.IsBurstDownloadModeEnabled = false;
                return value;
            }
            set
            {
                NetworkDownloadSpeedLimitGrid.Opacity = value ? 1 : 0.45;
                if (value)
                    LauncherConfig.IsBurstDownloadModeEnabled = false;
                LauncherConfig.IsUseDownloadSpeedLimiter = value;
            }
        }

        private double DownloadSpeedLimit
        {
            get
            {
                double val = LauncherConfig.DownloadSpeedLimit;
                double valDividedM = val / (1 << 20);
                return valDividedM;
            }
            set
            {
                long valBfromM = (long)(value * (1 << 20));

                LauncherConfig.DownloadSpeedLimit = Math.Max(valBfromM, 0);
            }
        }
        #endregion

        private void IgnoreInput(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
        }
    }
}
