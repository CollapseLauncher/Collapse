using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Animation;
using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace CollapseLauncher.Helper.Loading
{
    internal static class LoadingMessageHelper
    {
        internal static MainWindow currentMainWindow = null;
        internal static bool isLoadingProgressIndeterminate = true;
        internal static bool isCurrentlyShow = false;
        internal static List<RoutedEventHandler> currentActionButtonHandler = new List<RoutedEventHandler>();

        internal static void Initialize()
        {
            if (InnerLauncherConfig.m_window is MainWindow window)
                currentMainWindow = window;
        }

        internal static void SetMessage(string title, string subtitle)
        {
            if (!string.IsNullOrEmpty(title)) currentMainWindow.LoadingStatusTextTitle.Text = title;
            if (!string.IsNullOrEmpty(subtitle)) currentMainWindow.LoadingStatusTextSubtitle.Text = subtitle;
        }

        internal static void SetProgressBarValue(double value) => currentMainWindow.LoadingStatusProgressRing.Value = value;

        internal static void SetProgressBarState(double maxValue = 100d, bool isProgressIndeterminate = true)
        {
            isLoadingProgressIndeterminate = isProgressIndeterminate;
            currentMainWindow.LoadingStatusProgressRing.Maximum = maxValue;
            currentMainWindow.LoadingStatusProgressRing.IsIndeterminate = isLoadingProgressIndeterminate;
        }

        internal static async void ShowLoadingFrame()
        {
            if (isCurrentlyShow) return;
            currentMainWindow.LoadingStatusGrid.Visibility = Visibility.Visible;
            currentMainWindow.LoadingStatusGrid.Margin = new Thickness(0);
            currentMainWindow.LoadingStatusBackgroundGrid.Visibility = Visibility.Visible;
            await AnimationHelper.StartAnimation(currentMainWindow.LoadingStatusBackgroundGrid, TimeSpan.FromSeconds(0.25),
                currentMainWindow.LoadingStatusBackgroundGrid.GetElementCompositor().CreateScalarKeyFrameAnimation("Opacity", 1, 0));

            isCurrentlyShow = true;
        }

        internal static async void HideLoadingFrame()
        {
            if (!isCurrentlyShow) return;
            currentMainWindow.LoadingStatusGrid.Margin = new Thickness(0, 0, 0, -(currentMainWindow.LoadingStatusGrid.ActualHeight + 16));
            await AnimationHelper.StartAnimation(currentMainWindow.LoadingStatusBackgroundGrid, TimeSpan.FromSeconds(0.25),
                currentMainWindow.LoadingStatusBackgroundGrid.GetElementCompositor().CreateScalarKeyFrameAnimation("Opacity", 0, 1));
            currentMainWindow.LoadingStatusGrid.Visibility = Visibility.Collapsed;
            currentMainWindow.LoadingStatusBackgroundGrid.Visibility = Visibility.Collapsed;

            isCurrentlyShow = false;
        }

        internal static void ShowActionButton(object buttonContent, string buttonIconGlyph = null, RoutedEventHandler routedEvent = null)
        {
            if (routedEvent != null)
            {
                currentActionButtonHandler.Add(routedEvent);
                currentMainWindow.LoadingStatusActionButton.Click += routedEvent;
            }

            bool isHasIcon = !string.IsNullOrEmpty(buttonIconGlyph);
            currentMainWindow.LoadingStatusActionButtonIcon.Visibility = isHasIcon ? Visibility.Visible : Visibility.Collapsed;
            if (isHasIcon) currentMainWindow.LoadingStatusActionButtonIcon.Glyph = buttonIconGlyph;

            if (buttonContent is string buttonText)
            {
                TextBlock textBlock = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, isHasIcon ? -2 : 0, 0, 0)
                };
                textBlock.AddTextBlockLine(buttonText, FontWeights.Medium, 16);
                currentMainWindow.LoadingStatusActionButtonContentContainer.Children.Clear();
                currentMainWindow.LoadingStatusActionButtonContentContainer.AddElementToGridRowColumn(textBlock);
                return;
            }

            currentMainWindow.LoadingStatusActionButtonContentContainer.Children.Clear();
            currentMainWindow.LoadingStatusActionButtonContentContainer.AddElementToGridRowColumn(buttonContent as FrameworkElement);
            currentMainWindow.LoadingStatusActionButton.Visibility = Visibility.Visible;
        }

        internal static void HideActionButton()
        {
            if (currentActionButtonHandler.Count > 0)
            {
                for (int i = 0; i < currentActionButtonHandler.Count; i++)
                    currentMainWindow.LoadingStatusActionButton.Click -= currentActionButtonHandler[i];
                currentActionButtonHandler.Clear();
            }

            currentMainWindow.LoadingStatusActionButton.Visibility = Visibility.Collapsed;
        }
    }
}
