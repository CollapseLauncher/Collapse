using CollapseLauncher.Extension;
using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
