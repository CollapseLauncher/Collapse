using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Media.Playback;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    private void SubscribeGridEvents()
    {
        _elevateGrid.SizeChanged += ElevateGrid_OnSizeChanged;

        // Toggle parallax event explicitly for parallax animation
        ParallaxGrid_ToggleEnable(IsParallaxEnabled);
    }

    private void UnsubscribeGridEvents()
    {
        _elevateGrid.SizeChanged -= ElevateGrid_OnSizeChanged;

        // Unsubscribe event explicitly for parallax animation by toggling it off.
        ParallaxGrid_ToggleEnable(false);
    }

    #region Audio, Media Position and Volume

    private static void PropertyIsAudioEnabled_OnChange(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)obj;
        if (!instance.IsLoaded)
        {
            return;
        }

        instance.ToggleMute(!(bool)args.NewValue);
    }

    private static void PropertyAudioVolume_OnChange(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)obj;
        if (!instance.IsLoaded)
        {
            return;
        }

        instance.SetVolume((double)args.NewValue);
    }

    private static void PropertyMediaDurationPosition_OnChange(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        TimeSpan pos = (TimeSpan)args.NewValue;
        if (pos.Ticks >= 0)
        {
            return;
        }

        LayeredBackgroundImage instance = (LayeredBackgroundImage)obj;
        if (!instance.TryGetBackgroundSourceContext(out _, out MediaPlayer? player))
        {
            return;
        }

        player.Position = -pos;
    }

    #endregion

    #region Parallax and Elevation

    private static void PropertyIsBackgroundElevated_OnChange(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)obj;
        instance.ElevateGrid_ToggleEnable((bool)args.NewValue);
    }

    private static void PropertyIsParallelEnabled_OnChange(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)obj;
        if (!instance.IsLoaded)
        {
            return;
        }

        instance.ParallaxGrid_ToggleEnable((bool)args.NewValue);
    }

    private static void PropertyParallaxHoverSource_OnChange(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)obj;
        if (!instance.IsLoaded)
        {
            return;
        }

        instance.ParallaxGrid_RegisterEffect(); // Register source events.
    }

    #endregion

    #region Media Sources

    private static void PropertyForegroundSource_OnChange(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)obj;
        PropertyMediaSource_OnChange(instance._foregroundGrid, instance, args);
    }

    private static void PropertyBackgroundSource_OnChange(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)obj;
        PropertyMediaSource_OnChange(instance._backgroundGrid, instance, args);
    }

    private static async void PropertyMediaSource_OnChange(
        Grid                               parent,
        LayeredBackgroundImage             instance,
        DependencyPropertyChangedEventArgs args)
    {
        if (!instance.IsLoaded)
        {
            return;
        }

        await instance.LoadPresenter(parent, args.NewValue);
    }

    #endregion
}
