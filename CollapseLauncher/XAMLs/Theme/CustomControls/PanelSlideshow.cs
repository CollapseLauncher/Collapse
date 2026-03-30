#nullable enable
using CollapseLauncher.Extension;
using Microsoft.UI.Xaml.Controls;

namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class PanelSlideshow : Control
{
    public PanelSlideshow()
    {
        DefaultStyleKey = typeof(PanelSlideshow);
    }

    ~PanelSlideshow()
    {
        if (this.IsObjectDisposed()) return;

        Loaded   -= PanelSlideshow_Loaded;
        Unloaded -= PanelSlideshow_Unloaded;
    }
}
