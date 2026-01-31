using CollapseLauncher.Helper;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;
using System.Numerics;
using System.Threading;
using Windows.Foundation;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;

public partial class LayeredBackgroundImage
{
    public event Action<LayeredBackgroundImage>? ImageLoaded;

    #region Fields

    private object? _lastPlaceholderSource;
    private object? _lastBackgroundSource;
    private object? _lastBackgroundStaticSource;
    private object? _lastForegroundSource;

    private MediaSourceType _lastPlaceholderSourceType;
    private MediaSourceType _lastBackgroundSourceType;
    private MediaSourceType _lastBackgroundStaticSourceType;
    private MediaSourceType _lastForegroundSourceType;

    private bool _isPlaceholderHidden;

    #endregion

    #region Loaded and Unloaded

    private static bool IsSourceKindEquals(object? left, object? right)
    {
        if (left is string asStringLeft && right is string asStringRight)
        {
            return string.Equals(asStringLeft, asStringRight, StringComparison.OrdinalIgnoreCase);
        }

        return left == right;
    }

    private void LayeredBackgroundImage_OnLoaded(object sender, RoutedEventArgs e)
    {
        Interlocked.Exchange(ref _isLoaded, true);
        ParallaxView_ToggleEnable(IsParallaxEnabled);
        ParallaxGrid_OnUpdateCenterPoint();
        ElevateView_ToggleEnable(IsBackgroundElevated);
        ElevateGrid_OnUpdateCenterPoint();
        IsBackgroundElevated_OnChanged();

        if (!_isPlaceholderHidden &&
            !IsSourceKindEquals(_lastPlaceholderSource, PlaceholderSource))
        {
            LoadFromSourceAsyncDetached(PlaceholderSourceProperty,
                                        nameof(PlaceholderStretch),
                                        nameof(PlaceholderHorizontalAlignment),
                                        nameof(PlaceholderVerticalAlignment),
                                        _placeholderGrid,
                                        false,
                                        ref _lastPlaceholderSourceType);
            _lastPlaceholderSource = PlaceholderSource;
        }

        if (CanUseStaticBackground && !IsVideoAutoplay)
        {
            if (!IsSourceKindEquals(_lastBackgroundStaticSource, BackgroundStaticSource))
            {
                BackgroundSource_UseStatic(this);
                _lastBackgroundStaticSource = BackgroundStaticSource;
            }
        }
        else
        {
            if (!IsSourceKindEquals(_lastBackgroundSource, BackgroundSource))
            {
                BackgroundSource_UseNormal(this);
                _lastBackgroundSource = BackgroundSource;
            }
        }

        // ReSharper disable once InvertIf
        if (!IsSourceKindEquals(_lastForegroundSource, ForegroundSource))
        {
            LoadFromSourceAsyncDetached(ForegroundSourceProperty,
                                        nameof(ForegroundStretch),
                                        nameof(ForegroundHorizontalAlignment),
                                        nameof(ForegroundVerticalAlignment),
                                        _foregroundGrid,
                                        false,
                                        ref _lastForegroundSourceType);
            _lastForegroundSource = ForegroundSource;
        }
    }

    private void LayeredBackgroundImage_OnUnloaded(object sender, RoutedEventArgs e)
    {
        Interlocked.Exchange(ref _isLoaded, false);

        ParallaxGrid_UnregisterEffect();
        _lastParallaxHoverSource = null;
    }

    #endregion

    #region Parallax View

    private static void IsParallaxEnabled_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        LayeredBackgroundImage element = (LayeredBackgroundImage)d;
        if (!element.IsLoaded)
        {
            return;
        }

