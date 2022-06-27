using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Drawing;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.GameConfig;
using static Hi3Helper.Shared.Region.InstallationManagement;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public sealed partial class GameSettingsPage : Page
    {
        public GameSettingsPage()
        {
            try
            {
                this.InitializeComponent();
                ApplyButton.Translation = Shadow32;
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private async void InitializeSettings(object sender, RoutedEventArgs e)
        {
            try
            {
                GameResolutionSelector.ItemsSource = ScreenResolutionsList;

                if (GameInstallationState == GameInstallStateEnum.NotInstalled
                    || GameInstallationState == GameInstallStateEnum.NeedsUpdate
                    || GameInstallationState == GameInstallStateEnum.GameBroken)
                {
                    Overlay.Visibility = Visibility.Visible;
                    PageContent.Visibility = Visibility.Collapsed;
                    OverlayTitle.Text = Lang._GameSettingsPage.OverlayNotInstalledTitle;
                    OverlaySubtitle.Text = Lang._GameSettingsPage.OverlayNotInstalledSubtitle;
                }
                else if (App.IsGameRunning)
                {
                    Overlay.Visibility = Visibility.Visible;
                    PageContent.Visibility = Visibility.Collapsed;
                    OverlayTitle.Text = Lang._GameSettingsPage.OverlayGameRunningTitle;
                    OverlaySubtitle.Text = Lang._GameSettingsPage.OverlayGameRunningSubtitle;
                }
                else if (!IsRegKeyExist)
                {
                    Overlay.Visibility = Visibility.Visible;
                    PageContent.Visibility = Visibility.Collapsed;
                    OverlayTitle.Text = Lang._GameSettingsPage.OverlayFirstTimeTitle;
                    OverlaySubtitle.Text = Lang._GameSettingsPage.OverlayFirstTimeSubtitle;
                }
                else
                {
                    await CheckExistingGameSettings();
                    await LoadGameSettingsUI();
                    await LoadAudioSettingsUI();
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL ERROR!!!\r\n{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private async Task LoadGameSettingsUI() =>
        await Task.Run(() =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                GameResolutionFullscreen.IsChecked = gameIni.Settings[SectionName]["Fullscreen"].ToBool();

                if (gameIni.Settings[SectionName]["CustomScreenResolution"].ToBool()
                || !ScreenResolutionsList.Contains(gameIni.Settings[SectionName]["ScreenResolution"].ToString()))
                {
                    GameResolutionFullscreenExclusive.IsEnabled = false;
                    GameResolutionSelector.IsEnabled = false;
                    GameCustomResolutionCheckbox.IsChecked = true;

                    Size size = gameIni.Settings[SectionName]["ScreenResolution"].ToSize();
                    GameCustomResolutionWidth.IsEnabled = true;
                    GameCustomResolutionHeight.IsEnabled = true;
                    GameCustomResolutionWidth.Value = size.Width;
                    GameCustomResolutionHeight.Value = size.Height;
                }
                else
                {
                    GameResolutionSelector.SelectedItem = gameIni.Settings[SectionName]["ScreenResolution"].ToString();

                    Size size = gameIni.Settings[SectionName]["ScreenResolution"].ToSize();
                    GameCustomResolutionWidth.Value = size.Width;
                    GameCustomResolutionHeight.Value = size.Height;
                }

                if (gameIni.Settings[SectionName]["FullscreenExclusive"].ToBool())
                {
                    GameResolutionFullscreenExclusive.IsChecked = true;
                    GameCustomResolutionWidth.IsEnabled = false;
                    GameCustomResolutionHeight.IsEnabled = false;
                }
                else
                {
                    GameResolutionFullscreenExclusive.IsChecked = false;
                }

                GameMaxFPSInCombatValue.Value = gameIni.Settings[SectionName]["TargetFrameRateForInLevel"].ToInt();
                GameMaxFPSInMainMenuValue.Value = gameIni.Settings[SectionName]["TargetFrameRateForOthers"].ToInt();

                RenderingAccuracySelector.SelectedIndex = gameIni.Settings[SectionName]["ResolutionQuality"].ToInt();
                ShadowQualitySelector.SelectedIndex = gameIni.Settings[SectionName]["ShadowLevel"].ToInt();
                ReflectionQualitySelector.SelectedIndex = gameIni.Settings[SectionName]["ReflectionQuality"].ToInt();

                GameFXPostProcCheckBox.IsChecked = gameIni.Settings[SectionName]["UsePostFX"].ToBool();
                GameFXPhysicsCheckBox.IsChecked = gameIni.Settings[SectionName]["UseDynamicBone"].ToBool();

                GlobalIlluminationSelector.SelectedIndex = gameIni.Settings[SectionName]["GlobalIllumination"].ToBool() ? 1 : 0;
                AmbientOcclusionSelector.SelectedIndex = gameIni.Settings[SectionName]["AmbientOcclusion"].ToInt();
                LevelOfDetailSelector.SelectedIndex = gameIni.Settings[SectionName]["LodLevel"].ToInt();
                GameVolumetricLightCheckBox.IsChecked = gameIni.Settings[SectionName]["VolumetricLight"].ToBool();

                if (GameFXPostProcCheckBox.IsChecked ?? true)
                {
                    GameFXHDRCheckBox.IsChecked = gameIni.Settings[SectionName]["UseHDR"].ToBool();
                    GameFXHighQualityCheckBox.IsChecked = gameIni.Settings[SectionName]["HighQualityPostFX"].ToBool();
                    GameFXFXAACheckBox.IsChecked = gameIni.Settings[SectionName]["UseFXAA"].ToBool();
                    GameFXDistortionCheckBox.IsChecked = gameIni.Settings[SectionName]["UseDistortion"].ToBool();
                    GameFXPostProcExpander.IsExpanded = true;
                }
                else
                {
                    GameFXHDRCheckBox.IsEnabled = false;
                    GameFXHighQualityCheckBox.IsEnabled = false;
                    GameFXFXAACheckBox.IsEnabled = false;
                    GameFXDistortionCheckBox.IsEnabled = false;
                }

                GraphicsAPISelector.SelectedIndex = gameIni.Settings[SectionName]["GameGraphicsAPI"].ToInt();
            });
        });
        private async Task LoadAudioSettingsUI() =>
        await Task.Run(() =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                AudioBGMVolumeSlider.Value = gameIni.Settings[SectionName]["BGMVolume"].ToInt();
                AudioSFXVolumeSlider.Value = gameIni.Settings[SectionName]["SoundEffectVolume"].ToInt();
                AudioCVVolumeSlider.Value = gameIni.Settings[SectionName]["VoiceVolume"].ToInt();
                AudioElfCVVolumeSlider.Value = gameIni.Settings[SectionName]["ElfVolume"].ToInt();
                AudioCutscenesVolumeSlider.Value = gameIni.Settings[SectionName]["CGVolume"].ToInt();
                AudioCVLanguageSelector.SelectedIndex = gameIni.Settings[SectionName]["CVLanguage"].ToInt();
            });
        });

        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyText.Visibility = Visibility.Visible;

                await SetGraphicsSettingsIni();
                await SetAudioSettingsIni();
                SaveGameSettings();
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private async Task SetGraphicsSettingsIni() =>
        await Task.Run(() =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                gameIni.Settings[SectionName]["Fullscreen"] = GameResolutionFullscreen.IsChecked ?? false;
                if (GameCustomResolutionCheckbox.IsChecked ?? false)
                    gameIni.Settings[SectionName]["ScreenResolution"] = $"{GameCustomResolutionWidth.Value}x{GameCustomResolutionHeight.Value}";
                else
                    gameIni.Settings[SectionName]["ScreenResolution"] = (string)GameResolutionSelector.SelectedItem;

                gameIni.Settings[SectionName]["TargetFrameRateForInLevel"] = GameMaxFPSInCombatValue.Value;
                gameIni.Settings[SectionName]["TargetFrameRateForOthers"] = GameMaxFPSInMainMenuValue.Value;
                gameIni.Settings[SectionName]["ResolutionQuality"] = RenderingAccuracySelector.SelectedIndex;
                gameIni.Settings[SectionName]["ShadowLevel"] = ShadowQualitySelector.SelectedIndex;
                gameIni.Settings[SectionName]["ReflectionQuality"] = ReflectionQualitySelector.SelectedIndex;

                gameIni.Settings[SectionName]["UsePostFX"] = GameFXPostProcCheckBox.IsChecked ?? false;
                gameIni.Settings[SectionName]["UseDynamicBone"] = GameFXPhysicsCheckBox.IsChecked ?? false;
                gameIni.Settings[SectionName]["UseHDR"] = GameFXHDRCheckBox.IsChecked ?? false;
                gameIni.Settings[SectionName]["HighQualityPostFX"] = GameFXHighQualityCheckBox.IsChecked ?? false;
                gameIni.Settings[SectionName]["UseFXAA"] = GameFXFXAACheckBox.IsChecked ?? false;
                gameIni.Settings[SectionName]["UseDistortion"] = GameFXDistortionCheckBox.IsChecked ?? false;
                gameIni.Settings[SectionName]["GlobalIllumination"] = GlobalIlluminationSelector.SelectedIndex == 1;
                gameIni.Settings[SectionName]["AmbientOcclusion"] = AmbientOcclusionSelector.SelectedIndex;
                gameIni.Settings[SectionName]["LodGrade"] = LevelOfDetailSelector.SelectedIndex;
                gameIni.Settings[SectionName]["VolumetricLight"] = GameVolumetricLightCheckBox.IsChecked ?? false;

                gameIni.Settings[SectionName]["FullscreenExclusive"] = GameResolutionFullscreenExclusive.IsChecked ?? false;
                gameIni.Settings[SectionName]["CustomScreenResolution"] = GameCustomResolutionCheckbox.IsChecked ?? false;
                gameIni.Settings[SectionName]["GameGraphicsAPI"] = GraphicsAPISelector.SelectedIndex;
            });
        });

        private async Task SetAudioSettingsIni() =>
        await Task.Run(() =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                gameIni.Settings[SectionName]["BGMVolume"] = AudioBGMVolumeSlider.Value;
                gameIni.Settings[SectionName]["SoundEffectVolume"] = AudioSFXVolumeSlider.Value;
                gameIni.Settings[SectionName]["VoiceVolume"] = AudioCVVolumeSlider.Value;
                gameIni.Settings[SectionName]["ElfVolume"] = AudioElfCVVolumeSlider.Value;
                gameIni.Settings[SectionName]["CGVolume"] = AudioCutscenesVolumeSlider.Value;
                gameIni.Settings[SectionName]["CVLanguage"] = AudioCVLanguageSelector.SelectedIndex;
            });
        });

        private void GameResolutionFullscreen_IsEnabledChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                if (GameResolutionFullscreen.IsChecked ?? false)
                {
                    if (GameCustomResolutionCheckbox.IsChecked ?? false)
                    {
                        GameResolutionFullscreenExclusive.IsEnabled = false;
                        GameResolutionFullscreenExclusive.IsChecked = false;
                    }
                    else
                    {
                        GameResolutionFullscreenExclusive.IsEnabled = true;
                    }
                }
                else
                {
                    GameResolutionFullscreenExclusive.IsEnabled = false;
                    GameResolutionFullscreenExclusive.IsChecked = false;
                    GameCustomResolutionCheckbox.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void GameCustomResolutionCheckbox_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (GameCustomResolutionCheckbox.IsChecked ?? false)
                {
                    if (GameResolutionFullscreen.IsChecked ?? false)
                    {
                        GameResolutionFullscreenExclusive.IsEnabled = false;
                        GameResolutionFullscreenExclusive.IsChecked = false;
                    }
                    GameResolutionSelector.IsEnabled = false;
                    GameCustomResolutionWidth.IsEnabled = true;
                    GameCustomResolutionHeight.IsEnabled = true;

                    if (GameCustomResolutionWidth.Value == 0 || GameCustomResolutionHeight.Value == 0)
                    {
                        Size size = new Size();
                        if (GameResolutionSelector.SelectedValue == null)
                        {
                            size = Hi3Helper.Screen.ScreenProp.GetScreenSize();
                        }
                        else
                        {
                            string[] _size = GameResolutionSelector.SelectedValue.ToString().Split('x');
                            size.Width = int.Parse(_size[0]);
                            size.Height = int.Parse(_size[1]);
                        }

                        GameCustomResolutionWidth.Value = size.Width;
                        GameCustomResolutionHeight.Value = size.Height;
                    }
                }
                else
                {
                    if (GameResolutionFullscreen.IsChecked ?? false)
                    {
                        GameResolutionFullscreenExclusive.IsEnabled = true;
                    }
                    GameResolutionSelector.IsEnabled = true;
                    GameCustomResolutionWidth.IsEnabled = false;
                    GameCustomResolutionHeight.IsEnabled = false;

                    if (GameResolutionSelector.SelectedValue == null)
                    {
                        Size size = Hi3Helper.Screen.ScreenProp.GetScreenSize();
                        GameResolutionSelector.SelectedValue = $"{size.Width}x{size.Height}";
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void GameResolutionFullscreenExclusive_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (GameResolutionFullscreenExclusive.IsChecked ?? false)
                {
                    GameCustomResolutionCheckbox.IsEnabled = false;
                    GameCustomResolutionCheckbox.IsChecked = false;
                    GameCustomResolutionWidth.IsEnabled = false;
                    GameCustomResolutionHeight.IsEnabled = false;
                    GameResolutionSelector.IsEnabled = true;
                }
                else
                {
                    GameCustomResolutionCheckbox.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void GameFXPostProcCheckBox_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!GameFXPostProcCheckBox.IsChecked ?? false)
                {
                    GameFXHDRCheckBox.IsChecked = false;
                    GameFXFXAACheckBox.IsChecked = false;
                    GameFXHighQualityCheckBox.IsChecked = false;
                    GameFXDistortionCheckBox.IsChecked = false;

                    GameFXHDRCheckBox.IsEnabled = false;
                    GameFXFXAACheckBox.IsEnabled = false;
                    GameFXHighQualityCheckBox.IsEnabled = false;
                    GameFXDistortionCheckBox.IsEnabled = false;

                    GameFXPostProcExpander.IsExpanded = false;
                }
                else
                {
                    GameFXPostProcExpander.IsExpanded = true;

                    GameFXHDRCheckBox.IsEnabled = true;
                    GameFXFXAACheckBox.IsEnabled = true;
                    GameFXHighQualityCheckBox.IsEnabled = true;
                    GameFXDistortionCheckBox.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        int? prevGraphSelect;
        private async void RenderingAccuracySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (prevGraphSelect != null)
                {
                    if (RenderingAccuracySelector.SelectedIndex == 3)
                    {
                        var result = await Dialogs.SimpleDialogs.Dialog_GraphicsVeryHighWarning(Content);

                        switch (result)
                        {
                            case ContentDialogResult.Secondary:
                                RenderingAccuracySelector.SelectedIndex = prevGraphSelect ?? 0;
                                break;
                        }
                    }
                }

                prevGraphSelect = RenderingAccuracySelector.SelectedIndex;
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        public string CustomArgsValue
        {
            get => GetGameConfigValue("CustomArgs").ToString();
            set => SetGameConfigValue("CustomArgs", value);
        }
    }
}
