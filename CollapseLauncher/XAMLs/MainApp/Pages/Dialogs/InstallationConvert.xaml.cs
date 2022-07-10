using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.InstallationManagement;
using static Hi3Helper.Shared.Region.LauncherConfig;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CollapseLauncher.Dialogs
{
    public partial class InstallationConvert : Page
    {
        string SourceDataIntegrityURL;
        string TargetDataIntegrityURL;
        string GameVersion;
        PresetConfigClasses SourceProfile;
        PresetConfigClasses TargetProfile;
        GameConversionManagement Converter;
        IniFile SourceIniFile;
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        List<FilePropertiesRemote> BrokenFileIndexesProperty = new List<FilePropertiesRemote>();
        Dictionary<string, PresetConfigClasses> ConvertibleRegions;

        public InstallationConvert()
        {
            try
            {
                this.InitializeComponent();
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        public async void StartConversionProcess()
        {
            try
            {
                string EndpointURL = string.Format(CurrentRegion.ZipFileURL, Path.GetFileNameWithoutExtension(regionResourceProp.data.game.latest.path));
                bool IsAskContinue = true;
                while (IsAskContinue)
                {
                    (SourceProfile, TargetProfile) = await AskConvertionDestination();
                    if (IsSourceGameExist(SourceProfile))
                        IsAskContinue = false;
                    else
                    {
                        await new ContentDialog
                        {
                            Title = Lang._InstallConvert.SelectDialogTitle,
                            Content = Lang._InstallConvert.SelectDialogSubtitleNotInstalled,
                            CloseButtonText = null,
                            PrimaryButtonText = Lang._Misc.Okay,
                            SecondaryButtonText = null,
                            DefaultButton = ContentDialogButton.Primary,
                            Background = (Brush)Application.Current.Resources["DialogAcrylicBrush"],
                            XamlRoot = Content.XamlRoot
                        }.ShowAsync();
                    }
                }

                await DoSetProfileDataLocation();
                await DoDownloadRecipe();
                await DoPrepareIngredients();
                await DoConversion();
                await DoVerification();

                ApplyConfiguration();

                await new ContentDialog
                {
                    Title = Lang._InstallConvert.ConvertSuccessTitle,
                    Content = new TextBlock
                    {
                        Text = string.Format(Lang._InstallConvert.ConvertSuccessSubtitle, SourceProfile.ZoneName, TargetProfile.ZoneName),
                        TextWrapping = TextWrapping.Wrap
                    },
                    CloseButtonText = null,
                    PrimaryButtonText = Lang._Misc.OkayBackToMenu,
                    SecondaryButtonText = null,
                    DefaultButton = ContentDialogButton.Primary,
                    Background = (Brush)Application.Current.Resources["DialogAcrylicBrush"],
                    XamlRoot = Content.XamlRoot
                }.ShowAsync();

                OperationCancelled();
            }
            catch (OperationCanceledException)
            {
                LogWriteLine($"Conversion process is cancelled for Game Region: {CurrentRegion.ZoneName}");
                OperationCancelled();
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", Hi3Helper.LogType.Error, true);
                ErrorSender.SendException(ex);
                MainFrameChanger.ChangeWindowFrame(typeof(Pages.UnhandledExceptionPage));
            }
        }

        private async Task DoSetProfileDataLocation()
        {
            SourceProfile.ActualGameDataLocation = NormalizePath(SourceIniFile["launcher"]["game_install_path"].ToString());
            TargetProfile.ActualGameDataLocation = Path.Combine(Path.GetDirectoryName(SourceProfile.ActualGameDataLocation), $"{TargetProfile.GameDirectoryName}_ConvertedTo-{TargetProfile.ProfileName}");
            string TargetINIPath = Path.Combine(AppGameFolder, TargetProfile.ProfileName, "config.ini");
            SourceDataIntegrityURL = await FetchDataIntegrityURL(SourceProfile);
            TargetDataIntegrityURL = await FetchDataIntegrityURL(TargetProfile);

            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressSlider.Value = 0;

                Step1.Opacity = 1f;
                Step1ProgressRing.IsIndeterminate = false;
                Step1ProgressRing.Value = 100;
                Step1ProgressStatus.Text = Lang._Misc.Completed;
            });
        }

        private async Task<string> FetchDataIntegrityURL(PresetConfigClasses Profile)
        {
            RegionResourceProp _Entry;
            using (MemoryStream s = new MemoryStream())
            {
                await new Http().DownloadStream(Profile.LauncherResourceURL, s, tokenSource.Token);
                _Entry = JsonConvert.DeserializeObject<RegionResourceProp>(Encoding.UTF8.GetString(s.ToArray()));
            }

            GameVersion = _Entry.data.game.latest.version;

            return string.Format(Profile.ZipFileURL, Path.GetFileNameWithoutExtension(_Entry.data.game.latest.path));
        }

        public bool IsSourceGameExist(PresetConfigClasses Profile)
        {
            string INIPath = Path.Combine(AppGameFolder, Profile.ProfileName, "config.ini");
            string GamePath;
            string ExecPath;
            if (!File.Exists(INIPath))
                return false;

            SourceIniFile = new IniFile();
            SourceIniFile.Load(INIPath);
            try
            {
                GamePath = NormalizePath(SourceIniFile["launcher"]["game_install_path"].ToString());
                if (!Directory.Exists(GamePath))
                    return false;

                ExecPath = Path.Combine(GamePath, Profile.GameExecutableName);
                if (!File.Exists(ExecPath))
                    return false;
            }
            catch
            {
                return false;
            }

            return true;
        }

        public async Task<(PresetConfigClasses, PresetConfigClasses)> AskConvertionDestination()
        {
            ConvertibleRegions = new Dictionary<string, PresetConfigClasses>();
            foreach (PresetConfigClasses Config in ConfigStore.Config.Where(x => x.IsConvertible ?? false))
                ConvertibleRegions.Add(Config.ZoneName, Config);

            ComboBox SourceGame = new ComboBox();
            ComboBox TargetGame = new ComboBox();
            ContentDialog Dialog = new ContentDialog();

            SelectionChangedEventHandler SourceGameChangedArgs = new SelectionChangedEventHandler((object sender, SelectionChangedEventArgs e) =>
            {
                TargetGame.IsEnabled = true;
                Dialog.IsSecondaryButtonEnabled = false;
                TargetGame.ItemsSource = GetConvertibleNameList((sender as ComboBox).SelectedItem.ToString());
            });
            SelectionChangedEventHandler TargetGameChangedArgs = new SelectionChangedEventHandler((object sender, SelectionChangedEventArgs e) =>
            {
                if ((sender as ComboBox).SelectedIndex != -1)
                    Dialog.IsSecondaryButtonEnabled = true;
            });

            SourceGame = new ComboBox
            {
                Width = 200,
                ItemsSource = new List<string>(ConvertibleRegions.Keys),
                PlaceholderText = Lang._InstallConvert.SelectDialogSource
            };
            SourceGame.SelectionChanged += SourceGameChangedArgs;
            TargetGame = new ComboBox
            {
                Width = 200,
                PlaceholderText = Lang._InstallConvert.SelectDialogTarget,
                IsEnabled = false
            };
            TargetGame.SelectionChanged += TargetGameChangedArgs;

            StackPanel DialogContainer = new StackPanel() { Orientation = Orientation.Vertical };
            StackPanel ComboBoxContainer = new StackPanel() { Orientation = Orientation.Horizontal };
            ComboBoxContainer.Children.Add(SourceGame);
            ComboBoxContainer.Children.Add(new SymbolIcon() { Symbol = Symbol.Switch, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 16, 0), Opacity = 0.5f });
            ComboBoxContainer.Children.Add(TargetGame);
            DialogContainer.Children.Add(new TextBlock
            {
                Text = Lang._InstallConvert.SelectDialogSubtitle,
                Margin = new Thickness(0, 0, 0, 16),
                TextWrapping = TextWrapping.Wrap,
            });
            DialogContainer.Children.Add(ComboBoxContainer);

            Dialog = new ContentDialog
            {
                Title = Lang._InstallConvert.SelectDialogTitle,
                Content = DialogContainer,
                CloseButtonText = null,
                PrimaryButtonText = Lang._Misc.Cancel,
                SecondaryButtonText = Lang._Misc.Next,
                IsSecondaryButtonEnabled = false,
                DefaultButton = ContentDialogButton.Secondary,
                Background = (Brush)Application.Current.Resources["DialogAcrylicBrush"],
                XamlRoot = Content.XamlRoot
            };

            PresetConfigClasses SourceRet = null;
            PresetConfigClasses TargetRet = null;

            switch (await Dialog.ShowAsync())
            {
                case ContentDialogResult.Secondary:
                    SourceRet = ConfigStore.Config.Where(x => x.ZoneName == SourceGame.SelectedItem.ToString()).First();
                    TargetRet = ConfigStore.Config.Where(x => x.ZoneName == TargetGame.SelectedItem.ToString()).First();
                    break;
                case ContentDialogResult.Primary:
                    throw new OperationCanceledException();
            }
            return (SourceRet, TargetRet);
        }

        private List<string> GetConvertibleNameList(string ZoneName)
        {
            List<string> _out = new List<string>();
            List<string> GameTargetProfileName = ConfigStore.Config
                .Where(x => x.ZoneName == ZoneName)
                .Select(x => x.ConvertibleTo)
                .First();

            foreach (string Entry in GameTargetProfileName)
                _out.Add(ConfigStore.Config
                    .Where(x => x.ProfileName == Entry)
                    .Select(x => x.ZoneName)
                    .First());

            return _out;
        }

        private async Task DoDownloadRecipe()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressSlider.Value = 1;

                Step2.Opacity = 1f;
                Step2ProgressRing.IsIndeterminate = false;
                Step2ProgressRing.Value = 0;
                Step2ProgressStatus.Text = Lang._InstallConvert.Step2Subtitle;
            });
            Converter = new GameConversionManagement(SourceProfile, TargetProfile, SourceDataIntegrityURL, TargetDataIntegrityURL, GameVersion, Content);

            Converter.ProgressChanged += Step2ProgressEvents;
            await Converter.StartDownloadRecipe();
            Converter.ProgressChanged -= Step2ProgressEvents;

            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressSlider.Value = 1;

                Step2.Opacity = 1f;
                Step2ProgressRing.IsIndeterminate = false;
                Step2ProgressRing.Value = 100;
                Step2ProgressStatus.Text = Lang._Misc.Completed;
            });
        }

        private void Step2ProgressEvents(object sender, GameConversionManagement.ConvertProgress e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                Step2ProgressRing.Value = e.Percentage;
                Step2ProgressTitle.Text = e.ProgressStatus;
                Step2ProgressStatus.Text = e.ProgressDetail;
            });
        }

        private async Task DoPrepareIngredients()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressSlider.Value = 2;

                Step3.Opacity = 1f;
                Step3ProgressRing.IsIndeterminate = false;
                Step3ProgressRing.Value = 0;
                Step3ProgressStatus.Text = Lang._InstallConvert.Step3Subtitle;
            });

            Converter.ProgressChanged += Step3ProgressEvents;
            await Converter.StartPreparation();
            Converter.ProgressChanged -= Step3ProgressEvents;

            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressSlider.Value = 2;

                Step3.Opacity = 1f;
                Step3ProgressRing.IsIndeterminate = false;
                Step3ProgressRing.Value = 100;
                Step3ProgressStatus.Text = Lang._Misc.Completed;
            });
        }

        private void Step3ProgressEvents(object sender, GameConversionManagement.ConvertProgress e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                Step3ProgressRing.Value = e.Percentage;
                Step3ProgressTitle.Text = e.ProgressStatus;
                Step3ProgressStatus.Text = e.ProgressDetail;
            });
        }

        private async Task DoConversion()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressSlider.Value = 3;

                Step4.Opacity = 1f;
                Step4ProgressRing.IsIndeterminate = false;
                Step4ProgressRing.Value = 0;
                Step4ProgressStatus.Text = Lang._InstallConvert.Step4Subtitle;
            });

            Converter.ProgressChanged += Step4ProgressEvents;
            await Converter.StartConversion();
            Converter.ProgressChanged -= Step4ProgressEvents;

            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressSlider.Value = 3;

                Step4.Opacity = 1f;
                Step4ProgressRing.IsIndeterminate = false;
                Step4ProgressRing.Value = 100;
                Step4ProgressStatus.Text = Lang._Misc.Completed;
            });
        }

        private void Step4ProgressEvents(object sender, GameConversionManagement.ConvertProgress e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                Step4ProgressRing.Value = e.Percentage;
                Step4ProgressStatus.Text = e.ProgressDetail;
            });
        }

        private async Task DoVerification()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressSlider.Value = 4;

                Step5.Opacity = 1f;
                Step5ProgressRing.IsIndeterminate = false;
                Step5ProgressRing.Value = 0;
                Step5ProgressStatus.Text = Lang._InstallConvert.Step5Subtitle;
            });

            Converter.ProgressChanged += Step5ProgressEvents;
            await Converter.PostConversionVerify();
            Converter.ProgressChanged -= Step5ProgressEvents;

            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressSlider.Value = 4;

                Step5.Opacity = 1f;
                Step5ProgressRing.IsIndeterminate = false;
                Step5ProgressRing.Value = 100;
                Step5ProgressStatus.Text = "Completed!";
            });
        }

        private void Step5ProgressEvents(object sender, GameConversionManagement.ConvertProgress e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                Step5ProgressRing.Value = e.Percentage;
                Step5ProgressStatus.Text = e.ProgressDetail;
            });
        }

        public void ApplyConfiguration()
        {
            CurrentRegion = TargetProfile;
            CurrentRegion.GameDirectoryName = Path.GetFileNameWithoutExtension(TargetProfile.ActualGameDataLocation);
            gamePath = Path.GetDirectoryName(TargetProfile.ActualGameDataLocation);
            string IniPath = Path.Combine(AppGameFolder, TargetProfile.ProfileName);
            gameIni.ProfilePath = Path.Combine(IniPath, "config.ini");
            gameIni.Profile = new IniFile();
            BuildGameIniProfile();

            SetAndSaveConfigValue("CurrentRegion", ConfigStore.Config.FindIndex(x => x.ProfileName == TargetProfile.ProfileName));
            LoadAppConfig();
        }

        private void OperationCancelled()
        {
            MigrationWatcher.IsMigrationRunning = false;
            MainFrameChanger.ChangeWindowFrame(typeof(MainPage));
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            MigrationWatcher.IsMigrationRunning = true;
            StartConversionProcess();
        }
    }
}