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
    private object? _lastForegroundSource;

    private MediaSourceType _lastPlaceholderSourceType;
    private MediaSourceType _lastBackgroundSourceType;
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

        if (!IsSourceKindEquals(_lastBackgroundSource, BackgroundSource))
        {
            LoadFromSourceAsyncDetached(BackgroundSourceProperty,
                                        nameof(BackgroundStretch),
                                        nameof(BackgroundHorizontalAlignment),
                                        nameof(BackgroundVerticalAlignment),
                                        _backgroundGrid,
                                        true,
                                        ref _lastBackgroundSourceType);
            _lastBackgroundSource = BackgroundSource;
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

        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private void LayeredBackgroundImage_OnUnloaded(object sender, RoutedEventArgs e)
    {
        Interlocked.Exchange(ref _isLoaded, false);

        ParallaxGrid_UnregisterEffect();
        _lastParallaxHoverSource = null;

        GC.Collect();
        GC.WaitForPendingFinalizers();
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
        ParallaxGrid_OnPointerMoved(sender, e);
    }

    private void ParallaxGrid_OffsetReset()
    {
        StartElementElevateEasingAnimation(_parallaxGridCompositor,
                                           _parallaxGridVisual,
                                           Vector3.Zero,
                                           Vector3.One,
                                           250d);
    }

    private void ParallaxGrid_OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        FrameworkElement element = (FrameworkElement)sender;

        // Move
        Point  pos = e.GetCurrentPoint(element).Position;
        double w   = element.ActualWidth;
        double h   = element.ActualHeight;

        if (w <= 0 || h <= 0)
            return;

        // Normalize mouse position to range [-1, +1]
        double nx = pos.X / w * 2d - 1d;
        double ny = pos.Y / h * 2d - 1d;

        double offsetX = ParallaxHorizontalShift;
        double offsetY = ParallaxVerticalShift;

        bool isHighRefreshRate = WindowUtility.CurrentWindowMonitorRefreshRate > 75000 / 1001;
        double dur = isHighRefreshRate ? 40 : 10;

        StartElementOffsetAnimation(_parallaxGridCompositor,
                                    _parallaxGridVisual,
                                    _parallaxGrid,
                                    offsetX,
                                    offsetY,
                                    nx,
                                    ny,
                                    dur,
                                    !isHighRefreshRate);
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
        bool                       useSpring = true,
        CompositionEasingFunction? easingFunction = null)
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

        if (useSpring)
        {
            StartElementElevateSpringAnimation(compositor,
                                               visual,
                                               Vector3.Zero with { X = tx, Y = ty },
                                               Vector3.One with { X = factorScale, Y = factorScale },
                                               durationMs,
                                               easingFunction);
            return;
        }

        StartElementElevateEasingAnimation(compositor,
                                           visual,
                                           Vector3.Zero with { X = tx, Y = ty },
                                           Vector3.One with { X = factorScale, Y = factorScale },
                                           durationMs,
                                           easingFunction);
    }

    private static void StartElementElevateEasingAnimation(
        Compositor                 compositor,
        Visual                     visual,
        Vector3                    offset,
        Vector3                    scale,
        double                     duration,
        CompositionEasingFunction? easingFunction = null)
    {
        const string targetTranslation = "Translation";
        const string targetScale       = "Scale";

        if (compositor == null!)
        {
            return;
        }

        CompositionAnimationGroup? animGroup = compositor.CreateAnimationGroup();

        // Move
        Vector3KeyFrameAnimation? anim = compositor.CreateVector3KeyFrameAnimation();
        anim.Duration = TimeSpan.FromMilliseconds(duration);
        anim.InsertKeyFrame(1f, offset, easingFunction);
        anim.Target = targetTranslation;

        // Scale
        Vector3KeyFrameAnimation? scaleAnim = compositor.CreateVector3KeyFrameAnimation();
        scaleAnim.Duration = TimeSpan.FromMilliseconds(duration);
        scaleAnim.InsertKeyFrame(1f, scale, easingFunction);
        scaleAnim.Target = targetScale;

        animGroup.Add(anim);
        animGroup.Add(scaleAnim);

        visual.StartAnimationGroup(animGroup);
    }

    private static void StartElementElevateSpringAnimation(
        Compositor                 compositor,
        Visual                     visual,
        Vector3                    offset,
        Vector3                    scale,
        double                     duration,
        CompositionEasingFunction? easingFunction = null)
    {
        const string targetTranslation = "Translation";
        const string targetScale = "Scale";

        if (compositor == null!)
        {
            return;
        }

        CompositionAnimationGroup? animGroup = compositor.CreateAnimationGroup();

        // Move
        SpringVector3NaturalMotionAnimation? anim = compositor.CreateSpringVector3Animation();
        anim.Period = TimeSpan.FromMilliseconds(duration);
        anim.FinalValue = offset;
        anim.DampingRatio = 1f;
        anim.Target = targetTranslation;
        anim.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        // Scale
        SpringVector3NaturalMotionAnimation? scaleAnim = compositor.CreateSpringVector3Animation();
        scaleAnim.Period = TimeSpan.FromMilliseconds(duration);
        scaleAnim.FinalValue = scale;
        scaleAnim.DampingRatio = 1f;
        scaleAnim.Target = targetScale;
        scaleAnim.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        animGroup.Add(anim);
        animGroup.Add(scaleAnim);

        visual.StartAnimationGroup(animGroup);
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
                                    false,
                                    CompositionEasingFunction.CreateCircleEasingFunction(_elevateGridCompositor,
                                         CompositionEasingFunctionMode.Out));
    }

    #endregion
}

