using CollapseLauncher.AnimatedVisuals.Lottie;
using CollapseLauncher.CustomControls;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Animation;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.Loading;
using CollapseLauncher.Pages;
using CollapseLauncher.Pages.OOBE;
using CollapseLauncher.Statics;
using CollapseLauncher.XAMLs.Theme.CustomControls.FullPageOverlay;
using CommunityToolkit.WinUI.Animations;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Native.LibraryImport;
using Microsoft.UI.Composition;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable RedundantExtendsListEntry
// ReSharper disable IdentifierTypo
// ReSharper disable AsyncVoidMethod

namespace CollapseLauncher
{
    public sealed partial class MainWindow : Window
    {
        private static bool _isForceDisableIntro;

        private static readonly Lock CriticalOpLock = new();

        internal static bool IsIntroEnabled
        {
            get => LauncherConfig.IsIntroEnabled;
            set => LauncherConfig.IsIntroEnabled = value;
        }

        public static bool IsCriticalOpInProgress
        {
            get;
            set
            {
                using (CriticalOpLock.EnterScope())
                {
                    var lastValue = field;
                    field = value;
                    
                    if (value)
                    {
                        ShutdownBlocker.StartBlocking(WindowUtility.CurrentWindowPtr, Locale.Lang._Dialogs.EnsureExitSubtitle,
                                                      ILoggerHelper.GetILogger("ShutdownBlocker"));
                    }
                    else if (lastValue)
                    {
                        ShutdownBlocker.StopBlocking(WindowUtility.CurrentWindowPtr,
                                                     ILoggerHelper.GetILogger("ShutdownBlocker"));
                    }
                }
            }
        }

