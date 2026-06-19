using CollapseLauncher.GameSettings.Genshin;
using CollapseLauncher.GameSettings.Genshin.Enums;
using Hi3Helper.Win32.Screen;
using Microsoft.UI.Xaml;
using System;
using System.Drawing;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Pages
{
    public partial class GenshinGameSettingsPage
    {
        #region Fields
        private GenshinSettings? SettingsThis { get => field ??= Settings as GenshinSettings; }
        #endregion

        #region GameResolution
        public bool IsFullscreenEnabled
        {
            get
            {
               bool value = SettingsThis?.SettingsScreen?.isfullScreen ?? false;
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
                SettingsThis?.SettingsScreen.isfullScreen = value;
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
                bool valueCl = SettingsThis?.SettingsCollapseScreen.UseBorderlessScreen ?? false;
                bool valueGi = SettingsThis?.SettingVisibleBackground.isBorderless ?? false;
                if (valueCl || valueGi)
                {
                    SettingsThis?.SettingsCollapseScreen.UseBorderlessScreen = true;
                    SettingsThis?.SettingVisibleBackground.isBorderless      = true;

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
                SettingsThis?.SettingsCollapseScreen.UseBorderlessScreen = value;
                SettingsThis?.SettingVisibleBackground.isBorderless      = value;
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
            get => SettingsThis?.SettingsCollapseScreen.UseCustomResolution ?? false;
            set
            {
                SettingsThis?.SettingsCollapseScreen.UseCustomResolution = value;
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
                return IsFullscreenEnabled && (SettingsThis?.SettingsCollapseScreen.UseExclusiveFullscreen ?? false);
            }
            set
            {
                SettingsThis?.SettingsCollapseScreen.UseExclusiveFullscreen = value;
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
            get => !(SettingsThis?.SettingsScreen.isfullScreen ?? false) && !IsExclusiveFullscreenEnabled;
        }

        public bool IsResizableWindow
        {
            get => SettingsThis?.SettingsCollapseScreen.UseResizableWindow ?? false;
            set
            {
                SettingsThis?.SettingsCollapseScreen.UseResizableWindow = value;
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
            get => SettingsThis?.SettingsScreen.sizeRes.Width ?? 0;
            set => SettingsThis?.SettingsScreen.sizeRes = new Size(value, ResolutionH);
        }

        public int ResolutionH
        {
            get => SettingsThis?.SettingsScreen.sizeRes.Height ?? 0;
            set => SettingsThis?.SettingsScreen.sizeRes = new Size(ResolutionW, value);
        }

        public bool IsCanResolutionWH
        {
            get => IsCustomResolutionEnabled;
        }

        public string ResolutionSelected
        {
            get
            {
                string? res = SettingsThis?.SettingsScreen.sizeResString;
                if (!string.IsNullOrEmpty(res))
                {
                    return res;
                }

                Size size = ScreenProp.CurrentResolution;
                return $"{size.Width}x{size.Height}";
            }
            set => SettingsThis?.SettingsScreen.sizeResString = value;
        }

        #endregion

        #region Graphics Settings
        public double Gamma
        {
            get => Math.Round(SettingsThis?.SettingsGeneralData.gammaValue * -1 + 4.4 ?? 0, 5);
            set => SettingsThis?.SettingsGeneralData.gammaValue = Math.Round(value * -1 + 4.4, 5);
            // This should belong in programmer horror stories, *DO NOT EVER DO THIS*
            // Basically, calculate the stepper function which amounts to y = -x + 4.4, so we inverse the value and add 4.4
            // DON'T ASK HOW WE DID THIS, IT'S a 4AM THING :)
            // Round it to x.xxxxx because floating point
        }

        public bool VerticalSync
        {
            get => Convert.ToBoolean((int)(SettingsThis?.SettingsGeneralData.globalPerfData.VerticalSync ?? 0));
            set => SettingsThis?.SettingsGeneralData.globalPerfData.VerticalSync = (VerticalSyncOption)Convert.ToInt32(value);
        }

        public bool VolumetricFog
        {
            get => Convert.ToBoolean((int)(SettingsThis?.SettingsGeneralData.globalPerfData.VolumetricFog ?? 0));
            set => SettingsThis?.SettingsGeneralData.globalPerfData.VolumetricFog = (VolumetricFogOption)Convert.ToInt32(value);
        }

        public bool Reflections
        {
            get => Convert.ToBoolean((int)(SettingsThis?.SettingsGeneralData.globalPerfData.Reflections ?? 0));
            set => SettingsThis?.SettingsGeneralData.globalPerfData.Reflections = (ReflectionsOption)Convert.ToInt32(value);
        }

        public bool Bloom
        {
            get => Convert.ToBoolean((int)(SettingsThis?.SettingsGeneralData.globalPerfData.Bloom ?? 0));
            set => SettingsThis?.SettingsGeneralData.globalPerfData.Bloom = (BloomOption)Convert.ToInt32(value);
        }

        public int FPS
        {
            get
            {
                // Get the current value
                FPSOption curValue = SettingsThis?.SettingsGeneralData.globalPerfData.FPS ?? default;
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
                SettingsThis?.SettingsGeneralData.globalPerfData.FPS = valueFromIndex;
            }
        }

        public int RenderScale
        {
            get
            {
                int enumIndex = SettingsThis?.SettingsGeneralData.globalPerfData.RenderResolution ?? 0;
                int valueIndex = GlobalPerfData.RenderScaleIndex.IndexOf(enumIndex);
                double enumValue = GlobalPerfData.RenderScaleValues[valueIndex];
                return GlobalPerfData.RenderScaleValues.IndexOf(enumValue);
            }
            set
            {
                double enumValue = GlobalPerfData.RenderScaleValues[value];
                int enumIndex = DictionaryCategory.RenderResolutionOption[enumValue];
                SettingsThis?.SettingsGeneralData.globalPerfData.RenderResolution = enumIndex;
            }
        }

        public int ShadowQuality
        {
            get
            {
                int curValue = (int)(SettingsThis?.SettingsGeneralData.globalPerfData.ShadowQuality ?? default);

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

                SettingsThis?.SettingsGeneralData.globalPerfData.ShadowQuality = (ShadowQualityOption)value;
            }
        }

        public int VisualEffects
        {
            get => (int)(SettingsThis?.SettingsGeneralData.globalPerfData.VisualEffects ?? default);
            set => SettingsThis?.SettingsGeneralData.globalPerfData.VisualEffects = (VisualEffectsOption)value;
        }

        public int SFXQuality
        {
            get => (int)(SettingsThis?.SettingsGeneralData.globalPerfData.SFXQuality ?? default);
            set => SettingsThis?.SettingsGeneralData.globalPerfData.SFXQuality = (SFXQualityOption)value;
        }

        public int EnvironmentDetail
        {
            get => (int)(SettingsThis?.SettingsGeneralData.globalPerfData.EnvironmentDetail ?? default);
            set => SettingsThis?.SettingsGeneralData.globalPerfData.EnvironmentDetail = (EnvironmentDetailOption)value;
        }

        public int MotionBlur
        {
            get => (int)(SettingsThis?.SettingsGeneralData.globalPerfData.MotionBlur ?? default);
            set => SettingsThis?.SettingsGeneralData.globalPerfData.MotionBlur = (MotionBlurOption)value;
        }

        public int CrowdDensity
        {
            get => (int)(SettingsThis?.SettingsGeneralData.globalPerfData.CrowdDensity ?? default);
            set => SettingsThis?.SettingsGeneralData.globalPerfData.CrowdDensity = (CrowdDensityOption)value;
        }

        public int SubsurfaceScattering
        {
            get => (int)(SettingsThis?.SettingsGeneralData.globalPerfData.SubsurfaceScattering ?? default);
            set => SettingsThis?.SettingsGeneralData.globalPerfData.SubsurfaceScattering = (SubsurfaceScatteringOption)value;
        }

        public int CoOpTeammateEffects
        {
            get => (int)(SettingsThis?.SettingsGeneralData.globalPerfData.CoOpTeammateEffects ?? default);
            set => SettingsThis?.SettingsGeneralData.globalPerfData.CoOpTeammateEffects = (CoOpTeammateEffectsOption)value;
        }

        public int AnisotropicFiltering
        {
            get => (int)(SettingsThis?.SettingsGeneralData.globalPerfData.AnisotropicFiltering ?? default);
            set => SettingsThis?.SettingsGeneralData.globalPerfData.AnisotropicFiltering = (AnisotropicFilteringOption)value;
        }

        public int Antialiasing
        {
            get => (int)(SettingsThis?.SettingsGeneralData.globalPerfData.Antialiasing ?? default);
            set => SettingsThis?.SettingsGeneralData.globalPerfData.Antialiasing = (AntialiasingOption)value;
        }

        public bool TeamPageBackground
        {
            get => !(SettingsThis?.SettingsGeneralData.disableTeamPageBackgroundSwitch ?? false);
            set => SettingsThis?.SettingsGeneralData.disableTeamPageBackgroundSwitch = !value;
        }

        public int GlobalIllumination
        {
            get => (int)(SettingsThis?.SettingsGeneralData.globalPerfData.GlobalIllumination ?? default);
            set => SettingsThis?.SettingsGeneralData.globalPerfData.GlobalIllumination = (GlobalIlluminationOption)value;
        }

        public bool DynamicCharacterResolution
        {
            get => Convert.ToBoolean((int)(SettingsThis?.SettingsGeneralData.globalPerfData.DynamicCharacterResolution ?? default));
            set => SettingsThis?.SettingsGeneralData.globalPerfData.DynamicCharacterResolution = (DynamicCharacterResolutionOption)Convert.ToInt32(value);
        }
        #endregion

        #region Graphics Settings - HDR
        public bool IsHDR
        {
            get => GetAppConfigValue("ForceGIHDREnable").ToBool() || (SettingsThis?.SettingsWindowsHDR.isHDR ?? false) || (SettingsThis?.SettingsGeneralData.enableHDR ?? false);
            set
            {
                if (Settings is GenshinSettings genshinSettings)
                {
                    genshinSettings.SettingsWindowsHDR.isHDR = value;
                    genshinSettings.SettingsGeneralData.enableHDR = value;
                }
                SetAndSaveConfigValue("ForceGIHDREnable", value);
            } 
        }

        private double MaxLuminosity
        {
            get => (double)(SettingsThis?.SettingsGeneralData.maxLuminosity ?? 0);
            set => SettingsThis?.SettingsGeneralData.maxLuminosity = (decimal)Math.Round(value, 1);
        }

        private double UiPaperWhite
        {
            get => (double)(SettingsThis?.SettingsGeneralData.uiPaperWhite ?? 0);
            set => SettingsThis?.SettingsGeneralData.uiPaperWhite = (decimal)Math.Round(value, 1);
        }

        private double ScenePaperWhite
        {
            get => (double)(SettingsThis?.SettingsGeneralData.scenePaperWhite ?? 0);
            set => SettingsThis?.SettingsGeneralData.scenePaperWhite = (decimal)Math.Round(value, 1);
        }
        #endregion

        #region Audio
        public int Audio_Global
        {
            get => SettingsThis?.SettingsGeneralData.volumeGlobal ?? 0;
            set => SettingsThis?.SettingsGeneralData.volumeGlobal = value;
        }

        public int Audio_SFX
        {
            get => SettingsThis?.SettingsGeneralData.volumeSFX ?? 0;
            set => SettingsThis?.SettingsGeneralData.volumeSFX = value;
        }

        public int Audio_Music
        {
            get => SettingsThis?.SettingsGeneralData.volumeMusic ?? 0;
            set => SettingsThis?.SettingsGeneralData.volumeMusic = value;
        }

        public int Audio_Voice
        {
            get => SettingsThis?.SettingsGeneralData.volumeVoice ?? 0;
            set => SettingsThis?.SettingsGeneralData.volumeVoice = value;
        }

        public bool Audio_DynamicRange
        {
            get => !Convert.ToBoolean(SettingsThis?.SettingsGeneralData.audioDynamicRange ?? 0);
            set => SettingsThis?.SettingsGeneralData.audioDynamicRange = Convert.ToInt32(!value);
        }

        public bool Audio_Surround
        {
            get => Convert.ToBoolean(SettingsThis?.SettingsGeneralData.audioOutput ?? 0);
            set => SettingsThis?.SettingsGeneralData.audioOutput = Convert.ToInt32(value);
        }

        public bool Audio_MuteOnMinimized
        {
            get => SettingsThis?.SettingsGeneralData.muteAudioOnAppMinimized ?? false;
            set => SettingsThis?.SettingsGeneralData.muteAudioOnAppMinimized = value;
        }
        #endregion

        #region Language
        public int AudioLang
        {
            get => SettingsThis?.SettingsGeneralData.deviceVoiceLanguageType ?? 0;
            set => SettingsThis?.SettingsGeneralData.deviceVoiceLanguageType = value;
        }

        public int TextLang
        {
            get => SettingsThis?.SettingsGeneralData.deviceLanguageType - 1 ?? 0;
            set => SettingsThis?.SettingsGeneralData.deviceLanguageType = value + 1;
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
            //get => (Settings as GenshinSettings).SettingsCollapseMisc.LaunchMobileMode;
            get => false;
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
                SettingsThis?.SettingsCollapseMisc.UseAdvancedGameSettings = value;
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

                SettingsThis?.SettingsCollapseMisc.UseGamePreLaunchCommand = value;
            }
        }

        public string PreLaunchCommand
        {
            get => SettingsThis?.SettingsCollapseMisc.GamePreLaunchCommand ?? "";
            set => SettingsThis?.SettingsCollapseMisc.GamePreLaunchCommand = value;
        }

        public bool IsPreLaunchCommandExitOnGameClose
        {
            get => SettingsThis?.SettingsCollapseMisc.GamePreLaunchExitOnGameStop ?? false;
            set => SettingsThis?.SettingsCollapseMisc.GamePreLaunchExitOnGameStop = value;
        }

        public int LaunchDelay
        {
            get => SettingsThis?.SettingsCollapseMisc.GameLaunchDelay ?? 0;
            set => SettingsThis?.SettingsCollapseMisc.GameLaunchDelay = value;
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
                PostExitCommandTextBox.IsEnabled = value;
                SettingsThis?.SettingsCollapseMisc.UseGamePostExitCommand = value;
            }
        }

        public string PostExitCommand
        {
            get => SettingsThis?.SettingsCollapseMisc.GamePostExitCommand ?? "";
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
