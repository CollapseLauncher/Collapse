using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Composition;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Drawing;

using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;

using static Hi3Helper.Shared.Region.LauncherConfig;
using static Hi3Helper.Shared.Region.InstallationManagement;

using static Hi3Helper.Shared.Region.GameSettingsManagement;
using static Hi3Helper.Shared.Region.GameSettingsManagement;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CollapseLauncher.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GameSettingsPage : Page
    {
        public GameSettingsPage()
        {
            this.InitializeComponent();
        }

        private async void InitializeSettings(object sender, RoutedEventArgs e)
        {
            GameResolutionSelector.ItemsSource = ScreenResolutionsList;
            if (GameInstallationState == GameInstallStateEnum.Installed
                || GameInstallationState == GameInstallStateEnum.InstalledHavePreload)
            {
                await CheckExistingGameSettings();
                await LoadGameSettingsUI();
            }
            else
            {
                Overlay.Visibility = Visibility.Visible;
                OverlayTitle.Text = $"You can't use this feature since the region isn't yet installed or need to be updated!";
                OverlaySubtitle.Text = $"Please download/update the game first in Homepage Menu!";
            }
        }

        private async Task LoadGameSettingsUI() => 
        await Task.Run(() =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                GameResolutionFullscreen.IsChecked = gameIni.Settings[SectionName]["Fullscreen"].ToBool();

                if (gameIni.Settings[SectionName]["CustomScreenResolution"].ToBool() || !ScreenResolutionsList.Contains(gameIni.Settings[SectionName]["ScreenResolution"].ToString()))
                //if (gameIni.Settings[SectionName]["CustomScreenResolution"].ToBool())
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

                GameMaxFPSInCombatValue.Value = gameIni.Settings[SectionName]["MaximumCombatFPS"].ToInt();
                GameMaxFPSInMainMenuValue.Value = gameIni.Settings[SectionName]["MaximumMenuFPS"].ToInt();

                GameGraphicsSlider.Value = gameIni.Settings[SectionName]["VisualQuality"].ToInt();
                GameShadowSlider.Value = gameIni.Settings[SectionName]["ShadowQuality"].ToInt();

                GameFXPostProcCheckBox.IsChecked = gameIni.Settings[SectionName]["PostProcessing"].ToBool();
                GameFXReflectionCheckBox.IsChecked = gameIni.Settings[SectionName]["Reflection"].ToBool();
                GameFXPhysicsCheckBox.IsChecked = gameIni.Settings[SectionName]["Physics"].ToBool();

                if (GameFXPostProcCheckBox.IsChecked ?? true)
                {
                    GameFXHighQualityCheckBox.IsChecked = gameIni.Settings[SectionName]["HighQualityBloom"].ToBool();
                    GameFXHDRCheckBox.IsChecked = gameIni.Settings[SectionName]["DynamicRange"].ToBool();
                    GameFXFXAACheckBox.IsChecked = gameIni.Settings[SectionName]["FXAA"].ToBool();
                    GameFXDistortionCheckBox.IsChecked = gameIni.Settings[SectionName]["Distortion"].ToBool();
                    GameFXPostProcExpander.IsExpanded = true;
                }
                else
                {
                    GameFXHighQualityCheckBox.IsEnabled = false;
                    GameFXHDRCheckBox.IsEnabled = false;
                    GameFXFXAACheckBox.IsEnabled = false;
                    GameFXDistortionCheckBox.IsEnabled = false;
                }

                GraphicsAPISelector.SelectedIndex = gameIni.Settings[SectionName]["GameGraphicsAPI"].ToInt();
            });
        });

        private void ToggleApplyButton(bool show)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ApplyButton.IsEnabled = show;
                ApplyText.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            });
        }

        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyText.Visibility = Visibility.Visible;
            await SetGraphicsSettingsIni();
            await SaveGameSettings();
        }

        private async Task SetGraphicsSettingsIni()
        {
            await Task.Run(() =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    gameIni.Settings[SectionName]["Fullscreen"] = GameResolutionFullscreen.IsChecked ?? false;
                    if (GameCustomResolutionCheckbox.IsChecked ?? false)
                        gameIni.Settings[SectionName]["ScreenResolution"] = $"{GameCustomResolutionWidth.Value}x{GameCustomResolutionHeight.Value}";
                    else
                        gameIni.Settings[SectionName]["ScreenResolution"] = (string)GameResolutionSelector.SelectedItem;

                    gameIni.Settings[SectionName]["MaximumCombatFPS"] = GameMaxFPSInCombatValue.Value;
                    gameIni.Settings[SectionName]["MaximumMenuFPS"] = GameMaxFPSInMainMenuValue.Value;
                    gameIni.Settings[SectionName]["VisualQuality"] = GameGraphicsSlider.Value;
                    gameIni.Settings[SectionName]["ShadowQuality"] = GameShadowSlider.Value;
                    gameIni.Settings[SectionName]["PostProcessing"] = GameFXPostProcCheckBox.IsChecked ?? false;
                    gameIni.Settings[SectionName]["Reflection"] = GameFXReflectionCheckBox.IsChecked ?? false;
                    gameIni.Settings[SectionName]["Physics"] = GameFXPhysicsCheckBox.IsChecked ?? false;
                    gameIni.Settings[SectionName]["HighQualityBloom"] = GameFXHighQualityCheckBox.IsChecked ?? false;
                    gameIni.Settings[SectionName]["DynamicRange"] = GameFXHDRCheckBox.IsChecked ?? false;
                    gameIni.Settings[SectionName]["FXAA"] = GameFXFXAACheckBox.IsChecked ?? false;
                    gameIni.Settings[SectionName]["Distortion"] = GameFXDistortionCheckBox.IsChecked ?? false;

                    gameIni.Settings[SectionName]["FullscreenExclusive"] = GameResolutionFullscreenExclusive.IsChecked ?? false;
                    gameIni.Settings[SectionName]["CustomScreenResolution"] = GameCustomResolutionCheckbox.IsChecked ?? false;
                    gameIni.Settings[SectionName]["GameGraphicsAPI"] = GraphicsAPISelector.SelectedIndex;
                });
            });
        }

        private void GameResolutionFullscreen_IsEnabledChanged(object sender, RoutedEventArgs e)
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

        private void GameCustomResolutionCheckbox_Click(object sender, RoutedEventArgs e)
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

        private void GameResolutionFullscreenExclusive_Click(object sender, RoutedEventArgs e)
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

        private void GameFXPostProcCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (!GameFXPostProcCheckBox.IsChecked ?? false)
            {
                GameFXHighQualityCheckBox.IsChecked = false;
                GameFXHDRCheckBox.IsChecked = false;
                GameFXFXAACheckBox.IsChecked = false;
                GameFXDistortionCheckBox.IsChecked = false;

                GameFXHighQualityCheckBox.IsEnabled = false;
                GameFXHDRCheckBox.IsEnabled = false;
                GameFXFXAACheckBox.IsEnabled = false;
                GameFXDistortionCheckBox.IsEnabled = false;

                GameFXPostProcExpander.IsExpanded = false;
            }
            else
            {
                GameFXPostProcExpander.IsExpanded = true;

                GameFXHighQualityCheckBox.IsEnabled = true;
                GameFXHDRCheckBox.IsEnabled = true;
                GameFXFXAACheckBox.IsEnabled = true;
                GameFXDistortionCheckBox.IsEnabled = true;
            }
        }
    }
}
