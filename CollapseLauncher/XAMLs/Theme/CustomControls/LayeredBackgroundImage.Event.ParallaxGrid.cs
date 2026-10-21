using CollapseLauncher.Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;
using System.Numerics;
using Windows.Foundation;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    #region Fields

    private FrameworkElement? _lastParallaxHoverSource;

    #endregion

    private void ParallaxGrid_ToggleEnable(bool isEnable)
    {
        if (isEnable)
        {
            ParallaxGrid_RegisterEffect();
            return;
        }

        ParallaxGrid_UnregisterEffect();
    }

    private void ParallaxGrid_RegisterEffect()
    {
        ParallaxGrid_RegisterEvents();
        ParallaxGrid_OffsetReset();
    }

    private void ParallaxGrid_UnregisterEffect()
    {
        ParallaxGrid_UnregisterEvents();
        ParallaxGrid_OffsetReset();
    }

    private void ParallaxGrid_RegisterEvents()
    {
        FrameworkElement? eventSource = ParallaxGrid_UnregisterEvents();
        if (eventSource == null)
        {
            return;
        }

        eventSource.SizeChanged    += ParallaxGrid_OnSizeChanged;
        eventSource.PointerMoved   += ParallaxGrid_OnPointerMoved;
        eventSource.PointerEntered += ParallaxGrid_OnPointerEntered;
        eventSource.PointerExited  += ParallaxGrid_OnPointerExited;
        _lastParallaxHoverSource   =  eventSource;
    }

    private FrameworkElement? ParallaxGrid_UnregisterEvents()
    {
        if (_lastParallaxHoverSource != null)
        {
            _lastParallaxHoverSource.SizeChanged    -= ParallaxGrid_OnSizeChanged;
            _lastParallaxHoverSource.PointerMoved   -= ParallaxGrid_OnPointerMoved;
            _lastParallaxHoverSource.PointerEntered -= ParallaxGrid_OnPointerEntered;
            _lastParallaxHoverSource.PointerExited  -= ParallaxGrid_OnPointerExited;
        }

        FrameworkElement? eventSource = ParallaxHoverSource ?? _parallaxGrid;
        if (eventSource == null!)
        {
            return null;
        }

        return eventSource;
    }

    private void ParallaxGrid_CalculateCenterPoint()
    {
        if (!IsLoaded)
        {
            return;
        }

        _parallaxGridVisual.CenterPoint = new Vector3((float)_parallaxGrid.RenderSize.Width / 2,
                                                      (float)_parallaxGrid.RenderSize.Height / 2,
                                                      0);
    }

    private void ParallaxGrid_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ParallaxGrid_CalculateCenterPoint();
    }

    private void ParallaxGrid_OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (ParallaxResetOnUnfocused)
        {
            ParallaxGrid_OffsetReset();
        }
    }

    private void ParallaxGrid_OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        ParallaxGrid_CalculateCenterPoint(); // Explicitly calculate center point once the pointer enters.
        ParallaxGrid_OnPointerMovedCore(sender, e, true);
    }

    private void ParallaxGrid_OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        ParallaxGrid_OnPointerMovedCore(sender, e);
    }

    private void ParallaxGrid_OffsetReset()
    {
        StartElementElevateAnimation(_parallaxGridCompositor,
                                     _parallaxGridVisual,
                                     Vector3.Zero,
                                     Vector3.One,
                                     250d,
                                     easingFunction: _parallaxEasingFunction);
    }

    private void ParallaxGrid_OnPointerMovedCore(
        object                 sender,
        PointerRoutedEventArgs args,
        bool                   isUseResetAnimation = false)
    {
        FrameworkElement element = (FrameworkElement)sender;

        // Move
        Point  pos = args.GetCurrentPoint(element).Position;
        double w   = element.ActualWidth;
        double h   = element.ActualHeight;

        if (w <= 0 || h <= 0)
            return;

        // Normalize mouse position to range [-1, +1]
        double nx = pos.X / w * 2d - 1d;
        double ny = pos.Y / h * 2d - 1d;

        double offsetX = ParallaxHorizontalShift;
        double offsetY = ParallaxVerticalShift;

        double deltaMs = Math.Max(1000d / WindowUtility.CurrentWindowMonitorRefreshRate, 8d);

        StartElementElevateOffsetAnimation(_parallaxGridCompositor,
                                           _parallaxGridVisual,
                                           _parallaxGrid,
                                           offsetX,
                                           offsetY,
                                           nx,
                                           ny,
                                           isUseResetAnimation ? 250d : deltaMs,
                                           easingFunction: _parallaxEasingFunction,
                                           useAnimatedOffset: isUseResetAnimation);
    }
}
