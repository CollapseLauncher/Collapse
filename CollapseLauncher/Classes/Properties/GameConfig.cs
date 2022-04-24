using System;

using Windows.UI;

using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;

using Hi3Helper.Shared.ClassStruct;

namespace CollapseLauncher
{
    public static class AppConfig
    {
        public static IntPtr m_windowHandle;
        public static AppWindow m_AppWindow;
        public static OverlappedPresenter m_presenter;
        public static bool IsPreview = false;
        public static bool IsAppThemeNeedRestart = false;
        public static bool IsFirstInstall = false;
        public static ApplicationTheme CurrentRequestedAppTheme;
        public static AppThemeMode CurrentAppTheme;
        public static Color SystemAppTheme;

        public static ApplicationTheme GetAppTheme()
        {
            AppThemeMode AppTheme = CurrentAppTheme;

            switch (AppTheme)
            {
                case AppThemeMode.Light:
                    return ApplicationTheme.Light;
                case AppThemeMode.Dark:
                    return ApplicationTheme.Dark;
                default:
                    if (SystemAppTheme.ToString() == "#FFFFFFFF")
                        return ApplicationTheme.Light;
                    else
                        return ApplicationTheme.Dark;
            }
        }
    }
}
