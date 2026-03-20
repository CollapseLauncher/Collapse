using CollapseLauncher.GameSettings.StarRail;
using Hi3Helper;
using Hi3Helper.Win32.Screen;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.CompilerServices;
// ReSharper disable InconsistentNaming
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo

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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region GameResolution
        public bool IsFullscreenEnabled
        {
            get => ((StarRailSettings)Settings).SettingsScreen.isfullScreen;
            set
            {
                ((StarRailSettings)Settings).SettingsScreen.isfullScreen = value;
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
            get => ((StarRailSettings)Settings).SettingsCollapseScreen.UseBorderlessScreen;
            set
            {
                ((StarRailSettings)Settings).SettingsCollapseScreen.UseBorderlessScreen = value;
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
            get => ((StarRailSettings)Settings).SettingsCollapseScreen.UseCustomResolution;
            set
            {
                ((StarRailSettings)Settings).SettingsCollapseScreen.UseCustomResolution = value;
                if (value)
                {
                    GameResolutionFullscreenExclusive.IsEnabled = false;
                    GameResolutionSelector.IsEnabled = false;
                    GameCustomResolutionWidth.IsEnabled = true;
                    GameCustomResolutionHeight.IsEnabled = true;

                    string[] sizes = ResolutionSelected.Split('x');
                    GameCustomResolutionWidth.Value = int.Parse(sizes[0]);
                    GameCustomResolutionHeight.Value = int.Parse(sizes[1]);

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

        public bool IsCanExclusiveFullscreen
        {
            get => !(!IsFullscreenEnabled || IsCustomResolutionEnabled);
        }

        public bool IsExclusiveFullscreenEnabled
        {
            get
            {
                return IsFullscreenEnabled && ((StarRailSettings)Settings).SettingsCollapseScreen.UseExclusiveFullscreen;
            }
            set
            {
                ((StarRailSettings)Settings).SettingsCollapseScreen.UseExclusiveFullscreen = value;
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
            get => !((StarRailSettings)Settings).SettingsScreen.isfullScreen && !IsExclusiveFullscreenEnabled;
        }

        public bool IsResizableWindow
        {
            get => ((StarRailSettings)Settings).SettingsCollapseScreen.UseResizableWindow;
            set
            {
                ((StarRailSettings)Settings).SettingsCollapseScreen.UseResizableWindow = value;
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
            get => ((StarRailSettings)Settings).SettingsCollapseScreen.UseResizableWindow && !IsExclusiveFullscreenEnabled;
        }

        public int ResolutionW
        {
            get => ((StarRailSettings)Settings).SettingsScreen.sizeRes.Width;
            set => ((StarRailSettings)Settings).SettingsScreen.sizeRes = new Size(value, ResolutionH);
        }

        public int ResolutionH
        {
            get => ((StarRailSettings)Settings).SettingsScreen.sizeRes.Height;
            set => ((StarRailSettings)Settings).SettingsScreen.sizeRes = new Size(ResolutionW, value);
        }

        public bool IsCanResolutionWH
        {
            get => IsCustomResolutionEnabled;
        }

        public string ResolutionSelected
        {
            get
            {
                string res = ((StarRailSettings)Settings).SettingsScreen.sizeResString;
                if (!string.IsNullOrEmpty(res))
                {
                    return res;
                }

                Size size = ScreenProp.CurrentResolution;
                return $"{size.Width}x{size.Height}";
            }
            set => ((StarRailSettings)Settings).SettingsScreen.sizeResString = value;
        }
        #endregion

        #region Models
        //FPS
        public int FPS
        {
            get
            {
                int value = Model.FpsIndexDict[NormalizeFPSNumber(((StarRailSettings)Settings).GraphicsSettings.FPS)];
                if (value != 2)
                {
                    return value;
                }

                VSyncToggle.IsChecked = false;
                VSyncToggle.IsEnabled = false;

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

                ((StarRailSettings)Settings).GraphicsSettings.FPS = Model.FpsIndex[value];
            }
        }

        // Set it to 60 (default) if the value isn't within Model.FPSIndexDict
        private static int NormalizeFPSNumber(int input) => !Model.FpsIndexDict.ContainsKey(input) ? Model.FpsIndex[Model.FpsDefaultIndex] : input;

        //VSync
        public bool EnableVSync
        {
            get => ((StarRailSettings)Settings).GraphicsSettings.EnableVSync;
            set => ((StarRailSettings)Settings).GraphicsSettings.EnableVSync = value;
        }
        //RenderScale
        public double RenderScale
        {
            get => Math.Round(((StarRailSettings)Settings).GraphicsSettings.RenderScale, 1);
            set => ((StarRailSettings)Settings).GraphicsSettings.RenderScale = Math.Round(value, 1); // Round it to x.x (0.1) to fix floating-point rounding issue
        }
        //ResolutionQuality
        public int ResolutionQuality
        {
            get => Math.Clamp((int)((StarRailSettings)Settings).GraphicsSettings.ResolutionQuality, 0, 5);
            set => ((StarRailSettings)Settings).GraphicsSettings.ResolutionQuality = (Quality)value;
        }
        //ShadowQuality
        public int ShadowQuality
        {
            get
            {
                var v = Math.Clamp((int)((StarRailSettings)Settings).GraphicsSettings.ShadowQuality, 0, 4);
                if (v == 1) v = 2;
                return v;
            }
            set => ((StarRailSettings)Settings).GraphicsSettings.ShadowQuality = (Quality)value;
        }
        //LightQuality
        public int LightQuality
        {
            get => Math.Clamp((int)((StarRailSettings)Settings).GraphicsSettings.LightQuality, 1, 5);
            set => ((StarRailSettings)Settings).GraphicsSettings.LightQuality = (Quality)value;
        }
        //CharacterQuality
        public int CharacterQuality
        {
            get => Math.Clamp((int)((StarRailSettings)Settings).GraphicsSettings.CharacterQuality, 2, 4);
            set => ((StarRailSettings)Settings).GraphicsSettings.CharacterQuality = (Quality)value;
        }
        //EnvDetailQuality
        public int EnvDetailQuality
        {
            get => Math.Clamp((int)((StarRailSettings)Settings).GraphicsSettings.EnvDetailQuality, 1, 5);
            set => ((StarRailSettings)Settings).GraphicsSettings.EnvDetailQuality = (Quality)value;
        }
        //ReflectionQuality
        public int ReflectionQuality
        {
            get => Math.Clamp((int)((StarRailSettings)Settings).GraphicsSettings.ReflectionQuality, 1, 5);
            set => ((StarRailSettings)Settings).GraphicsSettings.ReflectionQuality = (Quality)value;
        }
        //SFXQuality
        public int SFXQuality
        {
            get => Math.Clamp((int)((StarRailSettings)Settings).GraphicsSettings.SFXQuality, 1, 4);
            set => ((StarRailSettings)Settings).GraphicsSettings.SFXQuality = (Quality)value;
        }

        //DLSSQuality
        public int DlssQuality
        {
            get => Math.Clamp((int)((StarRailSettings)Settings).GraphicsSettings.DlssQuality, 0, 5);
            set => ((StarRailSettings)Settings).GraphicsSettings.DlssQuality = (DLSSMode)value;
        }
        //BloomQuality
        public int BloomQuality
        {
            get => Math.Clamp((int)((StarRailSettings)Settings).GraphicsSettings.BloomQuality, 0, 5);
            set => ((StarRailSettings)Settings).GraphicsSettings.BloomQuality = (Quality)value;
        }
        //AAMode
        public int AAMode
        {
            get => Math.Clamp((int)((StarRailSettings)Settings).GraphicsSettings.AAMode, 0, 2);
            set => ((StarRailSettings)Settings).GraphicsSettings.AAMode = (AntialiasingMode)value;
        }
        //EnableSelfShadow
        public bool SelfShadow
        {
            get
            {
                var v = ((StarRailSettings)Settings).GraphicsSettings.EnableSelfShadow;
                switch (v)
                {
                    case 1:
                        return true;
                    case 2:
                        return false;
                    default:
                        Logger.LogWriteLine($"Self Shadow value is unknown! Val: {v}", LogType.Error, true);
                        return false;
                }
            }
            set => ((StarRailSettings)Settings).GraphicsSettings.EnableSelfShadow = value ? 1 : 2;
        }
        //HalfResTransparent
        public bool HalfResTransparent
        {
            get => ((StarRailSettings)Settings).GraphicsSettings.EnableHalfResTransparent;
            set => ((StarRailSettings)Settings).GraphicsSettings.EnableHalfResTransparent = value;
        }
        #endregion

        #region Audio
        public int AudioMasterVolume
        {
            get => ((StarRailSettings)Settings).AudioSettingsMaster.MasterVol = ((StarRailSettings)Settings).AudioSettingsMaster.MasterVol;
            set => ((StarRailSettings)Settings).AudioSettingsMaster.MasterVol = value;
        }

        public int AudioBGMVolume
        {
            get => ((StarRailSettings)Settings).AudioSettingsBgm.BGMVol = ((StarRailSettings)Settings).AudioSettingsBgm.BGMVol;
            set => ((StarRailSettings)Settings).AudioSettingsBgm.BGMVol = value;
        }

        public int AudioSFXVolume
        {
            get => ((StarRailSettings)Settings).AudioSettingsSfx.SFXVol = ((StarRailSettings)Settings).AudioSettingsSfx.SFXVol;
            set => ((StarRailSettings)Settings).AudioSettingsSfx.SFXVol = value;
        }

        public int AudioVOVolume
        {
            get => ((StarRailSettings)Settings).AudioSettingsVo.VOVol = ((StarRailSettings)Settings).AudioSettingsVo.VOVol;
            set => ((StarRailSettings)Settings).AudioSettingsVo.VOVol = value;
        }

        public int AudioLang
        {
            get => ((StarRailSettings)Settings).AudioLanguage.LocalAudioLangInt = ((StarRailSettings)Settings).AudioLanguage.LocalAudioLangInt;
            set => ((StarRailSettings)Settings).AudioLanguage.LocalAudioLangInt = value;
        }

        public int TextLang
        {
            get => ((StarRailSettings)Settings).TextLanguage.LocalTextLangInt = ((StarRailSettings)Settings).TextLanguage.LocalTextLangInt;
            set => ((StarRailSettings)Settings).TextLanguage.LocalTextLangInt = value;
        }
        #endregion

        #region Misc
        public bool IsGameBoost
        {
            get => ((StarRailSettings)Settings).SettingsCollapseMisc.UseGameBoost;
            set => ((StarRailSettings)Settings).SettingsCollapseMisc.UseGameBoost = value;
        }
        
        public bool IsMobileMode
        {
            get => ((StarRailSettings)Settings).SettingsCollapseMisc.LaunchMobileMode;
            set => ((StarRailSettings)Settings).SettingsCollapseMisc.LaunchMobileMode = value;
        }
        #endregion

        #region Advanced Settings
        public bool IsUseAdvancedSettings
        {
            get
            {
                bool value = ((StarRailSettings)Settings).SettingsCollapseMisc.UseAdvancedGameSettings;
                AdvancedSettingsPanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                return value;
            }
            set
            {
                ((StarRailSettings)Settings).SettingsCollapseMisc.UseAdvancedGameSettings = value;
                AdvancedSettingsPanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public bool IsUsePreLaunchCommand
        {
            get 
            { 
                bool value = ((StarRailSettings)Settings).SettingsCollapseMisc.UseGamePreLaunchCommand;

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

                ((StarRailSettings)Settings).SettingsCollapseMisc.UseGamePreLaunchCommand = value;
            }
        }

        public string PreLaunchCommand
        {
            get => ((StarRailSettings)Settings).SettingsCollapseMisc.GamePreLaunchCommand;
            set => ((StarRailSettings)Settings).SettingsCollapseMisc.GamePreLaunchCommand = value;
        }

        public bool IsPreLaunchCommandExitOnGameClose
        {
            get => ((StarRailSettings)Settings).SettingsCollapseMisc.GamePreLaunchExitOnGameStop;
            set => ((StarRailSettings)Settings).SettingsCollapseMisc.GamePreLaunchExitOnGameStop = value;
        }

        public int LaunchDelay
        {
            get => ((StarRailSettings)Settings).SettingsCollapseMisc.GameLaunchDelay;
            set => ((StarRailSettings)Settings).SettingsCollapseMisc.GameLaunchDelay = value;
        }
        
        public bool IsUsePostExitCommand
        {
            get
            {
                bool value = ((StarRailSettings)Settings).SettingsCollapseMisc.UseGamePostExitCommand;
                PostExitCommandTextBox.IsEnabled = value;
                return value;
            }
            set
            {
                PostExitCommandTextBox.IsEnabled                     = value;
                ((StarRailSettings)Settings).SettingsCollapseMisc.UseGamePostExitCommand = value;
            }
        }

        public string PostExitCommand
        {
            get => ((StarRailSettings)Settings).SettingsCollapseMisc.GamePostExitCommand;
            set => ((StarRailSettings)Settings).SettingsCollapseMisc.GamePostExitCommand = value;
        }

        private void GameLaunchDelay_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            // clamp for negative value when clearing the number box
            if ((int)sender.Value < 0)
                sender.Value = 0;
        }

        public bool RunWithExplorerAsParent
        {
            get => ((StarRailSettings)Settings).SettingsCollapseMisc.RunWithExplorerAsParent;
            set => ((StarRailSettings)Settings).SettingsCollapseMisc.RunWithExplorerAsParent = value;
        }
        #endregion
    }
}
