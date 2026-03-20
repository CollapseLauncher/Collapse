using CollapseLauncher.GameSettings.Genshin;
using CollapseLauncher.GameSettings.Genshin.Enums;
using Hi3Helper.Win32.Screen;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.CompilerServices;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo

namespace CollapseLauncher.Pages
{
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    public partial class GenshinGameSettingsPage : INotifyPropertyChanged
    {
        #region Methods
        public event PropertyChangedEventHandler PropertyChanged;

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
            get
            {
               bool value = Settings!.SettingsScreen!.isfullScreen;
               if (value)
               {
                   GameWindowResizable.IsEnabled      = false;
                   GameWindowResizable.IsChecked      = false;
                   GameResolutionBorderless.IsEnabled = false;
                   GameResolutionBorderless.IsChecked = false;
               }
               else
               {
                   GameWindowResizable.IsEnabled      = true;
                   GameResolutionBorderless.IsEnabled = true;
               }
               return value;
            }
            set
            {
                ((GenshinSettings)Settings).SettingsScreen.isfullScreen = value;
                if (value)
                {
                    GameWindowResizable.IsEnabled               = false;
                    GameWindowResizable.IsChecked               = false;
                    GameResolutionFullscreenExclusive!.IsEnabled = !IsCustomResolutionEnabled;
                    GameResolutionFullscreenExclusive.IsChecked = false;
                    GameResolutionBorderless.IsChecked          = false;
                    return;
                }
                GameWindowResizable.IsEnabled = true;
                GameResolutionFullscreenExclusive!.IsEnabled = false;
                GameResolutionBorderless.IsEnabled = true;
            }
        }

