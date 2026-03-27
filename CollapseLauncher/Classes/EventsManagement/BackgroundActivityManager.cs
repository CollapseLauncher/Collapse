using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Interfaces;
using CollapseLauncher.Statics;
using Hi3Helper;
using Hi3Helper.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Numerics;
// ReSharper disable StringLiteralTypo
// ReSharper disable ClassNeverInstantiated.Global
#pragma warning disable IDE0130

namespace CollapseLauncher
{
    internal class BackgroundActivityManager
    {
        private static readonly ThemeShadow                                   InfoBarShadow        = new();
        private static readonly Dictionary<PresetConfig, IBackgroundActivity> BackgroundActivities = new();

        public static void Attach(PresetConfig presetConfig, IBackgroundActivity activity, string activityTitle, string activitySubtitle)
        {
            if (!BackgroundActivities.TryAdd(presetConfig, activity))
            {
                return;
            }

            DispatcherQueueExtensions.TryEnqueue(() => AttachEventToNotification(presetConfig, activity, activityTitle, activitySubtitle));
        #if DEBUG
            Logger.LogWriteLine($"Background activity for {presetConfig} has been attached", LogType.Debug, true);
        #endif
        }

        public static void Detach(PresetConfig presetConfig)
        {
            if (BackgroundActivities.Remove(presetConfig))
            {
                NotificationSender.RemoveCustomNotification(presetConfig.GetHashCode());
            #if DEBUG
                Logger.LogWriteLine($"Background activity for {presetConfig} has been detached", LogType.Debug, true);
                return;
            #endif
            }

        #if DEBUG
            Logger.LogWriteLine($"Cannot detach background activity for {presetConfig} because it doesn't attached", LogType.Debug, true);
        #endif
        }

