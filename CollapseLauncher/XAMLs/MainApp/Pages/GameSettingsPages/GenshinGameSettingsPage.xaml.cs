using CollapseLauncher.GameManagement.ImageBackground;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Animation;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.Graphics.Display;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.Win32;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Globalization.NumberFormatting;
using Windows.Graphics.DirectX;
using Windows.Storage;
using Windows.Storage.Streams;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static CollapseLauncher.Statics.GamePropertyVault;
using WinRT;


#if !DISABLEDISCORD
using CollapseLauncher.DiscordPresence;
#endif

#pragma warning disable IDE0130
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
// ReSharper disable AsyncVoidMethod

namespace CollapseLauncher.Pages
{
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [GeneratedBindableCustomProperty]
    public partial class GenshinGameSettingsPage
    {
        #region Properties
        private CanvasBitmap _hdrCalibrationIcon;
        private CanvasBitmap _hdrCalibrationScene;
        private CanvasBitmap _hdrCalibrationUI;
        private bool IsHDREnabled { get; }
        private bool IsHDRSupported { get; }

        #endregion

        #region Main GSP Methods
        public GenshinGameSettingsPage() : base(GetCurrentGameProperty().GameSettings, Registry.CurrentUser.CreateSubKey(Path.Combine($"Software\\{GetCurrentGameProperty().GameVersion.VendorTypeProp.VendorType}", GetCurrentGameProperty().GameVersion.GamePreset.InternalGameNameInConfig!)))
        {
            try
            {
#nullable enable
                // ReSharper disable once UnusedVariable
                DisplayAdvancedColorInfo? colorInfo = WindowUtility.CurrentWindowDisplayColorInfo;
#if SIMULATEGIHDR
                IsHDREnabled = true;
                IsHDRSupported = true;
#else
                IsHDREnabled = colorInfo?.CurrentAdvancedColorKind == DisplayAdvancedColorKind.HighDynamicRange;
                IsHDRSupported = colorInfo?.IsAdvancedColorKindAvailable(DisplayAdvancedColorKind.HighDynamicRange) ?? false;
#endif
#nullable restore

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
                    OverlayTitle.Text      = Locale.Current.Lang?._StarRailGameSettingsPage.OverlayGameRunningTitle;
                    OverlaySubtitle.Text   = Locale.Current.Lang?._StarRailGameSettingsPage.OverlayGameRunningSubtitle;
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
                    OverlayTitle.Text      = Locale.Current.Lang?._StarRailGameSettingsPage.OverlayNotInstalledTitle;
                    OverlaySubtitle.Text   = Locale.Current.Lang?._StarRailGameSettingsPage.OverlayNotInstalledSubtitle;
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

        #endregion

        #region Method - NumberBox to Slider
        // All methods in this region responsible for too much time wasted because thank you WinUI!
        // Basically these handle value linking for a slider that has numberbox attached with it. Handles the rounding, and also error fallback when the numberbox is cleared.

        private void GammaValue_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            try
            {
                // Check if NumberBox is cleared (NaN) then reuse old value
                // Why is this not the default behavior instead of throwing errors? I don't know ask Microsoft
                if (double.IsNaN(args.NewValue))
                {
                    LogWriteLine($"Gamma value from NumberBox is invalid, resetting it to last value: {args.OldValue}", LogType.Warning);
                    GammaValue.Value = args.OldValue;
                }
                else
                {
                    IncrementNumberRounder rounder = new()
                    {
                        Increment = 0.0000001
                    };

                    DecimalFormatter formatter = new()
                    {
                        IntegerDigits  = 1,
                        FractionDigits = 5,
                        NumberRounder  = rounder
                    };
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
                    LogWriteLine($"MaxLuminosity value from NumberBox is invalid, resetting it to last value: {args.OldValue}", LogType.Warning);
                    MaxLuminosityValue.Value = args.OldValue;
                }
                else
                {
                    IncrementNumberRounder rounder = new()
                    {
                        Increment = 0.1
                    };

                    DecimalFormatter formatter = new()
                    {
                        IntegerDigits  = 3,
                        FractionDigits = 1,
                        NumberRounder  = rounder
                    };
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
                    LogWriteLine($"UiPaperWhite value from NumberBox is invalid, resetting it to last value: {args.OldValue}", LogType.Warning);
                    UiPaperWhiteValue.Value = args.OldValue;
                }
                else
                {
                    IncrementNumberRounder rounder = new()
                    {
                        Increment = 0.1
                    };

                    DecimalFormatter formatter = new()
                    {
                        IntegerDigits  = 3,
                        FractionDigits = 1,
                        NumberRounder  = rounder
                    };
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
                    LogWriteLine($"ScenePaperWhite value from NumberBox is invalid, resetting it to last value: {args.OldValue}", LogType.Warning);
                    ScenePaperWhiteValue.Value = args.OldValue;
                }
                else
                {
                    IncrementNumberRounder rounder = new()
                    {
                        Increment = 0.1
                    };

                    DecimalFormatter formatter = new()
                    {
                        IntegerDigits  = 3,
                        FractionDigits = 1,
                        NumberRounder  = rounder
                    };
                    ScenePaperWhiteValue.NumberFormatter = formatter;

                    ScenePaperWhiteSlider.Value = ScenePaperWhiteValue.Value;
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error when processing ScenePaperWhite NumberBox!\r\n{ex}", LogType.Error, true);
            }
        }
        #endregion

        #region Method - Slider to Numberbox
        // Methods to link all slider value to numberbox, for those with one.

        private void GammaSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            GammaValue.Value = Math.Round(e.NewValue, 5);
        }

        private void MaxLuminositySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (e.OldValue == 0) return;
            double value = Math.Round(e.NewValue, 1);
            MaxLuminosityValue.Value = value;
            MaxLuminosity            = value;
            DrawHDRCalibrationImage1();
        }

