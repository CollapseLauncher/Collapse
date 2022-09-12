using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using static CollapseLauncher.FileDialogNative;
using static Hi3Helper.InvokeProp;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
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

            if (IsPortable)
                Version += "-Portable";

            AppVersionTextBlock.Text = Version;
            CurrentVersion.Text = Version;
            GetLanguageList();
        }

        private void GetLanguageList()
        {
            List<string> _out = new List<string>();
            string CurrentLang = GetAppConfigValue("AppLanguage").ToString();
            int Index = -1;
            int SelectedIndex = -1;
            foreach (KeyValuePair<string, LangMetadata> Entry in LanguageNames)
            {
                Index++;
                if (Entry.Value.LangID == CurrentLang)
                    SelectedIndex = Index;

                _out.Add(string.Format(Lang._SettingsPage.LanguageEntry, Entry.Key, Entry.Value.Author));
            }

            LanguageSelector.ItemsSource = _out;
            LanguageSelector.SelectedIndex = SelectedIndex;
        }

        private async void RelocateFolder(object sender, RoutedEventArgs e)
        {
            switch (await Dialogs.SimpleDialogs.Dialog_RelocateFolder(Content))
            {
                case ContentDialogResult.Primary:
                    File.Delete(AppConfigFile);
                    File.Delete(AppNotifIgnoreFile);
                    try
                    {
                        Directory.Delete(AppGameConfigMetadataFolder, true);
                    }
                    catch { }
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
                    Arguments = AppGameFolder
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

        private void ForceUpdate(object sender, RoutedEventArgs e)
        {
            string ChannelName = IsPreview ? "Preview" : "Stable";
            if (IsPortable)
                ChannelName += "Portable";

            string ExecutableLocation = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            string UpdateArgument = $"elevateupdate --input \"{ExecutableLocation.Replace('\\', '/')}\" --channel {ChannelName}";
            Console.WriteLine(UpdateArgument);
            try
            {
                new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        FileName = Path.Combine(ExecutableLocation, "CollapseLauncher.exe"),
                        Arguments = UpdateArgument,
                        Verb = "runas"
                    }
                }.Start();
                App.Current.Exit();
            }
            catch
            {
                return;
            }
        }

        private void CheckUpdate(object sender, RoutedEventArgs e)
        {
            UpdateLoadingStatus.Visibility = Visibility.Visible;
            UpdateAvailableStatus.Visibility = Visibility.Collapsed;
            UpToDateStatus.Visibility = Visibility.Collapsed;
            CheckUpdateBtn.IsEnabled = false;

            ForceInvokeUpdate = true;

            LauncherUpdateInvoker.UpdateEvent += LauncherUpdateInvoker_UpdateEvent;
            LauncherUpdateWatcher.StartCheckUpdate();
        }

        private void LauncherUpdateInvoker_UpdateEvent(object sender, LauncherUpdateProperty e)
        {
            string ChannelName = IsPreview ? " Preview" : " Stable";
            if (IsPortable)
                ChannelName += "-Portable";

            CheckUpdateBtn.IsEnabled = true;
            if (e.IsUpdateAvailable)
            {
                UpdateLoadingStatus.Visibility = Visibility.Collapsed;
                UpdateAvailableStatus.Visibility = Visibility.Visible;
                UpToDateStatus.Visibility = Visibility.Collapsed;
                UpdateAvailableLabel.Text = e.NewVersionName + (ChannelName);
                LauncherUpdateInvoker.UpdateEvent -= LauncherUpdateInvoker_UpdateEvent;
                return;
            }
            else
            {
                UpdateLoadingStatus.Visibility = Visibility.Collapsed;
                UpdateAvailableStatus.Visibility = Visibility.Collapsed;
                UpToDateStatus.Visibility = Visibility.Visible;
                LauncherUpdateInvoker.UpdateEvent -= LauncherUpdateInvoker_UpdateEvent;
            }
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

        private void ClickButtonLinkFromTag(object sender, RoutedEventArgs e)
        {
            new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = (sender as Button).Tag.ToString(),
                    UseShellExecute = true
                }
            }.Start();
        }

        private async void SelectBackgroundImg(object sender, RoutedEventArgs e)
        {
            string file = await GetFilePicker(new Dictionary<string, string> { { "Supported formats", "*.jpg;*.jpeg;*.jfif;*.png;*.bmp;*.tiff;*.tif;*.webp" } });
            if (file != null)
            {
                regionBackgroundProp.imgLocalPath = file;
                SetAndSaveConfigValue("CustomBGPath", file);
                BGPathDisplay.Text = file;
                BackgroundImgChanger.ChangeBackground(file);
            }
        }

        int EggsAttempt = 1;
        private void Egg(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (EggsAttempt++ >= 10)
                HerLegacy.Visibility = Visibility.Visible;
        }

        bool EnableLanguageChange = false;
        private void LanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsAppLangNeedRestart)
                AppLangSelectionWarning.Visibility = Visibility.Visible;

            if (EnableLanguageChange)
            {
                SetAndSaveConfigValue("AppLanguage", new IniValue(LanguageNames.Values.ToList()[(sender as ComboBox).SelectedIndex].LangID));
                AppLangSelectionWarning.Visibility = Visibility.Visible;
                IsAppLangNeedRestart = true;
            }
            EnableLanguageChange = true;
        }

        public bool IsBGCustom
        {
            get
            {
                bool IsEnabled = GetAppConfigValue("UseCustomBG").ToBool();
                string BGPath = GetAppConfigValue("CustomBGPath").ToString();
                if (!string.IsNullOrEmpty(BGPath))
                    BGPathDisplay.Text = BGPath;
                else
                    BGPathDisplay.Text = Lang._Misc.NotSelected;

                BGSelector.IsEnabled = IsEnabled;
                return IsEnabled;
            }
            set
            {
                SetAndSaveConfigValue("UseCustomBG", new IniValue(value));
                if (!value)
                {
                    BGPathDisplay.Text = Lang._Misc.NotSelected;
                    regionBackgroundProp.imgLocalPath = GetAppConfigValue("CurrentBackground").ToString();
                    BackgroundImgChanger.ChangeBackground(regionBackgroundProp.imgLocalPath, false);
                }
                else
                {
                    string BGPath = GetAppConfigValue("CustomBGPath").ToString();
                    if (string.IsNullOrEmpty(BGPath))
                        regionBackgroundProp.imgLocalPath = AppDefaultBG;
                    else
                        regionBackgroundProp.imgLocalPath = BGPath;
                    BackgroundImgChanger.ChangeBackground(regionBackgroundProp.imgLocalPath);
                }
                BGSelector.IsEnabled = value;
            }
        }
        public bool IsConsoleEnabled
        {
            get => GetAppConfigValue("EnableConsole").ToBool();
            set
            {
                if (value)
                    ShowConsoleWindow();
                else
                    HideConsoleWindow();

                SetAndSaveConfigValue("EnableConsole", value);
                InitLog(true, AppDataFolder);
            }
        }
        public int CurrentThemeSelection
        {
            get
            {
                if (IsAppThemeNeedRestart)
                    AppThemeSelectionWarning.Visibility = Visibility.Visible;

                string AppTheme = GetAppConfigValue("ThemeMode").ToString();
                object ThemeIndex;
                bool IsParseSuccess = Enum.TryParse(typeof(AppThemeMode), AppTheme, out ThemeIndex);
                return IsParseSuccess ? (int)ThemeIndex : -1;
            }
            set
            {
                SetAndSaveConfigValue("ThemeMode", Enum.GetName(typeof(AppThemeMode), value));
                AppThemeSelectionWarning.Visibility = Visibility.Visible;
                IsAppThemeNeedRestart = true;
            }
        }
        public int CurrentAppThreadDownloadValue
        {
            get => GetAppConfigValue("DownloadThread").ToInt();
            set => SetAndSaveConfigValue("DownloadThread", value);
        }
        public int CurrentAppThreadExtractValue
        {
            get => GetAppConfigValue("ExtractionThread").ToInt();
            set => SetAndSaveConfigValue("ExtractionThread", value);
        }
    }
}