        element.ParallaxView_ToggleEnable(element.IsParallaxEnabled);
    }

    private static void ParallaxHover_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        LayeredBackgroundImage element = (LayeredBackgroundImage)d;
        if (!element.IsLoaded)
        {
            return;
        }

        element.ParallaxGrid_RegisterPointerEvents(); // Re-register hover event
    }

    private void ParallaxView_ToggleEnable(bool isEnable)
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
        ParallaxGrid_RegisterPointerEvents();
        ParallaxGrid_OffsetReset();

        if (_parallaxGrid != null!)
        {
            _parallaxGrid.SizeChanged += ParallaxGrid_OnSizeChanged;
        }
    }

    private void ParallaxGrid_UnregisterEffect()
    {
        ParallaxGrid_UnregisterPointerEvents();
        ParallaxGrid_OffsetReset();

        if (_parallaxGrid != null!)
        {
            _parallaxGrid.SizeChanged -= ParallaxGrid_OnSizeChanged;
        }
    }

    private void ParallaxGrid_RegisterPointerEvents()
    {
        UIElement? eventSource = ParallaxGrid_UnregisterPointerEvents();
        if (eventSource == null)
        {
            return;
        }

        eventSource.PointerMoved   += ParallaxGrid_OnPointerMoved;
        eventSource.PointerEntered += ParallaxGrid_OnPointerEntered;
        eventSource.PointerExited  += ParallaxGrid_OnPointerExited;
        _lastParallaxHoverSource   =  eventSource;
    }

    private UIElement? ParallaxGrid_UnregisterPointerEvents()
    {
        UIElement? eventSource = ParallaxHoverSource ?? _parallaxGrid;
        if (_lastParallaxHoverSource != null)
        {
            _lastParallaxHoverSource.PointerMoved   -= ParallaxGrid_OnPointerMoved;
            _lastParallaxHoverSource.PointerEntered -= ParallaxGrid_OnPointerEntered;
            _lastParallaxHoverSource.PointerExited  -= ParallaxGrid_OnPointerExited;
        }

        if (eventSource == null!)
        {
            return null;
        }

        eventSource.PointerMoved   -= ParallaxGrid_OnPointerMoved;
        eventSource.PointerEntered -= ParallaxGrid_OnPointerEntered;
        eventSource.PointerExited  -= ParallaxGrid_OnPointerExited;
        return eventSource;
    }

    private void ParallaxGrid_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ParallaxGrid_OnUpdateCenterPoint();
    }

    private void ParallaxGrid_OnUpdateCenterPoint()
    {
        if (!IsLoaded)
        {
            return;
        }

        _parallaxGridVisual.CenterPoint = new Vector3((float)_parallaxGrid.RenderSize.Width / 2,
                                                      (float)_parallaxGrid.RenderSize.Height / 2,
                                                      0);
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
        ParallaxGrid_OnPointerMovedCore(sender, e, true);
    }

    private void ParallaxGrid_OffsetReset()
    {
        StartElementElevateEasingAnimation(_parallaxGridCompositor,
                                           _parallaxGridVisual,
                                           Vector3.Zero,
                                           Vector3.One,
                                           250d,
                                           easingFunction: _parallaxEasingFunction);
    }

    private void ParallaxGrid_OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        ParallaxGrid_OnPointerMovedCore(sender, e);
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

        StartElementOffsetAnimation(_parallaxGridCompositor,
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

    private static void StartElementOffsetAnimation(
        Compositor                 compositor,
        Visual                     visual,
        UIElement                  element,
        double                     offsetX,
        double                     offsetY,
        double                     centerPointX,
        double                     centerPointY,
        double                     durationMs,
        CompositionEasingFunction? easingFunction    = null,
        bool                       useAnimatedOffset = true)
    {
        // Move opposite to center point
        float tx = (float)(-centerPointX * offsetX);
        float ty = (float)(-centerPointY * offsetY);

        // Scale with x2 as it counts with each side of axis (Left, Right) (Top, Bottom)
        Vector2 size    = element.ActualSize;
        double  sizeToX = size.X + Math.Abs(offsetX) * 2;
        double  sizeToY = size.Y + Math.Abs(offsetY) * 2;

        double addScaleX = sizeToX / size.X;
        double addScaleY = sizeToY / size.Y;

        // Gets the stronger axis
        float factorScale = (float)Math.Max(addScaleX, addScaleY);

        StartElementElevateEasingAnimation(compositor,
                                           visual,
                                           Vector3.Zero with { X = tx, Y = ty },
                                           Vector3.One with { X = factorScale, Y = factorScale },
                                           durationMs,
                                           easingFunction,
                                           useAnimatedOffset);
    }

    private static void StartElementElevateEasingAnimation(
        Compositor                 compositor,
        Visual                     visual,
        Vector3                    offset,
        Vector3                    scale,
        double                     duration,
        CompositionEasingFunction? easingFunction    = null,
        bool                       useAnimatedOffset = true)
    {
        const string targetTranslation = "Offset";
        const string targetScale       = "Scale";

        if (compositor == null!)
        {
            return;
        }

        // Scale
        Vector3KeyFrameAnimation scaleAnim = CreateVector3Keyframe(scale, targetScale);
        visual.StartAnimation(targetScale, scaleAnim);

        if (useAnimatedOffset)
        {
            // Move
            Vector3KeyFrameAnimation offsetAnim = CreateVector3Keyframe(offset, targetTranslation);
            visual.StartAnimation(targetTranslation, offsetAnim);
            return;
        }

        // Directly set visual offset
        visual.Offset = offset;
        return;

        Vector3KeyFrameAnimation CreateVector3Keyframe(
            Vector3 valueTo,
            string  targetProperty)
        {
            Vector3KeyFrameAnimation keyframe = compositor.CreateVector3KeyFrameAnimation();
            keyframe.InsertKeyFrame(1, valueTo, easingFunction);
            keyframe.Target   = targetProperty;
            keyframe.Duration = TimeSpan.FromMilliseconds(duration);

            return keyframe;
        }
    }

    #endregion

    #region Local Events

    private void NotifyImageLoaded() => ImageLoaded?.Invoke(this);

    #endregion

    #region Background Elevation

    private static void IsBackgroundElevated_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((LayeredBackgroundImage)d).IsBackgroundElevated_OnChanged();

    private void IsBackgroundElevated_OnChanged()
    {
        if (!IsLoaded)
        {
            return;
        }

        ElevateView_ToggleEnable(IsBackgroundElevated);
    }

    private void ElevateView_ToggleEnable(bool isEnable)
    {
        try
        {
            if (isEnable)
            {
                ElevateGrid_RegisterEffect();
                return;
            }

            ElevateGrid_UnregisterEffect();
        }
        finally
        {
            ElevateGrid_UpdateElevationSize(isEnable);
        }
    }

    private void ElevateGrid_RegisterEffect()
    {
        if (_elevateGrid != null!)
        {
            _elevateGrid.SizeChanged += ElevateGrid_OnSizeChanged;
        }
    }

    private void ElevateGrid_UnregisterEffect()
    {
        if (_parallaxGrid != null!)
        {
            _parallaxGrid.SizeChanged -= ElevateGrid_OnSizeChanged;
        }
    }

    private void ElevateGrid_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ElevateGrid_OnUpdateCenterPoint();
    }

    private void ElevateGrid_OnUpdateCenterPoint()
    {
        if (!IsLoaded)
        {
            return;
        }

        _elevateGridVisual.CenterPoint = new Vector3((float)_elevateGrid.RenderSize.Width / 2,
                                                     (float)_elevateGrid.RenderSize.Height / 2,
                                                     0);

        ElevateGrid_UpdateElevationSize(IsBackgroundElevated);
    }

    private void ElevateGrid_UpdateElevationSize(bool enable)
    {
        double elevationOffset = enable
            ? BackgroundElevationPixels
            : 0d;

        StartElementOffsetAnimation(_elevateGridCompositor,
                                    _elevateGridVisual,
                                    _elevateGrid,
                                    elevationOffset,
                                    elevationOffset,
                                    0d,
                                    0d,
                                    500d,
                                    _elevateEasingFunction);
    }

    #endregion
}