        public bool IsBorderlessEnabled
        {
            get
            { 
                bool valueCl = ((GenshinSettings)Settings).SettingsCollapseScreen.UseBorderlessScreen;
                bool valueGi = ((GenshinSettings)Settings).SettingVisibleBackground.isBorderless;
                if (valueCl || valueGi)
                {
                    ((GenshinSettings)Settings).SettingsCollapseScreen.UseBorderlessScreen = true;
                    ((GenshinSettings)Settings).SettingVisibleBackground.isBorderless      = true;

                    GameWindowResizable.IsEnabled      = false;
                    GameWindowResizable.IsChecked      = false;
                    GameResolutionFullscreen.IsChecked = false;

                    return true;
                }
                GameWindowResizable.IsEnabled      = true;
                GameResolutionFullscreen.IsEnabled = true;

                return false;
            } 
            set
            {
                ((GenshinSettings)Settings).SettingsCollapseScreen.UseBorderlessScreen = value;
                ((GenshinSettings)Settings).SettingVisibleBackground.isBorderless      = value;
                if (value)
                {
                    GameWindowResizable.IsEnabled = false;
                    GameWindowResizable.IsChecked = false;
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
            get => ((GenshinSettings)Settings).SettingsCollapseScreen.UseCustomResolution;
            set
            {
                ((GenshinSettings)Settings).SettingsCollapseScreen.UseCustomResolution = value;
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
                return IsFullscreenEnabled && ((GenshinSettings)Settings).SettingsCollapseScreen.UseExclusiveFullscreen;
            }
            set
            {
                ((GenshinSettings)Settings).SettingsCollapseScreen.UseExclusiveFullscreen = value;
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
            get => !((GenshinSettings)Settings).SettingsScreen.isfullScreen && !IsExclusiveFullscreenEnabled;
        }

        public bool IsResizableWindow
        {
            get => ((GenshinSettings)Settings).SettingsCollapseScreen.UseResizableWindow;
            set
            {
                ((GenshinSettings)Settings).SettingsCollapseScreen.UseResizableWindow = value;
                if (value)
                {
                    GameResolutionFullscreen.IsChecked = false;
                    GameResolutionFullscreen.IsEnabled = false;
                    GameResolutionBorderless.IsChecked = false;
                    GameResolutionBorderless.IsEnabled = false;
                }
                else
                {
                    GameResolutionFullscreen.IsEnabled = true;
                    GameResolutionBorderless.IsEnabled = true;
                }
            }
        }

        public int ResolutionW
        {
            get => ((GenshinSettings)Settings).SettingsScreen.sizeRes.Width;
            set => ((GenshinSettings)Settings).SettingsScreen.sizeRes = new Size(value, ResolutionH);
        }

        public int ResolutionH
        {
            get => ((GenshinSettings)Settings).SettingsScreen.sizeRes.Height;
            set => ((GenshinSettings)Settings).SettingsScreen.sizeRes = new Size(ResolutionW, value);
        }

        public bool IsCanResolutionWH
        {
            get => IsCustomResolutionEnabled;
        }

        public string ResolutionSelected
        {
            get
            {
                string res = ((GenshinSettings)Settings).SettingsScreen.sizeResString;
                if (!string.IsNullOrEmpty(res))
                {
                    return res;
                }

                Size size = ScreenProp.CurrentResolution;
                return $"{size.Width}x{size.Height}";
            }
            set => ((GenshinSettings)Settings).SettingsScreen.sizeResString = value;
        }

        #endregion

        #region Graphics Settings
        public double Gamma
        {
            get => Math.Round(((GenshinSettings)Settings).SettingsGeneralData.gammaValue * -1 + 4.4, 5);
            set => ((GenshinSettings)Settings).SettingsGeneralData.gammaValue = Math.Round(value * -1 + 4.4, 5);
            // This should belong in programmer horror stories, *DO NOT EVER DO THIS*
            // Basically, calculate the stepper function which amounts to y = -x + 4.4, so we inverse the value and add 4.4
            // DON'T ASK HOW WE DID THIS, IT'S a 4AM THING :)
            // Round it to x.xxxxx because floating point
        }

        public bool VerticalSync
        {
            get => Convert.ToBoolean((int)((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.VerticalSync);
            set => ((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.VerticalSync = (VerticalSyncOption)Convert.ToInt32(value);
        }

        public bool VolumetricFog
        {
            get => Convert.ToBoolean((int)((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.VolumetricFog);
            set => ((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.VolumetricFog = (VolumetricFogOption)Convert.ToInt32(value);
        }

        public bool Reflections
        {
            get => Convert.ToBoolean((int)((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.Reflections);
            set => ((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.Reflections = (ReflectionsOption)Convert.ToInt32(value);
        }

        public bool Bloom
        {
            get => Convert.ToBoolean((int)((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.Bloom);
            set => ((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.Bloom = (BloomOption)Convert.ToInt32(value);
        }

        public int FPS
        {
            get
            {
                // Get the current value
                FPSOption curValue = ((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.FPS;
                // Get the index of the current value in FPSOptionsList array
                int indexOfValue = Array.IndexOf(GlobalPerfData.FPSOptionsList!, curValue);
                // Return the index of the value
                return indexOfValue;
            }
            set
            {
                // [HACK]: Fix some rare occasion where the "value" turned into -1. If so, then return
                if (value < 0) return;

                // Get the FPSOption based on the selected index by the "value"
                FPSOption valueFromIndex = GlobalPerfData.FPSOptionsList[value];
                // Set the actual value to its property
                ((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.FPS = valueFromIndex;
            }
        }

        public int RenderScale
        {
            get
            {
                int enumIndex = ((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.RenderResolution;
                int valueIndex = GlobalPerfData.RenderScaleIndex.IndexOf(enumIndex);
                double enumValue = GlobalPerfData.RenderScaleValues[valueIndex];
                return GlobalPerfData.RenderScaleValues.IndexOf(enumValue);
            }
            set
            {
                double enumValue = GlobalPerfData.RenderScaleValues[value];
                int enumIndex = DictionaryCategory.RenderResolutionOption[enumValue];
                ((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.RenderResolution = enumIndex;
            }
        }

        public int ShadowQuality
        {
            get
            {
                int curValue = (int)((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.ShadowQuality;

                // Disable Volumetric Fog when ShadowQuality is not Medium or higher
                if (curValue < 2)
                {
                    VolumetricFogToggle.IsChecked = false;
                    VolumetricFogToggle.IsEnabled = false;
                }
                else
                {
                    VolumetricFogToggle.IsEnabled = true;
                }

                return curValue;
            }
            set
            {
                if (value < 2)
                {
                    VolumetricFogToggle.IsChecked = false;
                    VolumetricFogToggle.IsEnabled = false;
                }
                else
                {
                    VolumetricFogToggle.IsEnabled = true;
                }

                ((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.ShadowQuality = (ShadowQualityOption)value;
            }
        }

        public int VisualEffects
        {
            get => (int)((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.VisualEffects;
            set => ((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.VisualEffects = (VisualEffectsOption)value;
        }

        public int SFXQuality
        {
            get => (int)((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.SFXQuality;
            set => ((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.SFXQuality = (SFXQualityOption)value;
        }

        public int EnvironmentDetail
        {
            get => (int)((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.EnvironmentDetail;
            set => ((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.EnvironmentDetail = (EnvironmentDetailOption)value;
        }

        public int MotionBlur
        {
            get => (int)((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.MotionBlur;
            set => ((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.MotionBlur = (MotionBlurOption)value;
        }

        public int CrowdDensity
        {
            get => (int)((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.CrowdDensity;
            set => ((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.CrowdDensity = (CrowdDensityOption)value;
        }

        public int SubsurfaceScattering
        {
            get => (int)((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.SubsurfaceScattering;
            set => ((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.SubsurfaceScattering = (SubsurfaceScatteringOption)value;
        }

        public int CoOpTeammateEffects
        {
            get => (int)((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.CoOpTeammateEffects;
            set => ((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.CoOpTeammateEffects = (CoOpTeammateEffectsOption)value;
        }

        public int AnisotropicFiltering
        {
            get => (int)((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.AnisotropicFiltering;
            set => ((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.AnisotropicFiltering = (AnisotropicFilteringOption)value;
        }

        public int Antialiasing
        {
            get => (int)((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.Antialiasing;
            set => ((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.Antialiasing = (AntialiasingOption)value;
        }

        public bool TeamPageBackground
        {
            get => !((GenshinSettings)Settings).SettingsGeneralData.disableTeamPageBackgroundSwitch;
            set => ((GenshinSettings)Settings).SettingsGeneralData.disableTeamPageBackgroundSwitch = !value;
        }

        public int GlobalIllumination
        {
            get => (int)((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.GlobalIllumination;
            set => ((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.GlobalIllumination = (GlobalIlluminationOption)value;
        }

        public bool DynamicCharacterResolution
        {
            get => Convert.ToBoolean((int)((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.DynamicCharacterResolution);
            set => ((GenshinSettings)Settings).SettingsGeneralData.globalPerfData.DynamicCharacterResolution = (DynamicCharacterResolutionOption)Convert.ToInt32(value);
        }
        #endregion

        #region Graphics Settings - HDR
        public bool IsHDR
        {
            get => GetAppConfigValue("ForceGIHDREnable").ToBool() || ((GenshinSettings)Settings).SettingsWindowsHDR.isHDR || ((GenshinSettings)Settings).SettingsGeneralData.enableHDR;
            set
            {
                ((GenshinSettings)Settings).SettingsWindowsHDR.isHDR = value;
                ((GenshinSettings)Settings).SettingsGeneralData.enableHDR = value;
                SetAndSaveConfigValue("ForceGIHDREnable", value);
            } 
        }

        private double MaxLuminosity
        {
            get => (double)((GenshinSettings)Settings).SettingsGeneralData.maxLuminosity;
            set => ((GenshinSettings)Settings).SettingsGeneralData.maxLuminosity = (decimal)Math.Round(value, 1);
        }

        private double UiPaperWhite
        {
            get => (double)((GenshinSettings)Settings).SettingsGeneralData.uiPaperWhite;
            set => ((GenshinSettings)Settings).SettingsGeneralData.uiPaperWhite = (decimal)Math.Round(value, 1);
        }

        private double ScenePaperWhite
        {
            get => (double)((GenshinSettings)Settings).SettingsGeneralData.scenePaperWhite;
            set => ((GenshinSettings)Settings).SettingsGeneralData.scenePaperWhite = (decimal)Math.Round(value, 1);
        }
        #endregion

        #region Audio
        public int Audio_Global
        {
            get => ((GenshinSettings)Settings).SettingsGeneralData.volumeGlobal;
            set => ((GenshinSettings)Settings).SettingsGeneralData.volumeGlobal = value;
        }

        public int Audio_SFX
        {
            get => ((GenshinSettings)Settings).SettingsGeneralData.volumeSFX;
            set => ((GenshinSettings)Settings).SettingsGeneralData.volumeSFX = value;
        }

        public int Audio_Music
        {
            get => ((GenshinSettings)Settings).SettingsGeneralData.volumeMusic;
            set => ((GenshinSettings)Settings).SettingsGeneralData.volumeMusic = value;
        }

        public int Audio_Voice
        {
            get => ((GenshinSettings)Settings).SettingsGeneralData.volumeVoice;
            set => ((GenshinSettings)Settings).SettingsGeneralData.volumeVoice = value;
        }

        public bool Audio_DynamicRange
        {
            get => !Convert.ToBoolean(((GenshinSettings)Settings).SettingsGeneralData.audioDynamicRange);
            set => ((GenshinSettings)Settings).SettingsGeneralData.audioDynamicRange = Convert.ToInt32(!value);
        }

        public bool Audio_Surround
        {
            get => Convert.ToBoolean(((GenshinSettings)Settings).SettingsGeneralData.audioOutput);
            set => ((GenshinSettings)Settings).SettingsGeneralData.audioOutput = Convert.ToInt32(value);
        }

        public bool Audio_MuteOnMinimized
        {
            get => ((GenshinSettings)Settings).SettingsGeneralData.muteAudioOnAppMinimized;
            set => ((GenshinSettings)Settings).SettingsGeneralData.muteAudioOnAppMinimized = value;
        }
        #endregion

        #region Language
        public int AudioLang
        {
            get => ((GenshinSettings)Settings).SettingsGeneralData.deviceVoiceLanguageType;
            set => ((GenshinSettings)Settings).SettingsGeneralData.deviceVoiceLanguageType = value;
        }

        public int TextLang
        {
            get => ((GenshinSettings)Settings).SettingsGeneralData.deviceLanguageType - 1;
            set => ((GenshinSettings)Settings).SettingsGeneralData.deviceLanguageType = value + 1;
        }
        #endregion

        #region Misc
        public bool IsGameBoost
        {
            get => ((GenshinSettings)Settings).SettingsCollapseMisc.UseGameBoost;
            set => ((GenshinSettings)Settings).SettingsCollapseMisc.UseGameBoost = value;
        }

        public bool IsMobileMode
        {
            //get => ((GenshinSettings)Settings).SettingsCollapseMisc.LaunchMobileMode;
            get => false;
            set => ((GenshinSettings)Settings).SettingsCollapseMisc.LaunchMobileMode = value;
        }
        #endregion

        #region Advanced Settings
        public bool IsUseAdvancedSettings
        {
            get
            {
                bool value = ((GenshinSettings)Settings).SettingsCollapseMisc.UseAdvancedGameSettings;
                AdvancedSettingsPanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                return value;
            }
            set
            {
                ((GenshinSettings)Settings).SettingsCollapseMisc.UseAdvancedGameSettings = value;
                AdvancedSettingsPanel.Visibility                      = value ? Visibility.Visible : Visibility.Collapsed;
            } 
        }

        public bool IsUsePreLaunchCommand
        {
            get 
            { 
                bool value = ((GenshinSettings)Settings).SettingsCollapseMisc.UseGamePreLaunchCommand;

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

                ((GenshinSettings)Settings).SettingsCollapseMisc.UseGamePreLaunchCommand = value;
            }
        }

        public string PreLaunchCommand
        {
            get => ((GenshinSettings)Settings).SettingsCollapseMisc.GamePreLaunchCommand;
            set => ((GenshinSettings)Settings).SettingsCollapseMisc.GamePreLaunchCommand = value;
        }

        public bool IsPreLaunchCommandExitOnGameClose
        {
            get => ((GenshinSettings)Settings).SettingsCollapseMisc.GamePreLaunchExitOnGameStop;
            set => ((GenshinSettings)Settings).SettingsCollapseMisc.GamePreLaunchExitOnGameStop = value;
        }

        public int LaunchDelay
        {
            get => ((GenshinSettings)Settings).SettingsCollapseMisc.GameLaunchDelay;
            set => ((GenshinSettings)Settings).SettingsCollapseMisc.GameLaunchDelay = value;
        }
        
        public bool IsUsePostExitCommand
        {
            get 
            {
                bool value = ((GenshinSettings)Settings).SettingsCollapseMisc.UseGamePostExitCommand;
                PostExitCommandTextBox.IsEnabled = value;

                return value;
            }
            set
            {
                PostExitCommandTextBox.IsEnabled = value;
                ((GenshinSettings)Settings).SettingsCollapseMisc.UseGamePostExitCommand = value;
            }
        }

        public string PostExitCommand
        {
            get => ((GenshinSettings)Settings).SettingsCollapseMisc.GamePostExitCommand;
            set => ((GenshinSettings)Settings).SettingsCollapseMisc.GamePostExitCommand = value;
        }

        private void GameLaunchDelay_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            // clamp for negative value when clearing the number box
            if ((int)sender.Value < 0)
                sender.Value = 0;
        }

        public bool RunWithExplorerAsParent
        {
            get => ((GenshinSettings)Settings).SettingsCollapseMisc.RunWithExplorerAsParent;
            set => ((GenshinSettings)Settings).SettingsCollapseMisc.RunWithExplorerAsParent = value;
        }
        #endregion
    }
}
