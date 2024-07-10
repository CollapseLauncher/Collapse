using Hi3Helper.Screen;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace CollapseLauncher.Pages
{
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    public partial class ZenlessGameSettingsPage : INotifyPropertyChanged
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
        private List<bool> ScreenResolutionIsFullscreenIdx = new List<bool>();

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

        /// <summary>
        /// Zenless does not support exclusive fullscreen
        /// </summary>
        public readonly bool IsCanExclusiveFullscreen = false;

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

        public int ResolutionIndexSelected
        {
            get
            {
                int res = Settings.GeneralData?.ResolutionIndex ?? -1;
                bool isFullscreen = res >= 0 && (res + 1) < ScreenResolutionIsFullscreenIdx.Count ? ScreenResolutionIsFullscreenIdx[res] : false;
                IsFullscreenEnabled = isFullscreen;

                return res;
            }
            set
            {
                if (value < 0) return;
                Settings.GeneralData.ResolutionIndex = value;
                bool isFullscreen = value >= 0 && (value + 1) < ScreenResolutionIsFullscreenIdx.Count ? ScreenResolutionIsFullscreenIdx[value] : false;
                IsFullscreenEnabled = isFullscreen;
            }
        }
        #endregion

        #region Misc
        public bool IsGameBoost
        {
            get => Settings?.SettingsCollapseMisc?.UseGameBoost ?? false;
            set => Settings.SettingsCollapseMisc.UseGameBoost = value;
        }
        
        public bool IsMobileMode
        {
            get => Settings?.SettingsCollapseMisc?.LaunchMobileMode ?? false;
            set => Settings.SettingsCollapseMisc.LaunchMobileMode = value;
        }
        #endregion

        #region Advanced Settings
        public bool IsUseAdvancedSettings
        {
            get
            {
                bool value                                  = Settings?.SettingsCollapseMisc?.UseAdvancedGameSettings ?? false;
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
                bool value = Settings?.SettingsCollapseMisc?.UseGamePreLaunchCommand ?? false;

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

                Settings.SettingsCollapseMisc.UseGamePreLaunchCommand = value;
            }
        }

        public string PreLaunchCommand
        {
            get => Settings?.SettingsCollapseMisc?.GamePreLaunchCommand;
            set => Settings.SettingsCollapseMisc.GamePreLaunchCommand = value;
        }

        public bool IsPreLaunchCommandExitOnGameClose
        {
            get => Settings?.SettingsCollapseMisc?.GamePreLaunchExitOnGameStop ?? false;
            set => Settings.SettingsCollapseMisc.GamePreLaunchExitOnGameStop = value;
        }

        public int LaunchDelay
        {
            get => Settings?.SettingsCollapseMisc?.GameLaunchDelay ?? 0;
            set => Settings.SettingsCollapseMisc.GameLaunchDelay = value;
        }
        
        public bool IsUsePostExitCommand
        {
            get 
            {
                bool value = Settings?.SettingsCollapseMisc?.UseGamePostExitCommand ?? false;

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
            get => Settings?.SettingsCollapseMisc?.GamePostExitCommand;
            set => Settings.SettingsCollapseMisc.GamePostExitCommand = value;
        }
        
        private void GameLaunchDelay_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            // clamp for negative value when clearing the number box
            if ((int)sender.Value < 0)
                sender.Value = 0;
        }
        #endregion

        #region Graphics Settings - GENERAL_DATA > SystemSettingDataMap
        public bool EnableVSync
        {
            get => Settings.GeneralData?.VSync ?? false;
            set => Settings.GeneralData.VSync = value;
        }
        #endregion
    }
}
