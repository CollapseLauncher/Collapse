using CollapseLauncher.GameSettings.Zenless.Enums;
using Hi3Helper.Screen;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.CompilerServices;
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
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
                    FxQualitySelector.SelectedIndex          = (int)QualityOption3.High;
                    ShadingQualitySelector.SelectedIndex     = (int)QualityOption3.High;
                    CharacterQualitySelector.SelectedIndex   = (int)QualityOption2.High;
                    EnvironmentQualitySelector.SelectedIndex = (int)QualityOption2.High;
                    ReflectionQualitySelector.SelectedIndex  = (int)QualityOption4.High;
                    VolumetricFogSelector.SelectedIndex      = (int)QualityOption4.High;
                    BloomToggle.IsChecked                    = true;
                    DistortionToggle.IsChecked               = true;
                    MotionBlurToggle.IsChecked               = true;
                    HPCAToggle.IsChecked                     = true;
                    break;
                case GraphicsPresetOption.Medium:
                    VSyncToggle.IsChecked                    = true;
                    RenderResolutionSelector.SelectedIndex   = (int)RenderResOption.f10;
                    AntiAliasingSelector.SelectedIndex       = (int)AntiAliasingOption.TAA;
                    GlobalIlluminationSelector.SelectedIndex = (int)QualityOption3.High;
                    ShadowQualitySelector.SelectedIndex      = (int)QualityOption3.High;
                    FxQualitySelector.SelectedIndex          = (int)QualityOption3.Medium;
                    ShadingQualitySelector.SelectedIndex     = (int)QualityOption3.High;
                    CharacterQualitySelector.SelectedIndex   = (int)QualityOption2.High;
                    EnvironmentQualitySelector.SelectedIndex = (int)QualityOption2.High;
                    ReflectionQualitySelector.SelectedIndex  = (int)QualityOption4.Medium;
                    VolumetricFogSelector.SelectedIndex      = (int)QualityOption4.Medium;
                    BloomToggle.IsChecked                    = true;
                    DistortionToggle.IsChecked               = true;
                    MotionBlurToggle.IsChecked               = true;
                    HPCAToggle.IsChecked                     = false;
                    break;
                case GraphicsPresetOption.Low:
                    VSyncToggle.IsChecked                    = true;
                    RenderResolutionSelector.SelectedIndex   = (int)RenderResOption.f10;
                    AntiAliasingSelector.SelectedIndex       = (int)AntiAliasingOption.TAA;
                    GlobalIlluminationSelector.SelectedIndex = (int)QualityOption3.Medium;
                    ShadowQualitySelector.SelectedIndex      = (int)QualityOption3.Medium;
                    FxQualitySelector.SelectedIndex          = (int)QualityOption3.Low;
                    ShadingQualitySelector.SelectedIndex     = (int)QualityOption3.High;
                    CharacterQualitySelector.SelectedIndex   = (int)QualityOption2.High;
                    EnvironmentQualitySelector.SelectedIndex = (int)QualityOption2.High;
                    ReflectionQualitySelector.SelectedIndex  = (int)QualityOption4.Low;
                    VolumetricFogSelector.SelectedIndex      = (int)QualityOption4.Low;
                    BloomToggle.IsChecked                    = true;
                    DistortionToggle.IsChecked               = true;
                    MotionBlurToggle.IsChecked               = true;
                    HPCAToggle.IsChecked                     = false;
                    break;
            }

            _changingPreset = false;
        }

        private bool _changingPreset;
        
        private async void EnforceCustomPreset()
        {
            if (_changingPreset) return;
            if (GraphicsPresetSelector.SelectedIndex == (int)GraphicsPresetOption.Custom) return;
            _changingPreset                      = true;
            GraphicsPresetSelector.SelectedIndex = (int)GraphicsPresetOption.Custom;
            await System.Threading.Tasks.Task.Delay(200);
            _changingPreset = false;
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
        private List<bool> ScreenResolutionIsFullscreenIdx = new List<bool>();

        public bool IsFullscreenEnabled
        {
            get => Settings.SettingsScreen.isfullScreen;
            set
            {
                Settings.SettingsScreen.isfullScreen = value;
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
            get => Settings.SettingsCollapseScreen.UseBorderlessScreen;
            set
            {
                Settings.SettingsCollapseScreen.UseBorderlessScreen = value;
                if (value)
                {
                    GameWindowResizable.IsEnabled = false;
                    GameWindowResizable.IsChecked = false;
                    GameResolutionFullscreen.IsEnabled = false;
                    GameResolutionFullscreen.IsChecked = false;
                }
                else
                {
                    if (GameResolutionFullscreen.IsChecked == false)
                        GameWindowResizable.IsEnabled  = true;
                    else GameWindowResizable.IsEnabled = false;
                    GameResolutionFullscreen.IsEnabled = true;
                }
            }
        }

        public bool IsCustomResolutionEnabled
        {
            get => Settings.SettingsCollapseScreen.UseCustomResolution;
            set
            {
                Settings.SettingsCollapseScreen.UseCustomResolution = value;
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

                Size size = ScreenProp.GetScreenSize();
                GameResolutionSelector.SelectedItem = $"{size.Width}x{size.Height}";
            }
        }

        /// <summary>
        /// Zenless does not support exclusive fullscreen
        /// </summary>
        public readonly bool IsCanExclusiveFullscreen = false;

        public bool IsExclusiveFullscreenEnabled
        {
            get
            {
                if (!IsFullscreenEnabled)
                {
                    return false;
                }
                return Settings.SettingsCollapseScreen.UseExclusiveFullscreen;
            }
            set
            {
                Settings.SettingsCollapseScreen.UseExclusiveFullscreen = value;
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

        public bool IsCanResizableWindow
        {
            get => !Settings.SettingsScreen.isfullScreen && !IsExclusiveFullscreenEnabled;
        }

        public bool IsResizableWindow
        {
            get => Settings.SettingsCollapseScreen.UseResizableWindow;
            set
            {
                Settings.SettingsCollapseScreen.UseResizableWindow = value;
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

        public bool IsCanCustomResolution
        {
            get => Settings.SettingsCollapseScreen.UseResizableWindow && !IsExclusiveFullscreenEnabled;
        }

        public int ResolutionW
        {
            get => Settings.SettingsScreen.sizeRes.Width;
            set => Settings.SettingsScreen.sizeRes = new Size(value, ResolutionH);
        }

        public int ResolutionH
        {
            get => Settings.SettingsScreen.sizeRes.Height;
            set => Settings.SettingsScreen.sizeRes = new Size(ResolutionW, value);
        }

        public bool IsCanResolutionWH
        {
            get => IsCustomResolutionEnabled;
        }

        public string ResolutionSelected
        {
            get
            {
                string res = Settings.SettingsScreen.sizeResString;
                if (string.IsNullOrEmpty(res))
                {
                    Size size = ScreenProp.GetScreenSize();
                    return $"{size.Width}x{size.Height}";
                }
                return res;
            }
            set => Settings.SettingsScreen.sizeResString = value;
        }

        public int ResolutionIndexSelected
        {
            get
            {
                int res = Settings.GeneralData?.ResolutionIndex ?? -1;
                bool isFullscreen = res >= 0 && (res + 1) < ScreenResolutionIsFullscreenIdx.Count ? ScreenResolutionIsFullscreenIdx[res] : false;
                IsFullscreenEnabled = isFullscreen;

                return res;
            }
            set
            {
                if (value < 0) return;
                Settings.GeneralData.ResolutionIndex = value;
                // ReSharper disable once SimplifyConditionalTernaryExpression
                bool isFullscreen = (value + 1) < ScreenResolutionIsFullscreenIdx.Count ? ScreenResolutionIsFullscreenIdx[value] : false;
                IsFullscreenEnabled = isFullscreen;
            }
        }
        #endregion

        #region Misc
        public bool IsGameBoost
        {
            get => Settings?.SettingsCollapseMisc?.UseGameBoost ?? false;
            set => Settings.SettingsCollapseMisc.UseGameBoost = value;
        }
        
        public bool IsMobileMode
        {
            get => Settings?.SettingsCollapseMisc?.LaunchMobileMode ?? false;
            set => Settings.SettingsCollapseMisc.LaunchMobileMode = value;
        }
        #endregion

        #region Advanced Settings
        public bool IsUseAdvancedSettings
        {
            get
            {
                bool value                                  = Settings?.SettingsCollapseMisc?.UseAdvancedGameSettings ?? false;
                if (value){AdvancedSettingsPanel.Visibility = Visibility.Visible;}
                else AdvancedSettingsPanel.Visibility       = Visibility.Collapsed;
                return value;
            }
            set
            { 
                Settings.SettingsCollapseMisc.UseAdvancedGameSettings = value;
                if (value) AdvancedSettingsPanel.Visibility = Visibility.Visible;
                else AdvancedSettingsPanel.Visibility       = Visibility.Collapsed;
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

                Settings.SettingsCollapseMisc.UseGamePreLaunchCommand = value;
            }
        }

        public string PreLaunchCommand
        {
            get => Settings?.SettingsCollapseMisc?.GamePreLaunchCommand;
            set => Settings.SettingsCollapseMisc.GamePreLaunchCommand = value;
        }

        public bool IsPreLaunchCommandExitOnGameClose
        {
            get => Settings?.SettingsCollapseMisc?.GamePreLaunchExitOnGameStop ?? false;
            set => Settings.SettingsCollapseMisc.GamePreLaunchExitOnGameStop = value;
        }

        public int LaunchDelay
        {
            get => Settings?.SettingsCollapseMisc?.GameLaunchDelay ?? 0;
            set => Settings.SettingsCollapseMisc.GameLaunchDelay = value;
        }
        
        public bool IsUsePostExitCommand
        {
            get 
            {
                bool value = Settings?.SettingsCollapseMisc?.UseGamePostExitCommand ?? false;

                if (value) PostExitCommandTextBox.IsEnabled = true;
                else PostExitCommandTextBox.IsEnabled       = false;

                return value;
            }
            set
            {
                if (value) PostExitCommandTextBox.IsEnabled = true;
                else PostExitCommandTextBox.IsEnabled       = false;

                Settings.SettingsCollapseMisc.UseGamePostExitCommand = value;
            }
        }

        public string PostExitCommand
        {
            get => Settings?.SettingsCollapseMisc?.GamePostExitCommand;
            set => Settings.SettingsCollapseMisc.GamePostExitCommand = value;
        }
        
        private void GameLaunchDelay_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            // clamp for negative value when clearing the number box
            if ((int)sender.Value < 0)
                sender.Value = 0;
        }
        #endregion

        #region Language Settings - GENERAL_DATA
        public int Lang_Text
        {
            get
            {
                var v = (int)Settings.GeneralData.DeviceLanguageType;
                if (v <= 0) return 1;
                return v;
            }
            set => Settings.GeneralData.DeviceLanguageType = (LanguageText)value;
        }

        public int Lang_Audio
        {
            get
            {
                var v = (int)Settings.GeneralData.DeviceLanguageVoiceType;
                if (v <= 0) return 1;
                return v;
            }
            set => Settings.GeneralData.DeviceLanguageVoiceType = (LanguageVoice)value;
        }
        #endregion

        #region Graphics Settings - GENERAL_DATA > SystemSettingDataMap
        public bool EnableVSync
        {
            get => Settings.GeneralData?.VSync ?? false;
            set => Settings.GeneralData.VSync = value;
        }
        
        public int Graphics_Preset
        {
            get => (int)Settings.GeneralData.GraphicsPreset;
            set => Settings.GeneralData.GraphicsPreset = (GraphicsPresetOption)value;
        }
        
        public int Graphics_RenderRes
        {
            get => (int)Settings.GeneralData.RenderResolution;
            set => Settings.GeneralData.RenderResolution = (RenderResOption)value;
        }
        
        public int Graphics_Shadow
        {
            get => (int)Settings.GeneralData.ShadowQuality;
            set => Settings.GeneralData.ShadowQuality = (QualityOption3)value;
        }
        
        public int Graphics_AntiAliasing
        {
            get => (int)Settings.GeneralData.AntiAliasing;
            set => Settings.GeneralData.AntiAliasing = (AntiAliasingOption)value;
        }
        
        public int Graphics_VolFog
        {
            get => (int)Settings.GeneralData.VolumetricFogQuality;
            set => Settings.GeneralData.VolumetricFogQuality = (QualityOption4)value;
        }

        public bool Graphics_Bloom
        {
            get => Settings.GeneralData.Bloom;
            set => Settings.GeneralData.Bloom = value;
        }

        public bool Graphics_MotionBlur
        {
            get => Settings.GeneralData.MotionBlur;
            set => Settings.GeneralData.MotionBlur = value;
        }

        public int Graphics_Reflection
        {
            get => (int)Settings.GeneralData.ReflectionQuality;
            set => Settings.GeneralData.ReflectionQuality = (QualityOption4)value;
        }
        
        public int Graphics_Effects
        {
            get => (int)Settings.GeneralData.FxQuality;
            set => Settings.GeneralData.FxQuality = (QualityOption3)value;
        }

        public int Graphics_ColorFilter
        {
            get => Settings.GeneralData.ColorFilter;
            set => Settings.GeneralData.ColorFilter = value;
        }
        
        public int Graphics_Character
        {
            get => (int)Settings.GeneralData.CharacterQuality;
            set => Settings.GeneralData.CharacterQuality = (QualityOption2)value;
        }

        public bool Graphics_Distortion
        {
            get => Settings.GeneralData.Distortion;
            set => Settings.GeneralData.Distortion = value;
        }
        
        public int Graphics_Shading
        {
            get => (int)Settings.GeneralData.ShadingQuality;
            set => Settings.GeneralData.ShadingQuality = (QualityOption3)value;
        }
        
        public int Graphics_Environment
        {
            get => (int)Settings.GeneralData.EnvironmentQuality;
            set => Settings.GeneralData.EnvironmentQuality = (QualityOption2)value;
        }

        public int Graphics_GlobalIllumination
        {
            get => (int)Settings.GeneralData.GlobalIllumination;
            set => Settings.GeneralData.GlobalIllumination = (QualityOption3)value;
        }

        public int Graphics_Fps
        {
            get => (int)Settings.GeneralData.Fps;
            set => Settings.GeneralData.Fps = (FpsOption)value;
        }

        /// <inheritdoc cref="CollapseLauncher.GameSettings.Zenless.GeneralData.HiPrecisionCharaAnim"/>
        public bool Graphics_HiPreCharaAnim
        {
            get => Settings.GeneralData.HiPrecisionCharaAnim;
            set => Settings.GeneralData.HiPrecisionCharaAnim = value;
        }
        #endregion

        #region Audio Settings - GENERAL_DATA > SystemSettingDataMap

        public int Audio_VolMain
        {
            get => Settings.GeneralData.Audio_MainVolume;
            set => Settings.GeneralData.Audio_MainVolume = value;
        }

        public int Audio_VolMusic
        {
            get => Settings.GeneralData.Audio_MusicVolume;
            set => Settings.GeneralData.Audio_MusicVolume = value;
        }

        public int Audio_VolDialog
        {
            get => Settings.GeneralData.Audio_DialogVolume;
            set => Settings.GeneralData.Audio_DialogVolume = value;
        }

        public int Audio_VolSfx
        {
            get => Settings.GeneralData.Audio_SfxVolume;
            set => Settings.GeneralData.Audio_SfxVolume = value;
        }

        public int Audio_PlaybackDevice
        {
            get => (int)Settings.GeneralData.Audio_PlaybackDevice;
            set => Settings.GeneralData.Audio_PlaybackDevice = (AudioPlaybackDevice)value;
        }

        public bool Audio_MuteOnMinimize
        {
            get => Settings.GeneralData.Audio_MuteOnMinimize;
            set => Settings.GeneralData.Audio_MuteOnMinimize = value;
        }
        #endregion
    }
}
