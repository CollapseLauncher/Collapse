using CollapseLauncher.GameSettings.Genshin;
using CollapseLauncher.Interfaces;
using CollapseLauncher.Statics;
using Hi3Helper;
#if !DISABLEDISCORD
using Hi3Helper.DiscordPresence;
#endif
using Hi3Helper.Shared.ClassStruct;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Display;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using RegistryUtils;
using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Globalization.NumberFormatting;
using Windows.Graphics.DirectX;
using Windows.Storage;
using Windows.Storage.Streams;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static CollapseLauncher.Statics.GamePropertyVault;
using Brush = Microsoft.UI.Xaml.Media.Brush;

namespace CollapseLauncher.Pages
{
    public partial class GenshinGameSettingsPage : Page
    {
        private GamePresetProperty CurrentGameProperty { get; set; }
        private GenshinSettings Settings { get => (GenshinSettings)CurrentGameProperty._GameSettings; }
        private Brush InheritApplyTextColor { get; set; }
        private RegistryMonitor RegistryWatcher { get; set; }
        private bool IsNoReload { get; set; }
        private CanvasBitmap HDRCalibrationIcon;
        private CanvasBitmap HDRCalibrationScene;
        private CanvasBitmap HDRCalibrationUI;
        private bool IsHDREnabled { get; }
        private bool IsHDRSupported { get; }

        public GenshinGameSettingsPage()
        {
            try
            {
                CurrentGameProperty = GetCurrentGameProperty();
                DispatcherQueue.TryEnqueue(() =>
                {
                    RegistryWatcher = new RegistryMonitor(RegistryHive.CurrentUser, Path.Combine($"Software\\{CurrentGameProperty._GameVersion.VendorTypeProp.VendorType}", CurrentGameProperty._GameVersion.GamePreset.InternalGameNameInConfig));
                    ToggleRegistrySubscribe(true);
                });

                DisplayInformation displayInfo = DisplayInformation.CreateForWindowId(InnerLauncherConfig.m_windowID);
                DisplayAdvancedColorInfo colorInfo = displayInfo.GetAdvancedColorInfo();
                IsHDREnabled = colorInfo.CurrentAdvancedColorKind == DisplayAdvancedColorKind.HighDynamicRange;
                IsHDRSupported = colorInfo.IsAdvancedColorKindAvailable(DisplayAdvancedColorKind.HighDynamicRange);

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
            GameSettingsApplyGrid.Translation = new Vector3(0, 0, 64);

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

                if (CurrentGameProperty.IsGameRunning)
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
                try
                {
                    ToggleRegistrySubscribe(false);
                    RegistryWatcher.Dispose();
                }
                catch(Exception ex)
                {
                    LogWriteLine($"[GI GSP Module] Error when disposing RegistryWatcher module!\r\n{ex}", LogType.Error, true);
                }
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

        private void MaxLuminosityValue_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            try
            {
                if (double.IsNaN(args.NewValue))
                {
                    LogWriteLine($"MaxLuminosity value from NumberBox is invalid, resetting it to last value: {args.OldValue}", LogType.Warning, false);
                    MaxLuminosityValue.Value = args.OldValue;
                }
                else
                {
                    IncrementNumberRounder rounder = new IncrementNumberRounder();
                    rounder.Increment = 0.1;

                    DecimalFormatter formatter = new DecimalFormatter();
                    formatter.IntegerDigits = 3;
                    formatter.FractionDigits = 1;
                    formatter.NumberRounder = rounder;
                    MaxLuminosityValue.NumberFormatter = formatter;

                    MaxLuminositySlider.Value = MaxLuminosityValue.Value;
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error when processing MaxLuminosity NumberBox!\r\n{ex}", LogType.Error, true);
            }
        }

        private void UiPaperWhiteValue_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            try
            {
                if (double.IsNaN(args.NewValue))
                {
                    LogWriteLine($"UiPaperWhite value from NumberBox is invalid, resetting it to last value: {args.OldValue}", LogType.Warning, false);
                    UiPaperWhiteValue.Value = args.OldValue;
                }
                else
                {
                    IncrementNumberRounder rounder = new IncrementNumberRounder();
                    rounder.Increment = 0.1;

                    DecimalFormatter formatter = new DecimalFormatter();
                    formatter.IntegerDigits = 3;
                    formatter.FractionDigits = 1;
                    formatter.NumberRounder = rounder;
                    UiPaperWhiteValue.NumberFormatter = formatter;

                    UiPaperWhiteSlider.Value = UiPaperWhiteValue.Value;
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error when processing UiPaperWhite NumberBox!\r\n{ex}", LogType.Error, true);
            }
        }

        private void ScenePaperWhiteValue_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            try
            {
                if (double.IsNaN(args.NewValue))
                {
                    LogWriteLine($"ScenePaperWhite value from NumberBox is invalid, resetting it to last value: {args.OldValue}", LogType.Warning, false);
                    ScenePaperWhiteValue.Value = args.OldValue;
                }
                else
                {
                    IncrementNumberRounder rounder = new IncrementNumberRounder();
                    rounder.Increment = 0.1;

                    DecimalFormatter formatter = new DecimalFormatter();
                    formatter.IntegerDigits = 3;
                    formatter.FractionDigits = 1;
                    formatter.NumberRounder = rounder;
                    ScenePaperWhiteValue.NumberFormatter = formatter;

                    ScenePaperWhiteSlider.Value = ScenePaperWhiteValue.Value;
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error when processing ScenePaperWhite NumberBox!\r\n{ex}", LogType.Error, true);
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

        private void MaxLuminositySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            MaxLuminosityValue.Value = Math.Round(e.NewValue, 1);
            DrawHDRCalibrationImage1();
        }

        private void UiPaperWhiteSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            UiPaperWhiteValue.Value = Math.Round(e.NewValue, 1);
            DrawHDRCalibrationImage2();
        }

        private void ScenePaperWhiteSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            ScenePaperWhiteValue.Value = Math.Round(e.NewValue, 1);
            DrawHDRCalibrationImage2();
        }

        private async Task<StorageFile> GetAppFileAsync(Uri uri)
        {
            StorageFile file;
            try
            {
                file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            }
            catch (ArgumentException)
            {
                file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath("." + uri.LocalPath));
            }
            return file;
        }

