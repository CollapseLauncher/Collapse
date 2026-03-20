#if !DISABLEDISCORD
using CollapseLauncher.DiscordPresence;
#endif
using CollapseLauncher.GameManagement.ImageBackground;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Animation;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using Hi3Helper.Win32.Screen;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static CollapseLauncher.Statics.GamePropertyVault;
using Hi3Helper.SentryHelper;
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable AsyncVoidMethod

#nullable enable
namespace CollapseLauncher.Pages
{
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    public partial class ZenlessGameSettingsPage
    {
        public ZenlessGameSettingsPage() : base(GetCurrentGameProperty().GameSettings, Registry.CurrentUser.CreateSubKey(Path.Combine($"Software\\{GetCurrentGameProperty().GameVersion?.VendorTypeProp.VendorType}", GetCurrentGameProperty().GameVersion?.GamePreset.InternalGameNameInConfig!)))
        {
            try
            {
                InitializeComponent();

                ApplyButton.Translation           = new Vector3(0, 0, 32);
                GameSettingsApplyGrid.Translation = new Vector3(0, 0, 64);
                SettingsScrollViewer.EnableImplicitAnimation(true);

                SetApplyTextContainer(GameSettingsApplyGrid, gridColumn: 1);
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        protected override async void OnRegistryExportButtonClick(object sender, RoutedEventArgs e)
        {
            await SuspendRegistryMonitorOnActionAsync(Impl);
            return;

            async Task Impl()
            {
                try
                {
                    string gameBasePath = ConverterTool.NormalizePath(CurrentGameProperty.GameVersion?.GameDirPath);
                    string[] relativePaths = GetFilesRelativePaths(gameBasePath, $@"{CurrentGameProperty.GameExecutableNameWithoutExtension}_Data\Persistent\LocalStorage");
                    Exception? exc = await (Settings?.ExportSettings(true, gameBasePath, relativePaths) ?? Task.FromResult<Exception?>(null));

                    if (exc != null) throw exc;
                    SetApplyTextStatus("Lang._GameSettingsPage.SettingsRegExported");
                }
                catch (OperationCanceledException)
                {
                    SetApplyTextStatus("Lang._GameSettingsPage.SettingsRegErr1", true);
                }
                catch (Exception ex)
                {
                    Logger.LogWriteLine($"[GSP Module] An error has occurred while trying to exporting the registry!\r\n{ex}", LogType.Error, true);
                    SetApplyTextStatus(ex.Message, true);
                    ErrorSender.SendException(ex);
                    SentryHelper.ExceptionHandler(ex);
                }
            }
        }

        protected override async void OnRegistryImportButtonClick(object sender, RoutedEventArgs e)
        {
            await SuspendRegistryMonitorOnActionAsync(Impl);
            return;

            async Task Impl()
            {
                try
                {
                    string     gameBasePath = ConverterTool.NormalizePath(CurrentGameProperty.GameVersion?.GameDirPath);
                    Exception? exc          = await (Settings?.ImportSettings(gameBasePath) ?? Task.FromResult<Exception?>(null));

                    if (exc != null) throw exc;

                    SetApplyTextStatus("Lang._GameSettingsPage.SettingsRegImported");
                }
                catch (OperationCanceledException)
                {
                    SetApplyTextStatus("Lang._GameSettingsPage.SettingsRegErr1", true);
                }
                catch (Exception ex)
                {
                    Logger.LogWriteLine($"[GSP Module] An error has occurred while trying to importing the registry!\r\n{ex}", LogType.Error, true);
                    SetApplyTextStatus(ex.Message, true);
                    ErrorSender.SendException(ex);
                    SentryHelper.ExceptionHandler(ex);
                }
            }
        }

        private static string[] GetFilesRelativePaths(string gameDir, string relativePath)
        {
            string sourceDirPath = Path.Combine(gameDir, relativePath);
            return Directory.EnumerateFiles(sourceDirPath, "*", SearchOption.AllDirectories).Select(filePath =>
            {
                string fileName           = Path.GetFileName(filePath);
                string trimmedRelativeDir = Path.GetDirectoryName(filePath[gameDir.Length..])?.Trim('\\') ?? "";
                string relativeFilePath   = Path.Combine(trimmedRelativeDir, fileName);
                return relativeFilePath;
            }).ToArray();
        }

        private Size SizeProp { get; set; }

        private void InitializeSettings(object sender, RoutedEventArgs e)
        {
            try
            {
                ImageBackgroundManager.Shared.IsBackgroundElevated = true;
                ImageBackgroundManager.Shared.ForegroundOpacity    = 0d;
                ImageBackgroundManager.Shared.SmokeOpacity         = 1d;

                SizeProp = ScreenProp.CurrentResolution;

                // Get the native resolution first
                Size nativeResSize = GetNativeDefaultResolution();
                string nativeResString = string.Format(Locale.Current.Lang?._GameSettingsPage?.Graphics_ResPrefixFullscreen ?? "", nativeResSize.Width, nativeResSize.Height) + $" [{Locale.Current.Lang?._Misc?.Default}]";

                // Then get the rest of the list
                List<string> resFullscreen = GetResPairs_Fullscreen(nativeResSize);
                List<string> resWindowed   = GetResPairs_Windowed();

                // Add the index of fullscreen and windowed resolution booleans
                ScreenResolutionIsFullscreenIdx.Add(true);
                ScreenResolutionIsFullscreenIdx.AddRange(Enumerable.Range(0, resFullscreen.Count).Select(_ => true));
                ScreenResolutionIsFullscreenIdx.AddRange(Enumerable.Range(0, resWindowed.Count).Select(_ => false));

                // Add native resolution string, other fullscreen resolutions, and windowed resolutions
                List<string> resolutionList =
                [
                    nativeResString
                ];
                resolutionList.AddRange(resFullscreen);
                resolutionList.AddRange(resWindowed);

                GameResolutionSelector.ItemsSource   = resolutionList;
                _isAllowResolutionIndexChanged       = true; // Unlock resolution change
                GameResolutionSelector.SelectedIndex = ResolutionIndexSelected; // Refresh

                if (CurrentGameProperty.IsGameRunning)
                {
                #if !GSPBYPASSGAMERUNNING
                    Overlay.Visibility     = Visibility.Visible;
                    PageContent.Visibility = Visibility.Collapsed;
                    OverlayTitle.Text      = Locale.Current.Lang?._GameSettingsPage?.OverlayGameRunningTitle;
                    OverlaySubtitle.Text   = Locale.Current.Lang?._GameSettingsPage?.OverlayGameRunningSubtitle;
                #endif
                }
                else if (GameInstallationState
                    is GameInstallStateEnum.NotInstalled
                    or GameInstallStateEnum.NeedsUpdate
                    or GameInstallStateEnum.InstalledHavePlugin
                    or GameInstallStateEnum.GameBroken)
                {
                    Overlay.Visibility     = Visibility.Visible;
                    PageContent.Visibility = Visibility.Collapsed;
                    OverlayTitle.Text      = Locale.Current.Lang?._GameSettingsPage?.OverlayNotInstalledTitle;
                    OverlaySubtitle.Text   = Locale.Current.Lang?._GameSettingsPage?.OverlayNotInstalledSubtitle;
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
                Logger.LogWriteLine($"FATAL ERROR!!!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private static readonly List<int> AcceptableHeight = [4320, 2880, 2160, 1440, 1280, 1080, 900, 720];

        private static Size GetNativeDefaultResolution()
        {
            // Get the list of available resolutions. Otherwise, throw an exception.
            List<Size> currentAcceptedRes = ScreenProp.EnumerateScreenSizes().ToList();
            return currentAcceptedRes.Count == 0 ? throw new NullReferenceException("Cannot get screen resolution. Prolly the app cannot communicate with Win32 API???") :
                // Choose the maximium resolution
                currentAcceptedRes.MaxBy(x => (x.Width, x.Height));
        }
        
        private List<string> GetResPairs_Fullscreen(Size defaultResolution)
        {
            double       nativeAspRatio    = (double)SizeProp.Width / SizeProp.Height;
            List<int> acH               = AcceptableHeight;
            int       acceptedMaxHeight = ScreenProp.GetMaxHeight();

            acH.RemoveAll(h => h > acceptedMaxHeight);
            //acH.RemoveAll(h => h > 1600);

            // Get the resolution pairs and initialize default resolution index
            List<string> resPairs          = [];
            int          indexOfDefaultRes = -1;

            // ReSharper disable once LoopCanBeConvertedToQuery
            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < acH.Count; i++)
            {
                // Get height and calculate width
                int h = acH[i];
                int w = (int)Math.Round(h * nativeAspRatio);

                // If the resolution is the same as default, set the index
                if (h == defaultResolution.Height && w == defaultResolution.Width)
                    indexOfDefaultRes = i;

                // Add the resolution pair to the list
                resPairs.Add(string.Format(Locale.Current.Lang?._GameSettingsPage?.Graphics_ResPrefixFullscreen ?? "", w, h));
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
            double       nativeAspRatio    = (double)SizeProp.Width / SizeProp.Height;
            const double wideRatio         = (double)16 / 9;
            const double ulWideRatio       = (double)21 / 9;
            List<int>    acH               = AcceptableHeight;
            int          acceptedMaxHeight = ScreenProp.GetMaxHeight();

            acH.RemoveAll(h => h > acceptedMaxHeight);
            //acH.RemoveAll(h => h > 1600);
            List<string> resPairs = [];

            // If res is 21:9 then add proper native to the list
            if (Math.Abs(nativeAspRatio - ulWideRatio) < 0.01)
                resPairs.Add($"{SizeProp.Width}x{SizeProp.Height}");

            // ReSharper disable once LoopCanBeConvertedToQuery
            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < acH.Count; i++)
            {
                // Get height and calculate width
                int h = acH[i];
                int w = (int)Math.Round(h * wideRatio);

                // Add the resolution pair to the list
                resPairs.Add(string.Format(Locale.Current.Lang?._GameSettingsPage?.Graphics_ResPrefixWindowed ?? "", w, h));
            }

            return resPairs;
        }
    }
}
