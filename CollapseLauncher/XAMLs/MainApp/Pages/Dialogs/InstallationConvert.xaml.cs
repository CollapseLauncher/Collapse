﻿using CollapseLauncher.Statics;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Preset.ConfigV2Store;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Dialogs
{
    public partial class InstallationConvert : Page
    {
        string SourceDataIntegrityURL;
        string GameVersion;
        bool IsAlreadyConverted = false;
        PresetConfigV2 SourceProfile;
        PresetConfigV2 TargetProfile;
        GameConversionManagement Converter;
        IniFile SourceIniFile;
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        Dictionary<string, PresetConfigV2> ConvertibleRegions;

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

                DoSetProfileDataLocation();
                await DoDownloadRecipe();
                await DoPrepareIngredients();

                CancelBtn.IsEnabled = false;
                await DoConversion();

                IsAlreadyConverted = true;
                CancelBtn.IsEnabled = true;
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
            catch (TaskCanceledException)
            {
                LogWriteLine($"Conversion process is cancelled for Game {PageStatics._GameVersion.GamePreset.ZoneFullname}");
                OperationCancelled();
            }
            catch (OperationCanceledException)
            {
                LogWriteLine($"Conversion process is cancelled for Game {PageStatics._GameVersion.GamePreset.ZoneFullname}");
                OperationCancelled();
            }
            catch (Exception ex)
            {
                RevertConversion();
                LogWriteLine($"Conversion process has failed! But don't worry, the file have been reverted :D\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(new Exception($"Conversion process has failed! But don't worry, the file have been reverted :D\r\n{ex}", ex));
            }
            finally
            {
                Converter?.Dispose();
            }
        }

        private void DoSetProfileDataLocation()
        {
            SourceProfile.ActualGameDataLocation = NormalizePath(SourceIniFile["launcher"]["game_install_path"].ToString());
            TargetProfile.ActualGameDataLocation = Path.Combine(Path.GetDirectoryName(SourceProfile.ActualGameDataLocation), $"{TargetProfile.GameDirectoryName}_ConvertedTo-{TargetProfile.ProfileName}");
            string TargetINIPath = Path.Combine(AppGameFolder, TargetProfile.ProfileName, "config.ini");

            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressSlider.Value = 0;

                Step1.Opacity = 1f;
                Step1ProgressRing.IsIndeterminate = false;
                Step1ProgressRing.Value = 100;
                Step1ProgressStatus.Text = Lang._Misc.Completed;
            });
        }

        private async Task<string> FetchDataIntegrityURL(PresetConfigV2 Profile)
        {
            Http _Http = new Http();
            Dictionary<string, string> _RepoList;

            try
            {
                FallbackCDNUtil.DownloadProgress += Step2ProgressEvents;
                using (MemoryStream s = new MemoryStream())
                {
                    string repoListURL = string.Format(AppGameRepoIndexURLPrefix, Profile.ProfileName);
                    await FallbackCDNUtil.DownloadCDNFallbackContent(_Http, s, repoListURL, tokenSource.Token);
                    s.Position = 0;
                    _RepoList = (Dictionary<string, string>)JsonSerializer.Deserialize(s, typeof(Dictionary<string, string>), D_StringString.Default);
                }
            }
            finally
            {
                FallbackCDNUtil.DownloadProgress -= Step2ProgressEvents;
            }

            RegionResourceProp _Entry;

            using (MemoryStream s = new MemoryStream())
            {
                await _Http.Download(Profile.LauncherResourceURL, s, null, null, tokenSource.Token);
                s.Position = 0;
                _Entry = (RegionResourceProp)JsonSerializer.Deserialize(s, typeof(RegionResourceProp), RegionResourcePropContext.Default);
            }

            GameVersion = _Entry.data.game.latest.version;

            return _RepoList[GameVersion];
        }

        public bool IsSourceGameExist(PresetConfigV2 Profile)
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

        public async Task<(PresetConfigV2, PresetConfigV2)> AskConvertionDestination()
        {
            ConvertibleRegions = new Dictionary<string, PresetConfigV2>();
            foreach (KeyValuePair<string, PresetConfigV2> Config in ConfigV2.MetadataV2[CurrentConfigV2GameCategory].Where(x => x.Value.IsConvertible ?? false))
                ConvertibleRegions.Add(Config.Key, Config.Value);

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
            StackPanel ComboBoxContainer = new StackPanel() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            ComboBoxContainer.Children.Add(SourceGame);
            ComboBoxContainer.Children.Add(new FontIcon() { Glyph = "", FontFamily = Application.Current.Resources["FontAwesomeSolid"] as FontFamily, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 16, 0), Opacity = 0.5f });
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

            PresetConfigV2 SourceRet = null;
            PresetConfigV2 TargetRet = null;

            switch (await Dialog.ShowAsync())
            {
                case ContentDialogResult.Secondary:
                    SourceRet = ConfigV2.MetadataV2[CurrentConfigV2GameCategory].
                        Values.Where(x => x.ZoneName == SourceGame.SelectedItem.ToString()).First();
                    TargetRet = ConfigV2.MetadataV2[CurrentConfigV2GameCategory].
                        Values.Where(x => x.ZoneName == TargetGame.SelectedItem.ToString()).First();
                    break;
                case ContentDialogResult.Primary:
                    throw new OperationCanceledException();
            }
            return (SourceRet, TargetRet);
        }

        private List<string> GetConvertibleNameList(string ZoneName)
        {
            List<string> _out = new List<string>();
            List<string> GameTargetProfileName = ConfigV2.MetadataV2[CurrentConfigV2GameCategory].Values
                .Where(x => x.ZoneName == ZoneName)
                .Select(x => x.ConvertibleTo)
                .First();

            foreach (string Entry in GameTargetProfileName)
                _out.Add(ConfigV2.MetadataV2[CurrentConfigV2GameCategory].Values
                    .Where(x => x.ZoneName == Entry)
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

            SourceDataIntegrityURL = await FetchDataIntegrityURL(SourceProfile);

            bool IsChoosen = false;
            string cPath = null;
            while (!IsChoosen)
            {
                string FileName = string.Format("Cookbook_{0}_{1}_{2}_lzma2_crc32.diff", SourceProfile.ProfileName, TargetProfile.ProfileName, GameVersion);
                switch (await Dialog_LocateDownloadedConvertRecipe(Content, FileName))
                {
                    case ContentDialogResult.Primary:
                        cPath = await FileDialogNative.GetFilePicker(
                            new Dictionary<string, string> { { $"{SourceProfile.ProfileName} to {TargetProfile.ProfileName} Cookbook", FileName } });
                        IsChoosen = cPath != null;
                        break;
                    case ContentDialogResult.None:
                        throw new OperationCanceledException();
                }
            }

            Converter = new GameConversionManagement(SourceProfile, TargetProfile, SourceDataIntegrityURL, GameVersion, cPath, tokenSource.Token);

            DispatcherQueue.TryEnqueue(() =>
            {
                ProgressSlider.Value = 1;

                Step2.Opacity = 1f;
                Step2ProgressRing.IsIndeterminate = false;
                Step2ProgressRing.Value = 100;
                Step2ProgressStatus.Text = Lang._Misc.Completed;
            });
        }

        private void Step2ProgressEvents(object sender, DownloadEvent e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                Step2ProgressStatus.Text = $"{e.ProgressPercentage}% - {string.Format(Lang._Misc.SpeedPerSec, SummarizeSizeSimple(e.Speed))}";
                Step2ProgressRing.Value = e.ProgressPercentage;
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

        private void Step3ProgressEvents(object sender, ConvertProgress e)
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

        private void Step4ProgressEvents(object sender, ConvertProgress e)
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

        private void Step5ProgressEvents(object sender, ConvertProgress e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                Step5ProgressRing.Value = e.Percentage;
                Step5ProgressStatus.Text = e.ProgressDetail;
            });
        }

        public void ApplyConfiguration()
        {
            PageStatics._GameVersion.GamePreset = TargetProfile;
            PageStatics._GameVersion.UpdateGamePath(Path.GetFileNameWithoutExtension(TargetProfile.ActualGameDataLocation));

            string GameCategory = GetAppConfigValue("GameCategory").ToString();
            SetPreviousGameRegion(GameCategory, TargetProfile.ZoneName);
            LoadAppConfig();
        }

        private void OperationCancelled()
        {
            RevertConversion();
            MainFrameChanger.ChangeWindowFrame(typeof(MainPage));
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            StartConversionProcess();
        }

        private async void CancelConversion(object sender, RoutedEventArgs e)
        {
            string ContentText;
            if (IsAlreadyConverted)
                ContentText = string.Format(Lang._InstallConvert.CancelMsgSubtitle2, TargetProfile.ZoneName);
            else
                ContentText = Lang._InstallConvert.CancelMsgSubtitle1;

            ContentDialog Dialog = new ContentDialog
            {
                Title = Lang._InstallConvert.CancelMsgTitle,
                Content = new TextBlock
                {
                    Text = ContentText,
                    TextWrapping = TextWrapping.Wrap
                },
                CloseButtonText = null,
                PrimaryButtonText = Lang._Misc.Yes,
                SecondaryButtonText = Lang._Misc.No,
                DefaultButton = ContentDialogButton.Secondary,
                Background = (Brush)Application.Current.Resources["DialogAcrylicBrush"],
                XamlRoot = Content.XamlRoot
            };

            if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                tokenSource.Cancel();
                return;
            }
        }

        private void RevertConversion()
        {
            if (SourceProfile is null || TargetProfile is null) return;

            string OrigPath = SourceProfile.ActualGameDataLocation;
            string IngrPath = TargetProfile.ActualGameDataLocation + "_Ingredients";

            if (Directory.Exists(TargetProfile.ActualGameDataLocation))
            {
                // Do force config apply if the file has been actually converted.
                ApplyConfiguration();
                return;
            }
            else if (!Directory.Exists(IngrPath)) return;

            int DirLength = IngrPath.Length + 1;
            string destFilePath;
            string destFolderPath;
            foreach (string filePath in Directory.EnumerateFiles(IngrPath, "*", SearchOption.AllDirectories))
            {
                ReadOnlySpan<char> relativePath = filePath.AsSpan().Slice(DirLength);
                destFilePath = Path.Combine(OrigPath, relativePath.ToString());
                destFolderPath = Path.GetDirectoryName(destFilePath);

                if (!Directory.Exists(destFolderPath))
                    Directory.CreateDirectory(destFolderPath);

                try
                {
                    LogWriteLine($"Moving \"{relativePath.ToString()}\" to \"{destFolderPath}\"", Hi3Helper.LogType.Default, true);
                    File.Move(filePath, destFilePath, true);
                }
                catch (Exception ex)
                {
                    LogWriteLine($"Error while moving \"{relativePath.ToString()}\" to \"{destFolderPath}\"\r\nException: {ex}", Hi3Helper.LogType.Error, true);
                }
            }

            Directory.Delete(IngrPath, true);
        }
    }
}