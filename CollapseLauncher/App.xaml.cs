using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Image;
using Hi3Helper;
using Hi3Helper.Shared.Region;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using PhotoSauce.MagicScaler;
using PhotoSauce.NativeCodecs.Libwebp;
using System;
using System.Linq;
using Windows.UI;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.InvokeProp;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    public partial class App
    {
        public static bool IsAppKilled = false;

        public App()
        {
            if (DebugSettings != null)
            {
#if ENABLEFRAMECOUNTER
                DebugSettings.EnableFrameRateCounter = true;
#endif
#if DEBUG
                DebugSettings.LayoutCycleDebugBreakLevel = LayoutCycleDebugBreakLevel.High;
                DebugSettings.LayoutCycleTracingLevel = LayoutCycleTracingLevel.High;
                DebugSettings.IsXamlResourceReferenceTracingEnabled = true;
                DebugSettings.IsBindingTracingEnabled = true;
#endif
                DebugSettings.XamlResourceReferenceFailed += static (sender, args) =>
                {
                    LogWriteLine($"[XAML_RES_REFERENCE] Sender: {sender}\r\n{args!.Message}", LogType.Error, true);
                #if !DEBUG
                    MainEntryPoint.SpawnFatalErrorConsole(new Exception(args!.Message));
                #endif
                    
                };
                DebugSettings.BindingFailed += static (sender, args) =>
                {
                    LogWriteLine($"[XAML_BINDING] Sender: {sender}\r\n{args!.Message}", LogType.Error, true);
                #if !DEBUG
                    MainEntryPoint.SpawnFatalErrorConsole(new Exception(args!.Message));
                #endif
                };
                UnhandledException += static (sender, e) =>
                {
                    LogWriteLine($"[XAML_OTHER] Sender: {sender}\r\n{e!.Exception} {e.Exception!.InnerException}", LogType.Error, true);
                #if !DEBUG
                    MainEntryPoint.SpawnFatalErrorConsole(e!.Exception);
                #endif
                };
            }

            RequestedTheme = IsAppThemeLight ? ApplicationTheme.Light : ApplicationTheme.Dark;
            SetPreferredAppMode(ShouldAppsUseDarkMode() ? PreferredAppMode.AllowDark : PreferredAppMode.Default);

            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                ThemeChangerInvoker.ThemeEvent += (_, _) => 
                  {
                      WindowUtility.ApplyWindowTitlebarLegacyColor();
                      bool  isThemeLight = IsAppThemeLight;
                      Color color        = isThemeLight ? Colors.Black : Colors.White;
                      Current!.Resources!["WindowCaptionForeground"] = color;

                      WindowUtility.CurrentAppWindow!.TitleBar!.ButtonForegroundColor = color;
                      WindowUtility.CurrentAppWindow!.TitleBar!.ButtonInactiveBackgroundColor = color;

                      if (WindowUtility.CurrentWindow!.Content is not null and FrameworkElement frameworkElement)
                          frameworkElement.RequestedTheme = isThemeLight ? ElementTheme.Light : ElementTheme.Dark;
                  };

                Window toInitializeWindow = null;
                switch (m_appMode)
                {
                    case AppMode.Updater:
                        toInitializeWindow = new UpdaterWindow();
                        break;
                    case AppMode.Hi3CacheUpdater:
                    case AppMode.Launcher:
                        toInitializeWindow = new MainWindow();
                        ((MainWindow)toInitializeWindow).InitializeWindowProperties();
                        break;
                    case AppMode.OOBEState:
                        toInitializeWindow = new MainWindow();
                        ((MainWindow)toInitializeWindow).InitializeWindowProperties(true);
                        break;
                    case AppMode.StartOnTray:
                        toInitializeWindow = new MainWindow();
                        ((MainWindow)toInitializeWindow).InitializeWindowProperties();
                        LogWriteLine("Running Collapse in Tray Mode!", LogType.Scheme);
                        break;
                }

                // Disable AppUserModelId for now as Windows doesn't respect it on non UWP apps
                //string appUserModelId = "Collapse.CollapseLauncher";
                //int setAUMIDResult = SetCurrentProcessExplicitAppUserModelID(appUserModelId);
                //if (setAUMIDResult != 0) LogWriteLine($"Error when setting AppUserModelId to {appUserModelId}. Error code: {setAUMIDResult}", LogType.Error, true);
                //else LogWriteLine($"Successfully set AppUserModelId to {appUserModelId}", LogType.Default, true);

                toInitializeWindow!.Activate();
                
                bool isAcrylicEnabled = LauncherConfig.GetAppConfigValue("EnableAcrylicEffect").ToBool();
                if (!isAcrylicEnabled) ToggleBlurBackdrop(false);
                if (m_appMode == AppMode.StartOnTray)
                {
                    WindowUtility.ToggleToTray_AllWindow();
                }

                if (m_appMode != AppMode.Updater && LauncherConfig.GetAppConfigValue("EnableWaifu2X").ToBool())
                {
                    ImageLoaderHelper.InitWaifu2X();
                }

                // Initialize support for MagicScaler's WebP decoding
                CodecManager.Configure(codecs =>
                {
                    codecs.UseLibwebp();
                });
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL ERROR ON APP INITIALIZER LEVEL!!!\r\n{ex}", LogType.Error, true);
                LogWriteLine("\r\nIf this is not intended, please report it to: https://github.com/CollapseLauncher/Collapse/issues\r\nPress any key to exit...");
                //Console.ReadLine();
                throw;
            }
        }

        public static void ToggleBlurBackdrop(bool useBackdrop = true)
        {
            // Enumerate the dictionary (MergedDictionaries)
            foreach (ResourceDictionary resource in Current!.Resources!.MergedDictionaries!)
            {
                // Parse the dictionary (ThemeDictionaries) and read the type of KeyValuePair<object, object>,
                // then select the value, get the type of ResourceDictionary, then enumerate it
                foreach (ResourceDictionary list in resource!
                    .ThemeDictionaries!
                    .Select(x => x.Value)
                    .OfType<ResourceDictionary>())
                {
                    // Parse the dictionary as type of KeyValuePair<object, object>,
                    // and get the value which has type of AcrylicBrush only, then enumerate it
                    foreach (AcrylicBrush theme in list
                        .Select(x => x.Value)
                        .OfType<AcrylicBrush>())
                    {
                        // Set the theme AlwaysUseFallback as per toggle from useBackdrop.
                        theme.AlwaysUseFallback = !useBackdrop;
                    }
                }
            }
        }
    }
}
