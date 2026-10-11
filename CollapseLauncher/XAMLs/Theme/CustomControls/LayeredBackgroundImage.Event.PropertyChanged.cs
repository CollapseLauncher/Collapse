using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Text;

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
        PropertyMediaSource_OnChange(instance._placeholderGrid, obj, args);
    }

    private static void PropertyBackgroundSource_OnChange(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)obj;
        PropertyMediaSource_OnChange(instance._backgroundGrid, obj, args);
    }

    private static void PropertyForegroundSource_OnChange(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)obj;
        PropertyMediaSource_OnChange(instance._foregroundGrid, obj, args);
    }

    private static async void PropertyMediaSource_OnChange(
        Grid                               grid,
        DependencyObject                   obj,
        DependencyPropertyChangedEventArgs args)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)obj;
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
    }
}
