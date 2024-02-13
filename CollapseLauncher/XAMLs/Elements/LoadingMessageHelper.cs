using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using CommunityToolkit.WinUI.Animations;

namespace CollapseLauncher.XAMLs.Elements
{
    internal static class LoadingMessageHelper
    {
        internal static MainWindow currentMainWindow = null;
        internal static bool isLoadingProgressIndeterminate = true;
        internal static bool isCurrentlyShow = false;

        internal static void Initialize()
        {
            if (InnerLauncherConfig.m_window is MainWindow window)
                currentMainWindow = window;
        }

        internal static void SetMessage(string title, string subtitle)
        {
            currentMainWindow.LoadingStatusTextTitle.Text = title;
            currentMainWindow.LoadingStatusTextSubtitle.Text = subtitle;
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
    }
}
