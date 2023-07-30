using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using static Hi3Helper.Locale;

namespace CollapseLauncher
{
    internal class BackgroundActivityManager
    {
        private static ThemeShadow _infoBarShadow = new ThemeShadow();

        public static Dictionary<int, IBackgroundActivity> BackgroundActivities = new Dictionary<int, IBackgroundActivity>();

        public static void Attach(int hashID, IBackgroundActivity activity, string activityTitle, string activitySubtitle)
        {
            if (!BackgroundActivities.ContainsKey(hashID))
            {
                AttachEventToNotification(hashID, activity, activityTitle, activitySubtitle);
                BackgroundActivities.Add(hashID, activity);
#if DEBUG
                Logger.LogWriteLine($"Background activity with ID: {hashID} has been attached", LogType.Debug, true);
#endif
                return;
            }
        }

        public static void Detach(int hashID)
        {
            if (BackgroundActivities.ContainsKey(hashID))
            {
                BackgroundActivities.Remove(hashID);
                DetachEventFromNotification(hashID);
#if DEBUG
                Logger.LogWriteLine($"Background activity with ID: {hashID} has been detached", LogType.Debug, true);
#endif
                return;
            }

#if DEBUG
            Logger.LogWriteLine($"Cannot detach background activity with ID: {hashID} because it doesn't attached", LogType.Debug, true);
#endif
        }

