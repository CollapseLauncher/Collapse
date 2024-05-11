using CollapseLauncher.GameSettings.StarRail;
using Hi3Helper.Screen;
using Microsoft.UI.Xaml;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace CollapseLauncher.Pages
{
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    public partial class StarRailGameSettingsPage : INotifyPropertyChanged
    {
        #region Methods
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        // ReSharper disable once UnusedMember.Local
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
                    GameWindowResizable.IsEnabled = false;
                    GameWindowResizable.IsChecked = false;
                    GameResolutionFullscreenExclusive.IsEnabled = !IsCustomResolutionEnabled;
                    GameResolutionBorderless.IsChecked = false;
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
                    GameWindowResizable.IsEnabled = true;
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
        #endregion

        #region Models
        //FPS
        public int FPS
        {
            get
            {
                int value = Model.FPSIndexDict[NormalizeFPSNumber(Settings.GraphicsSettings.FPS)];
                if (value == 2)
                {
                    VSyncToggle.IsChecked = false;
                    VSyncToggle.IsEnabled = false;
                }

                return value;
            }
            set
            {
                if (value == 2)
                {
                    VSyncToggle.IsChecked = false;
                    VSyncToggle.IsEnabled = false;
                }
                else { VSyncToggle.IsEnabled = true; }

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
            get => Math.Round(Settings.GraphicsSettings.RenderScale, 1);
            set => Settings.GraphicsSettings.RenderScale = Math.Round(value, 1); // Round it to x.x (0.1) to fix floating-point rounding issue
        }
        //ResolutionQuality
        public int ResolutionQuality
        {
            get => (int)Settings.GraphicsSettings.ResolutionQuality;
            set => Settings.GraphicsSettings.ResolutionQuality = (Quality)value;
        }
        //ShadowQuality
        public int ShadowQuality
        {
            get
            {
                var v = (int)Settings.GraphicsSettings.ShadowQuality;
                
                // clamp values
                if (v > 4) v = 4;
                if (v == 1) v = 2;

                return v;
            }
            set => Settings.GraphicsSettings.ShadowQuality = (Quality)value;
        }
        //LightQuality
        public int LightQuality
        {
            get => (int)Settings.GraphicsSettings.LightQuality;
            set => Settings.GraphicsSettings.LightQuality = (Quality)value;
        }
        //CharacterQuality
        public int CharacterQuality
        {
            get
            {
                int value = (int)Settings.GraphicsSettings.CharacterQuality;

                // Clamp value
                if (value < 2) return 2; // Low
                if (value > 4) return 4; // High

                return value;
            }
            set => Settings.GraphicsSettings.CharacterQuality = (CharacterQualityEnum)value;
        }
        //EnvDetailQuality
        public int EnvDetailQuality
        {
            get => (int)Settings.GraphicsSettings.EnvDetailQuality;
            set => Settings.GraphicsSettings.EnvDetailQuality = (Quality)value;
        }
        //ReflectionQuality
        public int ReflectionQuality
        {
            get => (int)Settings.GraphicsSettings.ReflectionQuality;
            set => Settings.GraphicsSettings.ReflectionQuality = (Quality)value;
        }
        //SFXQuality
        public int SFXQuality
        {
            get => (int)Settings.GraphicsSettings.SFXQuality;
            set => Settings.GraphicsSettings.SFXQuality = (Quality)value;
        }
        //BloomQuality
        public int BloomQuality
        {
            get => (int)Settings.GraphicsSettings.BloomQuality;
            set => Settings.GraphicsSettings.BloomQuality = (Quality)value;
        }
        //AAMode
        public int AAMode
        {
            get => (int)Settings.GraphicsSettings.AAMode;
            set => Settings.GraphicsSettings.AAMode = (AntialiasingMode)value;
        }
        #endregion

        #region Audio
        public int AudioMasterVolume
        {
            get => Settings.AudioSettings_Master.MasterVol = Settings.AudioSettings_Master.MasterVol;
            set => Settings.AudioSettings_Master.MasterVol = value;
        }

        public int AudioBGMVolume
        {
            get => Settings.AudioSettings_BGM.BGMVol = Settings.AudioSettings_BGM.BGMVol;
            set => Settings.AudioSettings_BGM.BGMVol = value;
        }

        public int AudioSFXVolume
        {
            get => Settings.AudioSettings_SFX.SFXVol = Settings.AudioSettings_SFX.SFXVol;
            set => Settings.AudioSettings_SFX.SFXVol = value;
        }

        public int AudioVOVolume
        {
            get => Settings.AudioSettings_VO.VOVol = Settings.AudioSettings_VO.VOVol;
            set => Settings.AudioSettings_VO.VOVol = value;
        }

        public int AudioLang
        {
            get => Settings.AudioLanguage.LocalAudioLangInt = Settings.AudioLanguage.LocalAudioLangInt;
            set => Settings.AudioLanguage.LocalAudioLangInt = value;
        }

        public int TextLang
        {
            get => Settings.TextLanguage.LocalTextLangInt = Settings.TextLanguage.LocalTextLangInt;
            set => Settings.TextLanguage.LocalTextLangInt = value;
        }
        #endregion

        #region Misc
        public bool IsGameBoost
        {
            get => Settings.SettingsCollapseMisc.UseGameBoost;
            set => Settings.SettingsCollapseMisc.UseGameBoost = value;
        }
        #endregion

        #region Advanced Settings
        public bool IsUseAdvancedSettings
        {
            get
            {
                bool value                                  = Settings.SettingsCollapseMisc.UseAdvancedGameSettings;
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
                bool value = Settings.SettingsCollapseMisc.UseGamePreLaunchCommand;

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
                }
                else
                {
                    PreLaunchCommandTextBox.IsEnabled   = false;
                    PreLaunchForceCloseToggle.IsEnabled = false;
                }

                Settings.SettingsCollapseMisc.UseGamePreLaunchCommand = value;
            }
        }

        public string PreLaunchCommand
        {
            get => Settings.SettingsCollapseMisc.GamePreLaunchCommand;
            set => Settings.SettingsCollapseMisc.GamePreLaunchCommand = value;
        }

        public bool IsPreLaunchCommandExitOnGameClose
        {
            get => Settings.SettingsCollapseMisc.GamePreLaunchExitOnGameStop;
            set => Settings.SettingsCollapseMisc.GamePreLaunchExitOnGameStop = value;
        }

        public bool IsUsePostExitCommand
        {
            get 
            {
                bool value = Settings.SettingsCollapseMisc.UseGamePostExitCommand;

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
            get => Settings.SettingsCollapseMisc.GamePostExitCommand;
            set => Settings.SettingsCollapseMisc.GamePostExitCommand = value;
        }
        #endregion
    }
}
