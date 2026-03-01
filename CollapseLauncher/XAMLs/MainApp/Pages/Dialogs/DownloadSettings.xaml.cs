using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace CollapseLauncher.Dialogs
{
    public partial class DownloadSettings : UserControl
    {
        #region Properties
        private GamePresetProperty CurrentGameProperty { get; }
        private PostInstallBehaviour CurrentPostInstallBehaviour =>
            CurrentGameProperty.GameInstall?.PostInstallBehaviour ?? PostInstallBehaviour.Nothing;

        private int PostInstallShutdownTimeout
        {
            get => LauncherConfig.PostInstallShutdownTimeout;
            set => LauncherConfig.PostInstallShutdownTimeout = value;
        }
        #endregion

        internal DownloadSettings(GamePresetProperty currentGameProperty)
        {
            CurrentGameProperty = currentGameProperty;
            InitializeComponent();
        }

        private void Control_Loaded(object sender, RoutedEventArgs e)
        {
            PostInstallBox.SelectedIndex = (int)CurrentPostInstallBehaviour;
            if (MainPage.PreviousTag == "settings")
                NetworkSettings.Visibility = Visibility.Collapsed;
        }

        private void OnPostInstallBehaviourChange(object sender, SelectionChangedEventArgs e)
        {
            CurrentGameProperty.GameInstall?.PostInstallBehaviour =
                (PostInstallBehaviour)PostInstallBox.SelectedIndex;

            ShutdownTimeout.Visibility = CurrentPostInstallBehaviour is
                    PostInstallBehaviour.Shutdown or PostInstallBehaviour.Restart
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void IgnoreInput(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
        }
    }
}
