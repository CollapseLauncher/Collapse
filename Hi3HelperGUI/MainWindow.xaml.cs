using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Hi3Helper;
using Hi3Helper.Preset;

using static Hi3Helper.Logger;

namespace Hi3HelperGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public partial class MainWindow : Window
    {
        [STAThread]
        [DebuggerNonUserCode]
        public static void Main() => new Application() { StartupUri = new Uri("MainWindow.xaml", UriKind.Relative) }.Run();

        private const int STD_OUTPUT_HANDLE = -11;
        private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        private const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        public static extern uint GetLastError();

        [DllImport("Kernel32")]
        public static extern void AllocConsole();

        [DllImport("Kernel32")]
        public static extern void FreeConsole();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;
        public static void HideConsoleWindow()
        {
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);
            Logger.DisableConsole = true;
            WriteLog($"Console toggle: Hidden", LogType.Default);
        }
        public static void ShowConsoleWindow()
        {
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_SHOW);
            Logger.DisableConsole = false;
            WriteLog($"Console toggle: Show", LogType.Default);
        }

        public MainWindow()
        {
            LoadAppConfig();
            InitializeConsole();
            ApplyAppConfig();
            InitializeComponent();
            //CheckVersionAvailability();
#if (NETCOREAPP)
            Title = "Hi3HelperGUI InDev v" + GetRunningVersion().ToString() + " (NET Core)";
#else
            Title = "Hi3HelperGUI InDev v" + GetRunningVersion().ToString() + " (NET Framework)";
#endif
            CheckConfigSettings();
        }

        internal void DisableAllFunction()
        {
            Dispatcher.Invoke(() =>
            {
                UpdateSection.IsEnabled = false;
                BlockSection.IsEnabled = false;
                CutsceneSection.IsEnabled = false;
                SettingsSection.IsEnabled = false;
                MirrorSelector.IsEnabled = false;
            });
        }

        static void InitializeConsole()
        {
            AllocConsole();

            var iStdOut = GetStdHandle(STD_OUTPUT_HANDLE);
            if (!GetConsoleMode(iStdOut, out uint outConsoleMode))
            {
                LogWriteLine("failed to get output console mode", LogType.Error);
                Console.ReadKey();
                return;
            }

            outConsoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;
            if (!SetConsoleMode(iStdOut, outConsoleMode))
            {
                LogWriteLine($"failed to set output console mode, error code: {GetLastError()}", LogType.Error);
                Console.ReadKey();
                return;
            }

            InitLog();
        }

        private void EnableConsole(object sender, RoutedEventArgs e) => ShowConsoleWindow();
        private void DisableConsole(object sender, RoutedEventArgs e) => HideConsoleWindow();
        private void ApplySettings(object sender, RoutedEventArgs e)
        {
            SaveAppConfig();
            ApplyAppConfig();
        }

        private void ClickableLink(object sender, RequestNavigateEventArgs e) => new Process() { StartInfo = new ProcessStartInfo() { FileName = e.Uri.AbsoluteUri, UseShellExecute = true } }.Start();
    }
}