        public void InitializeWindowProperties(bool startOobe = false)
        {
            try
            {
                InitializeWindowSettings();
                LoadingMessageHelper.Initialize();

                if (WindowUtility.CurrentWindowTitlebarExtendContent) WindowUtility.CurrentWindowTitlebarHeightOption = TitleBarHeightOption.Tall;

                if (IsFirstInstall || startOobe)
                {
                    _isForceDisableIntro = true;
                    WindowUtility.CurrentWindowTitlebarExtendContent = true;
                    WindowUtility.SetWindowSize(WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Width, WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Height);
                    WindowUtility.ApplyWindowTitlebarLegacyColor();
                    WindowUtility.CurrentWindowIsResizable = false;
                    WindowUtility.CurrentWindowIsMaximizable = false;
                    RootFrame.Navigate(typeof(OOBEStartUpMenu), null, new DrillInNavigationTransitionInfo());
                }
                else
                    StartMainPage();

                TitleBarFrameGrid.EnableImplicitAnimation(true);
                LoadingStatusUIContainer.EnableImplicitAnimation(true);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failure while initializing window properties!!!\r\n{ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                //Console.ReadLine();
                throw;
            }
        }

        public void StartMainPage()
        {
            WindowUtility.SetWindowSize(WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Width, WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Height);

            if (IsCrisisIntroEnabled())
            {
                RunCrisisIntroSequence();
            }
            else
            {
                RunIntroSequence();
            }

            RootFrame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
        }

        #region TEMPORARY: August 27th, 2025 Temporary Intro due to current Indonesian's crisis
        public bool IsEnableCrisisIntro
        {
            get => GetAppConfigValue("Enable20250827CrisisIntro");
            set => SetAndSaveConfigValue("Enable20250827CrisisIntro", value);
        }

        public bool IsShownCrisisIntroDialog
        {
            get => GetAppConfigValue("Enable20250827CrisisIntroDialog");
            set => SetAndSaveConfigValue("Enable20250827CrisisIntroDialog", value);
        }

        // Starts the intro at 00:00 AM Jakarta Time - September 1st -> 00:00 AM Jakarta Time - September 8th.
        private readonly DateTime _dateTimeCrisisOffsetStart
            = new DateTimeOffset(2025, 9, 1, 0, 0, 0, TimeSpan.FromHours(7)).UtcDateTime;
        private readonly DateTime _dateTimeCrisisOffsetEnd
            = new DateTimeOffset(2025, 9, 8, 0, 0, 0, TimeSpan.FromHours(7)).UtcDateTime;

        private bool IsCrisisIntroEnabled()
        {
            DateTime nowDateTimeOffset = DateTime.UtcNow;
            if (nowDateTimeOffset < _dateTimeCrisisOffsetStart ||
                nowDateTimeOffset >= _dateTimeCrisisOffsetEnd)
            {
                return false;
            }

            if (_isForceDisableIntro || !IsEnableCrisisIntro)
            {
                return false;
            }

            // Try to disable the intro if the user is using certain region/CultureInfo in their system.
            (string langId, string countryId)[] disabledLocale = [
                ("zh", "cn")
                ];
            string currentCulture = CultureInfo.CurrentUICulture.Name;


            return !disabledLocale.Any(x => currentCulture.StartsWith(x.langId, StringComparison.OrdinalIgnoreCase) &&
                                            currentCulture.EndsWith(x.countryId, StringComparison.OrdinalIgnoreCase));
        }

        private void Temporary20250827CrisisIntroButton_OnClick(object sender, RoutedEventArgs e)
        {
            string[] articles = [
                "https://www.thejakartapost.com/indonesia/2025/08/31/five-things-to-know-about-indonesias-deadly-unrest.html",
                "https://www.aljazeera.com/news/2025/8/30/three-killed-in-fire-at-indonesian-government-building-blamed-on-protesters",
                "https://www.aljazeera.com/news/2025/8/26/indonesian-police-clash-with-students-protesting-lawmakers-salaries",
                "https://www.aljazeera.com/video/inside-story/2025/8/30/whats-behind-widespread-unrest-in-indonesia"
            ];

            foreach (string article in articles)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = article,
                    UseShellExecute = true
                });
            }
        }

        private async void RunCrisisIntroSequence()
        {
            Temporary20250827CrisisIntro.Visibility = Visibility.Visible;
            IntroSequenceToggle.Visibility          = Visibility.Collapsed;
            IntroAnimation.Visibility               = Visibility.Visible;

            InputSystemCursor cursorType = InputSystemCursor.Create(InputSystemCursorShape.Hand);
            Temporary20250827CrisisIntro.SetAllControlsCursorRecursive(cursorType);

            RootFrameGrid.Opacity = 0;
            WindowUtility.SetWindowBackdrop(WindowBackdropKind.Mica);

            try
            {
                if (IsShownCrisisIntroDialog)
                {
                    while (m_mainPage is null)
                    {
                        await Task.Delay(250);
                    }

                    TextBlock textBlock = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap
                    }.AddTextBlockLine("Hi there, it's neon-nyan. Sorry to interrupt you here but we have some important announcement we would like to share with you regarding the current situation in Indonesia. Though, you can skip this announcement and use your launcher normally.")
                         .AddTextBlockNewLine(2)
                         .AddTextBlockLine("Would you like to read the announcement? (Duration: ~1 minute 20 seconds)");

                    ContentDialogResult result =
                        await SpawnDialog("[EN] Important Announcement",
                                          textBlock,
                                          closeText: "No, Skip it",
                                          primaryText: "Yes, I would like to read it",
                                          dialogTheme: ContentDialogTheme.Warning,
                                          defaultButton: ContentDialogButton.Close);

                    if (result == ContentDialogResult.None)
                    {
                        IsEnableCrisisIntro = false;
                        return;
                    }
                }

                // in frames
                const double animAnnounceStart = 0d;
                const double animIntroStart    = 4350d;
                const double animIntroPause    = 4740d;
                const double animIntroDur      = 4800d;

                IntroAnimation.Source = new TempResetIndonesiaTaglineCrisis(); // Directly create new instance and so it triggers SetSource early.
                {
                    IntroAnimation.AnimationOptimization = PlayerAnimationOptimization.Resources;
                    if (IsAppThemeLight)
                    {
                        ((TempResetIndonesiaTaglineCrisis)IntroAnimation.Source).Color_FFFFFF = Color.FromArgb(255, 30, 30, 30);
                    }

                    if (IsShownCrisisIntroDialog)
                    {
                        await IntroAnimation.PlayAsync(animAnnounceStart / animIntroDur, animIntroStart / animIntroDur, false);
                        IsShownCrisisIntroDialog = false;
                    }

                    await IntroAnimation.PlayAsync(animIntroStart / animIntroDur, animIntroPause / animIntroDur, false);
                    await Task.Delay(2500);
                    await IntroAnimation.PlayAsync(animIntroPause / animIntroDur, animIntroDur / animIntroDur, false);
                    IntroAnimation.Stop();
                }
                IntroAnimation.Source = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            finally
            {
                Task rootFrameAnimTask = RootFrameGrid.StartAnimation(TimeSpan.FromSeconds(0.75),
                                                                      RootFrameGrid.GetElementCompositor().CreateScalarKeyFrameAnimation("Opacity", 1, 0)
                                                                     );
                Task introFrameAnimTask = IntroAnimationGrid.StartAnimation(TimeSpan.FromSeconds(0.75),
                                                                            IntroAnimationGrid.GetElementCompositor().CreateScalarKeyFrameAnimation("Opacity", 0, 1)
                                                                           );

                _ = Task.WhenAll(rootFrameAnimTask, introFrameAnimTask);
                WindowUtility.SetWindowBackdrop(WindowBackdropKind.None);

                _isForceDisableIntro           = true;
                IntroSequenceToggle.Visibility = Visibility.Collapsed;
                IntroAnimationGrid.Visibility  = Visibility.Collapsed;
            }
        }
        #endregion

        private async void RunIntroSequence()
        {
            bool isIntroEnabled = IsIntroEnabled && !_isForceDisableIntro;
            RootFrameGrid.Opacity = 0;

            if (isIntroEnabled)
            {
                WindowUtility.SetWindowBackdrop(WindowBackdropKind.Mica);
                IAnimatedVisualSource2 newIntro = new NewLogoTitleIntro();
                {
                    IntroAnimation.Source = newIntro;
                    IntroAnimation.AnimationOptimization = PlayerAnimationOptimization.Resources;

                    IntroSequenceToggle.Visibility = Visibility.Visible;
                    IntroAnimation.Visibility = Visibility.Visible;
                    IntroAnimation.PlaybackRate = 1.5d;
                    await Task.Delay(500);
                    await IntroAnimation.PlayAsync(0, 600d / 600d, false);
                    IntroAnimation.Stop();
                }
                IntroAnimation.Source = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();

                Task rootFrameAnimTask = RootFrameGrid.StartAnimation(TimeSpan.FromSeconds(0.75),
                    RootFrameGrid.GetElementCompositor().CreateScalarKeyFrameAnimation("Opacity", 1, 0)
                    );
                Task introFrameAnimTask = IntroAnimationGrid.StartAnimation(TimeSpan.FromSeconds(0.75),
                    IntroAnimationGrid.GetElementCompositor().CreateScalarKeyFrameAnimation("Opacity", 0, 1)
                    );

                _ = Task.WhenAll(rootFrameAnimTask, introFrameAnimTask);
            }
            else
            {
                RootFrameGrid.Opacity = 1;
            }
            WindowUtility.SetWindowBackdrop(WindowBackdropKind.None);

            _isForceDisableIntro = true;
            IntroSequenceToggle.Visibility = Visibility.Collapsed;
            IntroAnimationGrid.Visibility = Visibility.Collapsed;

            if (isIntroEnabled) await Task.Delay(250);
        }

        private void InitializeAppWindowAndIntPtr()
        {
            InitializeComponent();
            Activate();

            // TODO: #671 This App.IsAppKilled will be replaced with cancellable-awaitable event
            //       to ensure no hot-exit being called before all background tasks
            //       hasn't being cancelled.
            // Closed += (_, _) => { App.IsAppKilled = true; };

            // Initialize Window Handlers and register to Window Utility
            this.RegisterWindow();

            string title = "Collapse";
            if (IsPreview)
                title += " Preview";
#if DEBUG
            title += " [Debug]";
#endif
            var instanceCount = MainEntryPoint.InstanceCount;
            if (instanceCount > 1)
                title += $" - #{instanceCount}";

            WindowUtility.CurrentWindowTitle = title;
        }

        private static void LoadWindowIcon()
        {
            WindowUtility.SetWindowTitlebarIcon(AppIconLarge, AppIconSmall);
            WindowUtility.CurrentWindowTitlebarIconShowOption = IconShowOptions.HideIconAndSystemMenu;
        }

        public void InitializeWindowSettings()
        {
            InitializeAppWindowAndIntPtr();
            LoadWindowIcon();

            WindowUtility.SetWindowSize(WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Width, WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Height);

            // Check to see if customization is supported.
            m_windowSupportCustomTitle = AppWindowTitleBar.IsCustomizationSupported();

            if (m_windowSupportCustomTitle)
            {
                WindowUtility.SetWindowTitlebarDefaultDragArea();
                WindowUtility.CurrentWindowTitlebarExtendContent = true;
                WindowUtility.CurrentWindowTitlebarIconShowOption = IconShowOptions.HideIconAndSystemMenu;

                WindowUtility.CurrentWindowIsResizable = false;
                WindowUtility.CurrentWindowIsMaximizable = false;

                if (WindowUtility.CurrentAppWindow != null)
                {
                    WindowUtility.CurrentAppWindow.TitleBar.ButtonBackgroundColor =
                        new Color { A = 0, B = 0, G = 0, R = 0 };
                    WindowUtility.CurrentAppWindow.TitleBar.ButtonInactiveBackgroundColor =
                        new Color { A = 0, B = 0, G = 0, R = 0 };
                }

                // Hide system menu
                var controlsHwnd = PInvoke.FindWindowEx(WindowUtility.CurrentWindowPtr, 0, "ReunionWindowingCaptionControls", "ReunionCaptionControlsWindow");
                if (controlsHwnd != IntPtr.Zero)
                {
                    PInvoke.DestroyWindow(controlsHwnd);
                }
            }
            else
            {
                // Shouldn't happen
                // https://learn.microsoft.com/en-us/windows/apps/develop/title-bar#colors

                if (WindowUtility.CurrentOverlappedPresenter != null && WindowUtility.CurrentWindow != null)
                {
                    WindowUtility.CurrentOverlappedPresenter.IsResizable = false;
                    WindowUtility.CurrentOverlappedPresenter.IsMaximizable = false;
                    WindowUtility.CurrentWindowTitlebarExtendContent = false;
                    AppTitleBar.Visibility = Visibility.Collapsed;
                }
            }

            MainFrameChangerInvoker.WindowFrameEvent       += MainFrameChangerInvoker_WindowFrameEvent;
            MainFrameChangerInvoker.WindowFrameGoBackEvent += MainFrameChangerInvoker_WindowFrameGoBackEvent;
            LauncherUpdateInvoker.UpdateEvent              += LauncherUpdateInvoker_UpdateEvent;
            ChangeTitleDragAreaInvoker.TitleBarEvent       += ChangeTitleDragAreaInvoker_TitleBarEvent;

            m_consoleCtrlHandler += ConsoleCtrlHandler;
            if (m_consoleCtrlHandler != null)
            {
                PInvoke.SetConsoleCtrlHandler(m_consoleCtrlHandler, true);
            }
        }

        private static bool ConsoleCtrlHandler(uint dwCtrlType)
        {
            ImageLoaderHelper.DestroyWaifu2X();
            return true;
        }

        private void LauncherUpdateInvoker_UpdateEvent(object sender, LauncherUpdateProperty e)
        {
            if (e.QuitFromUpdateMenu)
            {
                OverlayFrame.Navigate(typeof(NullPage), null, new EntranceNavigationTransitionInfo());
                return;
            }

            if (e.IsUpdateAvailable)
            {
                OverlayFrame.Navigate(typeof(UpdatePage));
            }
        }

        private void MainFrameChangerInvoker_WindowFrameEvent(object sender, MainFrameProperties e)
        {
            RootFrame.Navigate(e.FrameTo, null, e.Transition);
        }

        private void MainFrameChangerInvoker_WindowFrameGoBackEvent(object sender, EventArgs e)
        {
            if (RootFrame.CanGoBack)
                RootFrame.GoBack();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowUtility.WindowMinimize();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => _ = CloseApp();
        
        /// <summary>
        /// Close app and do necessary events before closing
        /// </summary>
        public async Task CloseApp()
        {
            MainFrameChangerInvoker.WindowFrameEvent       -= MainFrameChangerInvoker_WindowFrameEvent;
            MainFrameChangerInvoker.WindowFrameGoBackEvent -= MainFrameChangerInvoker_WindowFrameGoBackEvent;
            LauncherUpdateInvoker.UpdateEvent              -= LauncherUpdateInvoker_UpdateEvent;
            ChangeTitleDragAreaInvoker.TitleBarEvent       -= ChangeTitleDragAreaInvoker_TitleBarEvent;

            if (IsCriticalOpInProgress)
            {
                WindowUtility.WindowRestore();
                if (await Dialog_EnsureExit() != ContentDialogResult.Primary)
                    return;
            }

            GamePropertyVault.SafeDisposeVaults();
            SentryHelper.StopSentrySdk();
            _TrayIcon?.Dispose();
            Close();
            Application.Current.Exit();
        }
        
        private void MainWindow_OnSizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            // Recalculate non-client area size
            WindowUtility.EnableWindowNonClientArea();
        }

        private static void ChangeTitleDragAreaInvoker_TitleBarEvent(object sender, ChangeTitleDragAreaProperty e)
        {
            m_mainPage?.UpdateLayout();

            InputNonClientPointerSource nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(WindowUtility.CurrentWindowId ?? throw new NullReferenceException());
            WindowUtility.EnableWindowNonClientArea();
            WindowUtility.SetWindowTitlebarDragArea(MainPage.DragAreaMode_Full);

            if (!e.Template.HasFlag(DragAreaTemplate.OverlayOpened) ||
                FullPageOverlay.CurrentlyOpenedOverlays.LastOrDefault() is not { LayoutCloseButton: { } currentOverlayCloseButton })
            {
                switch (e.Template)
                {
                    case DragAreaTemplate.None:
                        nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, [
                            GetElementPos((WindowUtility.CurrentWindow as MainWindow)?.AppTitleBar)
                        ]);
                        break;
                    case DragAreaTemplate.Full:
                        nonClientInputSrc.ClearRegionRects(NonClientRegionKind.Passthrough);
                        break;
                    case DragAreaTemplate.Default:
                        nonClientInputSrc.ClearAllRegionRects();
                        RectInt32[] rects = m_mainPage != null ? [
                            GetElementPos(m_mainPage.GridBG_RegionGrid),
                            GetElementPos(m_mainPage.GridBG_IconGrid),
                            GetElementPos(m_mainPage.GridBG_NotifBtn),
                            GetElementPos((WindowUtility.CurrentWindow as MainWindow)?.MinimizeButton),
                            GetElementPos((WindowUtility.CurrentWindow as MainWindow)?.CloseButton)
                        ] : [
                            GetElementPos((WindowUtility.CurrentWindow as MainWindow)?.MinimizeButton),
                            GetElementPos((WindowUtility.CurrentWindow as MainWindow)?.CloseButton)
                        ];
                        nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, rects);
                        break;
                }
            }
            else
            {
                nonClientInputSrc.ClearAllRegionRects();
                nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, [
                    GetElementPos(currentOverlayCloseButton)
                ]);
            }

            nonClientInputSrc.SetRegionRects(NonClientRegionKind.Close, null);
            nonClientInputSrc.SetRegionRects(NonClientRegionKind.Minimize, null);
        }

        private static RectInt32 GetElementPos(FrameworkElement element)
        {
            GeneralTransform transformTransform = element.TransformToVisual(null);
            Rect bounds = transformTransform.TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            double scaleFactor = WindowUtility.CurrentWindowMonitorScaleFactor;

            return new RectInt32(
                                 _X: (int)Math.Round(bounds.X * scaleFactor),
                                 _Y: (int)Math.Round(bounds.Y * scaleFactor),
                                 _Width: (int)Math.Round(bounds.Width * scaleFactor),
                                 _Height: (int)Math.Round(bounds.Height * scaleFactor)
                                );
        }

        private void IntroSequenceToggle_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Compositor curCompositor = Compositor;
            UIElement element = sender as UIElement;
            element.StartAnimationDetached(TimeSpan.FromSeconds(0.50),
                    curCompositor.CreateScalarKeyFrameAnimation("Opacity", 1f)
                );
        }

        private void IntroSequenceToggle_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Compositor curCompositor = Compositor;
            UIElement element = sender as UIElement;
            element.StartAnimationDetached(TimeSpan.FromSeconds(0.50),
                    curCompositor.CreateScalarKeyFrameAnimation("Opacity", 0.25f)
                );
        }

        private void SetWindowCaptionLoadedCursor(object sender, RoutedEventArgs e)
            => (sender as UIElement)?.SetCursor(InputSystemCursor.Create(InputSystemCursorShape.Hand));
    }
}
