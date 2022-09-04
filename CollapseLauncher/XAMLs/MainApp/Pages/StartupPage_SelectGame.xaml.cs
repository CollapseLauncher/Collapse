using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.UI;
using static CollapseLauncher.FileDialogNative;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Preset.ConfigStore;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public sealed partial class StartupPage_SelectGame : Page
    {
        bool AbortTransition = false;
        public StartupPage_SelectGame()
        {
            AbortTransition = false;
            this.InitializeComponent();
            LoadConfigTemplate();
            GameSelect.ItemsSource = GameConfigName;
        }

        private async void ChooseFolder(object sender, RoutedEventArgs e)
        {
            string folder;
            bool Selected = false;
            switch (await Dialogs.SimpleDialogs.Dialog_LocateFirstSetupFolder(Content, Path.Combine(AppDataFolder, "GameFolder")))
            {
                case ContentDialogResult.Primary:
                    AppGameFolder = Path.Combine(AppDataFolder, "GameFolder");
                    SetAppConfigValue("GameFolder", AppGameFolder);
                    Selected = true;
                    break;
                case ContentDialogResult.Secondary:
                    folder = await GetFolderPicker();
                    if (folder != null)
                        if (IsUserHasPermission(folder))
                        {
                            AppGameFolder = folder;
                            SetAppConfigValue("GameFolder", AppGameFolder);
                            Selected = true;
                        }
                        else
                        {
                            NextPage.IsEnabled = false;
                        }
                    else
                    {
                        NextPage.IsEnabled = false;
                    }
                    break;
            }

            if (Selected)
            {
                NextPage.IsEnabled = true;
                /*
                await HideLoadingPopup(false, Lang._StartupPage.OverlayPrepareFolderTitle, Lang._StartupPage.OverlayPrepareFolderSubtitle);
                await AppendFolderPermission(AppGameFolder);
                await HideLoadingPopup(true, Lang._StartupPage.OverlayPrepareFolderTitle, Lang._StartupPage.OverlayPrepareFolderSubtitle);

                if (!AbortTransition)
                {
                    SaveAppConfig();
                    MainFrameChanger.ChangeWindowFrame(typeof(MainPage));
                }
                */
            }
        }

        private async Task HideLoadingPopup(bool hide, string title, string subtitle)
        {
            Storyboard storyboard = new Storyboard();
            Storyboard storyboardBg = new Storyboard();

            DispatcherQueue.TryEnqueue(() =>
            {
                OverlayTitle.Text = title;
                OverlaySubtitle.Text = subtitle;
            });

            if (hide)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    Ring.IsIndeterminate = false;
                    Overlay.Visibility = Visibility.Visible;
                });

                await Task.Delay(500);

                DoubleAnimation OpacityAnimation = new DoubleAnimation();
                OpacityAnimation.From = 1;
                OpacityAnimation.To = 0;
                OpacityAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.25));

                Storyboard.SetTarget(OpacityAnimation, Overlay);
                Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
                storyboard.Children.Add(OpacityAnimation);

                storyboard.Begin();
                await Task.Delay(250);

                DispatcherQueue.TryEnqueue(() =>
                {
                    Overlay.Visibility = Visibility.Collapsed;
                });
            }
            else
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    Ring.IsIndeterminate = true;
                    Bg.Visibility = Visibility.Visible;
                    Overlay.Visibility = Visibility.Collapsed;
                });

                DispatcherQueue.TryEnqueue(() => Overlay.Visibility = Visibility.Visible);

                DoubleAnimation OpacityAnimation = new DoubleAnimation();
                OpacityAnimation.From = 0;
                OpacityAnimation.To = 1;
                OpacityAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.25));

                DoubleAnimation OpacityAnimationBg = new DoubleAnimation();
                OpacityAnimationBg.From = 1;
                OpacityAnimationBg.To = 0;
                OpacityAnimationBg.Duration = new Duration(TimeSpan.FromSeconds(0.25));

                Storyboard.SetTarget(OpacityAnimation, Overlay);
                Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
                storyboard.Children.Add(OpacityAnimation);

                Storyboard.SetTarget(OpacityAnimationBg, Bg);
                Storyboard.SetTargetProperty(OpacityAnimationBg, "Opacity");
                storyboardBg.Children.Add(OpacityAnimationBg);

                storyboard.Begin();
                storyboardBg.Begin();
                await Task.Delay(250);

                DispatcherQueue.TryEnqueue(() =>
                {
                    Bg.Visibility = Visibility.Collapsed;
                    Overlay.Visibility = Visibility.Visible;
                });
            }
        }

        private async Task AppendFolderPermission(string path)
        {
            try
            {
                Process proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(AppFolder, "CollapseLauncher.exe"),
                        UseShellExecute = true,
                        Verb = "runas",
                        Arguments = $"takeownership --input \"{path}\""
                    }
                };

                proc.Start();
                await proc.WaitForExitAsync();
            }
            catch (Win32Exception)
            {
                AbortTransition = true;
                MainFrameChanger.ChangeWindowFrame(typeof(StartupPage));
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            (m_window as MainWindow).rootFrame.Navigate(typeof(MainPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            (m_window as MainWindow).rootFrame.GoBack();
        }

        private void GameSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Set CurrentRegion in AppConfig
            SetAndSaveConfigValue("CurrentRegion", (sender as ComboBox).SelectedIndex);
            NextPage.IsEnabled = true;
        }
    }
}
