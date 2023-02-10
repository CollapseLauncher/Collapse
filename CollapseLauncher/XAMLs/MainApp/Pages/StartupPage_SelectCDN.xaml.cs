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
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static Hi3Helper.Logger;

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
            // Update string urls here, probably? Would be a good idea to create a central class for all GH outbound URLs
            // FIXME: Add localization for this page
            (m_window as MainWindow).rootFrame.Navigate(typeof(StartupPage_SelectGame), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight});
        }

        //private List<string> CDNList
        //{
        //    get
        //    {
        //        List<string> list = new List<string>();
        //        foreach (KeyValuePair<string, LangMetadata> entry in LanguageNames)
        //            list.Add($"{entry.Value.LangData.LanguageName} ({entry.Key}) by {entry.Value.LangData.Author}");
        //        return list;
        //    }
        //}

        private List<string> CDNList
        {
            get
            {
                List<string> list = new List<string>();
                list.Add($"Default (GitHub)");
                list.Add($"Statically");
                return list;
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string s = CDNSelect.SelectedItem.ToString();
            // LogWriteLine("Before if:" + s);
            // There is probably a better way to do this but it's 3AM and I don't feel like thinking about
            // Complex data types so it's a problem for either me or the person looking at this code. Sorry in advance ;-;
            if (s.Contains("Default")){
                SetAndSaveConfigValue("CDNType", "Default");
                AppNotifURLPrefix = AppNotifURLPrefix.Replace(AppNotifURLPrefix, "https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/main/notification_{0}.json");
                AppGameConfigURLPrefix = AppGameConfigURLPrefix.Replace(AppGameConfigURLPrefix, "https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/main/metadata/metadata_{0}.json");
                AppGameConfigV2URLPrefix = AppGameConfigV2URLPrefix.Replace(AppGameConfigV2URLPrefix, "https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/main/metadata/metadatav2_{0}.json");
                AppGameRepairIndexURLPrefix = AppGameRepairIndexURLPrefix.Replace(AppGameRepairIndexURLPrefix, "https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/main/metadata/repair_indexes/{0}/{1}/index");
                AppGameRepoIndexURLPrefix = AppGameRepoIndexURLPrefix.Replace(AppGameRepoIndexURLPrefix, "https://github.com/neon-nyan/CollapseLauncher-ReleaseRepo/main/metadata/repair_indexes/{0}/repo");
            } else
            {
                SetAndSaveConfigValue("CDNType", s);
                // 
                // 1: statically
                // This doesn't set 
                switch(s)
                {
                    case "Statically":
                        // there is probably a better way to do this instead of replacing the entire string (maybe prepending?)
                        // LogWriteLine(s);
                        // LogWriteLine("Statically case");
                        AppNotifURLPrefix = AppNotifURLPrefix.Replace(AppNotifURLPrefix, "https://cdn.statically.io/gh/neon-nyan/CollapseLauncher-ReleaseRepo/main/notification_{0}.json");
                        AppGameConfigURLPrefix = AppGameConfigURLPrefix.Replace(AppGameConfigURLPrefix, "https://cdn.statically.io/gh/neon-nyan/CollapseLauncher-ReleaseRepo/main/metadata/metadata_{0}.json");
                        AppGameConfigV2URLPrefix = AppGameConfigV2URLPrefix.Replace(AppGameConfigV2URLPrefix, "https://cdn.statically.io/gh/neon-nyan/CollapseLauncher-ReleaseRepo/main/metadata/metadatav2_{0}.json");
                        AppGameRepairIndexURLPrefix = AppGameRepairIndexURLPrefix.Replace(AppGameRepairIndexURLPrefix, "https://cdn.statically.io/gh/neon-nyan/CollapseLauncher-ReleaseRepo/main/metadata/repair_indexes/{0}/{1}/index");
                        AppGameRepoIndexURLPrefix = AppGameRepoIndexURLPrefix.Replace(AppGameRepoIndexURLPrefix, "https://cdn.statically.io/gh/neon-nyan/CollapseLauncher-ReleaseRepo/main/metadata/repair_indexes/{0}/repo");
                        break;
                }   
            }
            NextPage.IsEnabled = true;
            // LogWriteLine(GetAppConfigValue("CDNType").ToString());
            // LogWriteLine("-----------------");
            // LogWriteLine(AppNotifURLPrefix);
            // LogWriteLine(AppGameConfigURLPrefix);
            // LogWriteLine(AppGameConfigV2URLPrefix);
            // LogWriteLine(AppGameRepairIndexURLPrefix);
            // LogWriteLine(AppGameRepoIndexURLPrefix);
            // LogWriteLine("-----------------");
        }
    }
}
