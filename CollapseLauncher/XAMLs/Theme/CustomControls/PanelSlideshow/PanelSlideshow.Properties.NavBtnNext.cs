using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.PanelSlideshow;

public partial class PanelSlideshow
{
    #region Properties

    public double NextButtonOpacity
    {
        get => (double)GetValue(NextButtonOpacityProperty);
        set => SetValue(NextButtonOpacityProperty, value);
    }

    public Thickness NextButtonMargin
    {
        get => (Thickness)GetValue(NextButtonMarginProperty);
        set => SetValue(NextButtonMarginProperty, value);
    }

    public Thickness NextButtonPadding
    {
        get => (Thickness)GetValue(NextButtonPaddingProperty);
        set => SetValue(NextButtonPaddingProperty, value);
    }

    public HorizontalAlignment NextButtonHorizontalAlignment
    {
        get => (HorizontalAlignment)GetValue(NextButtonHorizontalAlignmentProperty);
        set => SetValue(NextButtonHorizontalAlignmentProperty, value);
    }

    public VerticalAlignment NextButtonVerticalAlignment
    {
        get => (VerticalAlignment)GetValue(NextButtonVerticalAlignmentProperty);
        set => SetValue(NextButtonVerticalAlignmentProperty, value);
    }

    public CornerRadius NextButtonCornerRadius
    {
        get => (CornerRadius)GetValue(NextButtonCornerRadiusProperty);
        set => SetValue(NextButtonCornerRadiusProperty, value);
    }

    public double NextButtonWidth
    {
        get => (double)GetValue(NextButtonWidthProperty);
        set => SetValue(NextButtonWidthProperty, value);
    }

    public double NextButtonMaxWidth
    {
        get => (double)GetValue(NextButtonMaxWidthProperty);
        set => SetValue(NextButtonMaxWidthProperty, value);
    }

    public double NextButtonMinWidth
    {
        get => (double)GetValue(NextButtonMinWidthProperty);
        set => SetValue(NextButtonMinWidthProperty, value);
    }

    public double NextButtonHeight
    {
        get => (double)GetValue(NextButtonHeightProperty);
        set => SetValue(NextButtonHeightProperty, value);
    }

    public double NextButtonMaxHeight
    {
        get => (double)GetValue(NextButtonMaxHeightProperty);
        set => SetValue(NextButtonMaxHeightProperty, value);
    }

    public double NextButtonMinHeight
    {
        get => (double)GetValue(NextButtonMinHeightProperty);
        set => SetValue(NextButtonMinHeightProperty, value);
    }

    public Visibility NextButtonVisibilityMode
    {
        get => (Visibility)GetValue(NextButtonVisibilityModeProperty);
        set => SetValue(NextButtonVisibilityModeProperty, value);
    }

    public Brush NextButtonBackgroundBrush
    {
        get => (Brush)GetValue(NextButtonBackgroundBrushProperty);
        set => SetValue(NextButtonBackgroundBrushProperty, value);
    }

    public Brush NextButtonForegroundBrush
    {
        get => (Brush)GetValue(NextButtonForegroundBrushProperty);
        set => SetValue(NextButtonForegroundBrushProperty, value);
    }

    public Style NextButtonStyle
    {
        get => (Style)GetValue(NextButtonStyleProperty);
        set => SetValue(NextButtonStyleProperty, value);
    }

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty NextButtonOpacityProperty =
        DependencyProperty.Register(nameof(NextButtonOpacity), typeof(double), typeof(PanelSlideshow),
                                    new PropertyMetadata(1));

    public static readonly DependencyProperty NextButtonMarginProperty =
        DependencyProperty.Register(nameof(NextButtonMargin), typeof(Thickness), typeof(PanelSlideshow),
                                    new PropertyMetadata(new Thickness(16d)));

    public static readonly DependencyProperty NextButtonPaddingProperty =
        DependencyProperty.Register(nameof(NextButtonPadding), typeof(Thickness), typeof(PanelSlideshow),
                                    new PropertyMetadata(default(Thickness)));

    public static readonly DependencyProperty NextButtonHorizontalAlignmentProperty =
        DependencyProperty.Register(nameof(NextButtonHorizontalAlignment), typeof(HorizontalAlignment), typeof(PanelSlideshow),
                                    new PropertyMetadata(HorizontalAlignment.Left));

    public static readonly DependencyProperty NextButtonVerticalAlignmentProperty =
        DependencyProperty.Register(nameof(NextButtonVerticalAlignment), typeof(VerticalAlignment), typeof(PanelSlideshow),
                                    new PropertyMetadata(VerticalAlignment.Bottom));

    public static readonly DependencyProperty NextButtonCornerRadiusProperty =
        DependencyProperty.Register(nameof(NextButtonCornerRadius), typeof(CornerRadius), typeof(PanelSlideshow),
                                    new PropertyMetadata(new CornerRadius(16)));

    public static readonly DependencyProperty NextButtonWidthProperty =
        DependencyProperty.Register(nameof(NextButtonWidth), typeof(double), typeof(PanelSlideshow),
                                    new PropertyMetadata(32d));

    public static readonly DependencyProperty NextButtonMaxWidthProperty =
        DependencyProperty.Register(nameof(NextButtonMaxWidth), typeof(double), typeof(PanelSlideshow),
                                    new PropertyMetadata(double.PositiveInfinity));

    public static readonly DependencyProperty NextButtonMinWidthProperty =
        DependencyProperty.Register(nameof(NextButtonMinWidth), typeof(double), typeof(PanelSlideshow),
                                    new PropertyMetadata(0));

    public static readonly DependencyProperty NextButtonHeightProperty =
        DependencyProperty.Register(nameof(NextButtonHeight), typeof(double), typeof(PanelSlideshow),
                                    new PropertyMetadata(32d));

    public static readonly DependencyProperty NextButtonMaxHeightProperty =
        DependencyProperty.Register(nameof(NextButtonMaxHeight), typeof(double), typeof(PanelSlideshow),
                                    new PropertyMetadata(double.PositiveInfinity));

    public static readonly DependencyProperty NextButtonMinHeightProperty =
        DependencyProperty.Register(nameof(NextButtonMinHeight), typeof(double), typeof(PanelSlideshow),
                                    new PropertyMetadata(0));

    public static readonly DependencyProperty NextButtonVisibilityModeProperty =
        DependencyProperty.Register(nameof(NextButtonVisibilityMode), typeof(Visibility), typeof(PanelSlideshow),
                                    new PropertyMetadata(Visibility.Visible));

    public static readonly DependencyProperty NextButtonBackgroundBrushProperty =
        DependencyProperty.Register(nameof(NextButtonBackgroundBrush), typeof(Brush), typeof(PanelSlideshow),
                                    new PropertyMetadata(null));

    public static readonly DependencyProperty NextButtonForegroundBrushProperty =
        DependencyProperty.Register(nameof(NextButtonForegroundBrush), typeof(Brush), typeof(PanelSlideshow),
                                    new PropertyMetadata(null));

    public static readonly DependencyProperty NextButtonStyleProperty =
        DependencyProperty.Register(nameof(NextButtonStyle), typeof(Style), typeof(PanelSlideshow),
                                    new PropertyMetadata(null));

    #endregion
}
