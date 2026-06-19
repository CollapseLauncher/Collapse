using CollapseLauncher.GameSettings.Zenless;
using CollapseLauncher.GameSettings.Zenless.Enums;
using Hi3Helper.SentryHelper;
using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Screen;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Drawing;
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Pages
{
    public partial class ZenlessGameSettingsPage
    {
        #region Fields
        private ZenlessSettings? SettingsThis { get => field ??= Settings as ZenlessSettings; }

        // ReSharper disable once IdentifierTypo
        private static bool? _isDeviceHasRTXGPU;
        // ReSharper disable once IdentifierTypo
        public bool IsDeviceHasRTXGPU
        {
            get
            {
                if (_isDeviceHasRTXGPU != null)
                {
                    return (bool)_isDeviceHasRTXGPU;
                }

                foreach (ReadOnlySpan<char> str in EnumerateGpuNames.GetEnumerateGpuNames())
                {
                    if (!str.Contains("Nvidia", StringComparison.OrdinalIgnoreCase) ||
                        !str.Contains("RTX",    StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    _isDeviceHasRTXGPU = true;
                    return true;
                }

                _isDeviceHasRTXGPU = false;
                return false;
            }
        }
        #endregion

        #region Methods
        private GraphicsPresetOption? oldPreset;
        private void PresetSelector(object sender, SelectionChangedEventArgs _)
        {
            _changingPreset = true;
            GraphicsPresetOption idx = (GraphicsPresetOption)((ComboBox)sender).SelectedIndex;
            if (oldPreset == idx) return;
            oldPreset = idx;

            switch (idx)
            {
                case GraphicsPresetOption.High:
                    VSyncToggle.IsChecked                    = true;
                    RenderResolutionSelector.SelectedIndex   = (int)RenderResOption.f10;
                    AntiAliasingSelector.SelectedIndex       = (int)AntiAliasingOption.TAA;
                    GlobalIlluminationSelector.SelectedIndex = (int)QualityOption3.High;
                    ShadowQualitySelector.SelectedIndex      = (int)QualityOption3.High;
                    FxQualitySelector.SelectedIndex          = (int)QualityOption5.High;
                    ShadingQualitySelector.SelectedIndex     = (int)QualityOption3.High;
                    CharacterQualitySelector.SelectedIndex   = (int)QualityOption2.High;
                    EnvironmentQualitySelector.SelectedIndex = (int)QualityOption2.High;
                    AnisotropicSamplingSelector.SelectedIndex = (int)AnisotropicSamplingOption.x8;
                    ReflectionQualitySelector.SelectedIndex  = (int)QualityOption4.High;
                    VolumetricFogSelector.SelectedIndex      = (int)QualityOption4.High;
                    HpcaSelector.SelectedIndex               = (int)HiPrecisionCharaAnimOption.Dynamic;
                    BloomToggle.IsChecked                    = true;
                    DistortionToggle.IsChecked               = true;
                    MotionBlurToggle.IsChecked               = true;
                    break;
                case GraphicsPresetOption.Medium:
                    VSyncToggle.IsChecked                     = true;
                    RenderResolutionSelector.SelectedIndex    = (int)RenderResOption.f10;
                    AntiAliasingSelector.SelectedIndex        = (int)AntiAliasingOption.TAA;
                    GlobalIlluminationSelector.SelectedIndex  = (int)QualityOption3.High;
                    ShadowQualitySelector.SelectedIndex       = (int)QualityOption3.High;
                    FxQualitySelector.SelectedIndex           = (int)QualityOption5.Medium;
                    ShadingQualitySelector.SelectedIndex      = (int)QualityOption3.High;
                    CharacterQualitySelector.SelectedIndex    = (int)QualityOption2.High;
                    EnvironmentQualitySelector.SelectedIndex  = (int)QualityOption2.High;
                    AnisotropicSamplingSelector.SelectedIndex = (int)AnisotropicSamplingOption.x8;
                    ReflectionQualitySelector.SelectedIndex   = (int)QualityOption4.Medium;
                    VolumetricFogSelector.SelectedIndex       = (int)QualityOption4.Medium;
                    HpcaSelector.SelectedIndex                = (int)HiPrecisionCharaAnimOption.Off;
                    BloomToggle.IsChecked                     = true;
                    DistortionToggle.IsChecked                = true;
                    MotionBlurToggle.IsChecked                = true;
                    break;
                case GraphicsPresetOption.Low:
                    VSyncToggle.IsChecked                     = true;
                    RenderResolutionSelector.SelectedIndex    = (int)RenderResOption.f10;
                    AntiAliasingSelector.SelectedIndex        = (int)AntiAliasingOption.TAA;
                    GlobalIlluminationSelector.SelectedIndex  = (int)QualityOption3.Low;
                    ShadowQualitySelector.SelectedIndex       = (int)QualityOption3.Medium;
                    FxQualitySelector.SelectedIndex           = (int)QualityOption5.Low;
                    ShadingQualitySelector.SelectedIndex      = (int)QualityOption3.High;
                    CharacterQualitySelector.SelectedIndex    = (int)QualityOption2.High;
                    EnvironmentQualitySelector.SelectedIndex  = (int)QualityOption2.High;
                    AnisotropicSamplingSelector.SelectedIndex = (int)AnisotropicSamplingOption.x8;
                    ReflectionQualitySelector.SelectedIndex   = (int)QualityOption4.Low;
                    VolumetricFogSelector.SelectedIndex       = (int)QualityOption4.Low;
                    HpcaSelector.SelectedIndex                = (int)HiPrecisionCharaAnimOption.Off;
                    BloomToggle.IsChecked                     = true;
                    DistortionToggle.IsChecked                = true;
                    MotionBlurToggle.IsChecked                = true;
                    break;
            }

            _changingPreset = false;
        }

        private bool _changingPreset;
        
        private async void EnforceCustomPreset()
        {
            try
            {
                if (_changingPreset) return;
                if (GraphicsPresetSelector.SelectedIndex == (int)GraphicsPresetOption.Custom) return;
                _changingPreset                      = true;
                GraphicsPresetSelector.SelectedIndex = (int)GraphicsPresetOption.Custom;
                await System.Threading.Tasks.Task.Delay(200);
                _changingPreset = false;
            }
            catch (Exception e)
            {
                await SentryHelper.ExceptionHandlerAsync(e);
            }
        }

        private void EnforceCustomPreset_Checkbox(object _, RoutedEventArgs n)
        {
            EnforceCustomPreset();
        }

        private void EnforceCustomPreset_ComboBox(object _, SelectionChangedEventArgs n)
        {
            EnforceCustomPreset();
        }
        #endregion

        #region GameResolution
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private List<bool> ScreenResolutionIsFullscreenIdx = [];

        public bool IsFullscreenEnabled
        {
            get => SettingsThis?.SettingsScreen.isfullScreen ?? false;
            set
            {
                SettingsThis?.SettingsScreen.isfullScreen = value;
                if (value)
                {
                    GameWindowResizable.IsEnabled = false;
                    GameWindowResizable.IsChecked = false;
                    GameResolutionFullscreenExclusive.IsEnabled = !IsCustomResolutionEnabled;
                    GameResolutionBorderless.IsChecked = false;
                    GameResolutionBorderless.IsEnabled = false;
                    return;
                }
                GameWindowResizable.IsEnabled = true;
                GameResolutionFullscreenExclusive.IsEnabled = false;
                GameResolutionFullscreenExclusive.IsChecked = false;
                GameResolutionBorderless.IsEnabled = true;
            }
        }

        public bool IsBorderlessEnabled
        {
            get => SettingsThis?.SettingsCollapseScreen.UseBorderlessScreen ?? false;
            set
            {
                SettingsThis?.SettingsCollapseScreen.UseBorderlessScreen = value;
                if (value)
                {
                    GameWindowResizable.IsEnabled = false;
                    GameWindowResizable.IsChecked = false;
                    GameResolutionFullscreen.IsEnabled = false;
                    GameResolutionFullscreen.IsChecked = false;
                }
                else
                {
                    GameWindowResizable.IsEnabled      = GameResolutionFullscreen.IsChecked == false;
                    GameResolutionFullscreen.IsEnabled = true;
                }
            }
        }

        public bool IsCustomResolutionEnabled
        {
            get => SettingsThis?.SettingsCollapseScreen.UseCustomResolution ?? false;
            set
            {
                SettingsThis?.SettingsCollapseScreen.UseCustomResolution = value;
                if (value)
                {
                    GameResolutionFullscreenExclusive.IsEnabled = false;
                    GameResolutionSelector.IsEnabled = false;
                    GameCustomResolutionWidth.IsEnabled = true;
                    GameCustomResolutionHeight.IsEnabled = true;

                    string[] _size = ResolutionSelected.Split('x');
                    GameCustomResolutionWidth.Value = int.Parse(_size[0]);
                    GameCustomResolutionHeight.Value = int.Parse(_size[1]);

                    return;
                }
                GameCustomResolutionWidth.IsEnabled = false;
                GameCustomResolutionHeight.IsEnabled = false;
                GameResolutionFullscreenExclusive.IsEnabled = IsFullscreenEnabled;
                GameResolutionSelector.IsEnabled = true;

                Size size = ScreenProp.CurrentResolution;
                GameResolutionSelector.SelectedItem = $"{size.Width}x{size.Height}";
            }
        }

        /// <summary>
        /// Zenless does not support exclusive fullscreen
        /// </summary>
        public readonly bool IsCanExclusiveFullscreen = false;

        public bool IsExclusiveFullscreenEnabled
        {
            get => IsFullscreenEnabled && (SettingsThis?.SettingsCollapseScreen.UseExclusiveFullscreen ?? false);
            set
            {
                SettingsThis?.SettingsCollapseScreen.UseExclusiveFullscreen = value;
                if (value)
                {
                    GameCustomResolutionCheckbox.IsEnabled = false;
                    GameCustomResolutionCheckbox.IsChecked = false;
                    GameCustomResolutionWidth.IsEnabled = false;
                    GameCustomResolutionHeight.IsEnabled = false;
                    return;
                }
                GameCustomResolutionCheckbox.IsEnabled = true;
            }
        }

        public bool IsCanResizableWindow => !(SettingsThis?.SettingsScreen.isfullScreen ?? false) && !IsExclusiveFullscreenEnabled;

        public bool IsResizableWindow
        {
            get => SettingsThis?.SettingsCollapseScreen.UseResizableWindow ?? false;
            set
            {
                SettingsThis?.SettingsCollapseScreen.UseResizableWindow = value;
                if (value)
                {
                    GameCustomResolutionCheckbox.IsEnabled = true;
                }
                else
                {
                    GameCustomResolutionCheckbox.IsEnabled = false;
                    GameCustomResolutionCheckbox.IsChecked = false;
                }
            }
        }

        public bool IsCanCustomResolution => (SettingsThis?.SettingsCollapseScreen.UseResizableWindow ?? false) && !IsExclusiveFullscreenEnabled;

        public int ResolutionW
        {
            get => SettingsThis?.SettingsScreen.sizeRes.Width ?? 0;
            set => SettingsThis?.SettingsScreen.sizeRes = new Size(value, ResolutionH);
        }

        public int ResolutionH
        {
            get => SettingsThis?.SettingsScreen.sizeRes.Height ?? 0;
            set => SettingsThis?.SettingsScreen.sizeRes = new Size(ResolutionW, value);
        }

        public bool IsCanResolutionWH => IsCustomResolutionEnabled;

        internal string ResolutionSelected
        {
            get
            {
                string? res = SettingsThis?.SettingsScreen.sizeResString;
                if (!string.IsNullOrEmpty(res))
                {
                    return res;
                }

                Size size = ScreenProp.CurrentResolution;
                return $"{size.Width}x{size.Height}";
            }
        }

        private bool _isAllowResolutionIndexChanged;
        public int ResolutionIndexSelected
        {
            get
            {
                // Set the resolution index to 0 if the list is empty
                if (ScreenResolutionIsFullscreenIdx.Count == 0)
                {
                    return 0;
                }

                // Get index of the resolution and clamp it to the valid index if possible (to default as 0).
                // [Added from @bagusnl docs] Usually, the game will use -1 as its arbitrary value (SMH)
                int res      = SettingsThis?.GeneralData?.ResolutionIndex ?? 0;
                int indexRes = ScreenResolutionIsFullscreenIdx.Count < res || res < 0 ? 0 : res;

                // Get the value from Fullscreen index of the resolution
                IsFullscreenEnabled = ScreenResolutionIsFullscreenIdx[indexRes];

                // Return the index to the ComboBox
                return indexRes;
            }
            set
            {
                // If resolution change isn't ready, then return
                if (!_isAllowResolutionIndexChanged)
                {
                    return;
                }

                // Clamp first and set to 0 (default resolution) if out of bound
                if (ScreenResolutionIsFullscreenIdx.Count < value || value < 0)
                {
                    value = 0;
                }

                // Get the fullscreen value
                bool isFullscreen = ScreenResolutionIsFullscreenIdx[value];
                IsFullscreenEnabled = isFullscreen;

                // Set the resolution index
                SettingsThis?.GeneralData.ResolutionIndex = value;
            }
        }
        #endregion

        #region Misc
        public bool IsGameBoost
        {
            get => SettingsThis?.SettingsCollapseMisc?.UseGameBoost ?? false;
            set => SettingsThis?.SettingsCollapseMisc.UseGameBoost = value;
        }
        
        public bool IsMobileMode
        {
            get => SettingsThis?.SettingsCollapseMisc?.LaunchMobileMode ?? false;
            set
            {
                SettingsThis?.SettingsCollapseMisc.LaunchMobileMode = value;
                SettingsContext.LocalUILayoutPlatform = value ? LocalUiLayoutPlatform.Mobile : LocalUiLayoutPlatform.PC;
            }
        }

        public bool AdvancedGraphics_UseDirectX12Api
        {
            get => SettingsThis?.SettingsCollapseScreen.GameGraphicsAPI == 4;
            set
            {
                SettingsThis?.SettingsCollapseScreen.GameGraphicsAPI = value ? 4 : 3;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Advanced Settings
        public bool IsUseAdvancedSettings
        {
            get => SettingsThis?.SettingsCollapseMisc?.UseAdvancedGameSettings ?? false;
            set
            {
                SettingsThis?.SettingsCollapseMisc.UseAdvancedGameSettings = value;
                OnPropertyChanged();
            }
        }

        public bool IsUsePreLaunchCommand
        {
            get 
            {
                bool value = SettingsThis?.SettingsCollapseMisc?.UseGamePreLaunchCommand ?? false;

                if (value)
                {
                    PreLaunchCommandTextBox.IsEnabled   = true;
                    PreLaunchForceCloseToggle.IsEnabled = true;
                }
                else
                {
                    PreLaunchCommandTextBox.IsEnabled   = false;
                    PreLaunchForceCloseToggle.IsEnabled = false;
                }

                return value;
            }
            set
            {
                if (value)
                {
                    PreLaunchCommandTextBox.IsEnabled   = true;
                    PreLaunchForceCloseToggle.IsEnabled = true;
                    GameLaunchDelay.IsEnabled           = true;
                }
                else
                {
                    PreLaunchCommandTextBox.IsEnabled   = false;
                    PreLaunchForceCloseToggle.IsEnabled = false;
                    GameLaunchDelay.IsEnabled           = false;
                }

                SettingsThis?.SettingsCollapseMisc.UseGamePreLaunchCommand = value;
                OnPropertyChanged();
            }
        }

        public string PreLaunchCommand
        {
            get => SettingsThis?.SettingsCollapseMisc?.GamePreLaunchCommand ?? string.Empty;
            set
            {
                SettingsThis?.SettingsCollapseMisc.GamePreLaunchCommand = value;
                OnPropertyChanged();
            }
        }

        public bool IsPreLaunchCommandExitOnGameClose
        {
            get => SettingsThis?.SettingsCollapseMisc?.GamePreLaunchExitOnGameStop ?? false;
            set
            {
                SettingsThis?.SettingsCollapseMisc.GamePreLaunchExitOnGameStop = value;
                OnPropertyChanged();
            }
        }

        public int LaunchDelay
        {
            get => SettingsThis?.SettingsCollapseMisc?.GameLaunchDelay ?? 0;
            set
            {
                SettingsThis?.SettingsCollapseMisc.GameLaunchDelay = value;
                OnPropertyChanged();
            }
        }
        
        public bool IsUsePostExitCommand
        {
            get 
            {
                bool value = SettingsThis?.SettingsCollapseMisc?.UseGamePostExitCommand ?? false;
                PostExitCommandTextBox.IsEnabled = value;
                return value;
            }
            set
            {
                PostExitCommandTextBox.IsEnabled = value;
                SettingsThis?.SettingsCollapseMisc.UseGamePostExitCommand = value;
            }
        }

        public string PostExitCommand
        {
            get => SettingsThis?.SettingsCollapseMisc?.GamePostExitCommand ?? string.Empty;
            set
            {
                SettingsThis?.SettingsCollapseMisc.GamePostExitCommand = value;
                OnPropertyChanged();
            }
        }

        public bool RunWithExplorerAsParent
        {
            get => SettingsThis?.SettingsCollapseMisc?.RunWithExplorerAsParent ?? false;
            set
            {
                SettingsThis?.SettingsCollapseMisc.RunWithExplorerAsParent = value;
                OnPropertyChanged();
            }
        }
        #endregion
    }
}
