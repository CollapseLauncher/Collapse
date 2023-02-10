using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using static Hi3Helper.FileDialogNative;
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
            GetCDNList();
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
                if (Entry.Key == CurrentLang)
                    SelectedIndex = Index;

                _out.Add(string.Format(Lang._SettingsPage.LanguageEntry, Entry.Value.LangData.LanguageName, Entry.Value.LangData.Author));
            }

            LanguageSelector.ItemsSource = _out;
            LanguageSelector.SelectedIndex = SelectedIndex;
        }

        private void GetCDNList()
        {
            List<string> list = new List<string>();
            string CurrentCDN = GetAppConfigValue("CDNType").ToString();
            list.Add($"Default (GitHub)");
            list.Add($"Statically");
            CDNSelector.ItemsSource = list;
            if (CurrentCDN.Contains("Default"))
            {
                CDNSelector.SelectedIndex = 0;
            } else
            {
                CDNSelector.SelectedIndex = 1;
            }
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

        private void ForceRestart(object sender, RoutedEventArgs e)
        {
            string execPath = Process.GetCurrentProcess().MainModule.FileName;
            string workingDir = Path.GetDirectoryName(execPath);
            string launcherPath = Path.Combine(workingDir, "CollapseLauncher.exe");
            App.Current.Exit();
            Thread.Sleep(1000);
            new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    FileName = launcherPath,
                    WorkingDirectory = workingDir
                }
            }.Start();
            MainFrameChanger.ChangeMainFrame(typeof(StartupLanguageSelect));
            
            
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
        public void StartSetupPage(object sender, RoutedEventArgs e)
        {
            MainFrameChanger.ChangeMainFrame(typeof(StartupPage));
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

#if DISABLE_COM
        private void SelectBackgroundImg(object sender, RoutedEventArgs e)
        {
            string file = GetFilePicker(new Dictionary<string, string> { { "Supported formats", "*.jpg;*.jpeg;*.jfif;*.png;*.bmp;*.tiff;*.tif;*.webp" } });
#else
        private async void SelectBackgroundImg(object sender, RoutedEventArgs e)
        {
            string file = await GetFilePicker(new Dictionary<string, string> { { "Supported formats", "*.jpg;*.jpeg;*.jfif;*.png;*.bmp;*.tiff;*.tif;*.webp" } });
#endif
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
        bool EnableCDNChange = false;
        private void LanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsAppLangNeedRestart)
                AppLangSelectionWarning.Visibility = Visibility.Visible;

            if (EnableLanguageChange)
            {
                string LangName = LanguageNames
                    .Values
                    .ToList()[(sender as ComboBox).SelectedIndex]
                    .LangData.LanguageID;
                SetAndSaveConfigValue("AppLanguage", new IniValue(LangName));
                AppLangSelectionWarning.Visibility = Visibility.Visible;
                IsAppLangNeedRestart = true;
            }
            EnableLanguageChange = true;
        }

        private void CDNChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsAppCDNNeedRestart)
                CDNSelectionWarning.Visibility = Visibility.Visible;

            if (EnableCDNChange)
            {
                string s = CDNSelector.SelectedItem.ToString();
                // Better to call a function
                if (s.Contains("Default"))
                {
                    SetAndSaveConfigValue("CDNType", "Default");
                    AppNotifURLPrefix = AppNotifURLPrefix.Replace(AppNotifURLPrefix, "https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/main/notification_{0}.json");
                    AppGameConfigURLPrefix = AppGameConfigURLPrefix.Replace(AppGameConfigURLPrefix, "https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/main/metadata/metadata_{0}.json");
                    AppGameConfigV2URLPrefix = AppGameConfigV2URLPrefix.Replace(AppGameConfigV2URLPrefix, "https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/main/metadata/metadatav2_{0}.json");
                    AppGameRepairIndexURLPrefix = AppGameRepairIndexURLPrefix.Replace(AppGameRepairIndexURLPrefix, "https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/main/metadata/repair_indexes/{0}/{1}/index");
                    AppGameRepoIndexURLPrefix = AppGameRepoIndexURLPrefix.Replace(AppGameRepoIndexURLPrefix, "https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/main/metadata/repair_indexes/{0}/repo");
                }
                else
                {
                    SetAndSaveConfigValue("CDNType", s);
                    // 1: statically
                    switch (s)
                    {
                        case "Statically":
                            // there is probably a better way to do this instead of replacing the entire string (maybe prepending?)
                            // LogWriteLine(s);
                            // LogWriteLine("Statically case");
                            AppNotifURLPrefix = AppNotifURLPrefix.Replace(AppNotifURLPrefix, "https://cdn.statically.io/gh/neon-nyan/CollapseLauncher-ReleaseRepo/main/notification_{0}.json");
                            AppGameConfigURLPrefix = AppGameConfigURLPrefix.Replace(AppGameConfigURLPrefix, "https://cdn.statically.io/gh/neon-nyan/CollapseLauncher-ReleaseRepo/main/metadata/metadata_{0}.json");
                            AppGameConfigV2URLPrefix = AppGameConfigV2URLPrefix.Replace(AppGameConfigV2URLPrefix, "https://cdn.statically.io/gh/neon-nyan/CollapseLauncher-ReleaseRepo/main/metadata/metadatav2_{0}.json");
                            AppGameRepairIndexURLPrefix = AppGameRepairIndexURLPrefix.Replace(AppGameRepairIndexURLPrefix, "https://cdn.statically.io/gh/neon-nyan/CollapseLauncher-ReleaseRepo/main/metadata/repair_indexes/{0}/{1}/index");
                            AppGameRepoIndexURLPrefix = AppGameRepoIndexURLPrefix.Replace(AppGameRepoIndexURLPrefix, "https://cdn.statically.io/gh/neon-nyan/CollapseLauncher-ReleaseRepo/main/metadata/repair_indexes/{0}/repo");
                            break;
                    }
                }
                SetAndSaveConfigValue("CDNType", new IniValue(s));
                CDNSelectionWarning.Visibility = Visibility.Visible;
                IsAppCDNNeedRestart = true;
            }
            EnableCDNChange = true;
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
                    {
                        regionBackgroundProp.imgLocalPath = AppDefaultBG;
                    }
                    else
                    {
                        if (!File.Exists(BGPath))
                        {
                            regionBackgroundProp.imgLocalPath = AppDefaultBG;
                        }
                        else
                        {
                            regionBackgroundProp.imgLocalPath = BGPath;
                        }
                    }
                    BGPathDisplay.Text = regionBackgroundProp.imgLocalPath;
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