        private void DrawHDRCalibrationImage1()
        {
            CanvasSwapChainPanel panel = HDRCalibrationPanel1;
            CanvasSwapChain swapChain = panel.SwapChain;
            if (swapChain == null) return;
            float w = (float)panel.Width;
            float h = (float)panel.Height;
            float bgGain = (float)MaxLuminosity / 80;

            using (CanvasDrawingSession ds = swapChain.CreateDrawingSession(Colors.White))
            {
                CanvasSolidColorBrush white = CanvasSolidColorBrush.CreateHdr(swapChain, new Vector4(125, 125, 125, 1));
                ds.FillRectangle(0, 0, w, h, white);

                LinearTransferEffect bg = new LinearTransferEffect
                {
                    Source = HDRCalibrationIcon,
                    BufferPrecision = CanvasBufferPrecision.Precision16Float,
                    RedSlope = bgGain,
                    GreenSlope = bgGain,
                    BlueSlope = bgGain
                };
                ds.DrawImage(bg, new Rect((w - 0.6 * h) / 2, h * 0.2, h * 0.6, h * 0.6), HDRCalibrationIcon.Bounds);
            }
            swapChain.Present();
        }

        private async void SwapChainPanel1_OnLoaded(object sender, RoutedEventArgs e)
        {
            CanvasSwapChainPanel panel = sender as CanvasSwapChainPanel;
            CanvasDevice device = CanvasDevice.GetSharedDevice();
            float w = (float)panel.Width;
            float h = (float)panel.Height;
            float dpi = 96 * (float)XamlRoot.RasterizationScale;
            CanvasSwapChain swapChain = new CanvasSwapChain(device, w, h, dpi, DirectXPixelFormat.R16G16B16A16Float, 2, CanvasAlphaMode.Premultiplied);
            panel.SwapChain = swapChain;

            // Unpacked app failed to open ms-appx uri, so we need to read it manually :(
            StorageFile bgFile = await GetAppFileAsync(new Uri("ms-appx:///Assets/Images/GenshinHDRCalibrationSign.png"));
            using (IRandomAccessStream stream = await bgFile.OpenReadAsync())
            {
                HDRCalibrationIcon = await CanvasBitmap.LoadAsync(swapChain, stream, dpi);
            }

            DrawHDRCalibrationImage1();
        }

