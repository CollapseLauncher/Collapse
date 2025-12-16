using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;
using System.Numerics;
using System.Threading;
using Windows.Foundation;

namespace BackgroundTest.CustomControl.LayeredBackgroundImage;

public partial class LayeredBackgroundImage
{
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

        if (!_isPlaceholderHidden &&
            !IsSourceKindEquals(_lastPlaceholderSource, PlaceholderSource))
        {
            LoadFromSourceAsyncDetached(PlaceholderSourceProperty,
                                        _lastPlaceholderSource,
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
                                        _lastBackgroundSource,
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
                                        _lastForegroundSource,
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

        _parallaxGrid.SizeChanged += ParallaxGrid_OnSizeChanged;
    }

    private void ParallaxGrid_UnregisterEffect()
    {
        ParallaxGrid_UnregisterPointerEvents();
        ParallaxGrid_OffsetReset();

        _parallaxGrid.SizeChanged -= ParallaxGrid_OnSizeChanged;
    }

    private void ParallaxGrid_RegisterPointerEvents()
    {
        UIElement eventSource = ParallaxGrid_UnregisterPointerEvents();

        eventSource.PointerMoved   += ParallaxGrid_OnPointerMoved;
        eventSource.PointerEntered += ParallaxGrid_OnPointerEntered;
        eventSource.PointerExited  += ParallaxGrid_OnPointerExited;
        _lastParallaxHoverSource   =  eventSource;
    }

    private UIElement ParallaxGrid_UnregisterPointerEvents()
    {
        UIElement eventSource = ParallaxHoverSource ?? _parallaxGrid;
        if (_lastParallaxHoverSource != null)
        {
            _lastParallaxHoverSource.PointerMoved   -= ParallaxGrid_OnPointerMoved;
            _lastParallaxHoverSource.PointerEntered -= ParallaxGrid_OnPointerEntered;
            _lastParallaxHoverSource.PointerExited  -= ParallaxGrid_OnPointerExited;
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
        ParallaxGrid_OffsetReset();
    }

    private void ParallaxGrid_OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        ParallaxGrid_OnPointerMoved(sender, e);
    }

    private void ParallaxGrid_OffsetReset()
    {
        ParallaxGrid_StartAnimation(Vector3.Zero, Vector3.One, 250d);
    }

    private void ParallaxGrid_OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        FrameworkElement element = (FrameworkElement)sender;

        double offsetX = ParallaxHorizontalShift;
        double offsetY = ParallaxVerticalShift;

        // Move
        Point  pos = e.GetCurrentPoint(element).Position;
        double w   = element.ActualWidth;
        double h   = element.ActualHeight;

        if (w <= 0 || h <= 0)
            return;

        // Normalize mouse position to range [-1, +1]
        double nx = pos.X / w * 2d - 1d;
        double ny = pos.Y / h * 2d - 1d;

        // Move opposite to pointer
        float tx = (float)(-nx * offsetX);
        float ty = (float)(-ny * offsetY);

        // Scale with x2 as it counts with each side of axis (Left, Right) (Top, Bottom)
        Vector2 size    = _parallaxGrid.ActualSize;
        double  sizeToX = size.X + Math.Abs(offsetX) * 2;
        double  sizeToY = size.Y + Math.Abs(offsetY) * 2;

        double addScaleX = sizeToX / size.X;
        double addScaleY = sizeToY / size.Y;

        // Gets the stronger axis
        float factorScale = (float)Math.Max(addScaleX, addScaleY);

        ParallaxGrid_StartAnimation(Vector3.Zero with { X = tx, Y = ty },
                                    Vector3.One with { X = factorScale, Y = factorScale },
                                    40);
    }

    private void ParallaxGrid_StartAnimation(Vector3 offset, Vector3 scale, double duration)
    {
        const string targetTranslation = "Translation";
        const string targetScale       = "Scale";

        CompositionAnimationGroup? animGroup = _parallaxGridCompositor.CreateAnimationGroup();

        // Move
        Vector3KeyFrameAnimation? anim = _parallaxGridCompositor.CreateVector3KeyFrameAnimation();
        anim.Duration = TimeSpan.FromMilliseconds(duration);
        anim.InsertKeyFrame(1f, offset);
        anim.Target = targetTranslation;

        // Scale
        Vector3KeyFrameAnimation? scaleAnim = _parallaxGridCompositor.CreateVector3KeyFrameAnimation();
        scaleAnim.Duration = TimeSpan.FromMilliseconds(duration);
        scaleAnim.InsertKeyFrame(1f, scale);
        scaleAnim.Target = targetScale;

        animGroup.Add(anim);
        animGroup.Add(scaleAnim);

        _parallaxGridVisual.StartAnimationGroup(animGroup);
    }

    #endregion
}

