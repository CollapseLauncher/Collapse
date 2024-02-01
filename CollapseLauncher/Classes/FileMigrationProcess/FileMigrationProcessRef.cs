using CollapseLauncher.CustomControls;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace CollapseLauncher
{
    internal struct FileMigrationProcessUIRef
    {
        internal ContentDialogCollapse mainDialogWindow;
        internal TextBlock pathActivitySubtitle;
        internal Run speedIndicatorSubtitle;
        internal Run fileCountIndicatorSubtitle;
        internal Run fileSizeIndicatorSubtitle;
        internal ProgressBar progressBarIndicator;
    }
}
