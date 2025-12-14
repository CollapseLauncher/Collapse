using Microsoft.UI.Xaml.Controls;

namespace BackgroundTest.CustomControl.LayeredBackgroundImage;

public partial class LayeredBackgroundImage : Control
{
    public LayeredBackgroundImage()
    {
        Loaded += LayeredBackgroundImage_OnLoaded;
        Unloaded += LayeredBackgroundImage_OnUnloaded;

        DefaultStyleKey = typeof(LayeredBackgroundImage);
    }

    ~LayeredBackgroundImage()
    {
        Loaded -= LayeredBackgroundImage_OnLoaded;
        Unloaded -= LayeredBackgroundImage_OnUnloaded;

        DisposeVideoPlayer();
    }
}
