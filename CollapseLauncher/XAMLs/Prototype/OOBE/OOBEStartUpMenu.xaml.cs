using CollapseLauncher.FileDialogCOM;
using CommunityToolkit.WinUI.Controls;
using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.WindowSize.WindowSize;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages.OOBE
{
    public sealed partial class OOBEStartUpMenu : Page
    {
        public OOBEStartUpMenu()
        {
            this.InitializeComponent();
            MainWindow.ToggleAcrylic(true);
            ChangeFontIconSettingsCard(SettingsCardContainer.Children);
            MakeFlyoutBaseAcrylic(SelectLang);

            ThemeChangerInvoker.ThemeEvent += ThemeChangerInvoker_ThemeEvent;

            InitialTitleTextSize = (TitleTextContainer.Children[0] as TextBlock).FontSize;
            InitialFirstMainGridRowSize = ContainerGrid.ColumnDefinitions[0].MaxWidth;
        }

        private void ThemeChangerInvoker_ThemeEvent(object sender, ThemeProperty e) => RequestedTheme = IsAppThemeLight ? ElementTheme.Light : ElementTheme.Dark;

        #region Recursive Methods
        public async void MakeFlyoutBaseAcrylic(ComboBox comboBox)
        {
            await Task.Delay(5000);
        }
        #endregion

        #region UIStuffs
        private void ChangeFontIconSettingsCard(UIElementCollection uiCollection)
        {
            FontFamily iconFont = Application.Current.Resources["FontAwesomeSolid"] as FontFamily;
            foreach (object containerObject in uiCollection)
            {
                if (containerObject.GetType() == typeof(SettingsExpander))
                {
                    ChangeSettingsCardIconFont(containerObject, iconFont);
                    ChangeSettingsExpanderInnerIconFont(containerObject, iconFont);
                    return;
                }

                ChangeSettingsCardIconFont(containerObject, iconFont);
            }
        }

        private void ChangeSettingsExpanderInnerIconFont(object containerObject, FontFamily iconFont)
        {
            SettingsExpander settingsCard = containerObject as SettingsExpander;
            if (settingsCard.Items != null && settingsCard.Items.Count != 0)
            {
                foreach (object item in settingsCard.Items)
                {
                    ChangeSettingsCardIconFont(item, iconFont);
                }
            }
        }

        private void ChangeSettingsCardIconFont(object containerObject, FontFamily iconFont)
        {
            if (containerObject.GetType() == typeof(SettingsCard))
            {
                SettingsCard settingsCard = containerObject as SettingsCard;
                if (settingsCard.HeaderIcon == null) return;
                ((FontIcon)settingsCard.HeaderIcon).FontFamily = iconFont;
            }

            if (containerObject.GetType() == typeof(SettingsExpander))
            {
                SettingsExpander settingsCard = containerObject as SettingsExpander;
                if (settingsCard.HeaderIcon == null) return;
                ((FontIcon)settingsCard.HeaderIcon).FontFamily = iconFont;
            }
        }

        private void GridBG_Icon_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            UIElement obj = sender as UIElement;
            obj.Scale += new System.Numerics.Vector3(0.1f);
            obj.Opacity = 1f;
        }

        private void GridBG_Icon_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            UIElement obj = sender as UIElement;
            obj.Scale -= new System.Numerics.Vector3(0.1f);
            obj.Opacity = 0.8f;
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

        private void RefreshSelection()
        {
            SelectLang.SelectedIndex = -1;
            SelectLang.SelectedIndex = SelectedLangIndex;
            SelectWindowSize.SelectedIndex = -1;
            SelectWindowSize.SelectedIndex = SelectedWindowSizeProfile;
            SelectCDN.UpdateLayout();
            SelectCDN.SelectedIndex = SelectedCDN;

            UpdateBindings.Update();

            int lastAppThemeIndex = SettingsAppThemeCombobox.SelectedIndex;
            SettingsAppThemeCombobox.SelectedIndex = -1;
            SettingsAppThemeCombobox.UpdateLayout();
            SettingsAppThemeCombobox.SelectedIndex = lastAppThemeIndex;
            Bindings.Update();
            UpdateLayout();
        }

        private void SetTitleTextContainerSize(int size = 48)
        {
            Thickness lastCustomContainer = CustomizationContainer.Margin;
            lastCustomContainer.Top = size < 48 ? -64 : 0;
            CustomizationContainer.Margin = lastCustomContainer;

            foreach (TextBlock textBlock in TitleTextContainer.Children.OfType<TextBlock>())
            {
                textBlock.FontSize = size;
            }
        }

        private double InitialTitleTextSize = 0;
        private double InitialFirstMainGridRowSize = 0;
        private double SmallWindowFactor = 0.67d;

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
                ContainerGrid.ColumnDefinitions[0].MaxWidth = InitialFirstMainGridRowSize * (value == 0 ? 1d : (SmallWindowFactor + 0.04d));
                SetTitleTextContainerSize((int)(InitialTitleTextSize * (value == 0 ? 1d : SmallWindowFactor)));
            }
        }

        private static int GetInitialTheme()
        {
            string themeValue = GetAppConfigValue("ThemeMode").ToString();
            if (!Enum.TryParse(themeValue, true, out CurrentAppTheme))
            {
                CurrentAppTheme = SystemAppTheme.ToString() == "#FFFFFFFF" ? AppThemeMode.Light : AppThemeMode.Dark;
                LogWriteLine($"ThemeMode: {themeValue} is invalid! Falling back to Dark-mode (Valid values are: {string.Join(',', Enum.GetNames(typeof(AppThemeMode)))})", LogType.Warning, true);
            }

            return (int)CurrentAppTheme;
        }

        private int _SelectedTheme = GetInitialTheme();

        private int SelectedTheme
        {
            get { return _SelectedTheme; }
            set
            {
                if (value < 0) return;
                _SelectedTheme = value;
                ThemeChanger.ChangeTheme((ElementTheme)value);
            }
        }

        private List<string> WindowSizeProfilesKey = WindowSizeProfiles.Keys.ToList();
        #endregion

        #region LanguageSelectionStuffs
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

        private Dictionary<string, string> LangFallbackDict = new()
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

        private List<string> LangList = LanguageNames.Select(x => $"{x.Value.LangName} ({x.Key} by {x.Value.LangAuthor})").ToList();

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
        #endregion

        #region CDNStuffs
        private int SelectedCDN
        {
            get => GetAppConfigValue("CurrentCDN").ToInt();
            set
            {
                if (value < 0) return;
                SetAppConfigValue("CurrentCDN", value);
            }
        }

        private List<string> CDNNameList = CDNList.Select(x => x.Name).ToList();
        #endregion

        private async void CustomBackgroundCheckedOpen(object sender, RoutedEventArgs e)
        {
            CheckBox senderSource = sender as CheckBox;
            string selectedPath = await FileDialogNative.GetFilePicker(ImageLoaderHelper.SupportedImageFormats);
            if (string.IsNullOrEmpty(selectedPath))
            {
                senderSource.IsChecked = false;
                return;
            }

            Stream a = await ImageLoaderHelper.LoadImage(selectedPath, true, true);
            if (a != null)
                a.Dispose();
        }
    }
}
