using Hi3Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using static Hi3Helper.Logger;
// ReSharper disable RedundantExtendsListEntry

namespace CollapseLauncher.Prototype
{
    public partial class MainPageNew : Page
    {
        public MainPageNew()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL CRASH!!!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void nvSample_PaneOpening(NavigationView sender, object args)
        {
            Thickness curMargin = GridBG_Icon.Margin;
            curMargin.Left = 48;
            GridBG_Icon.Margin = curMargin;
            GridBG_IconTitle.Width = double.NaN;
            if (PreviewBuildIndicator.Visibility == Visibility.Collapsed)
                GridBG_IconTitle.Visibility = Visibility.Visible;
        }

        private void nvSample_PaneClosing(NavigationView sender, NavigationViewPaneClosingEventArgs args)
        {
            Thickness curMargin = GridBG_Icon.Margin;
            curMargin.Left = 58;
            GridBG_IconTitle.Width = 0;
            GridBG_Icon.Margin = curMargin;
            if (PreviewBuildIndicator.Visibility == Visibility.Collapsed)
                GridBG_IconTitle.Visibility = Visibility.Collapsed;
        }

        private void NotificationContainerBackground_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
        }
    }
}
