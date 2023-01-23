using CollapseLauncher.GameSettings.Honkai;
using CollapseLauncher.Interfaces;
using CollapseLauncher.Statics;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public partial class GameSettingsPage : Page
    {
        private HonkaiSettings Settings { get => (HonkaiSettings)PageStatics._GameSettings; }
        public GameSettingsPage()
        {
            try
            {
                this.InitializeComponent();
                ApplyButton.Translation = Shadow32;
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void InitializeSettings(object sender, RoutedEventArgs e)
        {
            try
            {
                GameResolutionSelector.ItemsSource = ScreenResolutionsList;

                if (App.IsGameRunning)
                {
                    Overlay.Visibility = Visibility.Visible;
                    PageContent.Visibility = Visibility.Collapsed;
                    OverlayTitle.Text = Lang._GameSettingsPage.OverlayGameRunningTitle;
                    OverlaySubtitle.Text = Lang._GameSettingsPage.OverlayGameRunningSubtitle;
                }
                else if (GameInstallationState == GameInstallStateEnum.NotInstalled
                      || GameInstallationState == GameInstallStateEnum.NeedsUpdate
                      || GameInstallationState == GameInstallStateEnum.GameBroken)
                {
                    Overlay.Visibility = Visibility.Visible;
                    PageContent.Visibility = Visibility.Collapsed;
                    OverlayTitle.Text = Lang._GameSettingsPage.OverlayNotInstalledTitle;
                    OverlaySubtitle.Text = Lang._GameSettingsPage.OverlayNotInstalledSubtitle;
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL ERROR!!!\r\n{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyText.Visibility = Visibility.Visible;

                Settings.SaveSettings();
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        public string CustomArgsValue
        {
            get => ((IGameSettingsUniversal)PageStatics._GameSettings).SettingsCustomArgument.CustomArgumentValue;
            set => ((IGameSettingsUniversal)PageStatics._GameSettings).SettingsCustomArgument.CustomArgumentValue = value;
        }
    }
}
