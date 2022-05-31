using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Windows.ApplicationModel.DataTransfer;

namespace CollapseLauncher.Pages
{
    public sealed partial class UnhandledExceptionPage : Page
    {
        public UnhandledExceptionPage()
        {
            this.InitializeComponent();
            ExceptionTextBox.Text = ErrorSender.ExceptionContent;
            Title.Text = ErrorSender.ExceptionTitle;
            Subtitle.Text = ErrorSender.ExceptionSubtitle;
        }

        private void CopyTextToClipboard(object sender, RoutedEventArgs e)
        {
            DataPackage data = new DataPackage()
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            data.SetText(ErrorSender.ExceptionContent);
            Clipboard.SetContent(data);
            CopyThrow.Content = "Copied to Clipboard!";
            CopyThrow.IsEnabled = false;
        }
    }
}
