using CollapseLauncher.GameSettings.Genshin;
using CollapseLauncher.Interfaces;
#if !DISABLEDISCORD
using Hi3Helper.DiscordPresence;
#endif
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using RegistryUtils;
using System;
using System.IO;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static CollapseLauncher.Statics.GamePropertyVault;
using Hi3Helper;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Globalization.NumberFormatting;
using CollapseLauncher.Statics;

namespace CollapseLauncher.Pages
{
    public partial class GenshinGameSettingsPage : Page
    {
        private GamePresetProperty CurrentGameProperty { get; set; }
        private GenshinSettings Settings { get => (GenshinSettings)CurrentGameProperty._GameSettings; }
        private Brush InheritApplyTextColor { get; set; }
        private RegistryMonitor RegistryWatcher { get; set; }
        private bool IsNoReload = false;
        public GenshinGameSettingsPage()
        {
            CurrentGameProperty = GetCurrentGameProperty();
            try
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    RegistryWatcher = new RegistryMonitor(RegistryHive.CurrentUser, Path.Combine($"Software\\{CurrentGameProperty._GameVersion.VendorTypeProp.VendorType}", CurrentGameProperty._GameVersion.GamePreset.InternalGameNameInConfig));
                    RegistryWatcher.Start();
                    ToggleRegistrySubscribe(true);

                });

                LoadPage();
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void ToggleRegistrySubscribe(bool doSubscribe)
        {
            if (doSubscribe)
                RegistryWatcher.RegChanged += RegistryListener;
            else
                RegistryWatcher.RegChanged -= RegistryListener;
        }

        private void RegistryListener(object sender, EventArgs e)
        {
            if (!IsNoReload)
            {
                LogWriteLine("[GI GSP Module] RegistryMonitor has detected registry change outside of the launcher! Reloading the page...", LogType.Warning, true);
                DispatcherQueue.TryEnqueue(MainFrameChanger.ReloadCurrentMainFrame);
            }
        }

        private void LoadPage()
        {
            BackgroundImgChanger.ToggleBackground(true);
            Settings.ReloadSettings();

            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Disabled;
            ApplyButton.Translation = Shadow32;
            GameSettingsApplyGrid.Translation = new System.Numerics.Vector3(0, 0, 64);

            InheritApplyTextColor = ApplyText.Foreground;
        }

        private void RegistryExportClick(object sender, RoutedEventArgs e)
        {
            try
            {
                ToggleRegistrySubscribe(false);
                Exception exc = Settings.ExportSettings();

                if (exc != null) throw exc;

                ApplyText.Foreground = InheritApplyTextColor;
                ApplyText.Text = Lang._GameSettingsPage.SettingsRegExported;
                ApplyText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error has occurred while exporting registry!\r\n{ex}", LogType.Error, true);
                ApplyText.Foreground = new SolidColorBrush(new Windows.UI.Color { A = 255, R = 255, B = 0, G = 0 });
                ApplyText.Text = ex.Message;
                ApplyText.Visibility = Visibility.Visible;
            }
            finally
            {
                ToggleRegistrySubscribe(true);
            }
        }

        private void RegistryImportClick(object sender, RoutedEventArgs e)
        {
            try
            {
                ToggleRegistrySubscribe(false);
                Exception exc = Settings.ImportSettings();

                if (exc != null) throw exc;

                ApplyText.Foreground = InheritApplyTextColor;
                ApplyText.Text = Lang._GameSettingsPage.SettingsRegImported;
                ApplyText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error has occurred while importing registry!\r\n{ex}", LogType.Error, true);
                ApplyText.Foreground = new SolidColorBrush(new Windows.UI.Color { A = 255, R = 255, B = 0, G = 0 });
                ApplyText.Text = ex.Message;
                ApplyText.Visibility = Visibility.Visible;
            }
            finally
            {
                ToggleRegistrySubscribe(true);
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
#endif
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL ERROR!!!\r\n{ex}", LogType.Error, true);
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

                ToggleRegistrySubscribe(false);
                Settings.SaveSettings();
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
            finally
            {
                ToggleRegistrySubscribe(true);
            }
        }

        public string CustomArgsValue
        {
            get => ((IGameSettingsUniversal)CurrentGameProperty._GameSettings).SettingsCustomArgument.CustomArgumentValue;
            set
            {
                ToggleRegistrySubscribe(false);
                ((IGameSettingsUniversal)CurrentGameProperty._GameSettings).SettingsCustomArgument.CustomArgumentValue = value;
                ToggleRegistrySubscribe(true);
            }
        }

        private void OnUnload(object sender, RoutedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ToggleRegistrySubscribe(false);
                RegistryWatcher.Stop();
                RegistryWatcher.Dispose();
            });
        }

        /// <summary>
        /// This updates Gamma Slider every time GammaNumber is entered.
        /// Do NOT touch this unless really necessary to do so!
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void GammaValue_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            try
            {
                // Check if NumberBox is cleared (NaN) then reuse old value
                // Why is this not the default behavior instead of throwing errors? I don't know ask Microsoft
                if (double.IsNaN(args.NewValue))
                {
                    LogWriteLine($"Gamma value from NumberBox is invalid, resetting it to last value: {args.OldValue}", LogType.Warning, false);
                    GammaValue.Value = args.OldValue;
                }
                else
                {
                    IncrementNumberRounder rounder = new IncrementNumberRounder();
                    rounder.Increment = 0.0000001;

                    DecimalFormatter formatter = new DecimalFormatter();
                    formatter.IntegerDigits = 1;
                    formatter.FractionDigits = 5;
                    formatter.NumberRounder = rounder;
                    GammaValue.NumberFormatter = formatter;

                    GammaSlider.Value = GammaValue.Value;
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error when processing Gamma NumberBox!\r\n{ex}", LogType.Error, true);
            }
        }

        /// <summary>
        /// This updates Gamma NumberBox when slider is moved.
        /// no touchy :)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GammaSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            GammaValue.Value = Math.Round(e.NewValue, 5);
        }
    }
}
