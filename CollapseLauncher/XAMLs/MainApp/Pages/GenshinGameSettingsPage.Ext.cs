using CollapseLauncher.GameSettings.Genshin;
using CollapseLauncher.GameSettings.Genshin.Enums;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
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
            get => (int)Settings.SettingsGeneralData.graphicsData.FPS - 1;
            set => Settings.SettingsGeneralData.graphicsData.FPS = (FPSOption)(value + 1);
        }

        private List<double> _renderResolutions = new() { 0.6, 0.8, 1.0, 1.1, 1.2, 1.3, 1.4, 1.5 };
        public double RenderScale
        {
            get => _renderResolutions[(int)Settings.SettingsGeneralData.graphicsData.RenderResolution - 1];
            set => Settings.SettingsGeneralData.graphicsData.RenderResolution = (GameSettings.Genshin.Enums.RenderResolutionOption)(_renderResolutions.IndexOf(Math.Round(value, 1)) + 1);
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
    }
}
