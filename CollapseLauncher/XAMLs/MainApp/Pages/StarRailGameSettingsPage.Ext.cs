using CollapseLauncher.GameSettings.StarRail;
using Microsoft.UI.Xaml.Controls;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace CollapseLauncher.Pages
{
    public sealed partial class StarRailGameSettingsPage : Page, INotifyPropertyChanged
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
        #region Models
        private void UpdateModels()
        {
            OnPropertyChanged("FPS");
            OnPropertyChanged("EnableVSync");
            OnPropertyChanged("RenderScale");
            OnPropertyChanged("ResolutionQuality");
            OnPropertyChanged("ShadowQuality");
            OnPropertyChanged("LightQuality");
            OnPropertyChanged("CharacterQuality");
            OnPropertyChanged("EnvDetailQuality");
            OnPropertyChanged("ReflectionQuality");
            OnPropertyChanged("BloomQuality");
            OnPropertyChanged("AAMode");
        }
        //FPS
        public int FPS
        {
            get
            {
                int fpsValue = NormalizeFPSNumber(Settings.GraphicsSettings.FPS);
                return Model.FPSIndexDict[fpsValue];
            }
            set
            {
                Settings.GraphicsSettings.FPS = Model.FPSIndex[value];
            }
        }

        // Set it to 60 (default) if the value isn't within Model.FPSIndexDict
        private int NormalizeFPSNumber(int input) => !Model.FPSIndexDict.ContainsKey(input) ? Model.FPSIndex[Model.FPSDefaultIndex] : input;

        //VSync
        public bool EnableVSync
        {
            get => Settings.GraphicsSettings.EnableVSync;
            set => Settings.GraphicsSettings.EnableVSync = value;
        }
        //RenderScale
        public double RenderScale
        {
            get => (double)Math.Round(Settings.GraphicsSettings.RenderScale, 1);
            set => Settings.GraphicsSettings.RenderScale = Math.Round(value, 1); // Round it to x.x (0.1) to fix floating-point rounding issue
        }
        //ResolutionQuality
        public int ResolutionQuality
        {
            get => prevGraphSelect = Settings.GraphicsSettings.ResolutionQuality;
            set => Settings.GraphicsSettings.ResolutionQuality = value;
        }
        //ShadowQuality
        public int ShadowQuality
        {
            get => prevGraphSelect = Settings.GraphicsSettings.ShadowQuality;
            set => Settings.GraphicsSettings.ShadowQuality = value;
        }
        //LightQuality
        public int LightQuality
        {
            get => prevGraphSelect = Settings.GraphicsSettings.LightQuality;
            set => Settings.GraphicsSettings.LightQuality = value;
        }
        //CharacterQuality
        public int CharacterQuality
        {
            get => prevGraphSelect = Settings.GraphicsSettings.CharacterQuality;
            set => Settings.GraphicsSettings.CharacterQuality = value;
        }
        //EnvDetailQuality
        public int EnvDetailQuality
        {
            get => prevGraphSelect = Settings.GraphicsSettings.EnvDetailQuality;
            set => Settings.GraphicsSettings.EnvDetailQuality = value;
        }
        //ReflectionQuality
        public int ReflectionQuality
        {
            get => prevGraphSelect = Settings.GraphicsSettings.ReflectionQuality;
            set => Settings.GraphicsSettings.ReflectionQuality = value;
        }
        //BloomQuality
        public int BloomQuality
        {
            get => prevGraphSelect = Settings.GraphicsSettings.BloomQuality;
            set => Settings.GraphicsSettings.BloomQuality = value;
        }
        //AAMode
        public int AAMode
        {
            get => prevGraphSelect = Settings.GraphicsSettings.AAMode;
            set => Settings.GraphicsSettings.AAMode = value;
        }
        #endregion

        #region Audio
        private void UpdateAudioMasterVolume()
        {
            OnPropertyChanged("AudioMasterVolume");
        }
        public int AudioMasterVolume
        {
            get => Settings.AudioSettings_Master.MasterVol = Settings.AudioSettings_Master.MasterVol;
            set => Settings.AudioSettings_Master.MasterVol = value;
        }

        private void UpdateAudioBGMVolume()
        {
            OnPropertyChanged("AudioBGMVolume");
        }
        public int AudioBGMVolume
        {
            get => Settings.AudioSettings_BGM.BGMVol = Settings.AudioSettings_BGM.BGMVol;
            set => Settings.AudioSettings_BGM.BGMVol = value;
        }

        private void UpdateAudioSFXVolume()
        {
            OnPropertyChanged("AudioSFXVolume");
        }
        public int AudioSFXVolume
        {
            get => Settings.AudioSettings_SFX.SFXVol = Settings.AudioSettings_SFX.SFXVol;
            set => Settings.AudioSettings_SFX.SFXVol = value;
        }

        private void UpdateAudioVOVolume()
        {
            OnPropertyChanged("AudioVOVolume");
        }
        public int AudioVOVolume
        {
            get => Settings.AudioSettings_VO.VOVol = Settings.AudioSettings_VO.VOVol;
            set => Settings.AudioSettings_VO.VOVol = value;
        }
        #endregion
    }
}
