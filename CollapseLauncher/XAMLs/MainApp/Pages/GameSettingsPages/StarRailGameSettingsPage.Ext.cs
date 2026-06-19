using CollapseLauncher.GameSettings.StarRail;
using Hi3Helper;
using Hi3Helper.Win32.Screen;
using Microsoft.UI.Xaml;
using System;
using System.Drawing;
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Pages
{
    public partial class StarRailGameSettingsPage
    {
        #region Fields
        private StarRailSettings? SettingsThis { get => field ??= Settings as StarRailSettings; }
        #endregion
        
        #region GameResolution
        public bool IsFullscreenEnabled
        {
            get => SettingsThis?.SettingsScreen.isfullScreen ?? false;
            set
            {
                SettingsThis?.SettingsScreen.isfullScreen = value;
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
            get => SettingsThis?.SettingsCollapseScreen.UseBorderlessScreen ?? false;
            set
            {
                Settings?.SettingsCollapseScreen.UseBorderlessScreen = value;
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
            get => Settings?.SettingsCollapseScreen.UseCustomResolution ?? false;
            set
            {
                Settings?.SettingsCollapseScreen.UseCustomResolution = value;
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
                return IsFullscreenEnabled && (Settings?.SettingsCollapseScreen.UseExclusiveFullscreen ?? false);
            }
            set
            {
                Settings?.SettingsCollapseScreen.UseExclusiveFullscreen = value;
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
            get => !(Settings?.SettingsScreen.isfullScreen ?? false) && !IsExclusiveFullscreenEnabled;
        }

        public bool IsResizableWindow
        {
            get => Settings?.SettingsCollapseScreen.UseResizableWindow ?? false;
            set
            {
                Settings?.SettingsCollapseScreen.UseResizableWindow = value;
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
            get => (Settings?.SettingsCollapseScreen.UseResizableWindow ?? false) && !IsExclusiveFullscreenEnabled;
        }

        public int ResolutionW
        {
            get => Settings?.SettingsScreen.sizeRes.Width ?? 0;
            set => Settings?.SettingsScreen.sizeRes = new Size(value, ResolutionH);
        }

        public int ResolutionH
        {
            get => Settings?.SettingsScreen.sizeRes.Height ?? 0;
            set => Settings?.SettingsScreen.sizeRes = new Size(ResolutionW, value);
        }

        public bool IsCanResolutionWH
        {
            get => IsCustomResolutionEnabled;
        }

        public string ResolutionSelected
        {
            get
            {
                string? res = Settings?.SettingsScreen.sizeResString;
                if (!string.IsNullOrEmpty(res))
                {
                    return res;
                }

                Size size = ScreenProp.CurrentResolution;
                return $"{size.Width}x{size.Height}";
            }
            set => Settings?.SettingsScreen.sizeResString = value;
        }
        #endregion

        #region Models
        //FPS
        public int FPS
        {
            get
            {
                int value = Model.FpsIndexDict[NormalizeFPSNumber(SettingsThis?.GraphicsSettings.FPS ?? 0)];
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

                SettingsThis?.GraphicsSettings.FPS = Model.FpsIndex[value];
            }
        }

        // Set it to 60 (default) if the value isn't within Model.FPSIndexDict
        private static int NormalizeFPSNumber(int input) => !Model.FpsIndexDict.ContainsKey(input) ? Model.FpsIndex[Model.FpsDefaultIndex] : input;

        //VSync
        public bool EnableVSync
        {
            get => SettingsThis?.GraphicsSettings.EnableVSync ?? false;
            set => SettingsThis?.GraphicsSettings.EnableVSync = value;
        }
        //RenderScale
        public double RenderScale
        {
            get => Math.Round(SettingsThis?.GraphicsSettings.RenderScale ?? 0, 1);
            set => SettingsThis?.GraphicsSettings.RenderScale = Math.Round(value, 1); // Round it to x.x (0.1) to fix floating-point rounding issue
        }
        //ResolutionQuality
        public int ResolutionQuality
        {
            get => Math.Clamp((int)(SettingsThis?.GraphicsSettings.ResolutionQuality ?? 0), 0, 5);
            set => SettingsThis?.GraphicsSettings.ResolutionQuality = (Quality)value;
        }
        //ShadowQuality
        public int ShadowQuality
        {
            get
            {
                int v = Math.Clamp((int)(SettingsThis?.GraphicsSettings.ShadowQuality ?? 0), 0, 4);
                if (v == 1) v = 2;
                return v;
            }
            set => SettingsThis?.GraphicsSettings.ShadowQuality = (Quality)value;
        }
        //LightQuality
        public int LightQuality
        {
            get => Math.Clamp((int)(SettingsThis?.GraphicsSettings.LightQuality ?? 0), 1, 5);
            set => SettingsThis?.GraphicsSettings.LightQuality = (Quality)value;
        }
        //CharacterQuality
        public int CharacterQuality
        {
            get => Math.Clamp((int)(SettingsThis?.GraphicsSettings.CharacterQuality ?? 0), 2, 4);
            set => SettingsThis?.GraphicsSettings.CharacterQuality = (Quality)value;
        }
        //EnvDetailQuality
        public int EnvDetailQuality
        {
            get => Math.Clamp((int)(SettingsThis?.GraphicsSettings.EnvDetailQuality ?? 0), 1, 5);
            set => SettingsThis?.GraphicsSettings.EnvDetailQuality = (Quality)value;
        }
        //ReflectionQuality
        public int ReflectionQuality
        {
            get => Math.Clamp((int)(SettingsThis?.GraphicsSettings.ReflectionQuality ?? 0), 1, 5);
            set => SettingsThis?.GraphicsSettings.ReflectionQuality = (Quality)value;
        }
        //SFXQuality
        public int SFXQuality
        {
            get => Math.Clamp((int)(SettingsThis?.GraphicsSettings.SFXQuality ?? 0), 1, 4);
            set => SettingsThis?.GraphicsSettings.SFXQuality = (Quality)value;
        }

        //DLSSQuality
        public int DlssQuality
        {
            get => Math.Clamp((int)(SettingsThis?.GraphicsSettings.DlssQuality ?? 0), 0, 5);
            set => SettingsThis?.GraphicsSettings.DlssQuality = (DLSSMode)value;
        }
        //BloomQuality
        public int BloomQuality
        {
            get => Math.Clamp((int)(SettingsThis?.GraphicsSettings.BloomQuality ?? 0), 0, 5);
            set => SettingsThis?.GraphicsSettings.BloomQuality = (Quality)value;
        }
        //AAMode
        public int AAMode
        {
            get => Math.Clamp((int)(SettingsThis?.GraphicsSettings.AAMode ?? 0), 0, 2);
            set => SettingsThis?.GraphicsSettings.AAMode = (AntialiasingMode)value;
        }
        //EnableSelfShadow
        public bool SelfShadow
        {
            get
            {
                int? v = SettingsThis?.GraphicsSettings.EnableSelfShadow;
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
            set => SettingsThis?.GraphicsSettings.EnableSelfShadow = value ? 1 : 2;
        }
        //HalfResTransparent
        public bool HalfResTransparent
        {
            get => SettingsThis?.GraphicsSettings.EnableHalfResTransparent ?? false;
            set => SettingsThis?.GraphicsSettings.EnableHalfResTransparent = value;
        }
        #endregion

        #region Audio
        public int AudioMasterVolume
        {
            get => SettingsThis?.AudioSettingsMaster.MasterVol ?? 0;
            set => SettingsThis?.AudioSettingsMaster.MasterVol = value;
        }

        public int AudioBGMVolume
        {
            get => SettingsThis?.AudioSettingsBgm.BGMVol ?? 0;
            set => SettingsThis?.AudioSettingsBgm.BGMVol = value;
        }

        public int AudioSFXVolume
        {
            get => SettingsThis?.AudioSettingsSfx.SFXVol ?? 0;
            set => SettingsThis?.AudioSettingsSfx.SFXVol = value;
        }

        public int AudioVOVolume
        {
            get => SettingsThis?.AudioSettingsVo.VOVol ?? 0;
            set => SettingsThis?.AudioSettingsVo.VOVol = value;
        }

        public int AudioLang
        {
            get => SettingsThis?.AudioLanguage.LocalAudioLangInt ?? 0;
            set => SettingsThis?.AudioLanguage.LocalAudioLangInt = value;
        }

        public int TextLang
        {
            get => SettingsThis?.TextLanguage.LocalTextLangInt ?? 0;
            set => SettingsThis?.TextLanguage.LocalTextLangInt = value;
        }
        #endregion

        #region Misc
        public bool IsGameBoost
        {
            get => SettingsThis?.SettingsCollapseMisc.UseGameBoost ?? false;
            set => SettingsThis?.SettingsCollapseMisc.UseGameBoost = value;
        }
        
        public bool IsMobileMode
        {
            get => SettingsThis?.SettingsCollapseMisc.LaunchMobileMode ?? false;
            set => SettingsThis?.SettingsCollapseMisc.LaunchMobileMode = value;
        }
        #endregion

        #region Advanced Settings
        public bool IsUseAdvancedSettings
        {
            get
            {
                bool value = SettingsThis?.SettingsCollapseMisc.UseAdvancedGameSettings ?? false;
                AdvancedSettingsPanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                return value;
            }
            set
            {
                Settings?.SettingsCollapseMisc.UseAdvancedGameSettings = value;
                AdvancedSettingsPanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public bool IsUsePreLaunchCommand
        {
            get 
            { 
                bool value = SettingsThis?.SettingsCollapseMisc.UseGamePreLaunchCommand ?? false;

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

                Settings?.SettingsCollapseMisc.UseGamePreLaunchCommand = value;
            }
        }

        public string PreLaunchCommand
        {
            get => SettingsThis?.SettingsCollapseMisc.GamePreLaunchCommand ?? string.Empty;
            set => Settings?.SettingsCollapseMisc.GamePreLaunchCommand = value;
        }

        public bool IsPreLaunchCommandExitOnGameClose
        {
            get => SettingsThis?.SettingsCollapseMisc.GamePreLaunchExitOnGameStop ?? false;
            set => Settings?.SettingsCollapseMisc.GamePreLaunchExitOnGameStop = value;
        }

        public int LaunchDelay
        {
            get => SettingsThis?.SettingsCollapseMisc.GameLaunchDelay ?? 0;
            set => Settings?.SettingsCollapseMisc.GameLaunchDelay = value;
        }
        
        public bool IsUsePostExitCommand
        {
            get
            {
                bool value = SettingsThis?.SettingsCollapseMisc.UseGamePostExitCommand ?? false;
                PostExitCommandTextBox.IsEnabled = value;
                return value;
            }
            set
            {
                PostExitCommandTextBox.IsEnabled                     = value;
                Settings?.SettingsCollapseMisc.UseGamePostExitCommand = value;
            }
        }

        public string PostExitCommand
        {
            get => SettingsThis?.SettingsCollapseMisc.GamePostExitCommand ?? string.Empty;
            set => SettingsThis?.SettingsCollapseMisc.GamePostExitCommand = value;
        }

        public bool RunWithExplorerAsParent
        {
            get => SettingsThis?.SettingsCollapseMisc.RunWithExplorerAsParent ?? false;
            set => SettingsThis?.SettingsCollapseMisc.RunWithExplorerAsParent = value;
        }
        #endregion
    }
}
