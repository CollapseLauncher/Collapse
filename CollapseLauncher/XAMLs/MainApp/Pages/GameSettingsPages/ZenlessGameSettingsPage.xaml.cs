#if !DISABLEDISCORD
    using CollapseLauncher.DiscordPresence;
#endif
    using CollapseLauncher.GameSettings.Zenless;
    using CollapseLauncher.Helper.Animation;
    using CollapseLauncher.Statics;
    using Hi3Helper;
    using Hi3Helper.Data;
    using Hi3Helper.Shared.ClassStruct;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Media;
    using Microsoft.Win32;
    using Hi3Helper.Win32.Screen;
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
        
        private System.Drawing.Size SizeProp           { get; set; }

        private void InitializeSettings(object sender, RoutedEventArgs e)
        {
            try
            {
                BackgroundImgChanger.ToggleBackground(true);

                var resList   = new List<string>();
                SizeProp = ScreenProp.CurrentResolution;

                // Get the native resolution first
                var nativeResSize = GetNativeDefaultResolution();
                var nativeResString = string.Format(Lang._GameSettingsPage.Graphics_ResPrefixFullscreen, nativeResSize.Width, nativeResSize.Height) + $" [{Lang._Misc.Default}]";

                // Then get the rest of the list
                List<string> resFullscreen = GetResPairs_Fullscreen(nativeResSize);
                List<string> resWindowed   = GetResPairs_Windowed();

                // Add the index of fullscreen and windowed resolution booleans
                ScreenResolutionIsFullscreenIdx.Add(true);
                ScreenResolutionIsFullscreenIdx.AddRange(Enumerable.Range(0, resFullscreen.Count).Select(_ => true));
                ScreenResolutionIsFullscreenIdx.AddRange(Enumerable.Range(0, resWindowed.Count).Select(_ => false));

                // Add native resolution string, other fullscreen resolutions, and windowed resolutions
                resList.Add(nativeResString);
                resList.AddRange(resFullscreen);
                resList.AddRange(resWindowed);

                GameResolutionSelector.ItemsSource   = resList;
                GameResolutionSelector.SelectedIndex = ResolutionIndexSelected; // Refresh

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

        private System.Drawing.Size GetNativeDefaultResolution()
        {
            const int maxAcceptedNativeW = 2560; // This is the maximum native resolution width that is accepted.
                                                 // Tested on both 3840x2160 and 1920x1080 screen. The game keeps to only accept
                                                 // the resolution where width <= 2560 as its default resolution.
                                                 // In other scenario, if the screen has selection with width > 2569 but does
                                                 // not have one with width == 2560, the game will keep to use the
                                                 // resolution that has the width of 2560, like.... WTF?????
                                                 // HOYOOOOOOO!!!!!!!

            // Get the list of available resolutions. Otherwise, throw an exception.
            var currentAcceptedRes = ScreenProp.EnumerateScreenSizes().ToList();
            if (currentAcceptedRes.Count == 0)
                throw new NullReferenceException("Cannot get screen resolution. Prolly the app cannot communicate with Win32 API???");
            var maxAcceptedResW = currentAcceptedRes.Max(x => x.Width); // Find the maximum resolution width that can be accepted.

            // If the max accepted resolution width is more than or equal to maxAcceptedNativeW,
            // then clamp the resolution to the resolution that is equal to maxAcceptedNativeW.
            if (maxAcceptedResW >= maxAcceptedNativeW)
            {
                var nativeAspRatio          = (double)SizeProp.Height / SizeProp.Width;
                var nativeAspRationHeight   = (int)(maxAcceptedNativeW * nativeAspRatio);

                System.Drawing.Size nativeRes = new System.Drawing.Size(maxAcceptedNativeW, nativeAspRationHeight);
                return nativeRes;
            }

            // Otherwise, get the last resolution which always be the maximum for other less than width: 2560 (for example: 1920x1080).
            return currentAcceptedRes.LastOrDefault(x => x.Width == maxAcceptedResW);
        }
        
        private List<string> GetResPairs_Fullscreen(System.Drawing.Size defaultResolution)
        {
            var nativeAspRatio    = (double)SizeProp.Width / SizeProp.Height;
            var acH               = acceptableHeight;
            var acceptedMaxHeight = ScreenProp.GetMaxHeight();

            acH.RemoveAll(h => h > acceptedMaxHeight);
            //acH.RemoveAll(h => h > 1600);

            // Get the resolution pairs and initialize default resolution index
            List<string> resPairs = new List<string>();
            int indexOfDefaultRes = -1;

            for (int i = 0; i < acH.Count; i++)
            {
                // Get height and calculate width
                int h = acH[i];
                int w = (int)Math.Round(h * nativeAspRatio);

                // If the resolution is the same as default, set the index
                if (h == defaultResolution.Height && w == defaultResolution.Width)
                    indexOfDefaultRes = i;

                // Add the resolution pair to the list
                resPairs.Add(string.Format(Lang._GameSettingsPage.Graphics_ResPrefixFullscreen, w, h));
            }

            // If the index of default resolution is found, remove it from the list
            if (indexOfDefaultRes != -1)
            {
                resPairs.RemoveAt(indexOfDefaultRes);
            }

            return resPairs;
        }

        private List<string> GetResPairs_Windowed()
        {
            var nativeAspRatio    = (double)SizeProp.Width / SizeProp.Height;
            var wideRatio         = (double)16 / 9;
            var ulWideRatio       = (double)21 / 9;
            var acH               = acceptableHeight;
            var acceptedMaxHeight = ScreenProp.GetMaxHeight();

            acH.RemoveAll(h => h > acceptedMaxHeight);
            //acH.RemoveAll(h => h > 1600);
            List<string> resPairs = new List<string>();

            // If res is 21:9 then add proper native to the list
            if (Math.Abs(nativeAspRatio - ulWideRatio) < 0.01)
                resPairs.Add($"{SizeProp.Width}x{SizeProp.Height}");

            for (int i = 0; i < acH.Count; i++)
            {
                // Get height and calculate width
                int h = acH[i];
                int w = (int)Math.Round(h * wideRatio);

                // Add the resolution pair to the list
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
            get => CurrentGameProperty._GameSettings.SettingsCustomArgument.CustomArgumentValue;
            set
            {
                ToggleRegistrySubscribe(false);
                CurrentGameProperty._GameSettings.SettingsCustomArgument.CustomArgumentValue = value;
                ToggleRegistrySubscribe(true);
            }
        }
        
        public bool IsUseCustomArgs
        {
            get
            {
                bool value = CurrentGameProperty._GameSettings.SettingsCollapseMisc.UseCustomArguments;

                if (value) CustomArgsTextBox.IsEnabled = true;
                else CustomArgsTextBox.IsEnabled       = false;
                
                return value;
            }
            set
            {
                CurrentGameProperty._GameSettings.SettingsCollapseMisc.UseCustomArguments = value;
                
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