        private void UiPaperWhiteSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (e.OldValue == 0) return;
            double value = Math.Round(e.NewValue, 1);
            UiPaperWhiteValue.Value = value;
            UiPaperWhite            = value;
            DrawHDRCalibrationImage2();
        }

        private void ScenePaperWhiteSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (e.OldValue == 0) return;
            double value = Math.Round(e.NewValue, 1);
            ScenePaperWhiteValue.Value = value;
            ScenePaperWhite            = value;
            DrawHDRCalibrationImage2();
        }
        #endregion

        #region Method - HDR Calibration Panels
        private static async Task<StorageFile> GetAppFileAsync(Uri uri)
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
            CanvasSwapChainPanel panel     = HDRCalibrationPanel1;
            CanvasSwapChain      swapChain = panel.SwapChain;

            if (swapChain == null) return;
            float w      = (float)panel.Width;
            float h      = (float)panel.Height;
            float bgGain = (float)MaxLuminosity / 80;

            using (CanvasDrawingSession ds = swapChain.CreateDrawingSession(Colors.White))
            {
                CanvasSolidColorBrush white = CanvasSolidColorBrush.CreateHdr(swapChain, new Vector4(125, 125, 125, 1));
                ds.FillRectangle(0, 0, w, h, white);

                LinearTransferEffect bg = new()
                {
                    Source = _hdrCalibrationIcon,
                    BufferPrecision = CanvasBufferPrecision.Precision16Float,
                    RedSlope = bgGain,
                    GreenSlope = bgGain,
                    BlueSlope = bgGain
                };
                ds.DrawImage(bg, new Rect((w - 0.6 * h) / 2, h * 0.2, h * 0.6, h * 0.6), _hdrCalibrationIcon.Bounds);
            }
            swapChain.Present();
        }

        private async void SwapChainPanel1_OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                CanvasSwapChainPanel panel     = sender as CanvasSwapChainPanel;
                CanvasDevice         device    = CanvasDevice.GetSharedDevice();
                float                w         = (float)panel.Width;
                float                h         = (float)panel.Height;
                float                dpi       = 96 * (float)XamlRoot.RasterizationScale;
                CanvasSwapChain      swapChain = new(device, w, h, dpi, DirectXPixelFormat.R16G16B16A16Float, 2, CanvasAlphaMode.Premultiplied);
                panel.SwapChain = swapChain;

                // Unpacked app failed to open ms-appx uri, so we need to read it manually :(
                StorageFile bgFile = await GetAppFileAsync(new Uri("ms-appx:///Assets/Images/GenshinHDRCalibration/Sign.png"));
                using (IRandomAccessStream stream = await bgFile.OpenReadAsync())
                {
                    _hdrCalibrationIcon = await CanvasBitmap.LoadAsync(swapChain, stream, dpi);
                }

                MaxLuminositySlider.Value = MaxLuminosity;
                DrawHDRCalibrationImage1();
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
        }

        private static float GammaCorrection(float val, float max) => val * MathF.Pow(val / max, 2.2f);

        private void DrawHDRCalibrationImage2()
        {
            CanvasSwapChainPanel panel     = HDRCalibrationPanel2;
            CanvasSwapChain      swapChain = panel.SwapChain;
            
            if (swapChain == null) return;
            float w      = (float)panel.Width;
            float h      = (float)panel.Height;
            float bgGain = (float)ScenePaperWhite / 80;
            float uiGain = (GammaCorrection(((float)UiPaperWhite - (float)ScenePaperWhite + 350) * 2, 1600) + 50) / 80;

            using (CanvasDrawingSession ds = swapChain.CreateDrawingSession(Colors.White))
            {
                LinearTransferEffect bg = new()
                {
                    Source = _hdrCalibrationScene,
                    BufferPrecision = CanvasBufferPrecision.Precision16Float,
                    RedSlope = bgGain,
                    GreenSlope = bgGain,
                    BlueSlope = bgGain
                };
                ds.DrawImage(bg, new Rect(0, 0, w, h), _hdrCalibrationScene.Bounds);

                LinearTransferEffect ui = new()
                {
                    Source = _hdrCalibrationUI,
                    BufferPrecision = CanvasBufferPrecision.Precision16Float,
                    RedSlope = uiGain,
                    GreenSlope = uiGain,
                    BlueSlope = uiGain
                };
                ds.DrawImage(ui, new Rect(0, 0, w, h), _hdrCalibrationUI.Bounds);
            }
            swapChain.Present();
        }

        private async void SwapChainPanel2_OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                CanvasSwapChainPanel panel     = sender as CanvasSwapChainPanel;
                CanvasDevice         device    = CanvasDevice.GetSharedDevice();
                float                w         = (float)panel.Width;
                float                h         = (float)panel.Height;
                float                dpi       = 96 * (float)XamlRoot.RasterizationScale;
                CanvasSwapChain      swapChain = new(device, w, h, dpi, DirectXPixelFormat.R16G16B16A16Float, 2, CanvasAlphaMode.Premultiplied);
                panel.SwapChain = swapChain;

                StorageFile bgFile = await GetAppFileAsync(new Uri("ms-appx:///Assets/Images/GenshinHDRCalibration/Scene.jxr"));
                using (IRandomAccessStream stream = await bgFile.OpenReadAsync())
                {
                    _hdrCalibrationScene = await CanvasBitmap.LoadAsync(swapChain, stream, dpi);
                }

                StorageFile uiFile = await GetAppFileAsync(new Uri("ms-appx:///Assets/Images/GenshinHDRCalibration/UI.jxr"));
                using (IRandomAccessStream stream = await uiFile.OpenReadAsync())
                {
                    _hdrCalibrationUI = await CanvasBitmap.LoadAsync(swapChain, stream, dpi);
                }

                ScenePaperWhiteSlider.Value = ScenePaperWhite;
                UiPaperWhiteSlider.Value    = UiPaperWhite;
                DrawHDRCalibrationImage2();
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
        }

        private void HDRExpander_OnExpanding(Expander sender, ExpanderExpandingEventArgs args)
        {
            sender.IsExpanded = IsHDREnabled;
        }
        #endregion
    }
}