        private float GammaCorrection(float val, float max)
        {
            return val * MathF.Pow(val / max, 2.2f);
        }

        private void DrawHDRCalibrationImage2()
        {
            CanvasSwapChainPanel panel = HDRCalibrationPanel2;
            CanvasSwapChain swapChain = panel.SwapChain;
            if (swapChain == null) return;
            float w = (float)panel.Width;
            float h = (float)panel.Height;
            float bgGain = (float)ScenePaperWhite / 80;
            float uiGain = (GammaCorrection(((float)UiPaperWhite - (float)ScenePaperWhite + 350) * 2, 1600) + 50) / 80;

            using (CanvasDrawingSession ds = swapChain.CreateDrawingSession(Colors.White))
            {
                LinearTransferEffect bg = new LinearTransferEffect
                {
                    Source = HDRCalibrationScene,
                    BufferPrecision = CanvasBufferPrecision.Precision16Float,
                    RedSlope = bgGain,
                    GreenSlope = bgGain,
                    BlueSlope = bgGain
                };
                ds.DrawImage(bg, new Rect(0, 0, w, h), HDRCalibrationScene.Bounds);

                LinearTransferEffect ui = new LinearTransferEffect
                {
                    Source = HDRCalibrationUI,
                    BufferPrecision = CanvasBufferPrecision.Precision16Float,
                    RedSlope = uiGain,
                    GreenSlope = uiGain,
                    BlueSlope = uiGain
                };
                ds.DrawImage(ui, new Rect(0, 0, w, h), HDRCalibrationUI.Bounds);
            }
            swapChain.Present();
        }

        private async void SwapChainPanel2_OnLoaded(object sender, RoutedEventArgs e)
        {
            CanvasSwapChainPanel panel = sender as CanvasSwapChainPanel;
            CanvasDevice device = CanvasDevice.GetSharedDevice();
            float w = (float)panel.Width;
            float h = (float)panel.Height;
            float dpi = 96 * (float) XamlRoot.RasterizationScale;
            CanvasSwapChain swapChain = new CanvasSwapChain(device, w, h, dpi, DirectXPixelFormat.R16G16B16A16Float, 2, CanvasAlphaMode.Premultiplied);
            panel.SwapChain = swapChain;

            StorageFile bgFile = await GetAppFileAsync(new Uri("ms-appx:///Assets/Images/GenshinHDRCalibrationScene.jxr"));
            using (IRandomAccessStream stream = await bgFile.OpenReadAsync())
            {
                HDRCalibrationScene = await CanvasBitmap.LoadAsync(swapChain, stream, dpi);
            }

            StorageFile uiFile = await GetAppFileAsync(new Uri("ms-appx:///Assets/Images/GenshinHDRCalibrationUI.jxr"));
            using (IRandomAccessStream stream = await uiFile.OpenReadAsync())
            {
                HDRCalibrationUI = await CanvasBitmap.LoadAsync(swapChain, stream, dpi);
            }

            DrawHDRCalibrationImage2();
        }

        private void HDRExpander_OnExpanding(Expander sender, ExpanderExpandingEventArgs args)
        {
            sender.IsExpanded = IsHDREnabled;
        }
    }
}
