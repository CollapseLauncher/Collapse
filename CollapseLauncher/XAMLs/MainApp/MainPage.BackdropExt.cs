using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using static CollapseLauncher.InnerLauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class MainPage : Page
    {
        public void SetThemeParameters()
        {
            switch (m_currentBackdrop)
            {
                case BackdropType.DefaultColor:
                {
                    Background.Visibility = Visibility.Visible;
                    BackgroundAcrylicMask.Visibility = Visibility.Visible;
                }
                break;
            }
        }
    }
}
