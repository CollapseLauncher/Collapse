using Microsoft.UI.Xaml;

namespace BackgroundTest.CustomControl.LayeredBackgroundImage;

public partial class LayeredBackgroundImage
{
    #region Properties

    public bool IsParallaxEnabled
    {
        get => (bool)GetValue(IsParallaxEnabledProperty);
        set => SetValue(IsParallaxEnabledProperty, value);
    }

    public bool IsAudioEnabled
    {
        get => (bool)GetValue(IsAudioEnabledProperty);
        set => SetValue(IsAudioEnabledProperty, value);
    }

    public double ParallaxHorizontalShift
    {
        get => (double)GetValue(ParallaxHorizontalShiftProperty);
        set => SetValue(ParallaxHorizontalShiftProperty, value);
    }

    public double ParallaxVerticalShift
    {
        get => (double)GetValue(ParallaxVerticalShiftProperty);
        set => SetValue(ParallaxVerticalShiftProperty, value);
    }

    public UIElement? ParallaxHoverSource
    {
        get => (UIElement?)GetValue(ParallaxHoverSourceProperty);
        set => SetValue(ParallaxHoverSourceProperty, value);
    }

    public object? PlaceholderSource
    {
        get => (object?)GetValue(PlaceholderSourceProperty);
        set => SetValue(PlaceholderSourceProperty, value);
    }

    public object? BackgroundSource
    {
        get => (object?)GetValue(BackgroundSourceProperty);
        set => SetValue(BackgroundSourceProperty, value);
    }

    public object? ForegroundSource
    {
        get => (object?)GetValue(ForegroundSourceProperty);
        set => SetValue(ForegroundSourceProperty, value);
    }

    #endregion

    #region Fields

    private UIElement? _lastParallaxHoverSource;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty IsParallaxEnabledProperty =
        DependencyProperty.Register(nameof(IsParallaxEnabled),
                                    typeof(bool),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(false, IsParallaxEnabled_OnChange));

    public static readonly DependencyProperty IsAudioEnabledProperty =
        DependencyProperty.Register(nameof(IsAudioEnabled),
                                    typeof(bool),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(false));

    public static readonly DependencyProperty ParallaxHorizontalShiftProperty =
        DependencyProperty.Register(nameof(ParallaxHorizontalShift),
                                    typeof(double),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(32d));

    public static readonly DependencyProperty ParallaxVerticalShiftProperty =
        DependencyProperty.Register(nameof(ParallaxVerticalShift),
                                    typeof(double),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(32d));

    public static readonly DependencyProperty ParallaxHoverSourceProperty =
        DependencyProperty.Register(nameof(ParallaxHoverSource),
                                    typeof(UIElement),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(null!, ParallaxHover_OnChange));

    public static readonly DependencyProperty PlaceholderSourceProperty =
        DependencyProperty.Register(nameof(PlaceholderSource),
                                    typeof(object),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(null!));

    public static readonly DependencyProperty BackgroundSourceProperty =
        DependencyProperty.Register(nameof(BackgroundSource),
                                    typeof(object),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(null!));

    public static readonly DependencyProperty ForegroundSourceProperty =
        DependencyProperty.Register(nameof(ForegroundSource),
                                    typeof(object),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(null!));

    #endregion
}
