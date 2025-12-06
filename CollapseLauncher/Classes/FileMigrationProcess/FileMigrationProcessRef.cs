using CollapseLauncher.CustomControls;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace CollapseLauncher
{
    internal class FileMigrationProcessUIRef
    {
        internal ContentDialogCollapse MainDialogWindow           { get; init; }
        internal TextBlock             PathActivitySubtitle       { get; init; }
        internal Run                   SpeedIndicatorSubtitle     { get; init; }
        internal Run                   FileCountIndicatorSubtitle { get; init; }
        internal Run                   FileSizeIndicatorSubtitle  { get; init; }
        internal ProgressBar           ProgressBarIndicator       { get; init; }
    }
}
