using CollapseLauncher.GameManagement.ImageBackground;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Animation;
using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static CollapseLauncher.Statics.GamePropertyVault;
using WinRT;

#if !DISABLEDISCORD
using CollapseLauncher.DiscordPresence;
#endif

#pragma warning disable IDE0130
namespace CollapseLauncher.Pages
{
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [GeneratedBindableCustomProperty]
    public partial class HonkaiGameSettingsPage
    {

        public HonkaiGameSettingsPage() : base(GetCurrentGameProperty().GameSettings, Registry.CurrentUser.CreateSubKey(Path.Combine($"Software\\{GetCurrentGameProperty().GameVersion.VendorTypeProp.VendorType}", GetCurrentGameProperty().GameVersion.GamePreset.InternalGameNameInConfig!)))
        {
            try
            {
                InitializeComponent();

                ApplyButton.Translation           = new Vector3(0, 0, 32);
                GameSettingsApplyGrid.Translation = new Vector3(0, 0, 64);
                SettingsScrollViewer.EnableImplicitAnimation(true);

                SetApplyTextContainer(GameSettingsApplyGrid, gridColumn: 1);
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void InitializeSettings(object sender, RoutedEventArgs e)
        {
            try
            {
                ImageBackgroundManager.Shared.IsBackgroundElevated = true;
                ImageBackgroundManager.Shared.ForegroundOpacity    = 0d;
                ImageBackgroundManager.Shared.SmokeOpacity         = 1d;

                GameResolutionSelector.ItemsSource = ScreenResolutionsList;
                if (CurrentGameProperty.IsGameRunning)
                {
                #if !GSPBYPASSGAMERUNNING
                    Overlay.Visibility     = Visibility.Visible;
                    PageContent.Visibility = Visibility.Collapsed;
                    OverlayTitle.Text      = Locale.Current.Lang?._GameSettingsPage.OverlayGameRunningTitle;
                    OverlaySubtitle.Text   = Locale.Current.Lang?._GameSettingsPage.OverlayGameRunningSubtitle;
                #endif
                }
                else if (GameInstallationState
                    is GameInstallStateEnum.NotInstalled
                    or GameInstallStateEnum.NeedsUpdate
                    or GameInstallStateEnum.InstalledHavePlugin
                    or GameInstallStateEnum.GameBroken)
                {
                    Overlay.Visibility     = Visibility.Visible;
                    PageContent.Visibility = Visibility.Collapsed;
                    OverlayTitle.Text      = Locale.Current.Lang._GameSettingsPage.OverlayNotInstalledTitle;
                    OverlaySubtitle.Text   = Locale.Current.Lang._GameSettingsPage.OverlayNotInstalledSubtitle;
                }
                else
                {
                #if !DISABLEDISCORD
                    InnerLauncherConfig.AppDiscordPresence.SetActivity(ActivityType.GameSettings);
                #endif
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL ERROR!!!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }
    }
}
