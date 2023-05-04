using CollapseLauncher.GameSettings.StarRail;
using CollapseLauncher.Interfaces;
using CollapseLauncher.Statics;
#if !DISABLEDISCORD
using Hi3Helper.DiscordPresence;
#endif
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public partial class StarRailGameSettingsPage : Page
    {
        private StarRailSettings Settings { get => (StarRailSettings)PageStatics._GameSettings; }
        private Brush InheritApplyTextColor { get; set; }
        public StarRailGameSettingsPage()
        {
            try
            {
                this.InitializeComponent();
                ApplyButton.Translation = Shadow32;
                GameSettingsApplyGrid.Translation = new System.Numerics.Vector3(0, 0, 64);

                InheritApplyTextColor = ApplyText.Foreground;
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
                    OverlayTitle.Text = Lang._StarRailGameSettingsPage.OverlayGameRunningTitle;
                    OverlaySubtitle.Text = Lang._StarRailGameSettingsPage.OverlayGameRunningSubtitle;

                    return;
                }
                else if (GameInstallationState == GameInstallStateEnum.NotInstalled
                      || GameInstallationState == GameInstallStateEnum.NeedsUpdate
                      || GameInstallationState == GameInstallStateEnum.GameBroken)
                {
                    Overlay.Visibility = Visibility.Visible;
                    PageContent.Visibility = Visibility.Collapsed;
                    OverlayTitle.Text = Lang._StarRailGameSettingsPage.OverlayNotInstalledTitle;
                    OverlaySubtitle.Text = Lang._StarRailGameSettingsPage.OverlayNotInstalledSubtitle;

                    return;
                }
#if !DISABLEDISCORD
                AppDiscordPresence.SetActivity(ActivityType.GameSettings);
                LogWriteLine($"Loaded Volume Master = {AudioMasterVolume}", Hi3Helper.LogType.Default, true);
                LogWriteLine($"Loaded Volume BGM    = {AudioBGMVolume}", Hi3Helper.LogType.Default, true);
                LogWriteLine($"Loaded Volume SFX    = {AudioSFXVolume}", Hi3Helper.LogType.Default, true);
                LogWriteLine($"Loaded Volume VO     = {AudioVOVolume}", Hi3Helper.LogType.Default, true);
                LogWriteLine($"Loaded Graphics FPS      = {FPS}", Hi3Helper.LogType.Default, true);
                LogWriteLine($"Loaded Graphics VSync    = {EnableVSync}", Hi3Helper.LogType.Default, true);
                LogWriteLine($"Loaded Graphics RenScal  = {RenderScale}", Hi3Helper.LogType.Default, true);
                LogWriteLine($"Loaded Graphics ResQ     = {ResolutionQuality}", Hi3Helper.LogType.Default, true);
                LogWriteLine($"Loaded Graphics ShadowQ  = {ShadowQuality}", Hi3Helper.LogType.Default, true);
                LogWriteLine($"Loaded Graphics LightQ   = {LightQuality}", Hi3Helper.LogType.Default, true);
                LogWriteLine($"Loaded Graphics CharaQ   = {CharacterQuality}", Hi3Helper.LogType.Default, true);
                LogWriteLine($"Loaded Graphics EnvDetQ  = {EnvDetailQuality}", Hi3Helper.LogType.Default, true);
                LogWriteLine($"Loaded Graphics ReflQ    = {ReflectionQuality}", Hi3Helper.LogType.Default, true);
                LogWriteLine($"Loaded Graphics BloomQ   = {BloomQuality}", Hi3Helper.LogType.Default, true);
                LogWriteLine($"Loaded Graphics AAMode   = {AAMode}", Hi3Helper.LogType.Default, true);

#endif
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
                ApplyText.Foreground = InheritApplyTextColor;
                ApplyText.Text = Lang._StarRailGameSettingsPage.SettingsApplied;
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