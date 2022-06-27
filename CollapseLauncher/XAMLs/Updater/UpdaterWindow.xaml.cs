using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.IO;
using Windows.Graphics;
using WinRT.Interop;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.InvokeProp;
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
        public static string applyElevatedPath = Path.Combine(workingDir, "_Temp", $"ApplyUpdate.exe");
        public static string elevatedPath = Path.Combine(workingDir, Path.GetFileNameWithoutExtension(sourcePath) + ".Elevated.exe");

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

            UpdateChannelLabel.Text = m_arguments.Updater.UpdateChannel.ToString();
            CurrentVersionLabel.Text = AppCurrentVersion;

            StartAsyncRoutine();
        }

        private async void StartAsyncRoutine()
        {
            try
            {
                Updater updater = new Updater(m_arguments.Updater.AppPath, m_arguments.Updater.UpdateChannel.ToString().ToLower(), 4);
                updater.UpdaterProgressChanged += Updater_UpdaterProgressChanged;
                updater.UpdaterStatusChanged += Updater_UpdaterStatusChanged;

                await updater.StartFetch();
                await updater.StartCheck();
                await updater.StartUpdate();
                DispatcherQueue.TryEnqueue(() =>
                {
                    SpeedStatus.Visibility = Visibility.Collapsed;
                    TimeEstimation.Visibility = Visibility.Collapsed;
                    ActivitySubStatus.Visibility = Visibility.Collapsed;
                    ProgressStatus.Visibility = Visibility.Collapsed;
                });
                await updater.FinishUpdate();
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL CRASH!!!\r\n{ex}", Hi3Helper.LogType.Error, true);
                Console.ReadLine();
            }
        }

        private void Updater_UpdaterStatusChanged(object sender, Updater.UpdaterStatus e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                Status.Text = e.status;
                ActivityStatus.Text = e.message;
                NewVersionLabel.Text = e.newver;
            });
        }

        private void Updater_UpdaterProgressChanged(object sender, Updater.UpdaterProgress e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                progressBar.Value = e.ProgressPercentage;
                ActivitySubStatus.Text = $"{SummarizeSizeSimple(e.DownloadedSize)} / {SummarizeSizeSimple(e.TotalSizeToDownload)}";
                SpeedStatus.Text = $"{SummarizeSizeSimple(e.CurrentSpeed)}/s";
                TimeEstimation.Text = string.Format("{0:%h}h{0:%m}m{0:%s}s left", e.TimeLeft);
            });
        }

        public void InitializeWindowSettings()
        {
            m_backDrop = new BackdropManagement(this);
            m_wsdqHelper = new WindowsSystemDispatcherQueueHelper();
            m_wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

            m_AppWindow = GetAppWindowForCurrentWindow();
            m_AppWindow.Changed += AppWindow_Changed;

            SetWindowSize(m_windowHandle, 540, 320);

            // Check to see if customization is supported.
            // Currently only supported on Windows 11.
#if !DISABLETRANSPARENT
            m_backDrop.SetBackdrop(BackdropType.DesktopAcrylic);
#endif
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

        public double GetScaleAdjustment()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            DisplayArea displayArea = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);
            IntPtr hMonitor = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);

            int result = GetDpiForMonitor(hMonitor, Monitor_DPI_Type.MDT_Default, out uint dpiX, out uint _);
            if (result != 0)
            {
                throw new Exception("Could not get DPI for monitor.");
            }

            uint scaleFactorPercent = (uint)(((long)dpiX * 100 + (96 >> 1)) / 96);
            return scaleFactorPercent / 100.0;
        }

        PointInt32 LastPos;
        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            // TEMPORARY HACK:
            // This one to prevent app to maximize since Maximize button in Windows 10 cannot be disabled.
            if (args.DidPresenterChange)
            {
                if (m_AppWindow.Position.X > -128 || m_AppWindow.Position.Y > -128)
                    m_presenter.Restore();

                sender.Move(LastPos);
                SetWindowSize(m_windowHandle, 540, 320);
            }

            if (!(m_AppWindow.Position.X < 0 || m_AppWindow.Position.Y < 0))
                LastPos = m_AppWindow.Position;
        }
    }
}
