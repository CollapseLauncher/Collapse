using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace Hi3HelperGUI
{
    public partial class MainWindow : Window
    {
        [STAThread]
        [DebuggerNonUserCode]
        public static void Main() => new Application() { StartupUri = new Uri("MainWindow.xaml", UriKind.Relative) }.Run();

        public MainWindow()
        {
            LoadAppConfig();
            InitializeConsole();
            ApplyAppConfig();
            InitializeComponent();
#if (NETCOREAPP)
            Title = "Hi3HelperGUI InDev v" + GetRunningVersion().ToString() + " (NET Core)";
#else
            Title = "Hi3HelperGUI InDev v" + GetRunningVersion().ToString() + " (NET Framework)";
#endif
            CheckConfigSettings();
        }

        private void ApplySettings(object sender, RoutedEventArgs e)
        {
            SaveAppConfig();
            ApplyAppConfig();
        }

        private void ClickableLink(object sender, RequestNavigateEventArgs e) => new Process() { StartInfo = new ProcessStartInfo() { FileName = e.Uri.AbsoluteUri, UseShellExecute = true } }.Start();
    }
}
