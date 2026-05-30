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
                SettingsThis?.GeneralData.LocalUILayoutPlatform =
                    value ? LocalUiLayoutPlatform.Mobile : LocalUiLayoutPlatform.PC;
            }
            
        }
        #endregion

        #region Advanced Settings
        public bool IsUseAdvancedSettings
        {
            get
            {
                bool value = SettingsThis?.SettingsCollapseMisc?.UseAdvancedGameSettings ?? false;
                AdvancedSettingsPanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                return value;
            }
            set
            {
                SettingsThis?.SettingsCollapseMisc.UseAdvancedGameSettings = value;
                AdvancedSettingsPanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
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
            }
        }

        public string PreLaunchCommand
        {
            get => SettingsThis?.SettingsCollapseMisc?.GamePreLaunchCommand ?? string.Empty;
            set => SettingsThis?.SettingsCollapseMisc.GamePreLaunchCommand = value;
        }

        public bool IsPreLaunchCommandExitOnGameClose
        {
            get => SettingsThis?.SettingsCollapseMisc?.GamePreLaunchExitOnGameStop ?? false;
            set => SettingsThis?.SettingsCollapseMisc.GamePreLaunchExitOnGameStop = value;
        }

        public int LaunchDelay
        {
            get => SettingsThis?.SettingsCollapseMisc?.GameLaunchDelay ?? 0;
            set => SettingsThis?.SettingsCollapseMisc.GameLaunchDelay = value;
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
            set => SettingsThis?.SettingsCollapseMisc.GamePostExitCommand = value;
        }

        public bool RunWithExplorerAsParent
        {
            get => SettingsThis?.SettingsCollapseMisc?.RunWithExplorerAsParent ?? false;
            set => SettingsThis?.SettingsCollapseMisc.RunWithExplorerAsParent = value;
        }
        #endregion

        #region Language Settings - GENERAL_DATA
        public int Lang_Text
        {
            get
            {
                int v = (int)(SettingsThis?.GeneralData.DeviceLanguageType ?? default);
                return v <= 0 ? 1 : v;
            }
            set => SettingsThis?.GeneralData.DeviceLanguageType = (LanguageText)value;
        }

        public int Lang_Audio
        {
            get
            {
                int v = (int)(SettingsThis?.GeneralData.DeviceLanguageVoiceType ?? default);
                return v <= 0 ? 1 : v;
            }
            set => SettingsThis?.GeneralData.DeviceLanguageVoiceType = (LanguageVoice)value;
        }
        #endregion

        #region Graphics Settings - GENERAL_DATA > SystemSettingDataMap
        public bool EnableVSync
        {
            get => SettingsThis?.GeneralData?.VSync ?? false;
            set => SettingsThis?.GeneralData.VSync = value;
        }
        
        public int Graphics_Preset
        {
            get => (int)(SettingsThis?.GeneralData.GraphicsPreset ?? default);
            set => SettingsThis?.GeneralData.GraphicsPreset = (GraphicsPresetOption)value;
        }
        
        public int Graphics_RenderRes
        {
            get => (int)(SettingsThis?.GeneralData.RenderResolution ?? default);
            set => SettingsThis?.GeneralData.RenderResolution = (RenderResOption)value;
        }
        
        public int Graphics_Shadow
        {
            get => (int)(SettingsThis?.GeneralData.ShadowQuality ?? default);
            set => SettingsThis?.GeneralData.ShadowQuality = (QualityOption3)value;
        }
        
        public int Graphics_AntiAliasing
        {
            get => (int)(SettingsThis?.GeneralData.AntiAliasing ?? default);
            set => SettingsThis?.GeneralData.AntiAliasing = (AntiAliasingOption)value;
        }
        
        public int Graphics_VolFog
        {
            get => (int)(SettingsThis?.GeneralData.VolumetricFogQuality ?? default);
            set => SettingsThis?.GeneralData.VolumetricFogQuality = (QualityOption4)value;
        }

        public bool Graphics_Bloom
        {
            get => SettingsThis?.GeneralData.Bloom ?? false;
            set => SettingsThis?.GeneralData.Bloom = value;
        }

        public bool Graphics_MotionBlur
        {
            get => SettingsThis?.GeneralData.MotionBlur ?? false;
            set => SettingsThis?.GeneralData.MotionBlur = value;
        }

        public int Graphics_Reflection
        {
            get => (int)(SettingsThis?.GeneralData.ReflectionQuality ?? default);
            set => SettingsThis?.GeneralData.ReflectionQuality = (QualityOption4)value;
        }
        
        public int Graphics_Effects
        {
            get => (int)(SettingsThis?.GeneralData.FxQuality ?? default);
            set => SettingsThis?.GeneralData.FxQuality = (QualityOption5)value;
        }

        public int Graphics_ColorFilter
        {
            get => SettingsThis?.GeneralData.ColorFilter ?? 0;
            set => SettingsThis?.GeneralData.ColorFilter = value;
        }
        
        public int Graphics_Character
        {
            get => (int)(SettingsThis?.GeneralData.CharacterQuality ?? default);
            set => SettingsThis?.GeneralData.CharacterQuality = (QualityOption2)value;
        }

        public bool Graphics_Distortion
        {
            get => SettingsThis?.GeneralData.Distortion ?? false;
            set => SettingsThis?.GeneralData.Distortion = value;
        }
        
        public int Graphics_Shading
        {
            get => (int)(SettingsThis?.GeneralData.ShadingQuality ?? default);
            set => SettingsThis?.GeneralData.ShadingQuality = (QualityOption3)value;
        }
        
        public int Graphics_Environment
        {
            get => (int)(SettingsThis?.GeneralData.EnvironmentQuality ?? default);
            set => SettingsThis?.GeneralData.EnvironmentQuality = (QualityOption2)value;
        }
        
        public int Graphics_AnisotropicSampling
        {
            get => (int)(SettingsThis?.GeneralData.AnisotropicSampling ?? default);
            set => SettingsThis?.GeneralData.AnisotropicSampling = (AnisotropicSamplingOption)value;
        }

        public int Graphics_GlobalIllumination
        {
            get => (int)(SettingsThis?.GeneralData.GlobalIllumination ?? default);
            set => SettingsThis?.GeneralData.GlobalIllumination = (QualityOption3)value;
        }

        public int Graphics_Fps
        {
            get => (int)(SettingsThis?.GeneralData.Fps ?? default);
            set => SettingsThis?.GeneralData.Fps = (FpsOption)value;
        }

        /// <inheritdoc cref="GameSettings.Zenless.GeneralData.HiPrecisionCharaAnim"/>
        public int Graphics_HiPreCharaAnim
        {
            get => (int)(SettingsThis?.GeneralData.HiPrecisionCharaAnim ?? default);
            set => SettingsThis?.GeneralData.HiPrecisionCharaAnim = (HiPrecisionCharaAnimOption)value;
        }

        public bool AdvancedGraphics_UseDirectX12Api
        {
            get => SettingsThis?.SettingsCollapseScreen.GameGraphicsAPI == 4;
            set => SettingsThis?.SettingsCollapseScreen.GameGraphicsAPI = value ? 4 : 3;
        }

        public bool AdvancedGraphics_UseRayTracing
        {
            get => SettingsThis?.GeneralData.RayTracing_Enabled ?? false;
            set => SettingsThis?.GeneralData.RayTracing_Enabled = value;
        }

        public int AdvancedGraphics_RayTracingQuality
        {
            get => (int)(SettingsThis?.GeneralData.RayTracing_Quality ?? default);
            set => SettingsThis?.GeneralData.RayTracing_Quality = (QualityOption3)value;
        }

        public int AdvancedGraphics_SuperResolutionOption
        {
            get => (int)(SettingsThis?.GeneralData.SuperResolution_Option ?? default);
            set => SettingsThis?.GeneralData.SuperResolution_Option = (SuperResolutionScalingOption)value;
        }

        public int AdvancedGraphics_SuperResolutionQuality
        {
            get => (int)(SettingsThis?.GeneralData.SuperResolution_Quality ?? default);
            set => SettingsThis?.GeneralData.SuperResolution_Quality = (SuperResolutionScalingQuality)value;
        }

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

        #region Audio Settings - GENERAL_DATA > SystemSettingDataMap

        public int Audio_VolMain
        {
            get => SettingsThis?.GeneralData.Audio_MainVolume ?? 0;
            set => SettingsThis?.GeneralData.Audio_MainVolume = value;
        }

        public int Audio_VolMusic
        {
            get => SettingsThis?.GeneralData.Audio_MusicVolume ?? 0;
            set => SettingsThis?.GeneralData.Audio_MusicVolume = value;
        }

        public int Audio_VolDialog
        {
            get => SettingsThis?.GeneralData.Audio_DialogVolume ?? 0;
            set => SettingsThis?.GeneralData.Audio_DialogVolume = value;
        }

        public int Audio_VolSfx
        {
            get => SettingsThis?.GeneralData.Audio_SfxVolume ?? 0;
            set => SettingsThis?.GeneralData.Audio_SfxVolume = value;
        }

        public int Audio_VolAmbient
        {
            get => SettingsThis?.GeneralData.Audio_AmbientVolume ?? 0;
            set => SettingsThis?.GeneralData.Audio_AmbientVolume = value;
        }

        public int Audio_PlaybackDevice
        {
            get => (int)(SettingsThis?.GeneralData.Audio_PlaybackDevice ?? default);
            set => SettingsThis?.GeneralData.Audio_PlaybackDevice = (AudioPlaybackDevice)value;
        }

        public bool Audio_MuteOnMinimize
        {
            get => SettingsThis?.GeneralData.Audio_MuteOnMinimize ?? false;
            set => SettingsThis?.GeneralData.Audio_MuteOnMinimize = value;
        }
        #endregion
    }
}
