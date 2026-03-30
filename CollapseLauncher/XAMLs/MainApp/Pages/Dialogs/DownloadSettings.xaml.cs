using CollapseLauncher.Interfaces;
using CollapseLauncher.Pages;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;

#nullable enable
namespace CollapseLauncher.Dialogs
{
    public partial class DownloadSettings : UserControl
    {
        #region Properties
        private IGameInstallManager CurrentGameInstaller { get; }

        private PostInstallBehaviour CurrentPostInstallBehaviour => CurrentGameInstaller.PostInstallBehaviour;

        private int PostInstallShutdownTimeout
        {
            get => LauncherConfig.PostInstallShutdownTimeout;
            set => LauncherConfig.PostInstallShutdownTimeout = value;
        }
        #endregion

        internal DownloadSettings(IGameInstallManager gameInstaller)
        {
            CurrentGameInstaller = gameInstaller;
            InitializeComponent();
        }

        private void Control_Loaded(object sender, RoutedEventArgs e)
        {
            PostInstallBox.SelectedIndex = (int)CurrentPostInstallBehaviour;
            if ((InnerLauncherConfig.m_mainPage?.TryGetCurrentPageObject(out object? typeOfPageObj) ?? false) &&
                typeOfPageObj is Type asPageType && asPageType == typeof(SettingsPage))
                NetworkSettings.Visibility = Visibility.Collapsed;
        }

        private void OnPostInstallBehaviourChange(object sender, SelectionChangedEventArgs e)
        {
            CurrentGameInstaller.PostInstallBehaviour =
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
