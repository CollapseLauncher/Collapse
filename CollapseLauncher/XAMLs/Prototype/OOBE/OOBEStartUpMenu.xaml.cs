using CollapseLauncher.FileDialogCOM;
using CommunityToolkit.WinUI.Animations;
using CommunityToolkit.WinUI.Controls;
using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
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
            SaveInitialLogoAndTitleTextPos();

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
        private Thickness? InitialMainContainerMargin = null;
        private Vector3? LastLogoInitialScale = null;
        private Vector3? LastTitleTextInitialScale = null;
        private double InitialTitleTextSize = 0;
        private double InitialFirstMainGridRowSize = 0;
        private double SmallWindowFactor = 0.67d;
        private int LastTitleTextInitialColumnSpan = 0;

        private bool IsSmallSize = false;
        private bool IsLastLogoShrinkMode = false;

        private Compositor currentCompositor = CompositionTarget.GetCompositorForCurrentThread();
        private List<string> WindowSizeProfilesKey = WindowSizeProfiles.Keys.ToList();

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
            MainWindow.DisableNonClientArea();
        }

        private void SelectLang_OnDropDownClosed(object sender, object e)
        {
            // And restore the nc area to normal state.
            MainWindow.EnableNonClientArea();
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
                ContainerGrid.Margin = value == 0 ? InitialMainContainerMargin.Value : new Thickness(InitialMainContainerMargin.Value.Top * SmallWindowFactor);
                ContainerGrid.ColumnDefinitions[0].MaxWidth = InitialFirstMainGridRowSize * (value == 0 ? 1d : (SmallWindowFactor + 0.04d));
                SetTitleTextContainerSize((int)(InitialTitleTextSize * (value == 0 ? 1d : SmallWindowFactor)));
                ToggleLogoMode(IsLastLogoShrinkMode, IsSmallSize = value != 0);
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

        private void SaveInitialLogoAndTitleTextPos()
        {
            InitialMainContainerMargin = ContainerGrid.Margin;
            LastLogoInitialScale = CollapseLogoContainer.Scale;

            LastTitleTextInitialScale = TitleTextGrid.Scale;
            LastTitleTextInitialColumnSpan = Grid.GetColumnSpan(TitleTextGrid);
        }

        private async void ToggleLogoMode(bool shrink = false, bool isInSmallMode = false)
        {
            Vector3 titleTextAnimScaleVect = new Vector3(isInSmallMode ? (float)SmallWindowFactor + 0.09f : (float)SmallWindowFactor - 0.075f);
            Vector3 logoAnimScaleVect = new Vector3(isInSmallMode ? (float)SmallWindowFactor - 0.2f : (float)SmallWindowFactor - 0.1f);

            TimeSpan animDuration = TimeSpan.FromMilliseconds(250);
            Vector3KeyFrameAnimation titleTextAnimScale,
                                     titleTextAnimOffset,
                                     logoAnimScale;

            int titleTextXOffset = isInSmallMode ? 70 : 88;

            if (IsLastLogoShrinkMode == shrink) return;
            if ((IsLastLogoShrinkMode = shrink))
            {
                titleTextAnimScale = currentCompositor.CreateVector3KeyFrameAnimation(
                    "Scale",
                    titleTextAnimScaleVect,
                    LastTitleTextInitialScale.Value);
                titleTextAnimScale.Duration = animDuration;
                titleTextAnimOffset = currentCompositor.CreateVector3KeyFrameAnimation(
                    "Translation",
                    new Vector3(titleTextXOffset, -128, 0),
                    new Vector3(0, 0, 0));
                titleTextAnimOffset.Duration = animDuration;

                logoAnimScale = currentCompositor.CreateVector3KeyFrameAnimation(
                    "Scale",
                    logoAnimScaleVect,
                    LastLogoInitialScale.Value);
                logoAnimScale.Duration = animDuration;

                Grid.SetColumnSpan(TitleTextGrid, 2);
            }
            else
            {
                titleTextAnimScale = currentCompositor.CreateVector3KeyFrameAnimation(
                    "Scale",
                    LastTitleTextInitialScale.Value,
                    titleTextAnimScaleVect);
                titleTextAnimScale.Duration = animDuration;
                titleTextAnimOffset = currentCompositor.CreateVector3KeyFrameAnimation(
                    "Translation",
                    new Vector3(0, 0, 0),
                    new Vector3(titleTextXOffset, -128, 0));
                titleTextAnimOffset.Duration = animDuration;

                logoAnimScale = currentCompositor.CreateVector3KeyFrameAnimation(
                    "Scale",
                    LastLogoInitialScale.Value,
                    logoAnimScaleVect);
                logoAnimScale.Duration = animDuration;

                Grid.SetColumnSpan(TitleTextGrid, LastTitleTextInitialColumnSpan);
            }

            TitleTextGrid.StartAnimation(titleTextAnimScale);
            TitleTextGrid.StartAnimation(titleTextAnimOffset);
            CollapseLogoContainer.StartAnimation(logoAnimScale);

            if (shrink)
            {
                PrevPageButton.Visibility = Visibility.Visible;
                PrevPageButton.Opacity = 1;

                NextPageButton.Opacity = 0;
                await Task.Delay(200);
                NextPageButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                NextPageButton.Visibility = Visibility.Visible;
                NextPageButton.Opacity = 1;

                PrevPageButton.Opacity = 0;
                await Task.Delay(200);
                PrevPageButton.Visibility = Visibility.Collapsed;
            }
        }
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

        private void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleLogoMode(true, IsSmallSize);
        }

        private void PrevPageButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleLogoMode(false, IsSmallSize);
        }
    }
}
