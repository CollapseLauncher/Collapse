using CollapseLauncher.GameSettings.Honkai.Enums;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Drawing;

namespace CollapseLauncher.Pages
{
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string langInfo)
        {
            if (targetType != typeof(bool))
                throw new InvalidOperationException("The target must be a boolean");

            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string langInfo)
        {
            throw new NotSupportedException();
        }
    }

    public sealed partial class GameSettingsPage : Page
    {
        private int prevGraphSelect;

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
            get => (int)Settings.SettingsGraphics.LodGrade;
            set => Settings.SettingsGraphics.LodGrade = (SelectLodGrade)value;
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
