#if !DISABLEDISCORD
    using CollapseLauncher.DiscordPresence;
#endif
    using CollapseLauncher.GameSettings.Zenless;
    using CollapseLauncher.Helper.Animation;
    using CollapseLauncher.Interfaces;
    using CollapseLauncher.Statics;
    using Hi3Helper;
    using Hi3Helper.Data;
    using Hi3Helper.Shared.ClassStruct;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Media;
    using Microsoft.Win32;
    using RegistryUtils;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using Windows.UI;
    using static Hi3Helper.Locale;
    using static Hi3Helper.Logger;
    using static Hi3Helper.Shared.Region.LauncherConfig;
    using static CollapseLauncher.Statics.GamePropertyVault;
using CollapseLauncher.Helper;

namespace CollapseLauncher.Pages
{
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    public partial class ZenlessGameSettingsPage
    {
        private GamePresetProperty CurrentGameProperty   { get; set; }
        private ZenlessSettings    Settings              { get; set; }
        private Brush              InheritApplyTextColor { get; set; }
        private RegistryMonitor    RegistryWatcher       { get; set; }

        private bool IsNoReload = false;
        
        public ZenlessGameSettingsPage()
        {
            try
            {
                CurrentGameProperty = GetCurrentGameProperty();
                Settings = CurrentGameProperty._GameSettings as ZenlessSettings;

                DispatcherQueue?.TryEnqueue(() =>
                {
                    RegistryWatcher = new RegistryMonitor(RegistryHive.CurrentUser, Path.Combine($"Software\\{CurrentGameProperty._GameVersion.VendorTypeProp.VendorType}", CurrentGameProperty._GameVersion.GamePreset.InternalGameNameInConfig!));
                    ToggleRegistrySubscribe(true);
                });

                LoadPage();
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void ToggleRegistrySubscribe(bool doSubscribe)
        {
            if (doSubscribe)
                RegistryWatcher.RegChanged += RegistryListener;
            else
                RegistryWatcher.RegChanged -= RegistryListener;
        }

        private void RegistryListener(object sender, EventArgs e)
        {
            if (!IsNoReload)
            {
                LogWriteLine("[Zenless GSP Module] RegistryMonitor has detected registry change outside of the launcher! Reloading the page...", LogType.Warning, true);
                DispatcherQueue?.TryEnqueue(MainFrameChanger.ReloadCurrentMainFrame);
            }
        }

        private void LoadPage()
        {
            this.InitializeComponent();
            ApplyButton.Translation = Shadow32;
            GameSettingsApplyGrid.Translation = new Vector3(0, 0, 64);
            SettingsScrollViewer.EnableImplicitAnimation(true);
            Settings?.ReloadSettings();

            InheritApplyTextColor = ApplyText.Foreground!;
        }

        private void RegistryExportClick(object sender, RoutedEventArgs e)
        {
            try
            {
                ToggleRegistrySubscribe(false);
                string gameBasePath = ConverterTool.NormalizePath(CurrentGameProperty._GameVersion?.GameDirPath);
                string[] relativePaths = GetFilesRelativePaths(gameBasePath, $"{CurrentGameProperty?._GameExecutableNameWithoutExtension}_Data\\Persistent\\LocalStorage");
                Exception exc = Settings.ExportSettings(true, gameBasePath, relativePaths);

                if (exc != null) throw exc;

                ApplyText.Foreground = InheritApplyTextColor;
                ApplyText.Text = Lang._GameSettingsPage.SettingsRegExported;
                ApplyText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error has occured while exporting registry!\r\n{ex}", LogType.Error, true);
                ApplyText.Foreground = new SolidColorBrush(new Color { A = 255, R = 255, B = 0, G = 0 });
                ApplyText.Text = ex.Message;
                ApplyText.Visibility = Visibility.Visible;
            }
            finally
            {
                ToggleRegistrySubscribe(true);
            }
        }

        private string[] GetFilesRelativePaths(string gameDir, string relativePath)
        {
            string sourceDirPath = Path.Combine(gameDir, relativePath);
            return Directory.EnumerateFiles(sourceDirPath, "*", SearchOption.AllDirectories).Select(filePath =>
            {
                string fileName = Path.GetFileName(filePath);
                string trimmedRelativeDir = Path.GetDirectoryName(filePath.Substring(gameDir.Length)).Trim('\\');
                string relativeFilePath = Path.Combine(trimmedRelativeDir, fileName);
                return relativeFilePath;
            }).ToArray();
        }

        private void RegistryImportClick(object sender, RoutedEventArgs e)
        {
            try
            {
                ToggleRegistrySubscribe(false);
                string gameBasePath = ConverterTool.NormalizePath(CurrentGameProperty._GameVersion?.GameDirPath);
                Exception exc = Settings.ImportSettings(gameBasePath);

                if (exc != null) throw exc;

                ApplyText.Foreground = InheritApplyTextColor;
                ApplyText.Text = Lang._GameSettingsPage.SettingsRegImported;
                ApplyText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error has occured while importing registry!\r\n{ex}", LogType.Error, true);
                ApplyText.Foreground = new SolidColorBrush(new Color { A = 255, R = 255, B = 0, G = 0 });
                ApplyText.Text = ex.Message;
                ApplyText.Visibility = Visibility.Visible;
            }
            finally
            {
                ToggleRegistrySubscribe(true);
            }
        }

        private void InitializeSettings(object sender, RoutedEventArgs e)
        {
            try
            {
                BackgroundImgChanger.ToggleBackground(true);

                var resList = new List<string>();
                List<string> resFullscreen = GetResPairs_Fullscreen();
                List<string> resWindowed = GetResPairs_Windowed();
                ScreenResolutionIsFullscreenIdx.AddRange(Enumerable.Range(0, resFullscreen.Count).Select(_ => true));
                ScreenResolutionIsFullscreenIdx.AddRange(Enumerable.Range(0, resWindowed.Count).Select(_ => false));

                resList.AddRange(GetResPairs_Fullscreen());
                resList.AddRange(GetResPairs_Windowed());
                
                GameResolutionSelector.ItemsSource = resList;

                if (CurrentGameProperty.IsGameRunning)
                {
                    #if !GSPBYPASSGAMERUNNING
                    Overlay.Visibility = Visibility.Visible;
                    PageContent.Visibility = Visibility.Collapsed;
                    OverlayTitle.Text = Lang._GameSettingsPage.OverlayGameRunningTitle;
                    OverlaySubtitle.Text = Lang._GameSettingsPage.OverlayGameRunningSubtitle;
                    #endif
                }
                else if (GameInstallationState == GameInstallStateEnum.NotInstalled
                      || GameInstallationState == GameInstallStateEnum.NeedsUpdate
                      || GameInstallationState == GameInstallStateEnum.InstalledHavePlugin
                      || GameInstallationState == GameInstallStateEnum.GameBroken)
                {
                    Overlay.Visibility = Visibility.Visible;
                    PageContent.Visibility = Visibility.Collapsed;
                    OverlayTitle.Text = Lang._GameSettingsPage.OverlayNotInstalledTitle;
                    OverlaySubtitle.Text = Lang._GameSettingsPage.OverlayNotInstalledSubtitle;
                }
                else
                {
#if !DISABLEDISCORD
                    InnerLauncherConfig.AppDiscordPresence.SetActivity(ActivityType.GameSettings);
#endif
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL ERROR!!!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private static readonly List<int> acceptableHeight = [4320, 2880, 2160, 1440, 1280, 1080, 900, 720];
        
        private List<string> GetResPairs_Fullscreen()
        {
            var displayProp    = WindowUtility.CurrentScreenProp.GetScreenSize();
            var nativeAspRatio = (double)displayProp.Width / displayProp.Height;
            var acH            = acceptableHeight;
            
            acH.RemoveAll(h => h > WindowUtility.CurrentScreenProp.GetMaxHeight());
            
            List<string> resPairs = new List<string>();

            foreach (var h in acH)
            {
                int w = (int)(h * nativeAspRatio);
                resPairs.Add(string.Format(Lang._GameSettingsPage.Graphics_ResPrefixFullscreen, w, h));
            }

            return resPairs;
        }

        private List<string> GetResPairs_Windowed()
        {
            var displayProp    = WindowUtility.CurrentScreenProp.GetScreenSize();
            var nativeAspRatio = (double)displayProp.Width / displayProp.Height;
            var wideRatio      = (double)16 / 9;
            var ulWideRatio    = (double)21 / 9;
            var acH            = acceptableHeight;
            
            acH.RemoveAll(h => h > WindowUtility.CurrentScreenProp.GetMaxHeight());
            
            List<string> resPairs = new List<string>();

            // If res is 21:9 then add proper native to the list
            if (Math.Abs(nativeAspRatio - ulWideRatio) < 0.01)
                resPairs.Add($"{displayProp.Width}x{displayProp.Height}");
            
            foreach (var h in acH)
            {
                int w = (int)(h * wideRatio);
                resPairs.Add(string.Format(Lang._GameSettingsPage.Graphics_ResPrefixWindowed, w, h));
            }

            return resPairs;
        }
        
        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyText.Foreground = InheritApplyTextColor;
                ApplyText.Text = Lang._GameSettingsPage.SettingsApplied;
                ApplyText.Visibility = Visibility.Visible;

                ToggleRegistrySubscribe(false);
                Settings.SaveSettings();
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
            finally
            {
                ToggleRegistrySubscribe(true);
            }
        }

        public string CustomArgsValue
        {
            get => ((IGameSettingsUniversal)CurrentGameProperty._GameSettings).SettingsCustomArgument.CustomArgumentValue;
            set
            {
                ToggleRegistrySubscribe(false);
                ((IGameSettingsUniversal)CurrentGameProperty._GameSettings).SettingsCustomArgument.CustomArgumentValue = value;
                ToggleRegistrySubscribe(true);
            }
        }
        
        public bool IsUseCustomArgs
        {
            get
            {
                bool value = ((IGameSettingsUniversal)CurrentGameProperty._GameSettings).SettingsCollapseMisc.UseCustomArguments;

                if (value) CustomArgsTextBox.IsEnabled = true;
                else CustomArgsTextBox.IsEnabled       = false;
                
                return value;
            }
            set
            {
                ((IGameSettingsUniversal)CurrentGameProperty._GameSettings).SettingsCollapseMisc.UseCustomArguments = value;
                
                if (value) CustomArgsTextBox.IsEnabled = true;
                else CustomArgsTextBox.IsEnabled       = false;
            }
        }

        private void OnUnload(object sender, RoutedEventArgs e)
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                ToggleRegistrySubscribe(false);
                RegistryWatcher?.Dispose();
            });
        }
    }
}
