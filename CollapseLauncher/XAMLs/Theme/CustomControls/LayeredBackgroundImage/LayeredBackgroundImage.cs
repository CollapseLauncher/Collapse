using Microsoft.UI.Xaml.Controls;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
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

        DisposeVideoPlayer();
    }
}
