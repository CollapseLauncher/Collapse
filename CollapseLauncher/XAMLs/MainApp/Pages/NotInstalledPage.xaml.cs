using CollapseLauncher.GameManagement.ImageBackground;

namespace CollapseLauncher.Pages
{
    public sealed partial class NotInstalledPage
    {
        public NotInstalledPage()
        {
            InitializeComponent();

            ImageBackgroundManager.Shared.IsBackgroundElevated = true;
            ImageBackgroundManager.Shared.ForegroundOpacity    = 0d;
            ImageBackgroundManager.Shared.SmokeOpacity         = 1d;
        }
    }
}
