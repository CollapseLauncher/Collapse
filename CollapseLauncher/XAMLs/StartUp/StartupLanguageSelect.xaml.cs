using Hi3Helper;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Windows.Graphics;
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

        private string LanguageFallback(string tag)
        {
            tag = tag.ToLower();
            // English
            if (tag == "en" || tag.StartsWith("en-")) return "en-us";
            // Traditional Chinese
            if (tag == "zh-hant" || tag == "zh-hk" || tag == "zh-mo" || tag == "zh-tw") return "zh-tw";
            // Chinese defaults to zh-cn
            if (tag == "zh" || tag.StartsWith("zh-")) return "zh-cn";
            // German
            if (tag == "de" || tag.StartsWith("de-")) return "de-de";
            // Spanish
            if (tag == "es" || tag.StartsWith("es-")) return "es-419";
            // Indonesian
            if (tag == "id" || tag.StartsWith("id-")) return "id-id";
            // Japanese
            if (tag == "ja" || tag.StartsWith("ja-")) return "ja-jp";
            // Korean
            if (tag == "ko" || tag.StartsWith("ko-")) return "ko-kr";
            // Polish
            if (tag == "pl" || tag.StartsWith("pl-")) return "pl-pl";
            // Portuguese, Portugal
            if (tag == "pt-pt") return "pt-pt";
            // Portuguese defaults to pt-br
            if (tag == "pt" || tag.StartsWith("pt-")) return "pt-br";
            // Russian
            if (tag == "ru" || tag.StartsWith("ru-")) return "ru-ru";
            // Thai
            if (tag == "th" || tag.StartsWith("th-")) return "th-th";
            // Vietnamese
            if (tag == "vi" || tag.StartsWith("vi-")) return "vi-vn";
            // Default
            return "en-us";
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
                Bindings.Update();
                RefreshSelection();
            }
        }

        private void SelectLang_OnDropDownOpened(object sender, object e)
        {
            // Fix mouse event
            var incps = InputNonClientPointerSource.GetForWindowId(m_windowID);
            var safeArea = new RectInt32[] { new(0, 0, m_appWindow.Size.Width, (int)(48 * m_appDPIScale)) };
            incps.SetRegionRects(NonClientRegionKind.Passthrough, safeArea);
        }

        private void SelectLang_OnDropDownClosed(object sender, object e)
        {
            // Fix mouse event
            var incps = InputNonClientPointerSource.GetForWindowId(m_windowID);
            var safeArea = new RectInt32[] { new(m_appWindow.Size.Width - (int)((144 + 12) * m_appDPIScale), 0, (int)((144 + 12) * m_appDPIScale), (int)(48 * m_appDPIScale)) };
            incps.SetRegionRects(NonClientRegionKind.Passthrough, safeArea);
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
