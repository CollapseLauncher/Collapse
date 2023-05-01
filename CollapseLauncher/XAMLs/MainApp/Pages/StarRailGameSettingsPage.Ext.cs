using CollapseLauncher.GameSettings;
using CollapseLauncher.GameSettings.StarRail;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
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
        #region Models(basically graphics settings, well, partially anyway)
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
        public float FPS
        {
            get => Settings.GraphicsSettings.FPS;
            set => Settings.GraphicsSettings.FPS = value;
        }
        //VSync
        public bool EnableVSync
        {
            get => Settings.GraphicsSettings.EnableVSync;
            set => Settings.GraphicsSettings.EnableVSync = value;
        }
        //RenderScale
        public double RenderScale
        {
            get => Settings.GraphicsSettings.RenderScale;
            set => Settings.GraphicsSettings.RenderScale = value;
        }
        //ResolutionQuality
        public int ResolutionQuality
        {
            get => prevGraphSelect = (int)Settings.GraphicsSettings.ResolutionQuality;
            set => Settings.GraphicsSettings.ResolutionQuality = value;
        }
        //ShadowQuality
        public int ShadowQuality
        {
            get => prevGraphSelect = (int)Settings.GraphicsSettings.ShadowQuality;
            set => Settings.GraphicsSettings.ShadowQuality = value;
        }
        //LightQuality
        public int LightQuality
        {
            get => prevGraphSelect = (int)Settings.GraphicsSettings.LightQuality;
            set => Settings.GraphicsSettings.LightQuality = value;
        }
        //CharacterQuality
        public int CharacterQuality
        {
            get => prevGraphSelect = (int)Settings.GraphicsSettings.CharacterQuality;
            set => Settings.GraphicsSettings.CharacterQuality = value;
        }
        //EnvDetailQuality
        public int EnvDetailQuality
        {
            get => prevGraphSelect = (int)Settings.GraphicsSettings.EnvDetailQuality;
            set => Settings.GraphicsSettings.EnvDetailQuality = value;
        }
        //ReflectionQuality
        public int ReflectionQuality
        {
            get => prevGraphSelect = (int)Settings.GraphicsSettings.ReflectionQuality;
            set => Settings.GraphicsSettings.ReflectionQuality = value;
        }
        //BloomQuality
        public int BloomQuality
        {
            get => prevGraphSelect = (int)Settings.GraphicsSettings.BloomQuality;
            set => Settings.GraphicsSettings.BloomQuality = value;
        }
        //AAMode
        public int AAMode
        {
            get => prevGraphSelect = (int)Settings.GraphicsSettings.AAMode;
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
            get => Settings.AudioSettings_Master.mastervol;
            set => Settings.AudioSettings_Master.mastervol = value;
        }

        private void UpdateAudioBGMVolume()
        {
            OnPropertyChanged("AudioBGMVolume");
        }
        public int AudioBGMVolume
        {
            get => Settings.AudioSettings_BGM.bgmvol;
            set => Settings.AudioSettings_BGM.bgmvol = value;
        }

        private void UpdateAudioSFXVolume()
        {
            OnPropertyChanged("AudioSFXVolume");
        }
        public int AudioSFXVolume
        {
            get => Settings.AudioSettings_SFX.sfxvol;
            set => Settings.AudioSettings_SFX.sfxvol = value;
        }

        private void UpdateAudioVOVolume()
        {
            OnPropertyChanged("AudioVOVolume");
        }
        public int AudioVOVolume
        {
            get => Settings.AudioSettings_VO.vovol;
            set => Settings.AudioSettings_VO.vovol = value;
        }
        #endregion
    }
}
