using CollapseLauncher.GameSettings.Zenless;
using CollapseLauncher.GameSettings.Zenless.Enums;
using Hi3Helper.SentryHelper;
using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Screen;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.CompilerServices;
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable CommentTypo

// ReSharper disable InconsistentNaming

namespace CollapseLauncher.Pages
{
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    public partial class ZenlessGameSettingsPage : INotifyPropertyChanged
    {
        #region Methods
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        // ReSharper disable once UnusedMember.Local
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Raise the PropertyChanged event, passing the name of the property whose value has changed.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

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
            get => ((ZenlessSettings)Settings).SettingsScreen.isfullScreen;
            set
            {
                ((ZenlessSettings)Settings).SettingsScreen.isfullScreen = value;
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
            get => ((ZenlessSettings)Settings).SettingsCollapseScreen.UseBorderlessScreen;
            set
            {
                ((ZenlessSettings)Settings).SettingsCollapseScreen.UseBorderlessScreen = value;
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
            get => ((ZenlessSettings)Settings).SettingsCollapseScreen.UseCustomResolution;
            set
            {
                ((ZenlessSettings)Settings).SettingsCollapseScreen.UseCustomResolution = value;
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
            get => IsFullscreenEnabled && ((ZenlessSettings)Settings).SettingsCollapseScreen.UseExclusiveFullscreen;
            set
            {
                ((ZenlessSettings)Settings).SettingsCollapseScreen.UseExclusiveFullscreen = value;
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

        public bool IsCanResizableWindow => !((ZenlessSettings)Settings).SettingsScreen.isfullScreen && !IsExclusiveFullscreenEnabled;

        public bool IsResizableWindow
        {
            get => ((ZenlessSettings)Settings).SettingsCollapseScreen.UseResizableWindow;
            set
            {
                ((ZenlessSettings)Settings).SettingsCollapseScreen.UseResizableWindow = value;
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

        public bool IsCanCustomResolution => ((ZenlessSettings)Settings).SettingsCollapseScreen.UseResizableWindow && !IsExclusiveFullscreenEnabled;

        public int ResolutionW
        {
            get => ((ZenlessSettings)Settings).SettingsScreen.sizeRes.Width;
            set => ((ZenlessSettings)Settings).SettingsScreen.sizeRes = new Size(value, ResolutionH);
        }

        public int ResolutionH
        {
            get => ((ZenlessSettings)Settings).SettingsScreen.sizeRes.Height;
            set => ((ZenlessSettings)Settings).SettingsScreen.sizeRes = new Size(ResolutionW, value);
        }

        public bool IsCanResolutionWH => IsCustomResolutionEnabled;

        internal string ResolutionSelected
        {
            get
            {
                string res = ((ZenlessSettings)Settings).SettingsScreen.sizeResString;
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
                int res      = ((ZenlessSettings)Settings).GeneralData?.ResolutionIndex ?? 0;
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
                ((ZenlessSettings)Settings).GeneralData.ResolutionIndex = value;
            }
        }
        #endregion

        #region Misc
        public bool IsGameBoost
        {
            get => Settings?.SettingsCollapseMisc?.UseGameBoost ?? false;
            set => ((ZenlessSettings)Settings).SettingsCollapseMisc.UseGameBoost = value;
        }
        
        public bool IsMobileMode
        {
            get => Settings?.SettingsCollapseMisc?.LaunchMobileMode ?? false;
            set
            {
                ((ZenlessSettings)Settings).SettingsCollapseMisc.LaunchMobileMode = value;
                ((ZenlessSettings)Settings).GeneralData.LocalUILayoutPlatform =
                    value ? LocalUiLayoutPlatform.Mobile : LocalUiLayoutPlatform.PC;
            }
            
        }
        #endregion

        #region Advanced Settings
        public bool IsUseAdvancedSettings
        {
            get
            {
                bool value = Settings?.SettingsCollapseMisc?.UseAdvancedGameSettings ?? false;
                AdvancedSettingsPanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                return value;
            }
            set
            {
                ((ZenlessSettings)Settings).SettingsCollapseMisc.UseAdvancedGameSettings = value;
                AdvancedSettingsPanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public bool IsUsePreLaunchCommand
        {
            get 
            {
                bool value = Settings?.SettingsCollapseMisc?.UseGamePreLaunchCommand ?? false;

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

                ((ZenlessSettings)Settings).SettingsCollapseMisc.UseGamePreLaunchCommand = value;
            }
        }

        public string PreLaunchCommand
        {
            get => Settings?.SettingsCollapseMisc?.GamePreLaunchCommand;
            set => ((ZenlessSettings)Settings).SettingsCollapseMisc.GamePreLaunchCommand = value;
        }

        public bool IsPreLaunchCommandExitOnGameClose
        {
            get => Settings?.SettingsCollapseMisc?.GamePreLaunchExitOnGameStop ?? false;
            set => ((ZenlessSettings)Settings).SettingsCollapseMisc.GamePreLaunchExitOnGameStop = value;
        }

        public int LaunchDelay
        {
            get => Settings?.SettingsCollapseMisc?.GameLaunchDelay ?? 0;
            set => ((ZenlessSettings)Settings).SettingsCollapseMisc.GameLaunchDelay = value;
        }
        
        public bool IsUsePostExitCommand
        {
            get 
            {
                bool value = Settings?.SettingsCollapseMisc?.UseGamePostExitCommand ?? false;
                PostExitCommandTextBox.IsEnabled = value;
                return value;
            }
            set
            {
                PostExitCommandTextBox.IsEnabled = value;
                ((ZenlessSettings)Settings).SettingsCollapseMisc.UseGamePostExitCommand = value;
            }
        }

        public string PostExitCommand
        {
            get => Settings?.SettingsCollapseMisc?.GamePostExitCommand;
            set => ((ZenlessSettings)Settings).SettingsCollapseMisc.GamePostExitCommand = value;
        }

        private void GameLaunchDelay_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            // clamp for negative value when clearing the number box
            if ((int)sender.Value < 0)
                sender.Value = 0;
        }

        public bool RunWithExplorerAsParent
        {
            get => ((ZenlessSettings)Settings).SettingsCollapseMisc.RunWithExplorerAsParent;
            set => ((ZenlessSettings)Settings).SettingsCollapseMisc.RunWithExplorerAsParent = value;
        }
        #endregion

        #region Language Settings - GENERAL_DATA
        public int Lang_Text
        {
            get
            {
                var v = (int)((ZenlessSettings)Settings).GeneralData.DeviceLanguageType;
                return v <= 0 ? 1 : v;
            }
            set => ((ZenlessSettings)Settings).GeneralData.DeviceLanguageType = (LanguageText)value;
        }

        public int Lang_Audio
        {
            get
            {
                var v = (int)((ZenlessSettings)Settings).GeneralData.DeviceLanguageVoiceType;
                return v <= 0 ? 1 : v;
            }
            set => ((ZenlessSettings)Settings).GeneralData.DeviceLanguageVoiceType = (LanguageVoice)value;
        }
        #endregion

        #region Graphics Settings - GENERAL_DATA > SystemSettingDataMap
        public bool EnableVSync
        {
            get => ((ZenlessSettings)Settings).GeneralData?.VSync ?? false;
            set => ((ZenlessSettings)Settings).GeneralData.VSync = value;
        }
        
        public int Graphics_Preset
        {
            get => (int)((ZenlessSettings)Settings).GeneralData.GraphicsPreset;
            set => ((ZenlessSettings)Settings).GeneralData.GraphicsPreset = (GraphicsPresetOption)value;
        }
        
        public int Graphics_RenderRes
        {
            get => (int)((ZenlessSettings)Settings).GeneralData.RenderResolution;
            set => ((ZenlessSettings)Settings).GeneralData.RenderResolution = (RenderResOption)value;
        }
        
        public int Graphics_Shadow
        {
            get => (int)((ZenlessSettings)Settings).GeneralData.ShadowQuality;
            set => ((ZenlessSettings)Settings).GeneralData.ShadowQuality = (QualityOption3)value;
        }
        
        public int Graphics_AntiAliasing
        {
            get => (int)((ZenlessSettings)Settings).GeneralData.AntiAliasing;
            set => ((ZenlessSettings)Settings).GeneralData.AntiAliasing = (AntiAliasingOption)value;
        }
        
        public int Graphics_VolFog
        {
            get => (int)((ZenlessSettings)Settings).GeneralData.VolumetricFogQuality;
            set => ((ZenlessSettings)Settings).GeneralData.VolumetricFogQuality = (QualityOption4)value;
        }

        public bool Graphics_Bloom
        {
            get => ((ZenlessSettings)Settings).GeneralData.Bloom;
            set => ((ZenlessSettings)Settings).GeneralData.Bloom = value;
        }

        public bool Graphics_MotionBlur
        {
            get => ((ZenlessSettings)Settings).GeneralData.MotionBlur;
            set => ((ZenlessSettings)Settings).GeneralData.MotionBlur = value;
        }

        public int Graphics_Reflection
        {
            get => (int)((ZenlessSettings)Settings).GeneralData.ReflectionQuality;
            set => ((ZenlessSettings)Settings).GeneralData.ReflectionQuality = (QualityOption4)value;
        }
        
        public int Graphics_Effects
        {
            get => (int)((ZenlessSettings)Settings).GeneralData.FxQuality;
            set => ((ZenlessSettings)Settings).GeneralData.FxQuality = (QualityOption5)value;
        }

        public int Graphics_ColorFilter
        {
            get => ((ZenlessSettings)Settings).GeneralData.ColorFilter;
            set => ((ZenlessSettings)Settings).GeneralData.ColorFilter = value;
        }
        
        public int Graphics_Character
        {
            get => (int)((ZenlessSettings)Settings).GeneralData.CharacterQuality;
            set => ((ZenlessSettings)Settings).GeneralData.CharacterQuality = (QualityOption2)value;
        }

        public bool Graphics_Distortion
        {
            get => ((ZenlessSettings)Settings).GeneralData.Distortion;
            set => ((ZenlessSettings)Settings).GeneralData.Distortion = value;
        }
        
        public int Graphics_Shading
        {
            get => (int)((ZenlessSettings)Settings).GeneralData.ShadingQuality;
            set => ((ZenlessSettings)Settings).GeneralData.ShadingQuality = (QualityOption3)value;
        }
        
        public int Graphics_Environment
        {
            get => (int)((ZenlessSettings)Settings).GeneralData.EnvironmentQuality;
            set => ((ZenlessSettings)Settings).GeneralData.EnvironmentQuality = (QualityOption2)value;
        }
        
        public int Graphics_AnisotropicSampling
        {
            get => (int)((ZenlessSettings)Settings).GeneralData.AnisotropicSampling;
            set => ((ZenlessSettings)Settings).GeneralData.AnisotropicSampling = (AnisotropicSamplingOption)value;
        }

        public int Graphics_GlobalIllumination
        {
            get => (int)((ZenlessSettings)Settings).GeneralData.GlobalIllumination;
            set => ((ZenlessSettings)Settings).GeneralData.GlobalIllumination = (QualityOption3)value;
        }

        public int Graphics_Fps
        {
            get => (int)((ZenlessSettings)Settings).GeneralData.Fps;
            set => ((ZenlessSettings)Settings).GeneralData.Fps = (FpsOption)value;
        }

        /// <inheritdoc cref="Game((ZenlessSettings)Settings).Zenless.GeneralData.HiPrecisionCharaAnim"/>
        public int Graphics_HiPreCharaAnim
        {
            get => (int)((ZenlessSettings)Settings).GeneralData.HiPrecisionCharaAnim;
            set => ((ZenlessSettings)Settings).GeneralData.HiPrecisionCharaAnim = (HiPrecisionCharaAnimOption)value;
        }

        public bool AdvancedGraphics_UseDirectX12Api
        {
            get => ((ZenlessSettings)Settings).SettingsCollapseScreen.GameGraphicsAPI == 4;
            set => ((ZenlessSettings)Settings).SettingsCollapseScreen.GameGraphicsAPI = value ? 4 : 3;
        }

        public bool AdvancedGraphics_UseRayTracing
        {
            get => ((ZenlessSettings)Settings).GeneralData.RayTracing_Enabled;
            set => ((ZenlessSettings)Settings).GeneralData.RayTracing_Enabled = value;
        }

        public int AdvancedGraphics_RayTracingQuality
        {
            get => (int)((ZenlessSettings)Settings).GeneralData.RayTracing_Quality;
            set => ((ZenlessSettings)Settings).GeneralData.RayTracing_Quality = (QualityOption3)value;
        }

        public int AdvancedGraphics_SuperResolutionOption
        {
            get => (int)((ZenlessSettings)Settings).GeneralData.SuperResolution_Option;
            set => ((ZenlessSettings)Settings).GeneralData.SuperResolution_Option = (SuperResolutionScalingOption)value;
        }

        public int AdvancedGraphics_SuperResolutionQuality
        {
            get => (int)((ZenlessSettings)Settings).GeneralData.SuperResolution_Quality;
            set => ((ZenlessSettings)Settings).GeneralData.SuperResolution_Quality = (SuperResolutionScalingQuality)value;
        }

        private static bool? _isDeviceHasRTXGPU;
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
            get => ((ZenlessSettings)Settings).GeneralData.Audio_MainVolume;
            set => ((ZenlessSettings)Settings).GeneralData.Audio_MainVolume = value;
        }

        public int Audio_VolMusic
        {
            get => ((ZenlessSettings)Settings).GeneralData.Audio_MusicVolume;
            set => ((ZenlessSettings)Settings).GeneralData.Audio_MusicVolume = value;
        }

        public int Audio_VolDialog
        {
            get => ((ZenlessSettings)Settings).GeneralData.Audio_DialogVolume;
            set => ((ZenlessSettings)Settings).GeneralData.Audio_DialogVolume = value;
        }

        public int Audio_VolSfx
        {
            get => ((ZenlessSettings)Settings).GeneralData.Audio_SfxVolume;
            set => ((ZenlessSettings)Settings).GeneralData.Audio_SfxVolume = value;
        }

        public int Audio_VolAmbient
        {
            get => ((ZenlessSettings)Settings).GeneralData.Audio_AmbientVolume;
            set => ((ZenlessSettings)Settings).GeneralData.Audio_AmbientVolume = value;
        }

        public int Audio_PlaybackDevice
        {
            get => (int)((ZenlessSettings)Settings).GeneralData.Audio_PlaybackDevice;
            set => ((ZenlessSettings)Settings).GeneralData.Audio_PlaybackDevice = (AudioPlaybackDevice)value;
        }

        public bool Audio_MuteOnMinimize
        {
            get => ((ZenlessSettings)Settings).GeneralData.Audio_MuteOnMinimize;
            set => ((ZenlessSettings)Settings).GeneralData.Audio_MuteOnMinimize = value;
        }
        #endregion
    }
}
