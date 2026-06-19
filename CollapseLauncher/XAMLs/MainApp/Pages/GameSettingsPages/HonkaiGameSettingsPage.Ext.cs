using CollapseLauncher.Dialogs;
using CollapseLauncher.GameSettings;
using CollapseLauncher.GameSettings.Honkai;
using CollapseLauncher.GameSettings.Honkai.Enums;
using Hi3Helper.SentryHelper;
using Hi3Helper.Win32.Screen;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Drawing;
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Pages
{
    public partial class HonkaiGameSettingsPage
    {
        #region Fields
        private int _prevGraphSelect;
        
        private HonkaiSettings? SettingsThis { get => field ??= Settings as HonkaiSettings; }
        #endregion

        #region Presets
        public ICollection<string> PresetRenderingNames => SettingsThis?.PresetSettingsGraphics.PresetKeys ?? Array.Empty<string>();

        public int PresetRenderingIndex
        {
            get
            {
                if (SettingsThis == null)
                    return -1;

                string name = SettingsThis.PresetSettingsGraphics?.GetPresetKey(SettingsThis) ?? string.Empty;
                int index = SettingsThis.PresetSettingsGraphics?.PresetKeys?.IndexOf(name) ?? -1;
                PersonalGraphicsSettingV2? presetValue = SettingsThis.PresetSettingsGraphics?.GetPresetFromKey(name);

                if (presetValue != null)
                {
                    SettingsThis.SettingsGraphics = presetValue;
                }

                ToggleRenderingSettings(name == PresetConst.DefaultPresetName);
                return index;
            }
            set
            {
                if (value < 0) return;

                string name = SettingsThis?.PresetSettingsGraphics.PresetKeys?[value] ?? string.Empty;
                PersonalGraphicsSettingV2? presetValue = SettingsThis?.PresetSettingsGraphics.GetPresetFromKey(name);

                if (presetValue != null)
                {
                    SettingsThis?.SettingsGraphics = presetValue;
                }
                SettingsThis?.PresetSettingsGraphics.SetPresetKey(presetValue);

                ToggleRenderingSettings(name == PresetConst.DefaultPresetName);
                UpdatePresetRenderingSettings();
            }
        }

        private void ToggleRenderingSettings(bool isEnable = true)
        {
            RenderingAccuracySelector.IsEnabled   = isEnable;
            ShadowQualitySelector.IsEnabled       = isEnable;
            ReflectionQualitySelector.IsEnabled   = isEnable;
            GameFXPostProcExpander.IsEnabled      = isEnable;
            GlobalIlluminationSelector.IsEnabled  = isEnable;
            AmbientOcclusionSelector.IsEnabled    = isEnable;
            LevelOfDetailSelector.IsEnabled       = isEnable;
            GameVolumetricLightSelector.IsEnabled = isEnable;
            GameMaxFPSInCombatValue.IsEnabled     = isEnable;
            GameMaxFPSInMainMenuValue.IsEnabled   = isEnable;
        }

        private void UpdatePresetRenderingSettings()
        {
            UpdatePresetFPS();
            UpdatePresetRendering();
        }
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
                    GameWindowResizable.IsEnabled               = false;
                    GameWindowResizable.IsChecked               = false;
                    GameResolutionFullscreenExclusive.IsEnabled = !IsCustomResolutionEnabled;
                    GameResolutionBorderless.IsChecked          = false;
                    return;
                }
                GameWindowResizable.IsEnabled               = true;
                GameResolutionFullscreenExclusive.IsEnabled = false;
                GameResolutionFullscreenExclusive.IsChecked = false;
                GameResolutionBorderless.IsEnabled          = true;
            }
        }

        public bool IsBorderlessEnabled
        {
            get => SettingsThis?.SettingsCollapseScreen.UseBorderlessScreen ?? false;
            set
            {
                SettingsThis?.SettingsCollapseScreen.UseBorderlessScreen = value;
                if (value)
                {
                    GameWindowResizable.IsEnabled      = false;
                    GameWindowResizable.IsChecked      = false;
                    GameResolutionFullscreen.IsEnabled = false;
                    GameResolutionFullscreen.IsChecked = false;
                }
                else
                {
                    GameWindowResizable.IsEnabled      = true;
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

        public bool IsCanExclusiveFullscreen => !(!IsFullscreenEnabled || IsCustomResolutionEnabled);

        public bool IsExclusiveFullscreenEnabled
        {
            get => IsFullscreenEnabled && (SettingsThis?.SettingsCollapseScreen.UseExclusiveFullscreen ?? false);
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

        public bool IsCanResizableWindow => !(SettingsThis?.SettingsScreen.isfullScreen ?? false) && !IsExclusiveFullscreenEnabled;

        public bool IsResizableWindow
        {
            get => SettingsThis?.SettingsCollapseScreen.UseResizableWindow ?? false;
            set => SettingsThis?.SettingsCollapseScreen.UseResizableWindow = value;
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

        public bool IsCanResolutionWH => IsCustomResolutionEnabled;

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

        #region FPS
        private void UpdatePresetFPS()
        {
            OnPropertyChanged(nameof(FPSInCombat));
            OnPropertyChanged(nameof(FPSInMainMenu));
        }
        public short FPSInCombat
        {
            get => SettingsThis?.SettingsGraphics.TargetFrameRateForInLevel ?? 0;
            set => SettingsThis?.SettingsGraphics.TargetFrameRateForInLevel = value;
        }

        public short FPSInMainMenu
        {
            get => SettingsThis?.SettingsGraphics.TargetFrameRateForOthers ?? 0;
            set => SettingsThis?.SettingsGraphics.TargetFrameRateForOthers = value;
        }
        #endregion

        #region Rendering
        private void UpdatePresetRendering()
        {
            OnPropertyChanged(nameof(GraphicsRenderingAccuracy));
            OnPropertyChanged(nameof(GraphicsShadowQuality));
            OnPropertyChanged(nameof(GraphicsReflectionQuality));
            OnPropertyChanged(nameof(IsGraphicsPostFXEnabled));
            OnPropertyChanged(nameof(IsGraphicsPhysicsEnabled));
            OnPropertyChanged(nameof(IsGraphicsFXHDREnabled));
            OnPropertyChanged(nameof(IsGraphicsFXHighQualityEnabled));
            OnPropertyChanged(nameof(IsGraphicsFXFXAAEnabled));
            OnPropertyChanged(nameof(IsGraphicsFXDistortionEnabled));
            OnPropertyChanged(nameof(GraphicsGlobalIllumination));
            OnPropertyChanged(nameof(GraphicsAmbientOcclusion));
            OnPropertyChanged(nameof(GraphicsLevelOfDetail));
            OnPropertyChanged(nameof(GraphicsVolumetricLight));
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.ResolutionQuality"/><br/>
        /// </summary>
        public int GraphicsRenderingAccuracy
        {
            get => _prevGraphSelect = (int)(SettingsThis?.SettingsGraphics.ResolutionQuality ?? default);
            set => TryChallengeRenderingAccuracySet(value, value < 9);
        }

        private async void TryChallengeRenderingAccuracySet(int value, bool BypassChallenge = false)
        {
            try
            {
                if (!BypassChallenge)
                {
                    _prevGraphSelect = (int)(SettingsThis?.SettingsGraphics.ResolutionQuality ?? default);
                    ContentDialogResult result = await SimpleDialogs.Dialog_GraphicsVeryHighWarning();

                    RenderingAccuracySelector.SelectedIndex = result switch
                                                              {
                                                                  ContentDialogResult.Secondary => _prevGraphSelect,
                                                                  _ => RenderingAccuracySelector.SelectedIndex
                                                              };
                }

                SettingsThis?.SettingsGraphics.ResolutionQuality = (SelectResolutionQuality)value;
            }
            catch (Exception e)
            {
                // ignored
                await SentryHelper.ExceptionHandlerAsync(e);
            }
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.ShadowLevel"/><br/>
        /// </summary>
        public int GraphicsShadowQuality
        {
            get => (int)(SettingsThis?.SettingsGraphics.ShadowLevel ?? default);
            set => SettingsThis?.SettingsGraphics.ShadowLevel = (SelectShadowLevel)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.ReflectionQuality"/><br/>
        /// </summary>
        public int GraphicsReflectionQuality
        {
            get => (int)(SettingsThis?.SettingsGraphics.ReflectionQuality ?? default);
            set => SettingsThis?.SettingsGraphics.ReflectionQuality = (SelectReflectionQuality)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.LightingQuality"/><br/>
        /// </summary>
        public int GraphicsLightingQuality
        {
            get => (int)(SettingsThis?.SettingsGraphics.LightingQuality ?? default);
            set => SettingsThis?.SettingsGraphics.LightingQuality = (SelectLightningQuality)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.PostFXQuality"/><br/>
        /// </summary>
        public int GraphicsPostFXQuality
        {
            get => (int)(SettingsThis?.SettingsGraphics.PostFXQuality ?? default);
            set => SettingsThis?.SettingsGraphics.PostFXQuality = (SelectPostFXQuality)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.AAType"/><br/>
        /// </summary>
        public int GraphicsAAType
        {
            get => (int)(SettingsThis?.SettingsGraphics.AAType ?? default);
            set => SettingsThis?.SettingsGraphics.AAType = (SelectAAType)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.CharacterQuality"/><br/>
        /// </summary>
        public int GraphicsCharacterQuality
        {
            get => (int)(SettingsThis?.SettingsGraphics.CharacterQuality ?? default);
            set => SettingsThis?.SettingsGraphics.CharacterQuality = (SelectCharacterQuality)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.WeatherQuality"/><br/>
        /// </summary>
        public int GraphicsWeatherQuality
        {
            get => (int)(SettingsThis?.SettingsGraphics.WeatherQuality ?? default);
            set => SettingsThis?.SettingsGraphics.WeatherQuality = (SelectWeatherQuality)value;
        }
        
        /// <summary>
        /// Legacy
        /// </summary>
        public bool IsGraphicsPostFXEnabled
        {
            get => SettingsThis?.SettingsGraphics.UsePostFX ?? false;
            set
            {
                SettingsThis?.SettingsGraphics.UsePostFX = value;
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

        /// <summary>
        /// <inheritdoc cref="PhysicsSimulation.UsePhysicsSimulation"/><br/>
        /// </summary>
        public bool IsGraphicsPhysicsEnabled
        {
            get => SettingsThis?.SettingsPhysics.PhysicsSimulationBool ?? false;
            set => SettingsThis?.SettingsPhysics.PhysicsSimulationBool = value;
        }

        /// <summary>
        /// Legacy
        /// </summary>
        public bool IsGraphicsFXHDREnabled
        {
            get => IsGraphicsPostFXEnabled && (SettingsThis?.SettingsGraphics.UseHDR ?? false);
            set => SettingsThis?.SettingsGraphics.UseHDR = value;
        }

        /// <summary>
        /// Legacy
        /// </summary>
        public bool IsGraphicsFXHighQualityEnabled
        {
            get => IsGraphicsPostFXEnabled && (SettingsThis?.SettingsGraphics.PostFXGradeBool ?? false);
            set => SettingsThis?.SettingsGraphics.PostFXGradeBool = value;
        }

        /// <summary>
        /// Legacy
        /// </summary>
        public bool IsGraphicsFXFXAAEnabled
        {
            get => IsGraphicsPostFXEnabled && (SettingsThis?.SettingsGraphics.UseFXAA ?? false);
            set => SettingsThis?.SettingsGraphics.UseFXAA = value;
        }

        /// <summary>
        /// Legacy
        /// </summary>
        public bool IsGraphicsFXDistortionEnabled
        {
            get => IsGraphicsPostFXEnabled && (SettingsThis?.SettingsGraphics.UseDistortion ?? false);
            set => SettingsThis?.SettingsGraphics.UseDistortion = value;
        }

        public int GraphicsAPI
        {
            get => SettingsThis?.SettingsCollapseScreen.GameGraphicsAPI ?? 0;
            set => SettingsThis?.SettingsCollapseScreen.GameGraphicsAPI = value;
        }

        /// <summary>
        /// Legacy
        /// </summary>
        public int GraphicsGlobalIllumination
        {
            get => (int)(SettingsThis?.SettingsGraphics.GlobalIllumination ?? default);
            set => SettingsThis?.SettingsGraphics.GlobalIllumination = (SelectGlobalIllumination)value;
        }

        /// <summary>
        /// Legacy
        /// </summary>
        public int GraphicsAmbientOcclusion
        {
            get => (int)(SettingsThis?.SettingsGraphics.AmbientOcclusion ?? default);
            set => SettingsThis?.SettingsGraphics.AmbientOcclusion = (SelectAmbientOcclusion)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.LodGrade"/><br/>
        /// </summary>
        public int GraphicsLevelOfDetail
        {
            get => SettingsThis?.SettingsGraphics.LodGrade switch
            {
                SelectLodGrade.High => 2,
                SelectLodGrade.Medium => 1,
                _ => 0
            };
            set => SettingsThis?.SettingsGraphics.LodGrade = value switch
            {
                2 => SelectLodGrade.High,
                1 => SelectLodGrade.Medium,
                _ => SelectLodGrade.Low
            };
        }

        /// <summary>
        /// Legacy
        /// </summary>
        public int GraphicsVolumetricLight
        {
            get => (int)(SettingsThis?.SettingsGraphics.VolumetricLight ?? default);
            set => SettingsThis?.SettingsGraphics.VolumetricLight = (SelectVolumetricLight)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.ParticleEmitLevel"/><br/>
        /// </summary>
        public int GraphicsParticleQuality
        {
            get => (int)(SettingsThis?.SettingsGraphics.ParticleEmitLevel ?? default);
            set => SettingsThis?.SettingsGraphics.ParticleEmitLevel = (SelectParticleEmitLevel)value;
        }
        #endregion

        #region Audio
        public int AudioMasterVolume
        {
            get => SettingsThis?.SettingsAudio.MasterVolume ?? 0;
            set => SettingsThis?.SettingsAudio.MasterVolume = value;
        }

        public int AudioBGMVolume
        {
            get => SettingsThis?.SettingsAudio.BGMVolume ?? 0;
            set => SettingsThis?.SettingsAudio.BGMVolume = value;
        }

        public int AudioSFXVolume
        {
            get => SettingsThis?.SettingsAudio.SoundEffectVolume ?? 0;
            set => SettingsThis?.SettingsAudio.SoundEffectVolume = value;
        }

        public int AudioVoiceVolume
        {
            get => SettingsThis?.SettingsAudio.VoiceVolume ?? 0;
            set => SettingsThis?.SettingsAudio.VoiceVolume = value;
        }

        public int AudioElfVolume
        {
            get => SettingsThis?.SettingsAudio.ElfVolume ?? 0;
            set => SettingsThis?.SettingsAudio.ElfVolume = value;
        }

        public int AudioCutsceneVolume
        {
            get => SettingsThis?.SettingsAudio.CGVolumeV2 ?? 0;
            set => SettingsThis?.SettingsAudio.CGVolumeV2 = value;
        }

        public int AudioVoiceLanguage
        {
            get => SettingsThis?.SettingsAudio._userCVLanguageInt ?? 0;
            set => SettingsThis?.SettingsAudio._userCVLanguageInt = value;
        }

        public bool AudioMute
        {
            get => SettingsThis?.SettingsAudio.Mute ?? false;
            set => SettingsThis?.SettingsAudio.Mute = value;
        }

        #endregion

        #region Misc
        public bool IsGameBoost
        {
            get => SettingsThis?.SettingsCollapseMisc.UseGameBoost ?? false;
            set => SettingsThis?.SettingsCollapseMisc.UseGameBoost = value;
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
                    GameLaunchDelay.IsEnabled           = true;
                }
                else
                {
                    PreLaunchCommandTextBox.IsEnabled   = false;
                    PreLaunchForceCloseToggle.IsEnabled = false;
                    GameLaunchDelay.IsEnabled           = false;
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

                SettingsThis?.SettingsCollapseMisc.UseGamePreLaunchCommand = value;
            }
        }

        public string PreLaunchCommand
        {
            get => SettingsThis?.SettingsCollapseMisc.GamePreLaunchCommand ?? string.Empty;
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
