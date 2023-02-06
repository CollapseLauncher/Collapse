// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static CollapseLauncher.InnerLauncherConfig;

namespace CollapseLauncher.Pages
{
    public sealed partial class StartupPage_SelectCDN : Page
    {
        public StartupPage_SelectCDN()
        {
            this.InitializeComponent();
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            NextPage.IsEnabled = true;
            (m_window as MainWindow).rootFrame.Navigate(typeof(StartupPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            // Update string urls here
            (m_window as MainWindow).rootFrame.Navigate(typeof(StartupPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight});
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
