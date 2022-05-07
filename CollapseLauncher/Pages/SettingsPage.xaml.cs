using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Hi3Helper.Shared.ClassStruct;

using static Hi3Helper.Logger;
using static Hi3Helper.InvokeProp;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();
            LoadAppConfig();
            this.DataContext = this;

            string Version = $" {AppCurrentVersion}";
            if (IsPreview)
                Version = Version + " Preview";
            else
                Version = Version + " Stable";

            AppVersionTextBlock.Text = Version;
            CurrentVersion.Text = Version;
            AppThemeSelection.SelectedIndex = (int)Enum.Parse<AppThemeMode>(GetAppConfigValue("ThemeMode").ToString());
            DownloadThreadsNumBox.Value = GetAppConfigValue("DownloadThread").ToInt();
            ExtractionThreadsNumBox.Value = GetAppConfigValue("ExtractionThread").ToInt();
        }

        public bool EnableConsole { get { return Hi3Helper.Logger.EnableConsole; } }

        private void ConsoleToggle(object sender, RoutedEventArgs e)
        {
            if (((ToggleSwitch)sender).IsOn)
                ShowConsoleWindow();
            else
                HideConsoleWindow();

            SetAndSaveConfigValue("EnableConsole", ((ToggleSwitch)sender).IsOn);
            InitLog(true, AppDataFolder);
        }

        private async void RelocateFolder(object sender, RoutedEventArgs e)
        {
            switch (await Dialogs.SimpleDialogs.Dialog_RelocateFolder(Content))
            {
                case ContentDialogResult.Primary:
                    File.Delete(AppConfigFile);
                    File.Delete(AppNotifIgnoreFile);
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
            {
                logstream.Dispose();
                logstream = null;
                Directory.Delete(AppGameLogsFolder, true);
                InitLog(true, AppDataFolder);
            }

            Directory.CreateDirectory(AppGameLogsFolder);
            (sender as Button).IsEnabled = false;
        }

        private void CheckUpdate(object sender, RoutedEventArgs e)
        {
            UpdateLoadingStatus.Visibility = Visibility.Visible;
            UpdateAvailableStatus.Visibility = Visibility.Collapsed;
            UpToDateStatus.Visibility = Visibility.Collapsed;
            CheckUpdateBtn.IsEnabled = false;
            CheckUpdateBtn.Content = "Checking...";

            ForceInvokeUpdate = true;

            LauncherUpdateInvoker.UpdateEvent += LauncherUpdateInvoker_UpdateEvent;
            LauncherUpdateWatcher.StartCheckUpdate();
        }

        private void LauncherUpdateInvoker_UpdateEvent(object sender, LauncherUpdateProperty e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (e.IsUpdateAvailable)
                {
                    UpdateLoadingStatus.Visibility = Visibility.Collapsed;
                    UpdateAvailableStatus.Visibility = Visibility.Visible;
                    UpToDateStatus.Visibility = Visibility.Collapsed;
                    CheckUpdateBtn.IsEnabled = true;
                    UpdateAvailableLabel.Text = e.NewVersionName + (IsPreview ? " Preview" : " Stable");
                    CheckUpdateBtn.Content = "Check Update";
                    LauncherUpdateInvoker.UpdateEvent -= LauncherUpdateInvoker_UpdateEvent;
                    return;
                }
                else
                {
                    UpdateLoadingStatus.Visibility = Visibility.Collapsed;
                    UpdateAvailableStatus.Visibility = Visibility.Collapsed;
                    UpToDateStatus.Visibility = Visibility.Visible;
                    CheckUpdateBtn.Content = "Check Update";
                    CheckUpdateBtn.IsEnabled = false;
                    LauncherUpdateInvoker.UpdateEvent -= LauncherUpdateInvoker_UpdateEvent;
                }
            });
        }

        bool IsFirstChangeThemeSelection = false;
        private void ChangeThemeSelection(object sender, SelectionChangedEventArgs e)
        {
            SetAndSaveConfigValue("ThemeMode", Enum.GetName(typeof(AppThemeMode), (sender as RadioButtons).SelectedIndex));
            if (IsAppThemeNeedRestart)
                AppThemeSelectionWarning.Visibility = Visibility.Visible;

            if (IsFirstChangeThemeSelection)
                IsAppThemeNeedRestart = true;
            IsFirstChangeThemeSelection = true;
        }

        bool IsFirstDownloadThreadsValue = false;
        private void ChangeDownloadThreadsValue(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (IsFirstDownloadThreadsValue)
                SetAndSaveConfigValue("DownloadThread", (int)(double.IsNaN(sender.Value) ? 0 : sender.Value));
            IsFirstDownloadThreadsValue = true;
        }
        bool IsFirstExtractThreadsValue = false;
        private void ChangeExtractThreadsValue(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (IsFirstExtractThreadsValue)
                SetAndSaveConfigValue("ExtractionThread", (int)(double.IsNaN(sender.Value) ? 0 : sender.Value));
            IsFirstExtractThreadsValue = true;
        }

        private void ClickTextLinkFromTag(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = (sender as TextBlock).Tag.ToString(),
                    UseShellExecute = true
                }
            }.Start();
        }

        int EggsAttempt = 1;
        private void Egg(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (EggsAttempt++ >= 10)
                HerLegacy.Visibility = Visibility.Visible;
        }
    }
}
