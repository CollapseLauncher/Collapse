using CollapseLauncher.GameSettings;
using CollapseLauncher.GameSettings.Honkai;
using CollapseLauncher.GameSettings.Honkai.Enums;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace CollapseLauncher.Pages
{
    public sealed partial class GameSettingsPage : Page, INotifyPropertyChanged
    {
        #region Fields
        private int prevGraphSelect;
        #endregion
        #region Methods
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Raise the PropertyChanged event, passing the name of the property whose value has changed.
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Presets
        public ICollection<string> PresetRenderingNames
        {
            get => Settings.Preset_SettingsGraphics.PresetKeys;
        }

        public int PresetRenderingIndex
        {
            get
            {
                string name = Settings.Preset_SettingsGraphics.GetPresetKey();
                int index = Settings.Preset_SettingsGraphics.PresetKeys.IndexOf(name);
                PersonalGraphicsSettingV2 presetValue = Settings.Preset_SettingsGraphics.GetPresetFromKey(name);

                if (presetValue != null)
                {
                    Settings.SettingsGraphics = presetValue;
                }

                ToggleRenderingSettings(name == PresetConst.DefaultPresetName);
                return index;
            }
            set
            {
                if (value < 0) return;

                string name = Settings.Preset_SettingsGraphics.PresetKeys[value];
                PersonalGraphicsSettingV2 presetValue = Settings.Preset_SettingsGraphics.GetPresetFromKey(name);

                if (presetValue != null)
                {
                    Settings.SettingsGraphics = presetValue;
                }
                Settings.Preset_SettingsGraphics.SetPresetKey(presetValue);

                ToggleRenderingSettings(name == PresetConst.DefaultPresetName);
                UpdatePresetRenderingSettings();
            }
        }

        private void ToggleRenderingSettings(bool isEnable = true)
        {
            RenderingAccuracySelector.IsEnabled = isEnable;
            ShadowQualitySelector.IsEnabled = isEnable;
            ReflectionQualitySelector.IsEnabled = isEnable;
            GameFXPostProcExpander.IsEnabled = isEnable;
            GlobalIlluminationSelector.IsEnabled = isEnable;
            AmbientOcclusionSelector.IsEnabled = isEnable;
            LevelOfDetailSelector.IsEnabled = isEnable;
            GameVolumetricLightSelector.IsEnabled = isEnable;
            GameMaxFPSInCombatValue.IsEnabled = isEnable;
            GameMaxFPSInMainMenuValue.IsEnabled = isEnable;
        }

        private void UpdatePresetRenderingSettings()
        {
            UpdatePresetFPS();
            UpdatePresetRendering();
        }
        #endregion
        #region GameResolution
        public bool IsFullscreenEnabled
        {
            get => Settings.SettingsScreen.isfullScreen;
            set
            {
                Settings.SettingsScreen.isfullScreen = value;
                if (value)
                {
                    GameResolutionFullscreenExclusive.IsEnabled = !IsCustomResolutionEnabled;
                    return;
                }
                GameResolutionFullscreenExclusive.IsEnabled = false;
                GameResolutionFullscreenExclusive.IsChecked = false;
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

                Size size = Hi3Helper.Screen.ScreenProp.GetScreenSize();
                GameResolutionSelector.SelectedItem = $"{size.Width}x{size.Height}";
            }
        }

        public bool IsCanExclusiveFullscreen
        {
            get => !(!IsFullscreenEnabled || IsCustomResolutionEnabled);
        }

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
                    Size size = Hi3Helper.Screen.ScreenProp.GetScreenSize();
                    return $"{size.Width}x{size.Height}";
                }
                return res;
            }
            set => Settings.SettingsScreen.sizeResString = value;
        }
        #endregion
        #region FPS
        private void UpdatePresetFPS()
        {
            OnPropertyChanged("FPSInCombat");
            OnPropertyChanged("FPSInMainMenu");
        }
        public short FPSInCombat
        {
            get => Settings.SettingsGraphics.TargetFrameRateForInLevel;
            set => Settings.SettingsGraphics.TargetFrameRateForInLevel = value;
        }

        public short FPSInMainMenu
        {
            get => Settings.SettingsGraphics.TargetFrameRateForOthers;
            set => Settings.SettingsGraphics.TargetFrameRateForOthers = value;
        }
        #endregion
        #region Rendering
        private void UpdatePresetRendering()
        {
            OnPropertyChanged("GraphicsRenderingAccuracy");
            OnPropertyChanged("GraphicsShadowQuality");
            OnPropertyChanged("GraphicsReflectionQuality");
            OnPropertyChanged("IsGraphicsPostFXEnabled");
            OnPropertyChanged("IsGraphicsPhysicsEnabled");
            OnPropertyChanged("IsGraphicsFXHDREnabled");
            OnPropertyChanged("IsGraphicsFXHighQualityEnabled");
            OnPropertyChanged("IsGraphicsFXFXAAEnabled");
            OnPropertyChanged("IsGraphicsFXDistortionEnabled");
            OnPropertyChanged("GraphicsGlobalIllumination");
            OnPropertyChanged("GraphicsAmbientOcclusion");
            OnPropertyChanged("GraphicsLevelOfDetail");
            OnPropertyChanged("GraphicsVolumetricLight");
        }

        public int GraphicsRenderingAccuracy
        {
            get => prevGraphSelect = (int)Settings.SettingsGraphics.ResolutionQuality;
            set => TryChallengeRenderingAccuracySet(value, value < 3);
        }

        private async void TryChallengeRenderingAccuracySet(int value, bool BypassChallenge = false)
        {
            if (!BypassChallenge)
            {
                prevGraphSelect = (int)Settings.SettingsGraphics.ResolutionQuality;
                var result = await Dialogs.SimpleDialogs.Dialog_GraphicsVeryHighWarning(Content);

                switch (result)
                {
                    case ContentDialogResult.Secondary:
                        RenderingAccuracySelector.SelectedIndex = prevGraphSelect;
                        break;
                }
            }

            Settings.SettingsGraphics.ResolutionQuality = (SelectResolutionQuality)value;
        }

        public int GraphicsShadowQuality
        {
            get => (int)Settings.SettingsGraphics.ShadowLevel;
            set => Settings.SettingsGraphics.ShadowLevel = (SelectShadowLevel)value;
        }

        public int GraphicsReflectionQuality
        {
            get => (int)Settings.SettingsGraphics.ReflectionQuality;
            set => Settings.SettingsGraphics.ReflectionQuality = (SelectReflectionQuality)value;
        }

        public bool IsGraphicsPostFXEnabled
        {
            get => Settings.SettingsGraphics.UsePostFX;
            set
            {
                Settings.SettingsGraphics.UsePostFX = value;
                if (!(GameFXPostProcExpander.IsExpanded = value))
                {
                    GameFXHDRCheckBox.IsChecked = GameFXHDRCheckBox.IsEnabled = false;
                    GameFXHighQualityCheckBox.IsChecked = GameFXHighQualityCheckBox.IsEnabled = false;
                    GameFXFXAACheckBox.IsChecked = GameFXFXAACheckBox.IsEnabled = false;
                    GameFXDistortionCheckBox.IsChecked = GameFXDistortionCheckBox.IsEnabled = false;
                    return;
                }
                GameFXHDRCheckBox.IsEnabled = true;
                GameFXHighQualityCheckBox.IsEnabled = true;
                GameFXFXAACheckBox.IsEnabled = true;
                GameFXDistortionCheckBox.IsEnabled = true;
            }
        }

        public bool IsGraphicsPhysicsEnabled
        {
            get => Settings.SettingsGraphics.UseDynamicBone;
            set => Settings.SettingsGraphics.UseDynamicBone = value;
        }

        public bool IsGraphicsFXHDREnabled
        {
            get => !IsGraphicsPostFXEnabled ? false : Settings.SettingsGraphics.UseHDR;
            set => Settings.SettingsGraphics.UseHDR = value;
        }

        public bool IsGraphicsFXHighQualityEnabled
        {
            get => !IsGraphicsPostFXEnabled ? false : Settings.SettingsGraphics.PostFXGradeBool;
            set => Settings.SettingsGraphics.PostFXGradeBool = value;
        }

        public bool IsGraphicsFXFXAAEnabled
        {
            get => !IsGraphicsPostFXEnabled ? false : Settings.SettingsGraphics.UseFXAA;
            set => Settings.SettingsGraphics.UseFXAA = value;
        }

        public bool IsGraphicsFXDistortionEnabled
        {
            get => !IsGraphicsPostFXEnabled ? false : Settings.SettingsGraphics.UseDistortion;
            set => Settings.SettingsGraphics.UseDistortion = value;
        }

        public byte GraphicsAPI
        {
            get => Settings.SettingsCollapseScreen.GameGraphicsAPI;
            set => Settings.SettingsCollapseScreen.GameGraphicsAPI = value;
        }

        public int GraphicsGlobalIllumination
        {
            get => (int)Settings.SettingsGraphics.GlobalIllumination;
            set => Settings.SettingsGraphics.GlobalIllumination = (SelectGlobalIllumination)value;
        }

        public int GraphicsAmbientOcclusion
        {
            get => (int)Settings.SettingsGraphics.AmbientOcclusion;
            set => Settings.SettingsGraphics.AmbientOcclusion = (SelectAmbientOcclusion)value;
        }

        public int GraphicsLevelOfDetail
        {
            get
            {
                int val = (int)Settings.SettingsGraphics.LodGrade;
                return val > 2 ? 2 : val;
            }
            set
            {
                Settings.SettingsGraphics.LodGrade = (SelectLodGrade)value;
            }
        }

        public int GraphicsVolumetricLight
        {
            get => (int)Settings.SettingsGraphics.VolumetricLight;
            set => Settings.SettingsGraphics.VolumetricLight = (SelectVolumetricLight)value;
        }
        #endregion
        #region Audio
        public int AudioMasterVolume
        {
            get => Settings.SettingsAudio.MasterVolume;
            set => Settings.SettingsAudio.MasterVolume = value;
        }

        public int AudioBGMVolume
        {
            get => Settings.SettingsAudio.BGMVolume;
            set => Settings.SettingsAudio.BGMVolume = value;
        }

        public int AudioSFXVolume
        {
            get => Settings.SettingsAudio.SoundEffectVolume;
            set => Settings.SettingsAudio.SoundEffectVolume = value;
        }

        public int AudioVoiceVolume
        {
            get => Settings.SettingsAudio.VoiceVolume;
            set => Settings.SettingsAudio.VoiceVolume = value;
        }

        public int AudioElfVolume
        {
            get => Settings.SettingsAudio.ElfVolume;
            set => Settings.SettingsAudio.ElfVolume = value;
        }

        public int AudioCutsceneVolume
        {
            get => Settings.SettingsAudio.CGVolumeV2;
            set => Settings.SettingsAudio.CGVolumeV2 = value;
        }

        public int AudioVoiceLanguage
        {
            get => Settings.SettingsAudio._userCVLanguageInt;
            set => Settings.SettingsAudio._userCVLanguageInt = value;
        }

        public bool AudioMute
        {
            get => Settings.SettingsAudio.Mute;
            set => Settings.SettingsAudio.Mute = value;
        }

        #endregion
    }
}
