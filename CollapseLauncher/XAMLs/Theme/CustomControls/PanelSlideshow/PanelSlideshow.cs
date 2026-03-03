using CollapseLauncher.Extension;
using Microsoft.UI.Xaml.Controls;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.PanelSlideshow;

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
