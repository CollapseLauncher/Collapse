using CollapseLauncher.AnimatedVisuals.Lottie;
using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.FileDialogCOM;
using CollapseLauncher.GameManagement.ImageBackground;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Animation;
using CollapseLauncher.Helper.Loading;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.XAMLs.Theme.CustomControls;
using CommunityToolkit.WinUI.Animations;
using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Win32.FileDialogCOM;
using Hi3Helper.Win32.Native.LibraryImport;
using Microsoft.UI.Composition;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.UI.Text;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.WindowSize.WindowSize;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable RedundantExtendsListEntry
// ReSharper disable CollectionNeverQueried.Local
// ReSharper disable InconsistentNaming
// ReSharper disable AsyncVoidMethod
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault

#nullable enable
namespace CollapseLauncher.Pages.OOBE
{
    // ReSharper disable once PartialTypeWithSinglePart
    public partial class OOBEStartUpMenu : Page
    {
        internal static OOBEStartUpMenu?       ThisCurrent;
        internal        ImageBackgroundManager OOBEBackgroundManager;

        public OOBEStartUpMenu()
        {
            ThisCurrent = this;
            OOBEBackgroundManager = new ImageBackgroundManager
            {
                GlobalParallaxHoverSource = this
            };

            InitializeComponent();
            WindowUtility.EnableWindowNonClientArea();
            SaveInitialLogoAndTitleTextPos();

            WindowUtility.SetWindowBackdrop(WindowBackdropKind.Mica);
            CustomizationContainer.EnableImplicitAnimation(true);

            ThemeChangerInvoker.ThemeEvent += ThemeChangerInvoker_ThemeEvent;

            InitialTitleTextSize        = (TitleTextContainer.Children[0] as TextBlock)?.FontSize ?? 0;
            InitialFirstMainGridRowSize = ContainerGrid.ColumnDefinitions[0].MaxWidth;

            StartAsyncRoutines();
        }

        ~OOBEStartUpMenu()
        {
            _checkRecommendedCDNToken.Dispose();
        }

        #region Intro Sequence

        private void CreateIntroWelcomeTextStack(Panel panel)
        {
            if (!(Locale.Current.Lang?._OOBEStartUpMenu?.WelcomeTitleString.ContainsKey("Upper") ?? false)
                || !(Locale.Current.Lang?._OOBEStartUpMenu?.WelcomeTitleString.ContainsKey("Lower") ?? false))
                // If either Upper or Lower is not exist, then use the default one from XAML
                return;

            // Assign the value from each dictionary
            string[] upperTexts = Locale.Current.Lang?._OOBEStartUpMenu?.WelcomeTitleString["Upper"] ?? [];
            string[] lowerTexts = Locale.Current.Lang?._OOBEStartUpMenu?.WelcomeTitleString["Lower"] ?? [];

            // Initial StackPanel for both upper and lower
            StackPanel upperStackPanel = UIElementExtensions.CreateStackPanel(Orientation.Horizontal);
            StackPanel lowerStackPanel = UIElementExtensions.CreateStackPanel(Orientation.Horizontal);

            // Clear the content and add the texts
            panel.Children.Clear();
            AddIntoTextToStackPanel(upperStackPanel, upperTexts, FontWeights.Normal, 32, 0);
            AddIntoTextToStackPanel(lowerStackPanel, lowerTexts, FontWeights.Bold,   48, 0);

            // Add the children StackPanel into parent StackPanel
            panel.AddElementToStackPanel(upperStackPanel);
            panel.AddElementToStackPanel(lowerStackPanel);
        }

        private static void AddIntoTextToStackPanel(StackPanel panel, string[] texts, FontWeight weight, double size,
                                                    double     initialOpacity)
        {
            foreach (string textString in texts)
            {
                panel.AddElementToStackPanel(new TextBlock
                {
                    Text = textString, FontWeight = weight, FontSize = size, Opacity = initialOpacity
                });
            }
        }

