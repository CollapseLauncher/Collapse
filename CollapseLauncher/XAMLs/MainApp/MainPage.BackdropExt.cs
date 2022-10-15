using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using static CollapseLauncher.InnerLauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class MainPage : Page
    {
        public void SetThemeParameters()
        {
            if (!m_windowSupportCustomTitle)
            {
                GridBG_RegionMargin.Width = new GridLength(0, GridUnitType.Pixel);
                GridBG_RegionGrid.HorizontalAlignment = HorizontalAlignment.Left;
                GridBG_RegionInner.HorizontalAlignment = HorizontalAlignment.Left;
            }

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
