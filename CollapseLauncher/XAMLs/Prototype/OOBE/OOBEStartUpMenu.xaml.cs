using CollapseLauncher.FileDialogCOM;
using CollapseLauncher.XAMLs.Elements;
using CommunityToolkit.WinUI.Animations;
using CommunityToolkit.WinUI.Controls;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Composition;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI;
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
            StartAsyncRoutines();

            ThemeChangerInvoker.ThemeEvent += ThemeChangerInvoker_ThemeEvent;

            InitialTitleTextSize = (TitleTextContainer.Children[0] as TextBlock).FontSize;
            InitialFirstMainGridRowSize = ContainerGrid.ColumnDefinitions[0].MaxWidth;

            RunIntroSequence();
        }

        ~OOBEStartUpMenu()
        {
            checkRecommendedCDNToken.Dispose();
        }

        #region Intro Sequence
        private async void RunIntroSequence()
        {
            TimeSpan logoAnimAppearanceDuration = TimeSpan.FromSeconds(1);

            await WelcomeVLogo.StartAnimation(logoAnimAppearanceDuration,
                currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 1, 0),
                currentCompositor.CreateVector3KeyFrameAnimation(
                    "Translation",
                    new Vector3(0, 0, 0),
                    new Vector3(0, 32, 0)));

            await SpawnWelcomeText();
            await Task.Delay(3000);

            await SpawnWelcomeText(true);
            await WelcomeVLogo.StartAnimation(logoAnimAppearanceDuration,
                currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 0, 1),
                currentCompositor.CreateVector3KeyFrameAnimation(
                    "Translation",
                    new Vector3(0, 32, 0),
                    new Vector3(0, 0, 0)));

            OOBEAgreementMenuExtensions.oobeStartParentUI = this;
            OverlayFrame.Navigate(typeof(OOBEAgreementMenu), null, new DrillInNavigationTransitionInfo());
        }

        public async void StartLauncherConfiguration()
        {
            OverlayFrame.Navigate(typeof(BlankPage), null, new DrillInNavigationTransitionInfo());
            MainUI.Visibility = Visibility.Visible;

            TimeSpan mainUIAnimAppearanceDuration = TimeSpan.FromSeconds(1);
            await MainUI.StartAnimation(mainUIAnimAppearanceDuration,
                currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 1, 0),
                currentCompositor.CreateVector3KeyFrameAnimation(
                    "Translation",
                    new Vector3(0, 0, 0),
                    new Vector3(0, 32, 0)));
        }

        private async ValueTask SpawnWelcomeText(bool isHide = false)
        {
            TimeSpan textAnimAppearanceDuration = TimeSpan.FromSeconds(0.25);
            if (isHide)
            {
                List<Task> animTask = new List<Task>();
                foreach (StackPanel panel in WelcomeVCarouselGrid.Children.OfType<StackPanel>())
                {
                    foreach (TextBlock text in panel.Children.OfType<TextBlock>())
                    {
                        animTask.Add(text.StartAnimation(textAnimAppearanceDuration,
                            currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 0, 1)));
                    }
                }
                await Task.WhenAll(animTask);
                return;
            }

            foreach (StackPanel panel in WelcomeVCarouselGrid.Children.OfType<StackPanel>())
            {
                foreach (TextBlock text in panel.Children.OfType<TextBlock>())
                {
                    await text.StartAnimation(textAnimAppearanceDuration,
                        currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 1, 0),
                        currentCompositor.CreateVector3KeyFrameAnimation(
                            "Translation",
                            new Vector3(0, 0, 0),
                            new Vector3(0, 16, 0)));
                }
            }
        }
        #endregion

        private void ThemeChangerInvoker_ThemeEvent(object sender, ThemeProperty e) => RequestedTheme = IsAppThemeLight ? ElementTheme.Light : ElementTheme.Dark;

        #region Async Methods
        private void StartAsyncRoutines()
        {
            // Check for the recommended CDN Latency
            GetRecommendedCDN();
        }

        private async void GetRecommendedCDN()
        {
            // Initialize the token source
            checkRecommendedCDNToken = new CancellationTokenSource();

            // Set the selected CDN to -1
            SelectCDN.SelectedIndex = -1;
            SelectCDN.PlaceholderText = "Checking CDN Recommendation...";
            SelectCDN.ItemsSource = CDNNameList.Select(x => x + " (Checking latency...)").ToList();

            LoadingMessageHelper.SetMessage("Initializing Launcher's First Start-up", "Checking CDN Recommendation...");
            LoadingMessageHelper.SetProgressBarState(100, true);
            LoadingMessageHelper.ShowLoadingFrame();

            // Get the CDN latencies
            int indexOfLatency = -1;
            long[] latencies = null;
            try
            {
                latencies = await FallbackCDNUtil.GetCDNLatencies(checkRecommendedCDNToken);
                long minLatency = latencies.Min();
                indexOfLatency = latencies.ToList().IndexOf(minLatency);
            }
            catch { }
            finally
            {
                LoadingMessageHelper.HideLoadingFrame();
            }
            if (indexOfLatency < 0 || latencies == null)
            {
                SelectCDN.ItemsSource = CDNNameList;
                SelectCDN.SelectedIndex = 0;
                SelectCDN.PlaceholderText = "";
                return;
            }

            recommendedCDNSelected = true;
            SelectCDN.PlaceholderText = "";
            TextBlock[] CDNNameListWithLatency = new TextBlock[latencies.Length];
            for (int i = 0; i < latencies.Length; i++)
            {
                string latencyString = " (" + (latencies[i] == long.MaxValue ? "- " : $"{latencies[i]}") + "ms)";
                CDNNameListWithLatency[i] = new TextBlock();
                CDNNameListWithLatency[i].Inlines.Add(new Run() { Text = CDNNameList[i] + latencyString });
                if (i == indexOfLatency)
                    CDNNameListWithLatency[i].Inlines.Add(new Run() { Text = " [Recommended]", FontWeight = FontWeights.SemiBold });
            }
            SelectCDN.ItemsSource = CDNNameListWithLatency;
            SelectCDN.SelectedIndex = indexOfLatency;
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
        private bool IsSelectLauncherFolderDone = false;
        private bool IsLauncherCustomizationDone = false;

        private Compositor currentCompositor = CompositionTarget.GetCompositorForCurrentThread();
        private List<string> WindowSizeProfilesKey = WindowSizeProfiles.Keys.ToList();

        private void ToggleEnableNextPageButton(bool enableNextPageButton)
        {
            NextPageButton.IsEnabled = enableNextPageButton;
            NextPageButton.Opacity = enableNextPageButton ? 1 : 0;
            ErrMsg.Opacity = enableNextPageButton ? 1 : 0;
        }

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
            obj.Scale += new Vector3(0.1f);
            obj.Opacity = 1f;
        }

        private void GridBG_Icon_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            UIElement obj = sender as UIElement;
            obj.Scale -= new Vector3(0.1f);
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
                SetAppConfigValue("WindowSizeProfile", CurrentWindowSizeName);
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
                SetAppConfigValue("ThemeMode", ((AppThemeMode)value).ToString());
            }
        }

        private async void CustomBackgroundCheckedOpen(object sender, RoutedEventArgs e)
        {
            CheckBox senderSource = sender as CheckBox;
            string selectedPath = await FileDialogNative.GetFilePicker(ImageLoaderHelper.SupportedImageFormats);
            senderSource.IsEnabled = false;
            if (string.IsNullOrEmpty(selectedPath))
            {
                senderSource.IsChecked = false;
                await ReplaceBackgroundImage(Path.Combine(AppImagesFolder, "PageBackground", "StartupBackground2.png"), IsAppThemeLight ? 0.2f : 0.175f, 0.5f);
                SetAppConfigValue("UseCustomBG", false);
                SetAppConfigValue("CustomBGPath", "");

                senderSource.IsEnabled = true;
                return;
            }

            LoadingMessageHelper.ShowLoadingFrame();
            LoadingMessageHelper.SetProgressBarState();
            LoadingMessageHelper.SetMessage("Loading Image", "The image might take a while to get processed.");
            Stream imageStream = await ImageLoaderHelper.LoadImage(selectedPath, true, true);
            if (imageStream != null)
            {
                await ReplaceBackgroundImage(imageStream, 0.5f, IsAppThemeLight ? 0.2f : 0.175f);
                SetAppConfigValue("UseCustomBG", true);
                SetAppConfigValue("CustomBGPath", selectedPath);
                imageStream.Dispose();
            }
            else
                senderSource.IsChecked = false;
            senderSource.IsEnabled = true;

            LoadingMessageHelper.HideLoadingFrame();
        }

        private async void CustomBackgroundCheckedClose(object sender, RoutedEventArgs e)
        {
            CheckBox senderSource = sender as CheckBox;
            senderSource.IsEnabled = false;
            await ReplaceBackgroundImage(Path.Combine(AppImagesFolder, "PageBackground", "StartupBackground2.png"), IsAppThemeLight ? 0.2f : 0.175f, 0.5f);
            senderSource.IsEnabled = true;
        }

        private async Task ReplaceBackgroundImage(string filePath, float fromOpacity = 0.25f, float toOpacity = 0.25f)
        {
            if (!File.Exists(filePath)) return;
            using (FileStream fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await ReplaceBackgroundImage(fileStream, fromOpacity, toOpacity);
            }
        }

        private async Task ReplaceBackgroundImage(Stream sourceStream, float fromOpacity = 0.25f, float toOpacity = 0.25f)
        {
            float toScale = 1.2f;
            float toTranslateX = -((float)ContainerBackgroundImage.ActualWidth * (toScale - 1f) / 2);
            float toTranslateY = -((float)ContainerBackgroundImage.ActualHeight * (toScale - 1f) / 2);

            TimeSpan transitionDuration = TimeSpan.FromSeconds(0.5);
            await ContainerBackgroundImage.StartAnimation(transitionDuration,
                currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 0, fromOpacity),
                currentCompositor.CreateVector3KeyFrameAnimation("Scale", new Vector3(toScale), new Vector3(1f)),
                currentCompositor.CreateVector3KeyFrameAnimation("Translation", new Vector3(toTranslateX, toTranslateY, 0), new Vector3(0))
            );
            ContainerBackgroundImage.Visibility = Visibility.Collapsed;
            ContainerBackgroundImage.Source = await ImageLoaderHelper.Stream2BitmapImage(sourceStream.AsRandomAccessStream());
            ContainerBackgroundImage.Visibility = Visibility.Visible;
            await ContainerBackgroundImage.StartAnimation(transitionDuration,
                currentCompositor.CreateScalarKeyFrameAnimation("Opacity", toOpacity, 0),
                currentCompositor.CreateVector3KeyFrameAnimation("Scale", new Vector3(1f), new Vector3(toScale)),
                currentCompositor.CreateVector3KeyFrameAnimation("Translation", new Vector3(0), new Vector3(toTranslateX, toTranslateY, 0))
            );
        }

        private void SaveInitialLogoAndTitleTextPos()
        {
            InitialMainContainerMargin = ContainerGrid.Margin;
            LastLogoInitialScale = CollapseLogoContainer.Scale;

            LastTitleTextInitialScale = TitleTextGrid.Scale;
            LastTitleTextInitialColumnSpan = Grid.GetColumnSpan(TitleTextGrid);
        }

        private void SetTitleTextContainerSize(int size = 48)
        {
            foreach (TextBlock textBlock in TitleTextContainer.Children.OfType<TextBlock>())
            {
                textBlock.FontSize = size;
            }
        }

        private async void ToggleLogoMode(bool shrink = false, bool isInSmallMode = false, bool isHideNextPageButton = true)
        {
            ErrMsg.RequestedTheme = IsAppThemeLight ? ElementTheme.Light : ElementTheme.Dark;
            Vector3 titleTextAnimScaleVect = new Vector3(isInSmallMode ? (float)SmallWindowFactor + 0.09f : (float)SmallWindowFactor - 0.075f);
            Vector3 logoAnimScaleVect = new Vector3(isInSmallMode ? (float)SmallWindowFactor - 0.2f : (float)SmallWindowFactor - 0.1f);

            TimeSpan animDuration = TimeSpan.FromMilliseconds(500);
            int titleTextXOffset = isInSmallMode ? 70 : 88;

            if (IsLastLogoShrinkMode == shrink)
            {
                TitleTextGrid.Scale = titleTextAnimScaleVect;

                Vector3 lastTitleTextPos = TitleTextGrid.Translation;
                lastTitleTextPos.X = titleTextXOffset;
                TitleTextGrid.Translation = lastTitleTextPos;

                CollapseLogoContainer.Scale = logoAnimScaleVect;
                return;
            }
            if ((IsLastLogoShrinkMode = shrink))
            {
                PrevPageButton.Visibility = Visibility.Visible;
                PrevPageButton.Opacity = 1;

                if (isHideNextPageButton)
                {
                    NextPageButton.Opacity = 0;
                    NextPageButton.IsEnabled = false;
                }
                PrevPageButton.IsEnabled = true;
                Grid.SetColumnSpan(TitleTextGrid, 2);
                await Task.WhenAll(
                    TitleTextGrid.StartAnimation(animDuration,
                        currentCompositor.CreateVector3KeyFrameAnimation(
                            "Scale",
                            titleTextAnimScaleVect,
                            LastTitleTextInitialScale.Value),
                        currentCompositor.CreateVector3KeyFrameAnimation(
                            "Translation",
                            new Vector3(titleTextXOffset, -128, 0),
                            new Vector3(0, 0, 0))),
                    CollapseLogoContainer.StartAnimation(animDuration,
                        currentCompositor.CreateVector3KeyFrameAnimation(
                            "Scale",
                            logoAnimScaleVect,
                            LastLogoInitialScale.Value)),
                    isHideNextPageButton ? NextPageButton.StartAnimation(animDuration,
                        currentCompositor.CreateVector3KeyFrameAnimation(
                            "Translation",
                            new Vector3(0, 16, 0),
                            new Vector3(0, 0, 0))) : Task.CompletedTask
                    );
                if (isHideNextPageButton)
                    NextPageButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                NextPageButton.Visibility = Visibility.Visible;
                NextPageButton.Opacity = 1;

                PrevPageButton.Opacity = 0;
                NextPageButton.IsEnabled = true;
                PrevPageButton.IsEnabled = false;
                Grid.SetColumnSpan(TitleTextGrid, LastTitleTextInitialColumnSpan);
                await Task.WhenAll(
                    TitleTextGrid.StartAnimation(animDuration,
                        currentCompositor.CreateVector3KeyFrameAnimation(
                            "Scale",
                            LastTitleTextInitialScale.Value,
                            titleTextAnimScaleVect),
                        currentCompositor.CreateVector3KeyFrameAnimation(
                            "Translation",
                            new Vector3(0, 0, 0),
                            new Vector3(titleTextXOffset, -128, 0))),
                    CollapseLogoContainer.StartAnimation(animDuration,
                        currentCompositor.CreateVector3KeyFrameAnimation(
                            "Scale",
                            LastLogoInitialScale.Value,
                            logoAnimScaleVect)),
                    isHideNextPageButton ? NextPageButton.StartAnimation(animDuration,
                        currentCompositor.CreateVector3KeyFrameAnimation(
                            "Translation",
                            new Vector3(0, 0, 0),
                            new Vector3(0, 32, 0))) : Task.CompletedTask
                    );
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
        private CancellationTokenSource checkRecommendedCDNToken = new CancellationTokenSource();
        private bool recommendedCDNSelected = false;

        private int SelectedCDN
        {
            get => GetAppConfigValue("CurrentCDN").ToInt();
            set
            {
                if (value < 0) return;
                try
                {
                    checkRecommendedCDNToken?.Cancel();
                }
                catch { }
                if (!recommendedCDNSelected) SelectCDN.ItemsSource = CDNNameList;
                SetAppConfigValue("CurrentCDN", value);
            }
        }

        private List<string> CDNNameList = CDNList.Select(x => x.Name).ToList();
        #endregion

        #region LauncherFolderSelection
        private async void ChooseFolder(object sender, RoutedEventArgs e)
        {
            string folder;
            bool Selected = false;
            switch (await Dialogs.SimpleDialogs.Dialog_LocateFirstSetupFolder(Content, Path.Combine(AppDataFolder, "GameFolder")))
            {
                case ContentDialogResult.Primary:
                    AppGameFolder = Path.Combine(AppDataFolder, "GameFolder");
                    SetAppConfigValue("GameFolder", AppGameFolder);
                    Selected = true;
                    break;
                case ContentDialogResult.Secondary:
                    folder = await FileDialogNative.GetFolderPicker();
                    if (!string.IsNullOrEmpty(folder))
                    {
                        if (!CheckIfFolderIsValid(folder))
                        {
                            await Dialogs.SimpleDialogs.Dialog_CannotUseAppLocationForGameDir(Content);
                            break;
                        }

                        if (ConverterTool.IsUserHasPermission(folder))
                        {
                            AppGameFolder = folder;
                            SetAppConfigValue("GameFolder", AppGameFolder);
                            Selected = true;
                            _log.SetFolderPathAndInitialize(AppGameLogsFolder, Encoding.UTF8);
                        }
                        else
                        {
                            ErrMsg.Text = Lang._StartupPage.FolderInsufficientPermission;
                        }
                    }
                    else
                    {
                        ErrMsg.Text = Lang._StartupPage.FolderNotSelected;
                    }
                    break;
            }

            if (Selected)
            {
                // NextPage.IsEnabled = true;
                ToggleEnableNextPageButton(true);
                ErrMsg.Text = $"✅ {AppGameFolder}";
                ErrMsg.TextWrapping = TextWrapping.Wrap;
                ErrMsg.RequestedTheme = IsAppThemeLight ? ElementTheme.Light : ElementTheme.Dark;
            }
            else
                ToggleEnableNextPageButton(false);
        }

        private bool CheckIfFolderIsValid(string basePath)
        {
            bool IsInAppFolderExist = File.Exists(Path.Combine(basePath, AppExecutableName))
                                   || File.Exists(Path.Combine($"{basePath}\\..\\", AppExecutableName))
                                   || File.Exists(Path.Combine($"{basePath}\\..\\..\\", AppExecutableName))
                                   || File.Exists(Path.Combine($"{basePath}\\..\\..\\..\\", AppExecutableName));

            string DriveLetter = Path.GetPathRoot(basePath);

            bool IsInAppFolderExist2 = basePath.EndsWith("Collapse Launcher")
                                    || basePath.StartsWith(Path.Combine(DriveLetter, "Program Files"))
                                    || basePath.StartsWith(Path.Combine(DriveLetter, "Program Files (x86)"))
                                    || basePath.StartsWith(Path.Combine(DriveLetter, "Windows"));

            return !(IsInAppFolderExist || IsInAppFolderExist2);
        }
        #endregion

        #region Prepare Metadata and Settings Apply
        private async void PrepareMetadataAndApplySettings()
        {
            if (!ConfigV2Store.IsConfigV2StampExist() || !ConfigV2Store.IsConfigV2ContentExist())
            {
                try
                {
                    LoadingMessageHelper.SetMessage(Lang._StartupPage.Pg1LoadingTitle1, Lang._StartupPage.Pg1LoadingSubitle1);
                    LoadingMessageHelper.SetProgressBarState(100, true);
                    LoadingMessageHelper.SetProgressBarValue(100);
                    LoadingMessageHelper.ShowLoadingFrame();

                    await DownloadConfigV2Files(true, true);
                    LoadingMessageHelper.SetMessage(Lang._StartupPage.Pg1LoadingTitle1, Lang._StartupPage.Pg1LoadingSubitle2);
                    LoadingMessageHelper.SetProgressBarState(100, false);
                    LoadingMessageHelper.SetProgressBarValue(100);
                    await Task.Delay(5000);

                    LoadingMessageHelper.HideLoadingFrame();
                }
                catch { }
            }

            await HideUIs(MainUI, IntroSequenceUI);
            OverlayFrame.Navigate(typeof(StartupPage_SelectGame), null, new DrillInNavigationTransitionInfo());
        }

        private async Task HideUIs(params FrameworkElement[] elements)
        {
            TimeSpan duration = TimeSpan.FromSeconds(0.25f);
            await Task.WhenAll(elements.Select(x =>
            {
                float toScale = 1.2f;
                float toTranslateX = -((float)x.ActualWidth * (toScale - 1f) / 2);
                float toTranslateY = -((float)x.ActualHeight * (toScale - 1f) / 2);

                Task task = x.StartAnimation(duration,
                        currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 0, (float)x.Opacity),
                        currentCompositor.CreateVector3KeyFrameAnimation("Scale", new Vector3(toScale), new Vector3(1f)),
                        currentCompositor.CreateVector3KeyFrameAnimation("Translation", new Vector3(toTranslateX, toTranslateY, 0), new Vector3(0))
                    );
                return task;
            }));

            foreach (var element in elements)
                element.Visibility = Visibility.Collapsed;
        }
        #endregion

        private async void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsSelectLauncherFolderDone)
            {
                await Task.WhenAll(
                    Task.Run(async () =>
                    {
                        DispatcherQueue.TryEnqueue(() => ToggleLogoMode(true, IsSmallSize, IsLauncherCustomizationDone));
                        await LauncherFolderContainer.StartAnimation(TimeSpan.FromSeconds(0.5),
                            currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 0, 1),
                            currentCompositor.CreateVector3KeyFrameAnimation("Translation", new Vector3(0, -32, 0), new Vector3(0, 0, 0)));
                        DispatcherQueue.TryEnqueue(() => LauncherFolderContainer.Visibility = Visibility.Collapsed);
                    }),
                    Task.Run(async () =>
                    {
                        DispatcherQueue.TryEnqueue(() => CustomizationContainer.Visibility = Visibility.Visible);
                        await CustomizationContainer.StartAnimation(TimeSpan.FromSeconds(0.5),
                            currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 1, 0),
                            currentCompositor.CreateVector3KeyFrameAnimation("Translation", new Vector3(0, 0, 0), new Vector3(0, 32, 0)));
                    }));
                IsSelectLauncherFolderDone = true;
                return;
            }

            if (!IsLauncherCustomizationDone)
            {
                PrepareMetadataAndApplySettings();
                IsLauncherCustomizationDone = true;
                return;
            }
        }

        private async void PrevPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsLauncherCustomizationDone)
            {
                IsLauncherCustomizationDone = false;
            }

            if (IsSelectLauncherFolderDone)
            {
                await Task.WhenAll(
                    Task.Run(async () =>
                    {
                        DispatcherQueue.TryEnqueue(() => ToggleLogoMode(false, IsSmallSize, IsLauncherCustomizationDone));
                        await CustomizationContainer.StartAnimation(TimeSpan.FromSeconds(0.5),
                            currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 0, 1),
                            currentCompositor.CreateVector3KeyFrameAnimation("Translation", new Vector3(0, -32, 0), new Vector3(0, 0, 0)));
                        DispatcherQueue.TryEnqueue(() => CustomizationContainer.Visibility = Visibility.Collapsed);
                    }),
                    Task.Run(async () =>
                    {
                        DispatcherQueue.TryEnqueue(() => LauncherFolderContainer.Visibility = Visibility.Visible);
                        await LauncherFolderContainer.StartAnimation(TimeSpan.FromSeconds(0.5),
                            currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 1, 0),
                            currentCompositor.CreateVector3KeyFrameAnimation("Translation", new Vector3(0, 0, 0), new Vector3(0, 32, 0)));
                    }));
                IsSelectLauncherFolderDone = false;
                return;
            }
        }
    }
}
