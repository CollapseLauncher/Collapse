using CollapseLauncher.GameManagement.ImageBackground;
using Microsoft.UI.Xaml.Controls;
// ReSharper disable RedundantExtendsListEntry

namespace CollapseLauncher.Pages
{
    public sealed partial class UnavailablePage : Page
    {
        public UnavailablePage()
        {
            InitializeComponent();

            ImageBackgroundManager.Shared.IsBackgroundElevated = true;
            ImageBackgroundManager.Shared.ForegroundOpacity    = 0d;
            ImageBackgroundManager.Shared.SmokeOpacity         = 1d;
        }
    }
}
