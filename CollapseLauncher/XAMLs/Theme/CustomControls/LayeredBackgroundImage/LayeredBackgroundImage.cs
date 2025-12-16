using Microsoft.UI.Xaml.Controls;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;

public partial class LayeredBackgroundImage : Control
{
    public LayeredBackgroundImage()
    {
        Loaded   += LayeredBackgroundImage_OnLoaded;
        Unloaded += LayeredBackgroundImage_OnUnloaded;

        DefaultStyleKey = typeof(LayeredBackgroundImage);
    }

    ~LayeredBackgroundImage()
    {
        Loaded   -= LayeredBackgroundImage_OnLoaded;
        Unloaded -= LayeredBackgroundImage_OnUnloaded;

        DisposeRenderTarget();
        DisposeVideoPlayer();
    }
}
