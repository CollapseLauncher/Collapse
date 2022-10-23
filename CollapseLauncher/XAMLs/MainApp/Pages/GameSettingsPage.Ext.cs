using Hi3Helper.Data;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Drawing;
using static Hi3Helper.Shared.Region.GameConfig;
using static Hi3Helper.Shared.Region.InstallationManagement;

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
        private int? prevGraphSelect;

        #region GameResolution
        public bool IsFullscreenEnabled
        {
            get => gameIni.Settings[SectionName]["Fullscreen"].ToBool();
            set
            {
                gameIni.Settings[SectionName]["Fullscreen"] = value;
                if (value)
                {
                    GameResolutionFullscreenExclusive.IsEnabled = IsCustomResolutionEnabled ? false : true;
                    return;
                }
                GameResolutionFullscreenExclusive.IsEnabled = false;
                GameResolutionFullscreenExclusive.IsChecked = false;
            }
        }

        public bool IsCustomResolutionEnabled
        {
            get => gameIni.Settings[SectionName]["CustomScreenResolution"].ToBool();
            set
            {
                gameIni.Settings[SectionName]["CustomScreenResolution"] = value;
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
                return gameIni.Settings[SectionName]["FullscreenExclusive"].ToBool();
            }
            set
            {
                gameIni.Settings[SectionName]["FullscreenExclusive"] = value;
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
            get
            {
                Size size = gameIni.Settings[SectionName]["ScreenResolution"].ToSize();
                return size.Width;
            }
            set
            {
                Size size = new Size(value, ResolutionH);
                gameIni.Settings[SectionName]["ScreenResolution"] = new IniValue(size);
            }
        }

        public int ResolutionH
        {
            get
            {
                Size size = gameIni.Settings[SectionName]["ScreenResolution"].ToSize();
                return size.Height;
            }
            set
            {
                Size size = new Size(ResolutionW, value);
                gameIni.Settings[SectionName]["ScreenResolution"] = new IniValue(size);
            }
        }

        public bool IsCanResolutionWH
        {
            get => IsCustomResolutionEnabled;
        }

        public string ResolutionSelected
        {
            get
            {
                string res = gameIni.Settings[SectionName]["ScreenResolution"].ToString();
                if (string.IsNullOrEmpty(res))
                {
                    Size size = Hi3Helper.Screen.ScreenProp.GetScreenSize();
                    return $"{size.Width}x{size.Height}";
                }
                return res;
            }
            set => gameIni.Settings[SectionName]["ScreenResolution"] = value;
        }
        #endregion
        #region FPS
        public int FPSInCombat
        {
            get => gameIni.Settings[SectionName]["TargetFrameRateForInLevel"].ToInt();
            set => gameIni.Settings[SectionName]["TargetFrameRateForInLevel"] = value;
        }

        public int FPSInMainMenu
        {
            get => gameIni.Settings[SectionName]["TargetFrameRateForOthers"].ToInt();
            set => gameIni.Settings[SectionName]["TargetFrameRateForOthers"] = value;
        }
        #endregion
        #region Rendering
        public int GraphicsRenderingAccuracy
        {
            get => (prevGraphSelect = gameIni.Settings[SectionName]["ResolutionQuality"].ToInt()) ?? 0;
            set => TryChallengeRenderingAccuracySet(value, value < 3);
        }

        private async void TryChallengeRenderingAccuracySet(int value, bool BypassChallenge = false)
        {
            if (!BypassChallenge)
            {
                prevGraphSelect = gameIni.Settings[SectionName]["ResolutionQuality"].ToInt();
                var result = await Dialogs.SimpleDialogs.Dialog_GraphicsVeryHighWarning(Content);

                switch (result)
                {
                    case ContentDialogResult.Secondary:
                        RenderingAccuracySelector.SelectedIndex = prevGraphSelect ?? 0;
                        break;
                }
            }

            gameIni.Settings[SectionName]["ResolutionQuality"] = value;
        }

        public int GraphicsShadowQuality
        {
            get => gameIni.Settings[SectionName]["ShadowLevel"].ToInt();
            set => gameIni.Settings[SectionName]["ShadowLevel"] = value;
        }

        public int GraphicsReflectionQuality
        {
            get => gameIni.Settings[SectionName]["ReflectionQuality"].ToInt();
            set => gameIni.Settings[SectionName]["ReflectionQuality"] = value;
        }

        public bool IsGraphicsPostFXEnabled
        {
            get => gameIni.Settings[SectionName]["UsePostFX"].ToBool();
            set
            {
                gameIni.Settings[SectionName]["UsePostFX"] = value;
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
            get => gameIni.Settings[SectionName]["UseDynamicBone"].ToBool();
            set => gameIni.Settings[SectionName]["UseDynamicBone"] = value;
        }

        public bool IsGraphicsFXHDREnabled
        {
            get => !IsGraphicsPostFXEnabled ? false : gameIni.Settings[SectionName]["UseHDR"].ToBool();
            set => gameIni.Settings[SectionName]["UseHDR"] = value;
        }

        public bool IsGraphicsFXHighQualityEnabled
        {
            get => !IsGraphicsPostFXEnabled ? false : gameIni.Settings[SectionName]["HighQualityPostFX"].ToBool();
            set => gameIni.Settings[SectionName]["HighQualityPostFX"] = value;
        }

        public bool IsGraphicsFXFXAAEnabled
        {
            get => !IsGraphicsPostFXEnabled ? false : gameIni.Settings[SectionName]["UseFXAA"].ToBool();
            set => gameIni.Settings[SectionName]["UseFXAA"] = value;
        }

        public bool IsGraphicsFXDistortionEnabled
        {
            get => !IsGraphicsPostFXEnabled ? false : gameIni.Settings[SectionName]["UseDistortion"].ToBool();
            set => gameIni.Settings[SectionName]["UseDistortion"] = value;
        }

        public int GraphicsAPI
        {
            get => gameIni.Settings[SectionName]["GameGraphicsAPI"].ToInt();
            set => gameIni.Settings[SectionName]["GameGraphicsAPI"] = value;
        }

        public int GraphicsGlobalIllumination
        {
            get => gameIni.Settings[SectionName]["GlobalIllumination"].ToBool() ? 1 : 0;
            set => gameIni.Settings[SectionName]["GlobalIllumination"] = value == 1;
        }

        public int GraphicsAmbientOcclusion
        {
            get => gameIni.Settings[SectionName]["AmbientOcclusion"].ToInt();
            set => gameIni.Settings[SectionName]["AmbientOcclusion"] = value;
        }

        public int GraphicsLevelOfDetail
        {
            get => gameIni.Settings[SectionName]["LodLevel"].ToInt();
            set => gameIni.Settings[SectionName]["LodLevel"] = value;
        }

        public int GraphicsVolumetricLight
        {
            get => Boolean2IntFallback(gameIni.Settings[SectionName]["VolumetricLight"].ToBoolNullable());
            set => gameIni.Settings[SectionName]["VolumetricLight"] = value;
        }
        #endregion
        #region Audio
        public int AudioBGMVolume
        {
            get => gameIni.Settings[SectionName]["BGMVolume"].ToInt();
            set => gameIni.Settings[SectionName]["BGMVolume"] = value;
        }

        public int AudioSFXVolume
        {
            get => gameIni.Settings[SectionName]["SoundEffectVolume"].ToInt();
            set => gameIni.Settings[SectionName]["SoundEffectVolume"] = value;
        }

        public int AudioVoiceVolume
        {
            get => gameIni.Settings[SectionName]["VoiceVolume"].ToInt();
            set => gameIni.Settings[SectionName]["VoiceVolume"] = value;
        }

        public int AudioElfVolume
        {
            get => gameIni.Settings[SectionName]["ElfVolume"].ToInt();
            set => gameIni.Settings[SectionName]["ElfVolume"] = value;
        }

        public int AudioCutsceneVolume
        {
            get => gameIni.Settings[SectionName]["CGVolume"].ToInt();
            set => gameIni.Settings[SectionName]["CGVolume"] = value;
        }

        public int AudioVoiceLanguage
        {
            get => gameIni.Settings[SectionName]["CVLanguage"].ToInt();
            set => gameIni.Settings[SectionName]["CVLanguage"] = value;
        }
        #endregion
    }
}
