using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.Storage.Pickers;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.InteropServices;

using Windows.Foundation;
using Windows.Foundation.Collections;

using Microsoft.Win32;

using Newtonsoft.Json;

using CollapseLauncher.Dialogs;
using static CollapseLauncher.Dialogs.SimpleDialogs;

using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Shared.GameConversion;
using Hi3Helper.Shared.ClassStruct;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static Hi3Helper.Shared.Region.InstallationManagement;
using static Hi3Helper.Logger;
using static Hi3Helper.InvokeProp;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CollapseLauncher.Dialogs
{
    public partial class InstallationConvert : Page
    {
        string SourcePath;
        string SourceDataIntegrityURL;
        string TargetPath;
        string TargetDataIntegrityURL;
        string TargetINIPath;
        string GameVersion;
        PresetConfigClasses SourceProfile;
        PresetConfigClasses TargetProfile;
        GameConversionManagement Converter;
        IniFile SourceIniFile;
        string EndpointURL;
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
                EndpointURL = string.Format(CurrentRegion.ZipFileURL, Path.GetFileNameWithoutExtension(regionResourceProp.data.game.latest.path));
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
                            Title = "Select Game",
                            Content = "Source game isn't Installed! Please choose another game.",
                            CloseButtonText = null,
                            PrimaryButtonText = "Okay",
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
                    Title = "Conversion Successful",
                    Content = new TextBlock
                    {
                        Text = string.Format("Game Conversion from {0} to {1} has been successfully completed!", SourceProfile.ZoneName, TargetProfile.ZoneName),
                        TextWrapping = TextWrapping.Wrap
                    },
                    CloseButtonText = null,
                    PrimaryButtonText = "Okay, back to menu",
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
            TargetINIPath = Path.Combine(AppGameFolder, TargetProfile.ProfileName, "config.ini");
            SourceDataIntegrityURL = await FetchDataIntegrityURL(SourceProfile);
            TargetDataIntegrityURL = await FetchDataIntegrityURL(TargetProfile);

            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressSlider.Value = 0;

                Step1.Opacity = 1f;
                Step1ProgressRing.IsIndeterminate = false;
                Step1ProgressRing.Value = 100;
                Step1ProgressStatus.Text = "Completed!";
            });
        }

        private async Task<string> FetchDataIntegrityURL(PresetConfigClasses Profile)
        {
            RegionResourceProp _Entry;
            using (MemoryStream s = new MemoryStream())
            {
                await new HttpClientHelper().DownloadFileAsync(Profile.LauncherResourceURL, s, tokenSource.Token, null, null, false);
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
            SourceIniFile.Load(new FileStream(INIPath, FileMode.Open, FileAccess.Read));
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
                PlaceholderText = "Select Source"
            };
            SourceGame.SelectionChanged += SourceGameChangedArgs;
            TargetGame = new ComboBox
            {
                Width = 200,
                PlaceholderText = "Select Target",
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
                Text = "Note: This conversion will break the integration with default launcher if you installed this game by migrating it.\r\n\r\nPlease choose your source and target game to be converted:",
                Margin = new Thickness(0, 0, 0, 16),
                TextWrapping = TextWrapping.Wrap,
            });
            DialogContainer.Children.Add(ComboBoxContainer);

            Dialog = new ContentDialog
            {
                Title = "Select Game",
                Content = DialogContainer,
                CloseButtonText = null,
                PrimaryButtonText = "Cancel",
                SecondaryButtonText = "Next",
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
                Step2ProgressStatus.Text = "Fetching API...";
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
                Step2ProgressStatus.Text = "Completed!";
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
                Step3ProgressStatus.Text = "Preparing Ingredients...";
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
                Step3ProgressStatus.Text = "Completed!";
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
                Step4ProgressStatus.Text = "Starting Conversion...";
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
                Step4ProgressStatus.Text = "Completed!";
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
                Step5ProgressStatus.Text = "Waiting...";
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
            gameIni.ProfileStream = new FileStream(gameIni.ProfilePath, FileMode.Create, FileAccess.ReadWrite);
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