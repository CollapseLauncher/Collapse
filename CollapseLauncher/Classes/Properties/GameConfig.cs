using System;
using Microsoft.UI.Windowing;

namespace CollapseLauncher
{
    public static class AppConfig
    {
        public static IntPtr m_windowHandle;
        public static AppWindow m_AppWindow;
        public static OverlappedPresenter m_presenter;
        public static bool IsPreview = false;
    }
}
