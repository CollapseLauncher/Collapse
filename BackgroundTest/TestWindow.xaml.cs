using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace BackgroundTest
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class TestWindow
    {
        public TestWindow()
        {
            InitializeComponent();
        }

        private void ContainerGrid_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ContainerSizeIndicator.Text = e.NewSize.ToString();
        }
    }
}
