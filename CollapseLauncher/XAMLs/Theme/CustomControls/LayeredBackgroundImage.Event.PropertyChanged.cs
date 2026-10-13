using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    private static void PropertyIsAudioEnabled_OnChange(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)obj;
    }

    private static void PropertyAudioVolume_OnChange(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)obj;
    }

    private static void PropertyIsParallelEnabled_OnChange(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)obj;
    }

    private static void PropertyParallaxHoverSource_OnChange(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)obj;
    }

    private static void PropertyPlaceholderSource_OnChange(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)obj;
        PropertyMediaSource_OnChange(instance._placeholderGrid, instance, args);
    }

    private static void PropertyBackgroundSource_OnChange(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)obj;
        PropertyMediaSource_OnChange(instance._backgroundGrid, instance, args);
    }

    private static void PropertyForegroundSource_OnChange(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)obj;
        PropertyMediaSource_OnChange(instance._foregroundGrid, instance, args);
    }

    private static async void PropertyMediaSource_OnChange(
        Grid                               grid,
        LayeredBackgroundImage             instance,
        DependencyPropertyChangedEventArgs args)
    {
        if (!instance.IsLoaded)
        {
            return;
        }

        await instance.LoadPresenter(grid, args.NewValue);
    }

    private static void PropertyMediaDurationPosition_OnChange(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)obj;
    }

    private static void PropertyIsBackgroundElevated_OnChange(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)obj;
        instance.ElevateView_ToggleEnable((bool)args.NewValue);
    }
}
