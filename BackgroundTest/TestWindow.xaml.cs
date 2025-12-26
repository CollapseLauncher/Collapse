using Microsoft.UI.Xaml;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace BackgroundTest
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class TestWindow
    {
        public static Window ThisWindow;

        public TestWindow()
        {
            InitializeComponent();
            ThisWindow = this;
        }

        private void ContainerGrid_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ContainerSizeIndicator.Text = e.NewSize.ToString();
        }

        private async void Image_Loaded(object sender, RoutedEventArgs e)
        {
            UIElement element = (UIElement)sender;
            while (true)
            {
                await Task.Delay(1000);
                element.Visibility = element.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            }
        }
    }
}
