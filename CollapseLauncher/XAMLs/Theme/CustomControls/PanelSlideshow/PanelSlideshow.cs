using Microsoft.UI.Xaml.Controls;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.PanelSlideshow;

public partial class PanelSlideshow : Control
{
    public PanelSlideshow()
    {
        Loaded += PanelSlideshow_Loaded;
        Unloaded += PanelSlideshow_Unloaded;

        DefaultStyleKey = typeof(PanelSlideshow);
    }

    ~PanelSlideshow()
    {
        Loaded -= PanelSlideshow_Loaded;
        Unloaded -= PanelSlideshow_Unloaded;
    }
}
