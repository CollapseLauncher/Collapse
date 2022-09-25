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
using static Hi3Helper.Preset.ConfigV2Store;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public sealed partial class StartupPage : Page
    {
        public StartupPage()
        {
            this.InitializeComponent();
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
                            ErrMsg.Text = Lang._StartupPage.FolderInsufficientPermission;
                            ErrMsg.Foreground = new SolidColorBrush(new Color() { R = 255, G = 0, B = 0, A = 255 });
                        }
                    else
                    {
                        NextPage.IsEnabled = false;
                        ErrMsg.Text = Lang._StartupPage.FolderNotSelected;
                        ErrMsg.Foreground = new SolidColorBrush(new Color() { R = 255, G = 0, B = 0, A = 255 });
                    }
                    break;
            }

            if (Selected)
            {
                NextPage.IsEnabled = true;
                ErrMsg.Text = $"✅ {AppGameFolder}";
                ErrMsg.Foreground = new SolidColorBrush((Color)Application.Current.Resources["TextFillColorPrimary"]);
                ErrMsg.TextWrapping = TextWrapping.Wrap;
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

        private async void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (!IsConfigV2StampExist() || !IsConfigV2ContentExist())
            {
                await HideLoadingPopup(false, Lang._StartupPage.Pg1LoadingTitle1, Lang._StartupPage.Pg1LoadingSubitle1);
                await DownloadConfigV2Files(true, true);
                Ring.IsIndeterminate = false;
                OverlayTitle.Text = Lang._StartupPage.Pg1LoadingTitle1;
                OverlaySubtitle.Text = Lang._StartupPage.Pg1LoadingSubitle2;
                await Task.Delay(2000);
            }

            (m_window as MainWindow).rootFrame.Navigate(typeof(StartupPage_SelectGame), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
        }
    }
}
