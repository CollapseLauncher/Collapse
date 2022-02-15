using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CollapseLauncher.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
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
            DataPackage data = new DataPackage();
            data.SetText(ErrorSender.ExceptionContent);
            Clipboard.SetContent(data);
            CopyThrow.Content = "Copied to Clipboard!";
            CopyThrow.IsEnabled = false;
        }
    }
}