        private static void AttachEventToNotification(PresetConfig presetConfig, IBackgroundActivity activity, string activityTitle, string activitySubtitle)
        {
            Thickness containerNotClosableMargin = new(-28, -8, 24, 20);
            Thickness containerClosableMargin    = new(-28, -8, -28, 20);

            Brush brush = UIElementExtensions.GetApplicationResource<Brush>("InfoBarAnnouncementBrush");
            InfoBar parentNotificationUI = new InfoBar
                                           {
                                               Tag        = presetConfig.GetHashCode(),
                                               Severity   = InfoBarSeverity.Informational,
                                               Background = brush,
                                               IsOpen     = true,
                                               IsClosable = false,
                                               Shadow     = InfoBarShadow,
                                               Title      = activityTitle,
                                               Message    = activitySubtitle
                                           }
            .WithMargin(4d, 4d, 4d, 0)
            .WithCornerRadius(8);
            parentNotificationUI.Translation = new Vector3(0,0,32);

            StackPanel parentContainer = UIElementExtensions.CreateStackPanel()
                .WithMargin(parentNotificationUI.IsClosable ? containerClosableMargin : containerNotClosableMargin);

            parentNotificationUI.Content = parentContainer;
            Grid parentGrid = parentContainer.AddElementToStackPanel(
                UIElementExtensions.CreateGrid()
                    .WithColumns(new GridLength(72), new GridLength(1, GridUnitType.Star))
            );

            StackPanel progressLogoContainer = parentGrid.AddElementToGridColumn(
                UIElementExtensions.CreateStackPanel()
                    .WithWidthAndHeight(64d)
                    .WithMargin(0d, 4d, 8d, 4d)
                    .WithCornerRadius(8),
                0
            );

            _ = progressLogoContainer.AddElementToStackPanel(
                new Image
                {
                    Source = new BitmapImage(new Uri(presetConfig.GameType switch
                    {
                        GameNameType.Honkai => "ms-appx:///Assets/Images/GameLogo/honkai-logo.png",
                        GameNameType.Genshin => "ms-appx:///Assets/Images/GameLogo/genshin-logo.png",
                        GameNameType.StarRail => "ms-appx:///Assets/Images/GameLogo/starrail-logo.png",
                        GameNameType.Zenless => "ms-appx:///Assets/Images/GameLogo/zenless-logo.png",
                        _ => "ms-appx:///Assets/Images/GameMascot/PaimonWhat.png"
                    }))
                }.WithWidthAndHeight(64));

            StackPanel progressStatusContainer = parentGrid.AddElementToGridColumn(
                UIElementExtensions.CreateStackPanel()
                    .WithVerticalAlignment(VerticalAlignment.Center)
                    .WithMargin(8d, -4d, 0, 0),
                1
            );

            Grid progressStatusGrid = progressStatusContainer.AddElementToStackPanel(
                UIElementExtensions.CreateGrid()
                    .WithColumns(new GridLength(1, GridUnitType.Star), new GridLength(1, GridUnitType.Star))
                    .WithRows(new GridLength(1,    GridUnitType.Star), new GridLength(1, GridUnitType.Star))
                    .WithMargin(0d, 0d, 0d, 16d)
            );

            TextBlock progressLeftTitle = progressStatusGrid.AddElementToGridRowColumn(new TextBlock
            {
                Style = UIElementExtensions.GetApplicationResource<Style>("BodyStrongTextBlockStyle"),
                Text = Locale.Current.Lang?._BackgroundNotification?.LoadingTitle
            });
            TextBlock progressLeftSubtitle = progressStatusGrid.AddElementToGridRowColumn(new TextBlock
            {
                Style = UIElementExtensions.GetApplicationResource<Style>("CaptionTextBlockStyle"),
                Text = Locale.Current.Lang?._BackgroundNotification?.Placeholder
            }, 1);

            TextBlock progressRightTitle = progressStatusGrid.AddElementToGridRowColumn(new TextBlock
            {
                Style = UIElementExtensions.GetApplicationResource<Style>("BodyStrongTextBlockStyle"),
                Text = Locale.Current.Lang?._BackgroundNotification?.Placeholder
            }.WithHorizontalAlignment(HorizontalAlignment.Right), 0, 1);
            TextBlock progressRightSubtitle = progressStatusGrid.AddElementToGridRowColumn(new TextBlock
            {
                Style = UIElementExtensions.GetApplicationResource<Style>("CaptionTextBlockStyle"),
                Text = Locale.Current.Lang?._BackgroundNotification?.Placeholder
            }.WithHorizontalAlignment(HorizontalAlignment.Right), 1, 1);

            ProgressBar progressBar = progressStatusContainer.AddElementToStackPanel(
                new ProgressBar { Minimum = 0, Maximum = 100, Value = 0, IsIndeterminate = true });

            Button cancelButton =
                UIElementExtensions.CreateButtonWithIcon<Button>(Locale.Current.Lang?._HomePage?.PauseCancelDownloadBtn,
                                                                 "",
                                                                 "FontAwesomeSolid",
                                                                 "AccentButtonStyle"
                )
                .WithHorizontalAlignment(HorizontalAlignment.Right)
                .WithMargin(0d, 4d, 0d, 0d);

            cancelButton.Click += (_, _) =>
            {
                cancelButton.IsEnabled = false;
                activity!.CancelRoutine();
                parentNotificationUI.IsOpen = false;
            };

            bool isGameInstaller = activity is IGameInstallManager;
            Button settingsButton =
                UIElementExtensions.CreateButtonWithIcon<Button>(
                    Locale.Current.Lang?._Dialogs?.DownloadSettingsTitle,
                    "\uf013",
                    "FontAwesomeSolid",
                    "AccentButtonStyle"
                )
                .WithHorizontalAlignment(HorizontalAlignment.Right)
                .WithMargin(0d, 4d, 8d, 0d);

            if (isGameInstaller)
            {
                settingsButton.Click += async (_, _) => await SimpleDialogs.Dialog_DownloadSettings((activity as IGameInstallManager)!);
            }
            else
            {
                settingsButton.Visibility = Visibility.Collapsed;
            }

            StackPanel controlButtons = parentContainer.AddElementToStackPanel(
                UIElementExtensions.CreateStackPanel(Orientation.Horizontal)
                    .WithHorizontalAlignment(HorizontalAlignment.Right)
            );

            controlButtons.AddElementToStackPanel(settingsButton, cancelButton);

            EventHandler<TotalPerFileProgress> progressChangedEventHandler = (_, args) => activity?.Dispatch(() =>
            {
                progressBar.Value = args!.ProgressAllPercentage;
                progressLeftSubtitle.Text = string.Format(Locale.Current.Lang?._Misc?.Speed ?? "", ConverterTool.SummarizeSizeSimple(args.ProgressAllSpeed));
                progressRightTitle.Text = string.Format(Locale.Current.Lang?._Misc?.TimeRemainHMSFormat ?? "", args.ProgressAllTimeLeft);
                progressRightSubtitle.Text = string.Format(Locale.Current.Lang?._UpdatePage?.UpdateHeader1 + " {0}%", args.ProgressAllPercentage);
            });

            EventHandler<TotalPerFileStatus> statusChangedEventHandler = (_, args) => activity?.Dispatch(() =>
            {
                progressBar.IsIndeterminate = args!.IsProgressAllIndetermined;
                progressLeftTitle.Text = args.ActivityStatus;
                if (args.IsCanceled)
                {
                    cancelButton.IsEnabled = false;
                    settingsButton.IsEnabled = false;
                    controlButtons.Visibility = Visibility.Collapsed;
                    parentNotificationUI.Severity = InfoBarSeverity.Error;
                    parentNotificationUI.Title = string.Format(Locale.Current.Lang?._BackgroundNotification?.NotifBadge_Error!, activityTitle);
                    parentNotificationUI.IsClosable = true;
                    parentContainer.Margin = containerClosableMargin;
                }
                if (args.IsCompleted)
                {
                    cancelButton.IsEnabled = false;
                    settingsButton.IsEnabled = false;
                    controlButtons.Visibility = Visibility.Collapsed;
                    parentNotificationUI.Severity = InfoBarSeverity.Success;
                    parentNotificationUI.Title = string.Format(Locale.Current.Lang?._BackgroundNotification?.NotifBadge_Completed!, activityTitle);
                    parentNotificationUI.IsClosable = true;
                    parentContainer.Margin = containerClosableMargin;
                }

                if (!args.IsRunning)
                {
                    return;
                }

                cancelButton.IsEnabled          = true;
                settingsButton.IsEnabled        = true;
                controlButtons.Visibility       = Visibility.Visible;
                parentNotificationUI.Severity   = InfoBarSeverity.Informational;
                parentNotificationUI.Title      = activityTitle;
                parentNotificationUI.IsClosable = false;
                parentContainer.Margin          = containerNotClosableMargin;
            });

            activity!.ProgressChanged += progressChangedEventHandler;
            activity!.StatusChanged += statusChangedEventHandler;

            activity.FlushingTrigger += (_, _) =>
            {
                activity.ProgressChanged -= progressChangedEventHandler;
                activity.StatusChanged -= statusChangedEventHandler;
            };

            parentNotificationUI.Closing += (_, _) =>
            {
                activity.ProgressChanged -= progressChangedEventHandler;
                activity.StatusChanged -= statusChangedEventHandler;
                Detach(presetConfig);
            };

            NotificationSender.SendCustomNotification(presetConfig.GetHashCode(), parentNotificationUI);
        }
    }
}
