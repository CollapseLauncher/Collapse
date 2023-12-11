using Hi3Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.WindowSize.WindowSize;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class StartupLanguageSelect : Page
    {

        private List<string> WindowSizeProfilesKey = WindowSizeProfiles.Keys.ToList();

        public StartupLanguageSelect()
        {
            try
            {
                this.InitializeComponent();
                MenuPanel.Translation += Shadow32;
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL CRASH!!!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private static Dictionary<string, string> LangFallbackDict = new()
        {
            { "en", "en-us" },
            { "zh", "zh-cn" },
            { "de", "de-de" },
            { "es", "es-419" },
            { "id", "id-id" },
            { "ja", "ja-jp" },
            { "ko", "ko-kr" },
            { "pl", "pl-pl" },
            { "pt", "pt-br" },
            { "ru", "ru-ru" },
            { "th", "th-th" },
            { "vi", "vi-vn" },
        };

        private string LanguageFallback(string tag)
        {
            tag = tag.ToLower();
            // Traditional Chinese
            if (tag == "zh-hant" || tag == "zh-hk" || tag == "zh-mo" || tag == "zh-tw") return "zh-tw";
            // Portuguese, Portugal
            if (tag == "pt-pt") return "pt-pt";
            if (tag.Length < 2) return "en-us";
            tag = tag.Substring(0, 2);
            return LangFallbackDict.GetValueOrDefault(tag, "en-us");
        }

        private void RefreshSelection()
        {
            SelectLang.SelectedIndex = -1;
            SelectLang.SelectedIndex = SelectedLangIndex;
            SelectWindowSize.SelectedIndex = -1;
            SelectWindowSize.SelectedIndex = SelectedWindowSizeProfile;
            SelectCDN.UpdateLayout();
            SelectCDN.SelectedIndex = SelectedCDN;
        }

        private int SelectedLangIndex
        {
            get
            {
                var langID = GetAppConfigValue("AppLanguage").ToString() ?? LanguageFallback(CultureInfo.InstalledUICulture.Name);
                return LanguageNames[langID.ToLower()].LangIndex;
            }
            set
            {
                if (value < 0) return;
                var langID = LanguageIDIndex[value].ToLower();
                if (langID == GetAppConfigValue("AppLanguage").ToString().ToLower()) return;
                SetAppConfigValue("AppLanguage", langID);
                LoadLocale(langID);

                // Update the view
                LogWriteLine("Updating the view...");
                Bindings.Update();
                LogWriteLine("Update bindings done.");
                RefreshSelection();
                LogWriteLine("Refresh controls done.");
            }
        }

        private void SelectLang_OnDropDownOpened(object sender, object e)
        {
            // The dropdown panel collides with non-client area, making the first item not clickable.
            // Use this to disable whole non-client area temporarily.
            (m_window as MainWindow).DisableNonClientArea();
        }

        private void SelectLang_OnDropDownClosed(object sender, object e)
        {
            // And restore the nc area to normal state.
            (m_window as MainWindow).EnableNonClientArea();
        }

        private IEnumerable<string> LangList
        {
            get => LanguageNames.Select(x => $"{x.Value.LangName} ({x.Key} by {x.Value.LangAuthor})");
        }

        private void NextBtn_Click(object sender, RoutedEventArgs e) => (m_window as MainWindow).StartSetupPage();

        private int SelectedWindowSizeProfile
        {
            get
            {
                string val = CurrentWindowSizeName;
                return WindowSizeProfilesKey.IndexOf(val);
            }
            set
            {
                if (value < 0) return;
                CurrentWindowSizeName = WindowSizeProfilesKey[value];
            }
        }

        private int SelectedCDN
        {
            get => GetAppConfigValue("CurrentCDN").ToInt();
            set
            {
                if (value < 0) return;
                SetAppConfigValue("CurrentCDN", value);
            }
        }
    }
}
