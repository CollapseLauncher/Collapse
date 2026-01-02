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
        _presenterGrid?.Loaded -= PanelSlideshow_Loaded;
        _presenterGrid?.Unloaded -= PanelSlideshow_Unloaded;
    }
}
