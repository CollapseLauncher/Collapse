using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage : Control
{
    public LayeredBackgroundImage()
    {
        Loaded   += LayeredBackgroundImage_OnLoaded;
        Unloaded += LayeredBackgroundImage_OnUnloaded;

        DefaultStyleKey = typeof(LayeredBackgroundImage);
    }

    private static void LayeredBackgroundImage_OnLoaded(object sender, RoutedEventArgs e)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)sender;
        instance.SubscribeGridEvents();
        instance.LoadAllPresenters();
    }

    private static void LayeredBackgroundImage_OnUnloaded(object sender, RoutedEventArgs e)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)sender;
        instance.UnsubscribeGridEvents();
        instance.UnloadAllPresenters();
    }
}
