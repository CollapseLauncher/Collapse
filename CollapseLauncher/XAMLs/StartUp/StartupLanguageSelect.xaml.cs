using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Windows.Foundation;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class StartupLanguageSelect : Page
    {
        public StartupLanguageSelect()
        {
            try
            {
                this.InitializeComponent();
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL CRASH!!!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadLocalization(LanguageNames.ToList()[(sender as ComboBox).SelectedIndex].Value.LangID);
            NextBtn.IsEnabled = true;
        }

        private List<string> LangList
        {
            get
            {
                List<string> list = new List<string>();
                foreach (KeyValuePair<string, LangMetadata> entry in LanguageNames)
                    list.Add($"{entry.Key} ({entry.Value.LangID}) by {entry.Value.Author}");
                return list;
            }
        }

        private void NextBtn_Click(object sender, RoutedEventArgs e) => (App.m_window as MainWindow).StartSetupPage();
    }
}