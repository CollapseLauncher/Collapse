using CollapseLauncher.CustomControls;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace CollapseLauncher
{
    internal class FileMigrationProcessUIRef
    {
        internal ContentDialogCollapse MainDialogWindow           { get; set; }
        internal TextBlock             PathActivitySubtitle       { get; set; }
        internal Run                   SpeedIndicatorSubtitle     { get; set; }
        internal Run                   FileCountIndicatorSubtitle { get; set; }
        internal Run                   FileSizeIndicatorSubtitle  { get; set; }
        internal ProgressBar           ProgressBarIndicator       { get; set; }
    }
}
