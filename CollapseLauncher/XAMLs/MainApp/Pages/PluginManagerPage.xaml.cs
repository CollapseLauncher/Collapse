using CollapseLauncher.Extension;

using InternalExtension = CollapseLauncher.Extension.UIElementExtensions;

namespace CollapseLauncher.Pages
{
    public sealed partial class PluginManagerPage
    {
        public PluginManagerPage()
        {
            InitializeComponent();

            ImportBoxButton.SetAllControlsCursorRecursive(InternalExtension.HandCursor);
        }
    }
}
