using CollapseLauncher.Dialogs;
using CollapseLauncher.Helper;
using CollapseLauncher.GameManagement.ImageBackground;
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

#if !DISABLEDISCORD
using CollapseLauncher.DiscordPresence;
// ReSharper disable AsyncVoidMethod
#endif

#nullable enable
namespace CollapseLauncher.Pages
{
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    public partial class StarRailGameSettingsPage
    {
        private const string AbValueName = "App_Settings_h2319593470";

        public StarRailGameSettingsPage() : base(GetCurrentGameProperty().GameSettings, Registry.CurrentUser.CreateSubKey(Path.Combine($"Software\\{GetCurrentGameProperty().GameVersion?.VendorTypeProp.VendorType}", GetCurrentGameProperty().GameVersion?.GamePreset.InternalGameNameInConfig!)))
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

        protected override async void OnLoaded(object? sender, RoutedEventArgs args)
        {
            base.OnLoaded(sender, args);
            MobileModeToggleText.Text = $"[{Locale.Current.Lang?._Misc?.Tag_Deprecated}] {Locale.Current.Lang?._GameSettingsPage?.MobileLayout}";

            // A/B Testing as of 2023-12-26 (HSR v1.6.0)
            if (CheckAbTest(CurrentGameProperty))
            {
                await SimpleDialogs.Dialog_StarRailABTestingWarning();
            }
        }

        /// <summary>
        /// Returns true if A/B test registry identifier is found.
        /// </summary>
        public static bool CheckAbTest(GamePresetProperty? property = null)
        {
            property ??= GetCurrentGameProperty();

            object? abValue = Registry.CurrentUser
                                      .OpenSubKey($@"Software\{property.GameVersion?.VendorTypeProp.VendorType}\{property.GameVersion?.GamePreset.InternalGameNameInConfig}")?
                                      .GetValue(AbValueName);
            if (abValue == null!)
            {
                return false;
            }

            LogWriteLine("A/B Value Found. Settings will not apply to the game.", LogType.Warning, true);
            return true;
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
                    OverlayTitle.Text      = Locale.Current.Lang?._StarRailGameSettingsPage?.OverlayGameRunningTitle;
                    OverlaySubtitle.Text   = Locale.Current.Lang?._StarRailGameSettingsPage?.OverlayGameRunningSubtitle;
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
                    OverlayTitle.Text      = Locale.Current.Lang?._StarRailGameSettingsPage?.OverlayNotInstalledTitle;
                    OverlaySubtitle.Text   = Locale.Current.Lang?._StarRailGameSettingsPage?.OverlayNotInstalledSubtitle;
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
