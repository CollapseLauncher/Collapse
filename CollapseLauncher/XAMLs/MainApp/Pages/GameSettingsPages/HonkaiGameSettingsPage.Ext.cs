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
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.CompilerServices;
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
// ReSharper disable UnusedMember.Global

namespace CollapseLauncher.Pages
{
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
    public partial class HonkaiGameSettingsPage : INotifyPropertyChanged
    {
        #region Fields
        private int _prevGraphSelect;
        #endregion

        #region Methods
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Raise the PropertyChanged event, passing the name of the property whose value has changed.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Presets
        public ICollection<string> PresetRenderingNames => ((HonkaiSettings)Settings).PresetSettingsGraphics.PresetKeys;

        public int PresetRenderingIndex
        {
            get
            {
                string                    name        = ((HonkaiSettings)Settings).PresetSettingsGraphics.GetPresetKey(Settings);
                int                       index       = ((HonkaiSettings)Settings).PresetSettingsGraphics.PresetKeys.IndexOf(name);
                PersonalGraphicsSettingV2 presetValue = ((HonkaiSettings)Settings).PresetSettingsGraphics.GetPresetFromKey(name);

                if (presetValue != null)
                {
                    ((HonkaiSettings)Settings).SettingsGraphics = presetValue;
                }

                ToggleRenderingSettings(name == PresetConst.DefaultPresetName);
                return index;
            }
            set
            {
                if (value < 0) return;

                string                    name = ((HonkaiSettings)Settings).PresetSettingsGraphics.PresetKeys[value];
                PersonalGraphicsSettingV2 presetValue = ((HonkaiSettings)Settings).PresetSettingsGraphics.GetPresetFromKey(name);

                if (presetValue != null)
                {
                    ((HonkaiSettings)Settings).SettingsGraphics = presetValue;
                }
                ((HonkaiSettings)Settings).PresetSettingsGraphics.SetPresetKey(presetValue);

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
            get => Settings.SettingsScreen.isfullScreen;
            set
            {
                Settings.SettingsScreen.isfullScreen = value;
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
            get => Settings.SettingsCollapseScreen.UseBorderlessScreen;
            set
            {
                Settings.SettingsCollapseScreen.UseBorderlessScreen = value;
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
            get => IsFullscreenEnabled && Settings.SettingsCollapseScreen.UseExclusiveFullscreen;
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

        public bool IsCanResizableWindow => !Settings.SettingsScreen.isfullScreen && !IsExclusiveFullscreenEnabled;

        public bool IsResizableWindow
        {
            get => Settings.SettingsCollapseScreen.UseResizableWindow;
            set => Settings.SettingsCollapseScreen.UseResizableWindow = value;
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

        public bool IsCanResolutionWH => IsCustomResolutionEnabled;

        public string ResolutionSelected
        {
            get
            {
                string res = Settings.SettingsScreen.sizeResString;
                if (!string.IsNullOrEmpty(res))
                {
                    return res;
                }

                Size size = ScreenProp.CurrentResolution;
                return $"{size.Width}x{size.Height}";
            }
            set => Settings.SettingsScreen.sizeResString = value;
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
            get => ((HonkaiSettings)Settings).SettingsGraphics.TargetFrameRateForInLevel;
            set => ((HonkaiSettings)Settings).SettingsGraphics.TargetFrameRateForInLevel = value;
        }

        public short FPSInMainMenu
        {
            get => ((HonkaiSettings)Settings).SettingsGraphics.TargetFrameRateForOthers;
            set => ((HonkaiSettings)Settings).SettingsGraphics.TargetFrameRateForOthers = value;
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
            get => _prevGraphSelect = (int)((HonkaiSettings)Settings).SettingsGraphics.ResolutionQuality;
            set => TryChallengeRenderingAccuracySet(value, value < 9);
        }

        private async void TryChallengeRenderingAccuracySet(int value, bool BypassChallenge = false)
        {
            try
            {
                if (!BypassChallenge)
                {
                    _prevGraphSelect = (int)((HonkaiSettings)Settings).SettingsGraphics.ResolutionQuality;
                    ContentDialogResult result = await SimpleDialogs.Dialog_GraphicsVeryHighWarning();

                    RenderingAccuracySelector.SelectedIndex = result switch
                                                              {
                                                                  ContentDialogResult.Secondary => _prevGraphSelect,
                                                                  _ => RenderingAccuracySelector.SelectedIndex
                                                              };
                }

                ((HonkaiSettings)Settings).SettingsGraphics.ResolutionQuality = (SelectResolutionQuality)value;
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
            get => (int)((HonkaiSettings)Settings).SettingsGraphics.ShadowLevel;
            set => ((HonkaiSettings)Settings).SettingsGraphics.ShadowLevel = (SelectShadowLevel)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.ReflectionQuality"/><br/>
        /// </summary>
        public int GraphicsReflectionQuality
        {
            get => (int)((HonkaiSettings)Settings).SettingsGraphics.ReflectionQuality;
            set => ((HonkaiSettings)Settings).SettingsGraphics.ReflectionQuality = (SelectReflectionQuality)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.LightingQuality"/><br/>
        /// </summary>
        public int GraphicsLightingQuality
        {
            get => (int)((HonkaiSettings)Settings).SettingsGraphics.LightingQuality;
            set => ((HonkaiSettings)Settings).SettingsGraphics.LightingQuality = (SelectLightningQuality)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.PostFXQuality"/><br/>
        /// </summary>
        public int GraphicsPostFXQuality
        {
            get => (int)((HonkaiSettings)Settings).SettingsGraphics.PostFXQuality;
            set => ((HonkaiSettings)Settings).SettingsGraphics.PostFXQuality = (SelectPostFXQuality)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.AAType"/><br/>
        /// </summary>
        public int GraphicsAAType
        {
            get => (int)((HonkaiSettings)Settings).SettingsGraphics.AAType;
            set => ((HonkaiSettings)Settings).SettingsGraphics.AAType = (SelectAAType)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.CharacterQuality"/><br/>
        /// </summary>
        public int GraphicsCharacterQuality
        {
            get => (int)((HonkaiSettings)Settings).SettingsGraphics.CharacterQuality;
            set => ((HonkaiSettings)Settings).SettingsGraphics.CharacterQuality = (SelectCharacterQuality)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.WeatherQuality"/><br/>
        /// </summary>
        public int GraphicsWeatherQuality
        {
            get => (int)((HonkaiSettings)Settings).SettingsGraphics.WeatherQuality;
            set => ((HonkaiSettings)Settings).SettingsGraphics.WeatherQuality = (SelectWeatherQuality)value;
        }
        
        /// <summary>
        /// Legacy
        /// </summary>
        public bool IsGraphicsPostFXEnabled
        {
            get => ((HonkaiSettings)Settings).SettingsGraphics.UsePostFX;
            set
            {
                ((HonkaiSettings)Settings).SettingsGraphics.UsePostFX = value;
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
            get => ((HonkaiSettings)Settings).SettingsPhysics.PhysicsSimulationBool;
            set => ((HonkaiSettings)Settings).SettingsPhysics.PhysicsSimulationBool = value;
        }

        /// <summary>
        /// Legacy
        /// </summary>
        public bool IsGraphicsFXHDREnabled
        {
            get => IsGraphicsPostFXEnabled && ((HonkaiSettings)Settings).SettingsGraphics.UseHDR;
            set => ((HonkaiSettings)Settings).SettingsGraphics.UseHDR = value;
        }

        /// <summary>
        /// Legacy
        /// </summary>
        public bool IsGraphicsFXHighQualityEnabled
        {
            get => IsGraphicsPostFXEnabled && ((HonkaiSettings)Settings).SettingsGraphics.PostFXGradeBool;
            set => ((HonkaiSettings)Settings).SettingsGraphics.PostFXGradeBool = value;
        }

        /// <summary>
        /// Legacy
        /// </summary>
        public bool IsGraphicsFXFXAAEnabled
        {
            get => IsGraphicsPostFXEnabled && ((HonkaiSettings)Settings).SettingsGraphics.UseFXAA;
            set => ((HonkaiSettings)Settings).SettingsGraphics.UseFXAA = value;
        }

        /// <summary>
        /// Legacy
        /// </summary>
        public bool IsGraphicsFXDistortionEnabled
        {
            get => IsGraphicsPostFXEnabled && ((HonkaiSettings)Settings).SettingsGraphics.UseDistortion;
            set => ((HonkaiSettings)Settings).SettingsGraphics.UseDistortion = value;
        }

        public int GraphicsAPI
        {
            get => Settings.SettingsCollapseScreen.GameGraphicsAPI;
            set => Settings.SettingsCollapseScreen.GameGraphicsAPI = value;
        }

        /// <summary>
        /// Legacy
        /// </summary>
        public int GraphicsGlobalIllumination
        {
            get => (int)((HonkaiSettings)Settings).SettingsGraphics.GlobalIllumination;
            set => ((HonkaiSettings)Settings).SettingsGraphics.GlobalIllumination = (SelectGlobalIllumination)value;
        }

        /// <summary>
        /// Legacy
        /// </summary>
        public int GraphicsAmbientOcclusion
        {
            get => (int)((HonkaiSettings)Settings).SettingsGraphics.AmbientOcclusion;
            set => ((HonkaiSettings)Settings).SettingsGraphics.AmbientOcclusion = (SelectAmbientOcclusion)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.LodGrade"/><br/>
        /// </summary>
        public int GraphicsLevelOfDetail
        {
            get => ((HonkaiSettings)Settings).SettingsGraphics.LodGrade switch
            {
                SelectLodGrade.High => 2,
                SelectLodGrade.Medium => 1,
                _ => 0
            };
            set => ((HonkaiSettings)Settings).SettingsGraphics.LodGrade = value switch
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
            get => (int)((HonkaiSettings)Settings).SettingsGraphics.VolumetricLight;
            set => ((HonkaiSettings)Settings).SettingsGraphics.VolumetricLight = (SelectVolumetricLight)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.ParticleEmitLevel"/><br/>
        /// </summary>
        public int GraphicsParticleQuality
        {
            get => (int)((HonkaiSettings)Settings).SettingsGraphics.ParticleEmitLevel;
            set => ((HonkaiSettings)Settings).SettingsGraphics.ParticleEmitLevel = (SelectParticleEmitLevel)value;
        }
        #endregion

        #region Audio
        public int AudioMasterVolume
        {
            get => ((HonkaiSettings)Settings).SettingsAudio.MasterVolume;
            set => ((HonkaiSettings)Settings).SettingsAudio.MasterVolume = value;
        }

        public int AudioBGMVolume
        {
            get => ((HonkaiSettings)Settings).SettingsAudio.BGMVolume;
            set => ((HonkaiSettings)Settings).SettingsAudio.BGMVolume = value;
        }

        public int AudioSFXVolume
        {
            get => ((HonkaiSettings)Settings).SettingsAudio.SoundEffectVolume;
            set => ((HonkaiSettings)Settings).SettingsAudio.SoundEffectVolume = value;
        }

        public int AudioVoiceVolume
        {
            get => ((HonkaiSettings)Settings).SettingsAudio.VoiceVolume;
            set => ((HonkaiSettings)Settings).SettingsAudio.VoiceVolume = value;
        }

        public int AudioElfVolume
        {
            get => ((HonkaiSettings)Settings).SettingsAudio.ElfVolume;
            set => ((HonkaiSettings)Settings).SettingsAudio.ElfVolume = value;
        }

        public int AudioCutsceneVolume
        {
            get => ((HonkaiSettings)Settings).SettingsAudio.CGVolumeV2;
            set => ((HonkaiSettings)Settings).SettingsAudio.CGVolumeV2 = value;
        }

        public int AudioVoiceLanguage
        {
            get => ((HonkaiSettings)Settings).SettingsAudio._userCVLanguageInt;
            set => ((HonkaiSettings)Settings).SettingsAudio._userCVLanguageInt = value;
        }

        public bool AudioMute
        {
            get => ((HonkaiSettings)Settings).SettingsAudio.Mute;
            set => ((HonkaiSettings)Settings).SettingsAudio.Mute = value;
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
                bool value = Settings.SettingsCollapseMisc.UseAdvancedGameSettings;
                AdvancedSettingsPanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                return value;
            }
            set
            {
                Settings.SettingsCollapseMisc.UseAdvancedGameSettings = value;
                AdvancedSettingsPanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
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
        
        public int LaunchDelay
        {
            get => Settings.SettingsCollapseMisc.GameLaunchDelay;
            set => Settings.SettingsCollapseMisc.GameLaunchDelay = value;
        }

        public bool IsUsePostExitCommand
        {
            get 
            {
                bool value = Settings.SettingsCollapseMisc.UseGamePostExitCommand;
                PostExitCommandTextBox.IsEnabled = value;
                return value;
            }
            set
            {
                PostExitCommandTextBox.IsEnabled = value;
                Settings.SettingsCollapseMisc.UseGamePostExitCommand = value;
            }
        }

        public string PostExitCommand
        {
            get => Settings.SettingsCollapseMisc.GamePostExitCommand;
            set => Settings.SettingsCollapseMisc.GamePostExitCommand = value;
        }

        private void GameLaunchDelay_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            // clamp for negative value when clearing the number box
            if ((int)sender.Value < 0)
                sender.Value = 0;
        }

        public bool RunWithExplorerAsParent
        {
            get => Settings.SettingsCollapseMisc.RunWithExplorerAsParent;
            set => Settings.SettingsCollapseMisc.RunWithExplorerAsParent = value;
        }
        #endregion
    }
}
