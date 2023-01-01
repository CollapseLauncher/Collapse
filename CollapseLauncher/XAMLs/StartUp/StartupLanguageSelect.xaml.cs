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
            string LangID = LanguageIDIndex[(sender as ComboBox).SelectedIndex];
            SetAndSaveConfigValue("AppLanguage", LangID);
            LoadLocale(LangID);
            NextBtn.IsEnabled = true;
        }

        private IEnumerable<string> LangList
        {
            get => LanguageNames.Select(x => $"{x.Value.LangName} ({x.Key} by {x.Value.LangAuthor})");
        }

        private void NextBtn_Click(object sender, RoutedEventArgs e) => (m_window as MainWindow).StartSetupPage();
    }
}