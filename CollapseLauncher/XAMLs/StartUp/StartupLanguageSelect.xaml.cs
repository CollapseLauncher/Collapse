using Hi3Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
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
            string LangID = LanguageNames.ToList()[(sender as ComboBox).SelectedIndex].Value.LangID;
            LoadLocalization(LangID);
            SetAndSaveConfigValue("AppLanguage", LangID);
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

        private void NextBtn_Click(object sender, RoutedEventArgs e) => (m_window as MainWindow).StartSetupPage();
    }
}