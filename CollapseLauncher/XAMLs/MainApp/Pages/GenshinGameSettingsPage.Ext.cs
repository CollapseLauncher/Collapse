using CollapseLauncher.GameSettings.Genshin;
using CollapseLauncher.GameSettings.Genshin.Enums;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Drawing;

namespace CollapseLauncher.Pages
{
    public sealed partial class GenshinGameSettingsPage : Page
    {
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

        #region Graphics Settings
        public bool VerticalSync
        {
            get => Convert.ToBoolean((int)Settings.SettingsGeneralData.graphicsData.VerticalSync - 1);
            set => Settings.SettingsGeneralData.graphicsData.VerticalSync = (VerticalSyncOption)(Convert.ToInt32(value) + 1);
        }

        public bool VolumetricFog
        {
            get => Convert.ToBoolean((int)Settings.SettingsGeneralData.graphicsData.VolumetricFog - 1);
            set => Settings.SettingsGeneralData.graphicsData.VolumetricFog = (VolumetricFogOption)(Convert.ToInt32(value) + 1);
        }

        public bool Reflections
        {
            get => Convert.ToBoolean((int)Settings.SettingsGeneralData.graphicsData.Reflections - 1);
            set => Settings.SettingsGeneralData.graphicsData.Reflections = (ReflectionsOption)(Convert.ToInt32(value) + 1);
        }

        public bool Bloom
        {
            get => Convert.ToBoolean((int)Settings.SettingsGeneralData.graphicsData.Bloom - 1);
            set => Settings.SettingsGeneralData.graphicsData.Bloom = (BloomOption)(Convert.ToInt32(value) + 1);
        }

        public int FPS
        {
            get
            {
                // Get the current value
                FPSOption curValue = Settings.SettingsGeneralData.graphicsData.FPS;
                // Get the index of the current value in FPSOptionsList array
                int indexOfValue = Array.IndexOf(GraphicsData.FPSOptionsList, curValue);
                // Return the index of the value
                return indexOfValue;
            }
            set
            {
                // [HACK]: Fix some rare occasion where the "value" turned into -1. If so, then return
                if (value < 0) return;

                // Get the FPSOption based on the selected index by the "value"
                FPSOption valueFromIndex = GraphicsData.FPSOptionsList[value];
                // Set the actual value to its property
                Settings.SettingsGeneralData.graphicsData.FPS = valueFromIndex;
            }
        }

        public int RenderScale
        {
            get => (int)Settings.SettingsGeneralData.graphicsData.RenderResolution - 1;
            set => Settings.SettingsGeneralData.graphicsData.RenderResolution = (RenderResolutionOption)(value + 1);
        }

        public int ShadowQuality
        {
            get => (int)Settings.SettingsGeneralData.graphicsData.ShadowQuality - 1;
            set => Settings.SettingsGeneralData.graphicsData.ShadowQuality = (ShadowQualityOption)(value + 1);
        }

        public int VisualEffects
        {
            get => (int)Settings.SettingsGeneralData.graphicsData.VisualEffects - 1;
            set => Settings.SettingsGeneralData.graphicsData.VisualEffects = (VisualEffectsOption)(value + 1);
        }

        public int SFXQuality
        {
            get => (int)Settings.SettingsGeneralData.graphicsData.SFXQuality - 1;
            set => Settings.SettingsGeneralData.graphicsData.SFXQuality = (SFXQualityOption)(value + 1);
        }

        public int EnvironmentDetail
        {
            get => (int)Settings.SettingsGeneralData.graphicsData.EnvironmentDetail - 1;
            set => Settings.SettingsGeneralData.graphicsData.EnvironmentDetail = (EnvironmentDetailOption)(value + 1);
        }

        public int MotionBlur
        {
            get => (int)Settings.SettingsGeneralData.graphicsData.MotionBlur - 1;
            set => Settings.SettingsGeneralData.graphicsData.MotionBlur = (MotionBlurOption)(value + 1);
        }

        public int CrowdDensity
        {
            get => (int)Settings.SettingsGeneralData.graphicsData.CrowdDensity - 1;
            set => Settings.SettingsGeneralData.graphicsData.CrowdDensity = (CrowdDensityOption)(value + 1);
        }

        public int SubsurfaceScattering
        {
            get => (int)Settings.SettingsGeneralData.graphicsData.SubsurfaceScattering - 1;
            set => Settings.SettingsGeneralData.graphicsData.SubsurfaceScattering = (SubsurfaceScatteringOption)(value + 1);
        }

        public int CoOpTeammateEffects
        {
            get => (int)Settings.SettingsGeneralData.graphicsData.CoOpTeammateEffects - 1;
            set => Settings.SettingsGeneralData.graphicsData.CoOpTeammateEffects = (CoOpTeammateEffectsOption)(value + 1);
        }

        public int AnisotropicFiltering
        {
            get => (int)Settings.SettingsGeneralData.graphicsData.AnisotropicFiltering - 1;
            set => Settings.SettingsGeneralData.graphicsData.AnisotropicFiltering = (AnisotropicFilteringOption)(value + 1);
        }

        public int Antialiasing
        {
            get => (int)Settings.SettingsGeneralData.graphicsData.Antialiasing - 1;
            set => Settings.SettingsGeneralData.graphicsData.Antialiasing = (AntialiasingOption)(value + 1);
        }
        #endregion

        #region Audio
        public int Audio_Global
        {
            get => (int)Settings.SettingsGeneralData.volumeGlobal;
            set => Settings.SettingsGeneralData.volumeGlobal = value;
        }

        public int Audio_SFX
        {
            get => (int)Settings.SettingsGeneralData.volumeSFX;
            set => Settings.SettingsGeneralData.volumeSFX = value;
        }

        public int Audio_Music
        {
            get => (int)Settings.SettingsGeneralData.volumeMusic;
            set => Settings.SettingsGeneralData.volumeMusic = value;
        }

        public int Audio_Voice
        {
            get => (int)Settings.SettingsGeneralData.volumeVoice;
            set => Settings.SettingsGeneralData.volumeVoice = value;
        }

        public bool Audio_DynamicRange
        {
            get => !Convert.ToBoolean((int)Settings.SettingsGeneralData.audioDynamicRange);
            set => Settings.SettingsGeneralData.audioDynamicRange = Convert.ToInt32(!value);
        }

        public bool Audio_Surround
        {
            get => Convert.ToBoolean((int)Settings.SettingsGeneralData.audioOutput);
            set => Settings.SettingsGeneralData.audioOutput = Convert.ToInt32(value);
        }
        #endregion

        #region Language
        public int AudioLang
        {
            get => (int)Settings.SettingsGeneralData.deviceVoiceLanguageType;
            set => Settings.SettingsGeneralData.deviceVoiceLanguageType = value;
        }

        public int TextLang
        {
            get => (int)Settings.SettingsGeneralData.deviceLanguageType - 1;
            set => Settings.SettingsGeneralData.deviceLanguageType = value + 1;
        }
        #endregion
    }
}