        private async void RunIntroSequence()
        {
            try
            {
                await Task.Delay(250);
                TimeSpan logoAnimAppearanceDuration = TimeSpan.FromSeconds(0.5);
                CreateIntroWelcomeTextStack(WelcomeVCarouselGrid);

                IAnimatedVisualSource2 newIntro = new NewLogoTitleIntro();
                {
                    WelcomeLogoIntro.Source                = newIntro;
                    WelcomeLogoIntro.AnimationOptimization = PlayerAnimationOptimization.Resources;

                    await WelcomeLogoIntro.PlayAsync(0, 0.0001d, false);
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    await WelcomeLogoIntro.PlayAsync(0, 488d / 600d, false);

                    // Adding delay and make sure the CDN recommendation has already been loaded
                    // before hiding the intro sequence
                    while (_isLoadingCDNRecommendation)
                    {
                        await Task.Delay(250);
                    }
                    await WelcomeLogoIntro.PlayAsync(488d / 600d, 570d / 600d, false);
                    WelcomeLogoIntro.Stop();
                    await WelcomeLogoIntro.StartAnimation(logoAnimAppearanceDuration,
                                                          currentCompositor.CreateVector3KeyFrameAnimation(
                                                               "Translation",
                                                               new Vector3(0, -138, 0),
                                                               WelcomeLogoIntro.Translation
                                                              ));
                    await SpawnWelcomeText();

                    await Task.Delay(1000);

                    await SpawnWelcomeText(true);
                    await WelcomeLogoIntro.StartAnimation(logoAnimAppearanceDuration,
                                                          currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 0, 1),
                                                          currentCompositor.CreateVector3KeyFrameAnimation(
                                                               "Translation",
                                                               new Vector3(0, -32, 0),
                                                               WelcomeLogoIntro.Translation
                                                              ));

                    OOBEAgreementMenuExtensions.OobeStartParentUI = this;
                    OverlayFrame.Navigate(typeof(OOBEAgreementMenu), null, new DrillInNavigationTransitionInfo());
                    WelcomeLogoIntro.Visibility     = Visibility.Collapsed;
                    WelcomeVCarouselGrid.Visibility = Visibility.Collapsed;
                }
                WelcomeLogoIntro.Source = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch
            {
                // ignored
            }
        }

        public async void StartLauncherConfiguration()
        {
            try
            {
                OverlayFrame.Navigate(typeof(BlankPage), null, new DrillInNavigationTransitionInfo());
                MainUI.Visibility = Visibility.Visible;

                TimeSpan mainUIAnimAppearanceDuration = TimeSpan.FromSeconds(0.5);
                await MainUI.StartAnimation(mainUIAnimAppearanceDuration,
                                            currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 1, 0),
                                            currentCompositor.CreateVector3KeyFrameAnimation(
                                                 "Translation",
                                                 new Vector3(0, 0,  0),
                                                 new Vector3(0, 32, 0)));
            }
            catch
            {
                // ignored
            }
        }

