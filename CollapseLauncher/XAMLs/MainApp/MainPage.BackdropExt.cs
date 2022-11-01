using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using static CollapseLauncher.InnerLauncherConfig;

namespace CollapseLauncher
{
    public partial class MainPage : Page
    {
        public void SetThemeParameters()
        {
            if (!m_windowSupportCustomTitle)
            {
                GridBG_RegionMargin.Width = new GridLength(0, GridUnitType.Pixel);
                GridBG_RegionGrid.HorizontalAlignment = HorizontalAlignment.Left;
                GridBG_RegionInner.HorizontalAlignment = HorizontalAlignment.Left;
            }

            Background.Visibility = Visibility.Visible;
            BackgroundAcrylicMask.Visibility = Visibility.Visible;
        }
    }
}
