using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

using static Hi3Helper.Logger;
using static Hi3Helper.InvokeProp;
using static Hi3Helper.Shared.Region.LauncherConfig;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CollapseLauncher.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();
            this.DataContext = this;

            AppVersionTextBlock.Text = $" {Assembly.GetExecutingAssembly().GetName().Version}";
        }

        public bool EnableConsole { get { return Hi3Helper.Logger.EnableConsole; } }

        private void ConsoleToggle(object sender, RoutedEventArgs e)
        {
            if (((ToggleSwitch)sender).IsOn)
                ShowConsoleWindow();
            else
                HideConsoleWindow();

            SetAppConfigValue("EnableConsole", ((ToggleSwitch)sender).IsOn);
            InitLog(true, AppDataFolder);
        }

        private async void RelocateFolder(object sender, RoutedEventArgs e)
        {
            switch (await Dialogs.SimpleDialogs.Dialog_RelocateFolder(Content))
            {
                case ContentDialogResult.Primary:
                    File.Delete(AppConfigFile);
                    MainFrameChanger.ChangeWindowFrame(typeof(StartupPage));
                    break;
                default:
                    break;
            }
        }

        private void OpenAppDataFolder(object sender, RoutedEventArgs e)
        {
            new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    FileName = "explorer.exe",
                    Arguments = AppDataFolder
                }
            }.Start();
        }

        private void ClearImgFolder(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(AppGameImgFolder))
                Directory.Delete(AppGameImgFolder, true);

            Directory.CreateDirectory(AppGameImgFolder);
            (sender as Button).IsEnabled = false;
        }

        private void ClearLogsFolder(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(AppGameLogsFolder))
                Directory.Delete(AppGameLogsFolder, true);

            Directory.CreateDirectory(AppGameLogsFolder);
            (sender as Button).IsEnabled = false;
        }
    }
}
