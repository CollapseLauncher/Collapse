using Hi3Helper;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    public partial class App : Application
    {
        public static bool IsAppKilled = false;

        public App()
        {
            try
            {
                DebugSettings.XamlResourceReferenceFailed += (sender, args) => { LogWriteLine($"[XAML_RES_REFERENCE] {args.Message}", LogType.Error, true); };
                DebugSettings.BindingFailed += (sender, args) => { LogWriteLine($"[XAML_BINDING] {args.Message}", LogType.Error, true); };
                UnhandledException += (sender, e) => { LogWriteLine($"[XAML_OTHER] {e.Exception} {e.Exception.InnerException}", LogType.Error, true); };

                this.InitializeComponent();
                RequestedTheme = IsAppThemeLight ? ApplicationTheme.Light : ApplicationTheme.Dark;

                switch (m_appMode)
                {
                    case AppMode.Updater:
                        m_window = new UpdaterWindow();
                        break;
                    case AppMode.Hi3CacheUpdater:
                    case AppMode.Launcher:
                        m_window = new MainWindow();
                        ((MainWindow)m_window).InitializeWindowProperties();
                        break;
                    case AppMode.OOBEState:
                        m_window = new MainWindow();
                        ((MainWindow)m_window).InitializeWindowProperties(true);
                        break;
                }

                m_window.Activate();
                bool IsAcrylicEnabled = LauncherConfig.GetAppConfigValue("EnableAcrylicEffect").ToBool();
                if (!IsAcrylicEnabled) ToggleBlurBackdrop(false);
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL ERROR ON APP INITIALIZER LEVEL!!!\r\n{ex}", LogType.Error, true);
                LogWriteLine("\r\nIf this is not intended, please report it to: https://github.com/CollapseLauncher/Collapse/issues\r\nPress any key to exit...");
                Console.ReadLine();
            }
        }

        public static void ToggleBlurBackdrop(bool useBackdrop = true)
        {
            // Enumerate the dictionary (MergedDictionaries)
            foreach (ResourceDictionary resource in Current
                .Resources
                .MergedDictionaries)
            {
                // Parse the dictionary (ThemeDictionaries) and read the type of KeyValuePair<object, object>,
                // then select the value, get the type of ResourceDictionary, then enumerate it
                foreach (ResourceDictionary list in resource
                    .ThemeDictionaries
                    .OfType<KeyValuePair<object, object>>()
                    .Select(x => x.Value)
                    .OfType<ResourceDictionary>())
                {
                    // Parse the dictionary as type of KeyValuePair<object, object>,
                    // and get the value which has type of AcrylicBrush only, then enumerate it
                    foreach (AcrylicBrush theme in list
                        .OfType<KeyValuePair<object, object>>()
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