        private static void AttachEventToNotification(int hashID, IBackgroundActivity activity, string activityTitle, string activitySubtitle)
        {
            // TODO: Attach the event to notification
            InfoBar _parentNotifUI = new InfoBar()
            {
                Tag = hashID,
                Severity = InfoBarSeverity.Informational,
                Background = (Brush)Application.Current.Resources["InfoBarAnnouncementBrush"],
                IsOpen = true,
                IsClosable = false,
                Margin = new Thickness(4, 4, 4, 0),
                CornerRadius = new CornerRadius(8),
                Shadow = _infoBarShadow,
                Title = activityTitle,
                Message = activitySubtitle
            };
            _parentNotifUI.Translation += LauncherConfig.Shadow32;

            StackPanel _parentContainer = new StackPanel() { Margin = new Thickness(-28, -8, _parentNotifUI.IsClosable ? -28 : 24, 20) };
            _parentNotifUI.Content = _parentContainer;
            Grid _parentGrid = new Grid()
            {
                ColumnDefinitions = {
                    new ColumnDefinition() { Width = new GridLength(72) },
                    new ColumnDefinition()
                }
            };
            _parentContainer.Children.Add(_parentGrid);

            StackPanel progressLogoContainer = new StackPanel()
            {
                CornerRadius = new CornerRadius(8),
                Width = 64,
                Height = 64,
                Margin = new Thickness(0, 4, 8, 4)
            };
            _parentGrid.Children.Add(progressLogoContainer);
            Grid.SetColumn(progressLogoContainer, 0);

            Image progressLogo = new Image()
            {
                Source = new BitmapImage(new Uri("ms-appx:///XAMLs/Prototype/honkai-logo.png")),
                Width = 64,
                Height = 64
            };
            progressLogoContainer.Children.Add(progressLogo);

            StackPanel progressStatusContainer = new StackPanel()
            {
                Margin = new Thickness(8, -4, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _parentGrid.Children.Add(progressStatusContainer);
            Grid.SetColumn(progressStatusContainer, 1);

            Grid progressStatusGrid = new Grid()
            {
                Margin = new Thickness(0, 0, 0, 16),
                ColumnDefinitions =
                {
                    new ColumnDefinition(),
                    new ColumnDefinition()
                },
                RowDefinitions =
                {
                    new RowDefinition(),
                    new RowDefinition()
                }
            };
            progressStatusContainer.Children.Add(progressStatusGrid);

            TextBlock progressLeftTitle = new TextBlock()
            {
                Style = Application.Current.Resources["BodyStrongTextBlockStyle"] as Style,
                Text = "Downloading Package: 1 / 3"
            };
            TextBlock progressLeftSubtitle = new TextBlock()
            {
                Style = Application.Current.Resources["CaptionTextBlockStyle"] as Style,
                Text = "Speed: 69.42 MB/s"
            };

            TextBlock progressRightTitle = new TextBlock()
            {
                Style = Application.Current.Resources["BodyStrongTextBlockStyle"] as Style,
                Text = "Estimated Time: 0h 32m left",
                HorizontalAlignment = HorizontalAlignment.Right
            };
            TextBlock progressRightSubtitle = new TextBlock()
            {
                Style = Application.Current.Resources["CaptionTextBlockStyle"] as Style,
                Text = "Progress: 69.42%",
                HorizontalAlignment = HorizontalAlignment.Right
            };
            progressStatusGrid.Children.Add(progressLeftTitle);
            progressStatusGrid.Children.Add(progressLeftSubtitle);
            progressStatusGrid.Children.Add(progressRightTitle);
            progressStatusGrid.Children.Add(progressRightSubtitle);
            Grid.SetColumn(progressLeftTitle, 0); Grid.SetRow(progressLeftTitle, 0);
            Grid.SetColumn(progressLeftSubtitle, 0); Grid.SetRow(progressLeftSubtitle, 1);
            Grid.SetColumn(progressRightTitle, 1); Grid.SetRow(progressRightTitle, 0);
            Grid.SetColumn(progressRightSubtitle, 1); Grid.SetRow(progressRightSubtitle, 1);

            ProgressBar progressBar = new ProgressBar() { Minimum = 0, Maximum = 100, Value = 69.42 };
            progressStatusContainer.Children.Add(progressBar);

            Button cancelButton = new Button()
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 4, 0, 0),
                CornerRadius = new CornerRadius(14),
                Style = Application.Current.Resources["AccentButtonStyle"] as Style,
                Content = new StackPanel()
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(4, 0, 4, 0),
                    Children =
                    {
                        new FontIcon()
                        {
                            FontFamily = Application.Current.Resources["FontAwesomeSolid"] as FontFamily,
                            Glyph = "",
                            FontSize = 18
                        },
                        new TextBlock()
                        {
                            Text = Lang._HomePage.PauseCancelDownloadBtn,
                            FontWeight = FontWeights.Medium,
                            Margin = new Thickness(8, -2, 0, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };

            cancelButton.Click += (sender, args) =>
            {
                Button btnSender = (Button)sender;
                btnSender.IsEnabled = false;
                activity.CancelRoutine();
                _parentNotifUI.IsOpen = false;
            };

            activity.ProgressChanged += (sender, args) =>
            {
                progressBar.Value = args.ProgressTotalPercentage;
                progressLeftSubtitle.Text = string.Format(Lang._Misc.Speed, ConverterTool.SummarizeSizeSimple(args.ProgressTotalSpeed));
                progressRightTitle.Text = string.Format(Lang._Misc.TimeRemainHMSFormat, args.ProgressTotalTimeLeft);
                progressRightSubtitle.Text = string.Format(Lang._UpdatePage.UpdateHeader1 + " {0}%", args.ProgressTotalPercentage);
            };

            activity.StatusChanged += (sender, args) =>
            {
                progressLeftTitle.Text = args.ActivityStatus;
                if (args.IsCanceled || args.IsCompleted)
                {
                    Button btnSender = (Button)sender;
                    btnSender.IsEnabled = false;
                    activity.CancelRoutine();
                    _parentNotifUI.IsOpen = false;
                }
            };

            _parentNotifUI.Closed += (sender, args) =>
            {
                Detach(hashID);
            };

            _parentContainer.Children.Add(cancelButton);

            NotificationSender.SendCustomNotification(hashID, _parentNotifUI);
        }

        private static void DetachEventFromNotification(int hashID)
        {
            // TODO: Detach the event to notification

            NotificationSender.RemoveCustomNotification(hashID);
        }
    }
}