        private async ValueTask SpawnWelcomeText(bool isHide = false)
        {
            TimeSpan textAnimAppearanceDuration = TimeSpan.FromSeconds(0.25);
            if (isHide)
            {
                List<Task> animTask = [];
                foreach (StackPanel panel in WelcomeVCarouselGrid.Children.OfType<StackPanel>())
                {
                    animTask
                       .AddRange(panel.Children.OfType<TextBlock>()
                                      .Select(text => text.StartAnimation(textAnimAppearanceDuration, currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 0, 1))));
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
                                               new Vector3(0, 0,  0),
                                               new Vector3(0, 16, 0)));
                }
            }
        }

        #endregion

        private void ThemeChangerInvoker_ThemeEvent(object? sender, ThemeProperty e)
        {
            RequestedTheme = IsAppThemeLight ? ElementTheme.Light : ElementTheme.Dark;
        }

        #region Async Methods

        private void StartAsyncRoutines()
        {
            // Run intro sequence
            RunIntroSequence();

            // Check for the recommended CDN Latency
            GetRecommendedCDN();

            // Set the initial text container size
            InitializeTextContainerSize(null, false);
        }

        private long[]? _latencies;
        private int     _indexOfLatency = -1;
        private bool    _isLoadingCDNRecommendation;

        private async void GetRecommendedCDN()
        {
            // Initialize the token source
            _checkRecommendedCDNToken = new CancellationTokenSourceWrapper();

            // Set the selected CDN to -1
            SelectCDN.SelectedIndex   = -1;
            SelectCDN.PlaceholderText = Locale.Current.Lang?._OOBEStartUpMenu?.LoadingCDNCheckboxPlaceholder;
            SelectCDN.ItemsSource = _cdnNameList
                                   .Select(x => x + ' ' + Locale.Current.Lang?._OOBEStartUpMenu?.LoadingCDNCheckboxCheckLatency)
                                   .ToList();

            LoadingMessageHelper.SetMessage(Locale.Current.Lang?._OOBEStartUpMenu?.LoadingInitializationTitle,
                                            Locale.Current.Lang?._OOBEStartUpMenu?.LoadingCDNCheckingSubitle);
            LoadingMessageHelper.SetProgressBarState(0);
            LoadingMessageHelper.ShowLoadingFrame();
            LoadingMessageHelper.ShowActionButton(Locale.Current.Lang?._OOBEStartUpMenu?.LoadingCDNCheckingSkipButton, "",
                                                  (_, _) =>
                                                  {
                                                      if (!_checkRecommendedCDNToken.IsDisposed) _checkRecommendedCDNToken.Cancel();
                                                      LoadingMessageHelper.HideLoadingFrame();
                                                  });

            // Get the CDN latencies
            try
            {
                _isLoadingCDNRecommendation = true;
                _latencies                  = await FallbackCDNUtil.GetCDNLatencies(_checkRecommendedCDNToken);
                long minLatency = _latencies.Min();
                _indexOfLatency = _latencies.ToList().IndexOf(minLatency);
            }
            catch
            {
                // ignored
            }
            finally
            {
                _isLoadingCDNRecommendation = false;
                LoadingMessageHelper.HideLoadingFrame();
            }

            PrintCDNList();
        }

        private void PrintCDNList()
        {
            if (_indexOfLatency < 0 || _latencies == null)
            {
                SelectCDN.ItemsSource     = _cdnNameList;
                SelectCDN.SelectedIndex   = 0;
                SelectCDN.PlaceholderText = "";
                return;
            }

            _recommendedCDNSelected    = true;
            SelectCDN.PlaceholderText = "";
            TextBlock[] cdnNameListWithLatency = new TextBlock[_latencies.Length];
            for (int i = 0; i < _latencies.Length; i++)
            {
                string? latencyString = _latencies[i] == long.MaxValue
                    ? Locale.Current.Lang?._OOBEStartUpMenu?.CDNCheckboxItemLatencyUnknownFormat
                    : string.Format(Locale.Current.Lang?._OOBEStartUpMenu?.CDNCheckboxItemLatencyFormat ?? "", _latencies[i]);
                cdnNameListWithLatency[i] = new TextBlock();
                cdnNameListWithLatency[i].Inlines.Add(new Run { Text = _cdnNameList[i] + latencyString });
                if (i == _indexOfLatency)
                {
                    cdnNameListWithLatency[i].Inlines.Add(new Run
                    {
                        Text       = Locale.Current.Lang?._OOBEStartUpMenu?.CDNCheckboxItemLatencyRecommendedFormat,
                        FontWeight = FontWeights.SemiBold
                    });
                }
            }

            SelectCDN.ItemsSource   = cdnNameListWithLatency;
            SelectCDN.SelectedIndex = _indexOfLatency;
        }

        #endregion

        #region UIStuffs

        private          Thickness? InitialMainContainerMargin;
        private          Vector3?   LastLogoInitialScale;
        private          Vector3?   LastTitleTextInitialScale;
        private readonly double     InitialTitleTextSize;
        private readonly double     InitialFirstMainGridRowSize;
        private const    double     SmallWindowFactor = 0.67d;
        private          int        LastTitleTextInitialColumnSpan;

        private bool IsSmallSize;
        private bool IsLastLogoShrinkMode;
        private bool IsSelectLauncherFolderDone;
        private bool IsLauncherCustomizationDone;

        private readonly Compositor              currentCompositor = CompositionTarget.GetCompositorForCurrentThread();
        private readonly List<WindowSizeProfile> WindowSizeProfilesKey = [.. WindowSizeProfiles.Keys];

        private void ToggleEnableNextPageButton(bool enableNextPageButton)
        {
            NextPageButton.IsEnabled = enableNextPageButton;
            NextPageButton.Opacity   = enableNextPageButton ? 1 : 0;
            ErrMsg.Opacity           = enableNextPageButton ? 1 : 0;
        }

        private void GridBG_Icon_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            UIElement? obj = sender as UIElement;
            if (obj == null) return;

            obj.Scale   += new Vector3(0.1f);
            obj.Opacity =  1f;
        }

        private void GridBG_Icon_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            UIElement? obj = sender as UIElement;
            if (obj == null) return;

            obj.Scale   -= new Vector3(0.1f);
            obj.Opacity =  0.8f;
        }

        private int SelectedWindowSizeProfile
        {
            get
            {
                WindowSizeProfile val = CurrentWindowSizeName;
                return WindowSizeProfilesKey.IndexOf(val);
            }
            set
            {
                if (value < 0) return; 
                if (value > WindowSizeProfilesKey.Count - 1) value = 0;

                InitializeTextContainerSize(value);
            }
        }

        private static int GetInitialTheme()
        {
            string? themeValue = GetAppConfigValue("ThemeMode").ToString();
            if (Enum.TryParse(themeValue, true, out CurrentAppTheme)) return (int)CurrentAppTheme;

            CurrentAppTheme = !PInvoke.ShouldAppsUseDarkMode() ? AppThemeMode.Light : AppThemeMode.Dark;
            LogWriteLine($"ThemeMode: {themeValue} is invalid! Falling back to Dark-mode (Valid values are: {string.Join(',', Enum.GetNames(typeof(AppThemeMode)))})",
                         LogType.Warning, true);

            return (int)CurrentAppTheme;
        }

        private int SelectedTheme
        {
            get;
            set
            {
                if (value < 0) return;

                field = value;
                ThemeChanger.ChangeTheme((ElementTheme)value);
                SetAppConfigValue("ThemeMode", ((AppThemeMode)value).ToString());
            }
        } = GetInitialTheme();

        private async void CustomBackgroundCheckedOpen(object sender, RoutedEventArgs e)
        {
            CheckBox? senderSource = sender as CheckBox;
            if (senderSource == null) return;

            if (!IsLoaded)
            {
                return;
            }

            string selectedPath = await FileDialogNative.GetFilePicker(LayeredBackgroundImage.SupportedAllBackgroundFormatsFilter);
            if (!string.IsNullOrEmpty(selectedPath) && await OOBEBackgroundManager.SetGlobalCustomBackground(selectedPath))
            {
                OOBEBackgroundManager.GlobalIsEnableCustomImage = true;
                await StartBackgroundTransition(false, IsAppThemeLight ? 0.2f : 0.175f, 0.5f);
                return;
            }

            CustomBackgroundCheckedClose(sender, e);
        }

        private async void CustomBackgroundCheckedClose(object? sender, RoutedEventArgs e)
        {
            OOBEBackgroundManager.GlobalIsEnableCustomImage       = false;
            OOBEBackgroundManager.GlobalCustomBackgroundImagePath = null;
            await StartBackgroundTransition(true, IsAppThemeLight ? 0.2f : 0.175f, 0.5f);
        }

        private async Task StartBackgroundTransition(bool  isShow,
                                                     float fromOpacity = 0.25f,
                                                     float toOpacity   = 0.25f)
        {
            const float toScale      = 1.2f;
            float       toTranslateX = -((float)ContainerBackgroundImage.ActualWidth * (toScale - 1f) / 2);
            float       toTranslateY = -((float)ContainerBackgroundImage.ActualHeight * (toScale - 1f) / 2);

            TimeSpan transitionDuration = TimeSpan.FromSeconds(0.5);

            if (isShow)
            {
                ContainerBackgroundImage.Visibility = Visibility.Visible;
                await ContainerBackgroundImage.StartAnimation(transitionDuration,
                                                              currentCompositor.CreateScalarKeyFrameAnimation("Opacity", toOpacity, 0),
                                                              currentCompositor.CreateVector3KeyFrameAnimation("Scale", new Vector3(1f), new Vector3(toScale)),
                                                              currentCompositor.CreateVector3KeyFrameAnimation("Translation", new Vector3(0), new Vector3(toTranslateX, toTranslateY, 0))
                                                             );
                return;
            }

            await ContainerBackgroundImage.StartAnimation(transitionDuration,
                                                          currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 0, fromOpacity),
                                                          currentCompositor.CreateVector3KeyFrameAnimation("Scale", new Vector3(toScale), new Vector3(1f)),
                                                          currentCompositor.CreateVector3KeyFrameAnimation("Translation", new Vector3(toTranslateX, toTranslateY, 0), new Vector3(0))
                                                         );
            ContainerBackgroundImage.Visibility = Visibility.Collapsed;
        }

        private void SaveInitialLogoAndTitleTextPos()
        {
            InitialMainContainerMargin = ContainerGrid.Margin;
            LastLogoInitialScale       = CollapseLogoContainer.Scale;

            LastTitleTextInitialScale      = TitleTextGrid.Scale;
            LastTitleTextInitialColumnSpan = Grid.GetColumnSpan(TitleTextGrid);
        }

        private void SetTitleTextContainerSize(int size = 48)
        {
            foreach (TextBlock textBlock in TitleTextContainer.Children.OfType<TextBlock>())
            {
                textBlock.FontSize = size;
            }
        }

        private void InitializeTextContainerSize(int? indexOfKey = null, bool isNeedShrink = true)
        {
            if (indexOfKey == null)
            {
                WindowSizeProfile currentWindowSizeKeyName = CurrentWindowSizeName;
                indexOfKey = WindowSizeProfiles.Keys.ToList().IndexOf(currentWindowSizeKeyName);
                if (indexOfKey < 0)
                {
                    indexOfKey = 0;
                }
            }

            IsSmallSize           = indexOfKey != 0;
            CurrentWindowSizeName = WindowSizeProfilesKey[indexOfKey.Value];
            ContainerGrid.Margin = indexOfKey == 0 && InitialMainContainerMargin.HasValue
                ? InitialMainContainerMargin.Value
                : new Thickness((InitialMainContainerMargin?.Top ?? 0) * SmallWindowFactor);
            ContainerGrid.ColumnDefinitions[0].MaxWidth =
                InitialFirstMainGridRowSize * (indexOfKey == 0 ? 1d : SmallWindowFactor + 0.04d);
            SetTitleTextContainerSize((int)(InitialTitleTextSize * (indexOfKey == 0 ? 1d : SmallWindowFactor)));
            if (isNeedShrink) ToggleLogoMode(IsLastLogoShrinkMode, IsSmallSize);
        }

        private async void ToggleLogoMode(bool shrink               = false, bool isInSmallMode = false,
                                          bool isHideNextPageButton = true)
        {
            if (!LastTitleTextInitialScale.HasValue || !LastLogoInitialScale.HasValue) return;

            ErrMsg.RequestedTheme = IsAppThemeLight ? ElementTheme.Light : ElementTheme.Dark;
            Vector3 titleTextAnimScaleVect = new(isInSmallMode ? (float)SmallWindowFactor + 0.09f : (float)SmallWindowFactor - 0.075f);
            Vector3 logoAnimScaleVect = new(isInSmallMode ? (float)SmallWindowFactor - 0.2f : (float)SmallWindowFactor - 0.1f);

            TimeSpan animDuration     = TimeSpan.FromMilliseconds(500);
            int      titleTextXOffset = isInSmallMode ? 90 : 100;

            if (IsLastLogoShrinkMode == shrink)
            {
                TitleTextGrid.Scale = titleTextAnimScaleVect;

                Vector3 lastTitleTextPos = TitleTextGrid.Translation;
                lastTitleTextPos.X        = titleTextXOffset;
                TitleTextGrid.Translation = lastTitleTextPos;

                CollapseLogoContainer.Scale = logoAnimScaleVect;
                return;
            }

            // ReSharper disable once AssignmentInConditionalExpression
            if (IsLastLogoShrinkMode = shrink)
            {
                PrevPageButton.Visibility = Visibility.Visible;
                PrevPageButton.Opacity    = 1;

                if (isHideNextPageButton)
                {
                    NextPageButton.Opacity   = 0;
                    NextPageButton.IsEnabled = false;
                }

                PrevPageButton.IsEnabled = true;
                Grid.SetColumnSpan(TitleTextGrid, 2);
                await Task.WhenAll(
                    TitleTextGrid.StartAnimation(animDuration,
                                                 currentCompositor.CreateVector3KeyFrameAnimation(
                                                  "Scale",
                                                  titleTextAnimScaleVect,
                                                  LastTitleTextInitialScale),
                                                 currentCompositor.CreateVector3KeyFrameAnimation(
                                                  "Translation",
                                                  new Vector3(titleTextXOffset, -168, 0),
                                                  new Vector3(0,                0,    0))),
                    CollapseLogoContainer.StartAnimation(animDuration,
                                                         currentCompositor
                                                            .CreateVector3KeyFrameAnimation(
                                                              "Scale",
                                                              logoAnimScaleVect,
                                                              LastLogoInitialScale)),
                    isHideNextPageButton
                        ? NextPageButton.StartAnimation(animDuration,
                                                        currentCompositor.CreateVector3KeyFrameAnimation(
                                                         "Translation",
                                                         new Vector3(0, 16, 0),
                                                         new Vector3(0, 0,  0)))
                        : Task.CompletedTask
                );

                if (isHideNextPageButton)
                    NextPageButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                NextPageButton.Visibility = Visibility.Visible;
                NextPageButton.Opacity    = 1;

                PrevPageButton.Opacity   = 0;
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
                                                   new Vector3(0,                0,    0),
                                                   new Vector3(titleTextXOffset, -128, 0))),
                     CollapseLogoContainer.StartAnimation(animDuration,
                                                          currentCompositor
                                                             .CreateVector3KeyFrameAnimation(
                                                               "Scale",
                                                               LastLogoInitialScale.Value,
                                                               logoAnimScaleVect)),
                     isHideNextPageButton
                         ? NextPageButton.StartAnimation(animDuration,
                                                         currentCompositor.CreateVector3KeyFrameAnimation(
                                                          "Translation",
                                                          new Vector3(0, 0,  0),
                                                          new Vector3(0, 32, 0)))
                         : Task.CompletedTask
                );
                PrevPageButton.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region CDNStuffs

        private CancellationTokenSourceWrapper _checkRecommendedCDNToken = new();
        private bool                           _recommendedCDNSelected;

        private int SelectedCDN
        {
            get => GetAppConfigValue("CurrentCDN").ToInt();
            set
            {
                if (value < 0) return;
                try
                {
                    _checkRecommendedCDNToken.Cancel();
                }
                catch
                {
                    // ignored
                }

                if (!_recommendedCDNSelected) SelectCDN.ItemsSource = _cdnNameList;
                SetAppConfigValue("CurrentCDN", value);
            }
        }

        private readonly List<string> _cdnNameList = FallbackCDNUtil.CDNList.Select(x => x.Name).ToList();

        #endregion

        #region LauncherFolderSelection

        private async void ChooseFolder(object sender, RoutedEventArgs e)
        {
            bool   selected = false;
            switch (await SimpleDialogs.Dialog_LocateFirstSetupFolder(Path.Combine(AppDataFolder, "GameFolder")))
            {
                case ContentDialogResult.Primary:
                    AppGameFolder = Path.Combine(AppDataFolder, "GameFolder");
                    SetAppConfigValue("GameFolder", AppGameFolder);
                    selected = true;
                    break;
                case ContentDialogResult.Secondary:
                    string folder = await FileDialogHelper.GetRestrictedFolderPathDialog(Locale.Current.Lang?._Dialogs?.FolderDialogTitle1);
                    if (!string.IsNullOrEmpty(folder))
                    {
                        AppGameFolder = folder;
                        SetAppConfigValue("GameFolder", AppGameFolder);
                        selected = true;
                        CurrentLogger?.SetFolderPathAndInitialize(AppGameLogsFolder, Encoding.UTF8);
                    }
                    else
                    {
                        ToggleEnableNextPageButton(false);
                        ErrMsg.Text = Locale.Current.Lang?._StartupPage?.FolderNotSelected;
                    }

                    break;
            }

            if (selected)
            {
                // NextPage.IsEnabled = true;
                ToggleEnableNextPageButton(true);
                ErrMsg.Text           = $"✅ {AppGameFolder}";
                ErrMsg.TextWrapping   = TextWrapping.Wrap;
                ErrMsg.RequestedTheme = IsAppThemeLight ? ElementTheme.Light : ElementTheme.Dark;
            }
            else
            {
                ToggleEnableNextPageButton(false);
            }
        }

        
        #endregion

        #region Prepare Metadata and Settings Apply

        private async void PrepareMetadataAndApplySettings()
        {
            try
            {
                LoadingMessageHelper.SetMessage(Locale.Current.Lang?._StartupPage?.Pg1LoadingTitle1,
                                                Locale.Current.Lang?._StartupPage?.Pg1LoadingSubitle1);
                LoadingMessageHelper.SetProgressBarState();
                LoadingMessageHelper.SetProgressBarValue(100);
                LoadingMessageHelper.ShowLoadingFrame();

                await MetadataHelper.InitializeAsync(false);
                LoadingMessageHelper.SetMessage(Locale.Current.Lang?._StartupPage?.Pg1LoadingTitle1,
                                                Locale.Current.Lang?._StartupPage?.Pg1LoadingSubitle2);
                LoadingMessageHelper.SetProgressBarState(100, false);
                LoadingMessageHelper.SetProgressBarValue(100);
                await Task.Delay(2000);

                LoadingMessageHelper.HideLoadingFrame();
            }
            catch
            {
                // ignored
            }

            OOBEBackgroundManager.Pause(false);
            HideUIs(MainUI, IntroSequenceUI);
            OverlayFrame.Navigate(typeof(OOBESelectGame), null, null);
            await OverlayFrame.StartAnimation(TimeSpan.FromSeconds(0.5),
                                              currentCompositor.CreateVector3KeyFrameAnimation("Translation",
                                                  new Vector3(0),
                                                  new Vector3(0, (float)OverlayFrame.ActualHeight, 0)),
                                              currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 1, 0)
                                             );
        }

        private struct PreviousHiddenUIInfo
        {
            public FrameworkElement                                Element;
            public ValueTuple<float, Vector3, Vector3, Visibility> FromTuple;
            public ValueTuple<float, Vector3, Vector3, Visibility> ToTuple;
        }

        private readonly List<PreviousHiddenUIInfo> _previouslyHiddenUIsList = [];

        private async void HideUIs(params FrameworkElement[] elements)
        {
            _previouslyHiddenUIsList.Clear();
            TimeSpan duration = TimeSpan.FromSeconds(0.5f);

            List<Task> tasks = [];
            foreach (FrameworkElement element in elements)
            {
                (float, Vector3, Vector3, Visibility) fromTuple = ((float)element.Opacity, new Vector3(1f), new Vector3(0), element.Visibility);
                (float, Vector3, Vector3, Visibility) toTuple   = (0f, new Vector3(1f), new Vector3(0, -(float)element.ActualHeight, 0), Visibility.Collapsed);

                _previouslyHiddenUIsList.Add(new PreviousHiddenUIInfo
                {
                    Element = element,
                    FromTuple = fromTuple,
                    ToTuple = toTuple
                });

                ScalarKeyFrameAnimation opacityAnim = currentCompositor.CreateScalarKeyFrameAnimation("Opacity", toTuple.Item1, fromTuple.Item1);
                Vector3KeyFrameAnimation scaleAnim = currentCompositor.CreateVector3KeyFrameAnimation("Scale", toTuple.Item2, fromTuple.Item2);
                Vector3KeyFrameAnimation translationAnim = currentCompositor.CreateVector3KeyFrameAnimation("Translation", toTuple.Item3, fromTuple.Item3);

                Task task = element.StartAnimation(duration, opacityAnim, scaleAnim, translationAnim)
                                   .ContinueWith(_ =>
                                   {
                                       DispatcherQueue?.TryEnqueue(() => element.Visibility = toTuple.Item4);
                                   });
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
        }

        private async void ShowResetUIs()
        {
            TimeSpan   duration = TimeSpan.FromSeconds(0.5f);
            List<Task> animTask = [];

            foreach (PreviousHiddenUIInfo info in _previouslyHiddenUIsList)
            {
                FrameworkElement                      element   = info.Element;
                (float, Vector3, Vector3, Visibility) fromTuple = info.FromTuple;
                (float, Vector3, Vector3, Visibility) toTuple   = info.ToTuple;

                ScalarKeyFrameAnimation opacityAnim = currentCompositor.CreateScalarKeyFrameAnimation("Opacity", fromTuple.Item1, toTuple.Item1);
                Vector3KeyFrameAnimation scaleAnim = currentCompositor.CreateVector3KeyFrameAnimation("Scale", fromTuple.Item2, toTuple.Item2);
                Vector3KeyFrameAnimation translationAnim = currentCompositor.CreateVector3KeyFrameAnimation("Translation", fromTuple.Item3, toTuple.Item3);

                DispatcherQueue?.TryEnqueue(() => element.Visibility = fromTuple.Item4);
                Task task = element.StartAnimation(duration, opacityAnim, scaleAnim, translationAnim);
                animTask.Add(task);
            }

            await Task.WhenAll(animTask);
        }

        #endregion

        private async void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            Control element = (Control)sender;
            element.IsEnabled = false;
            element.Opacity   = 0;

            try
            {
                if (!IsSelectLauncherFolderDone)
                {
                    await Task.WhenAll(Task.Run(async () =>
                                       {
                                           DispatcherQueue?.TryEnqueue(() => ToggleLogoMode(true, IsSmallSize, IsLauncherCustomizationDone));
                                           await LauncherFolderContainer.StartAnimation(TimeSpan.FromSeconds(0.5),
                                                    currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 0, 1),
                                                    currentCompositor.CreateVector3KeyFrameAnimation("Translation", new Vector3(0, -32, 0), new Vector3(0, 0, 0))
                                               );

                                           DispatcherQueue?.TryEnqueue(() => LauncherFolderContainer.Visibility = Visibility.Collapsed);
                                       }),
                                       Task.Run(async () =>
                                       {
                                           DispatcherQueue?.TryEnqueue(() => CustomizationContainer.Visibility = Visibility.Visible);
                                           await CustomizationContainer.StartAnimation(TimeSpan.FromSeconds(0.5),
                                                    currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 1, 0),
                                                    currentCompositor.CreateVector3KeyFrameAnimation("Translation", new Vector3(0, 0, 0), new Vector3(0, 32, 0))
                                               );
                                       }));
                    IsSelectLauncherFolderDone = true;
                    return;
                }

                if (IsLauncherCustomizationDone) return;

                PrepareMetadataAndApplySettings();
                IsLauncherCustomizationDone = true;
            }
            finally
            {
                element.IsEnabled = true;
                element.Opacity   = 1;
            }
        }

        internal async void OverlayFrameGoBack()
        {
            if (!IsLauncherCustomizationDone || !OverlayFrame.CanGoBack) return;

            OOBEBackgroundManager.Play(false);
            IsLauncherCustomizationDone = false;
            OverlayFrame.GoBack();
            ShowResetUIs();
            await OverlayFrame.StartAnimation(TimeSpan.FromSeconds(0.5),
                                              currentCompositor.CreateVector3KeyFrameAnimation("Translation",
                                                       new Vector3(0, (float)OverlayFrame.ActualHeight, 0),
                                                       new Vector3(0)),
                                              currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 0, 1)
                                             );
        }

        private async void PrevPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsLauncherCustomizationDone) IsLauncherCustomizationDone = false;
            if (!IsSelectLauncherFolderDone) return;

            await Task.WhenAll(Task.Run(async () =>
                               {
                                   DispatcherQueue?.TryEnqueue(() => ToggleLogoMode(false, IsSmallSize, IsLauncherCustomizationDone));
                                   await CustomizationContainer.StartAnimation(TimeSpan.FromSeconds(0.5),
                                                                               currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 0, 1),
                                                                               currentCompositor.CreateVector3KeyFrameAnimation("Translation", new Vector3(0, -32, 0), new Vector3(0, 0, 0))
                                                                              );

                                   DispatcherQueue?.TryEnqueue(() => CustomizationContainer.Visibility = Visibility.Collapsed);
                               }),
                               Task.Run(async () =>
                               {
                                   DispatcherQueue?.TryEnqueue(() => LauncherFolderContainer.Visibility = Visibility.Visible);
                                   await LauncherFolderContainer.StartAnimation(TimeSpan.FromSeconds(0.5),
                                                                                    currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 1, 0),
                                                                                    currentCompositor.CreateVector3KeyFrameAnimation("Translation", new Vector3(0, 0, 0), new Vector3(0, 32, 0))
                                                                               );
                               }));
            IsSelectLauncherFolderDone = false;
        }

        private void RefreshCDNCheckButtonClick(object sender, RoutedEventArgs e)
        {
            GetRecommendedCDN();
        }

        private void FixComboBoxSize(object sender, SelectionChangedEventArgs e)
        {
            // Sync PlaceholderText with current selected item
            // Fix https://github.com/microsoft/microsoft-ui-xaml/issues/4551
            ComboBox? comboBox = sender as ComboBox;
            if (comboBox == null || comboBox.SelectedValue == null) return;

            string selectedValueString = comboBox.SelectedValue switch
            {
                ContentControl contentControl => contentControl.Content as string ?? "",
                TextBlock textBlock           => textBlock.Text,
                string str                    => str,
                _                             => ""
            };

            comboBox.PlaceholderText = selectedValueString;
        }

        private async void SelectAnyParallaxPixelAmount_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            ComboBox comboBox = (ComboBox)sender;

            int index = comboBox.SelectedIndex;
            if (index is < 0 or > 3) return;

            switch (index)
            {
                case 0:
                    OOBEBackgroundManager.GlobalBackgroundParallaxPixelShift = 2;
                    break;
                case 1:
                    OOBEBackgroundManager.GlobalBackgroundParallaxPixelShift = 4;
                    break;
                case 2:
                    OOBEBackgroundManager.GlobalBackgroundParallaxPixelShift = 8;
                    break;
                default:
                    await SimpleDialogs.Dialog_SelectCustomBackgroundParallaxPixels(OOBEBackgroundManager);
                    return;
            }
        }

        private void OpenAppSettingsOverlay_OnClick(object sender, RoutedEventArgs e)
        {
            GoBackFromSettingsPageBtn.Visibility = Visibility.Visible;
            SettingsPageFrame.Visibility         = Visibility.Visible;
            SettingsPageFrame.Navigate(typeof(SettingsPage));
        }

        private void GoBackFromOverlayFrame(object sender, RoutedEventArgs e)
        {
            SettingsPageFrame.Navigate(typeof(NullPage));

            if (sender is not Button btn)
            {
                return;
            }

            SettingsPageFrame.Visibility = Visibility.Collapsed;
            btn.Visibility               = Visibility.Collapsed;
        }

        private void OOBEStartUpMenu_OnLoaded(object sender, RoutedEventArgs e)
        {
            OOBEBackgroundManager.Initialize(BackgroundGrid);
            OOBEBackgroundManager.GlobalParallaxHoverSource = this;
        }
    }
}
