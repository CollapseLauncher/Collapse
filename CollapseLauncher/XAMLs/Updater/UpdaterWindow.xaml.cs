using Hi3Helper.Http;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Graphics;
using WinRT.Interop;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.InvokeProp;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class UpdaterWindow : Window
    {
        public static string execPath = Process.GetCurrentProcess().MainModule.FileName;
        public static string workingDir = Path.GetDirectoryName(execPath);
        public static string sourcePath = Path.Combine(workingDir, Path.GetFileName(execPath));
        public static string applyPath = Path.Combine(workingDir, $"ApplyUpdate.exe");
        public static string applyElevatedPath = Path.Combine(workingDir, "..\\", $"ApplyUpdate.exe");
        public static string elevatedPath = Path.Combine(workingDir, Path.GetFileNameWithoutExtension(sourcePath) + ".Elevated.exe");
        public static string launcherPath = Path.Combine(workingDir, "CollapseLauncher.exe");

        public UpdaterWindow()
        {
            this.InitializeComponent();
            InitializeWindowSettings();

            string title = $"Collapse Launcher Updater";
            if (IsPreview)
                this.Title = title += "[PREVIEW]";
#if DEBUG
            this.Title = title += "[DEBUG]";
#endif
#if PORTABLE
            this.Title += "[PORTABLE]";
#endif
            UpdateChannelLabel.Text = m_arguments.Updater.UpdateChannel.ToString();
            CurrentVersionLabel.Text = AppCurrentVersion.VersionString;

            StartAsyncRoutine();
        }

        private async void StartAsyncRoutine()
        {
            try
            {
                string newVerTagPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "CollapseLauncher", "_NewVer");
                progressBar.IsIndeterminate = true;
                UpdateChannelLabel.Text = m_arguments.Updater.UpdateChannel.ToString();
                ActivityStatus.Text = Lang._UpdatePage.UpdateMessage1;

                AppUpdateVersionProp updateInfo = new AppUpdateVersionProp();

                using (Http _httpClient = new Http(true))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        await FallbackCDNUtil.DownloadCDNFallbackContent(_httpClient, ms, $"{m_arguments.Updater.UpdateChannel.ToString().ToLower()}/fileindex.json", default);
                        ms.Position = 0;
                        updateInfo = (AppUpdateVersionProp)JsonSerializer.Deserialize(ms, typeof(AppUpdateVersionProp), AppUpdateVersionPropContext.Default);
                        NewVersionLabel.Text = new GameVersion(updateInfo.ver).VersionString;
                    }

                    FallbackCDNUtil.DownloadProgress += FallbackCDNUtil_DownloadProgress;
                    await FallbackCDNUtil.DownloadCDNFallbackContent(_httpClient, applyElevatedPath, Environment.ProcessorCount > 8 ? 8 : Environment.ProcessorCount, $"{m_arguments.Updater.UpdateChannel.ToString().ToLower()}/ApplyUpdate.exe", default);
                    FallbackCDNUtil.DownloadProgress -= FallbackCDNUtil_DownloadProgress;
                }

                File.WriteAllText(Path.Combine(workingDir, "..\\", "release"), m_arguments.Updater.UpdateChannel.ToString().ToLower());
                Status.Text = string.Format(Lang._UpdatePage.UpdateStatus5, updateInfo.ver);
                ActivityStatus.Text = Lang._UpdatePage.UpdateMessage5;

                File.WriteAllText(newVerTagPath, updateInfo.ver);

                await Task.Delay(5000);
                Process applyUpdate = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = applyElevatedPath,
                        UseShellExecute = true
                    }
                };
                applyUpdate.Start();

                App.Current.Exit();
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL CRASH!!!\r\n{ex}", Hi3Helper.LogType.Error, true);
            }
        }

        private void FallbackCDNUtil_DownloadProgress(object sender, DownloadEvent e)
        {
            progressBar.IsIndeterminate = false;
            progressBar.Value = e.ProgressPercentage;
            ActivityStatus.Text = string.Format(Lang._UpdatePage.UpdateStatus3, 1, 1);
            ActivitySubStatus.Text = $"{SummarizeSizeSimple(e.SizeDownloaded)} / {SummarizeSizeSimple(e.SizeToBeDownloaded)}";

            SpeedStatus.Text = string.Format(Lang._Misc.SpeedPerSec, SummarizeSizeSimple(e.Speed));
            TimeEstimation.Text = string.Format("{0:%h}h{0:%m}m{0:%s}s left", e.TimeLeft);
        }

        private void InitializeAppWindowAndIntPtr()
        {
            this.InitializeComponent();
            this.Activate();
            // Initialize Window Handlers
            m_windowHandle = GetActiveWindow();
            m_windowID = Win32Interop.GetWindowIdFromWindow(m_windowHandle);
            m_appWindow = AppWindow.GetFromWindowId(m_windowID);
            m_appWindow.Changed += AppWindow_Changed;
            m_presenter = m_appWindow.Presenter as OverlappedPresenter;
            DisplayArea displayArea = DisplayArea.GetFromWindowId(m_windowID, DisplayAreaFallback.Primary);

            // Get Monitor DPI
            IntPtr hMonitor = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);
            if (GetDpiForMonitor(hMonitor, Monitor_DPI_Type.MDT_Default, out uint dpiX, out uint _) != 0)
            {
                throw new Exception("Could not get DPI for monitor.");
            }

            m_appDPIScale = (uint)(((long)dpiX * 100 + (96 >> 1)) / 96) / 100.0;
        }

        public void InitializeWindowSettings()
        {
            InitializeAppWindowAndIntPtr();

            SetWindowSize(m_windowHandle, 540, 320);

            ExtendsContentIntoTitleBar = true;

            SetTitleBar(DragArea);
            m_presenter.IsResizable = false;
            m_presenter.IsMaximizable = false;

            switch (GetAppTheme())
            {
                case ApplicationTheme.Light:
                    Application.Current.Resources["WindowCaptionForeground"] = new Windows.UI.Color { A = 255, B = 0, G = 0, R = 0 };
                    break;
                case ApplicationTheme.Dark:
                    Application.Current.Resources["WindowCaptionForeground"] = new Windows.UI.Color { A = 255, B = 255, G = 255, R = 255 };
                    break;
            }

            Application.Current.Resources["WindowCaptionBackground"] = new SolidColorBrush(new Windows.UI.Color { A = 0, B = 0, G = 0, R = 0 });
            Application.Current.Resources["WindowCaptionBackgroundDisabled"] = new SolidColorBrush(new Windows.UI.Color { A = 0, B = 0, G = 0, R = 0 });
        }

        public void SetWindowSize(IntPtr hwnd, int width, int height, int x = 0, int y = 0)
        {
            var dpi = GetDpiForWindow(hwnd);
            float scalingFactor = (float)dpi / 96;
            width = (int)(width * scalingFactor);
            height = (int)(height * scalingFactor);

            SetWindowPos(hwnd, (IntPtr)SpecialWindowHandles.HWND_TOP,
                                        x, y, width, height,
                                        SetWindowPosFlags.SWP_NOMOVE);
        }

        private AppWindow GetAppWindowForCurrentWindow()
        {
            m_windowHandle = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(m_windowHandle);
            AppWindow window = AppWindow.GetFromWindowId(wndId);
            m_presenter = window.Presenter as OverlappedPresenter;
            m_windowHandle = WindowNative.GetWindowHandle(this);
            return window;
        }

        PointInt32 LastPos;
        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            // TEMPORARY HACK:
            // This one to prevent app to maximize since Maximize button in Windows 10 cannot be disabled.
            if (args.DidPresenterChange)
            {
                if (m_appWindow.Position.X > -128 || m_appWindow.Position.Y > -128)
                    m_presenter.Restore();

                sender.Move(LastPos);
                SetWindowSize(m_windowHandle, 540, 320);
            }

            if (!(m_appWindow.Position.X < 0 || m_appWindow.Position.Y < 0))
                LastPos = m_appWindow.Position;
        }
    }
}
