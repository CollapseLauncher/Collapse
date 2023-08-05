using Microsoft.UI.Xaml.Controls;

namespace CollapseLauncher.Pages
{
    public sealed partial class NotInstalledPage : Page
    {
        public NotInstalledPage()
        {
            BackgroundImgChanger.ToggleBackground(true);
            this.InitializeComponent();
        }
    }
}
