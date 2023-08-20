using Hi3Helper;
using Hi3Helper.Shared.Region;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    public partial class App : Application
    {
        public static bool IsAppKilled = false;
        public static bool IsGameRunning = false;

        public App()
        {
            try
            {
                this.InitializeComponent();
                RequestedTheme = CurrentRequestedAppTheme = GetAppTheme();

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
                LogWriteLine("\r\nIf this is not intended, please report it to: https://github.com/neon-nyan/CollapseLauncher/issues\r\nPress any key to exit...");
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
