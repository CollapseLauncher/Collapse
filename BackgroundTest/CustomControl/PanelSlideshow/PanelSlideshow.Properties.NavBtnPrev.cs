using BackgroundTest.CustomControl.NewPipsPager;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace BackgroundTest.CustomControl.PanelSlideshow;

public partial class PanelSlideshow
{
    #region Properties

    public double PreviousButtonOpacity
    {
        get => (double)GetValue(PreviousButtonOpacityProperty);
        set => SetValue(PreviousButtonOpacityProperty, value);
    }

    public Thickness PreviousButtonMargin
    {
        get => (Thickness)GetValue(PreviousButtonMarginProperty);
        set => SetValue(PreviousButtonMarginProperty, value);
    }

    public Thickness PreviousButtonPadding
    {
        get => (Thickness)GetValue(PreviousButtonPaddingProperty);
        set => SetValue(PreviousButtonPaddingProperty, value);
    }

    public HorizontalAlignment PreviousButtonHorizontalAlignment
    {
        get => (HorizontalAlignment)GetValue(PreviousButtonHorizontalAlignmentProperty);
        set => SetValue(PreviousButtonHorizontalAlignmentProperty, value);
    }

    public VerticalAlignment PreviousButtonVerticalAlignment
    {
        get => (VerticalAlignment)GetValue(PreviousButtonVerticalAlignmentProperty);
        set => SetValue(PreviousButtonVerticalAlignmentProperty, value);
    }

    public CornerRadius PreviousButtonCornerRadius
    {
        get => (CornerRadius)GetValue(PreviousButtonCornerRadiusProperty);
        set => SetValue(PreviousButtonCornerRadiusProperty, value);
    }

    public double PreviousButtonWidth
    {
        get => (double)GetValue(PreviousButtonWidthProperty);
        set => SetValue(PreviousButtonWidthProperty, value);
    }

    public double PreviousButtonMaxWidth
    {
        get => (double)GetValue(PreviousButtonMaxWidthProperty);
        set => SetValue(PreviousButtonMaxWidthProperty, value);
    }

    public double PreviousButtonMinWidth
    {
        get => (double)GetValue(PreviousButtonMinWidthProperty);
        set => SetValue(PreviousButtonMinWidthProperty, value);
    }

    public double PreviousButtonHeight
    {
        get => (double)GetValue(PreviousButtonHeightProperty);
        set => SetValue(PreviousButtonHeightProperty, value);
    }

    public double PreviousButtonMaxHeight
    {
        get => (double)GetValue(PreviousButtonMaxHeightProperty);
        set => SetValue(PreviousButtonMaxHeightProperty, value);
    }

    public double PreviousButtonMinHeight
    {
        get => (double)GetValue(PreviousButtonMinHeightProperty);
        set => SetValue(PreviousButtonMinHeightProperty, value);
    }

    public NewPipsPagerNavigationMode PreviousButtonVisibilityMode
    {
        get => (NewPipsPagerNavigationMode)GetValue(PreviousButtonMinHeightProperty);
        set => SetValue(PreviousButtonMinHeightProperty, value);
    }

    public Brush PreviousButtonBackgroundBrush
    {
        get => (Brush)GetValue(PreviousButtonBackgroundBrushProperty);
        set => SetValue(PreviousButtonBackgroundBrushProperty, value);
    }

    public Brush PreviousButtonForegroundBrush
    {
        get => (Brush)GetValue(PreviousButtonForegroundBrushProperty);
        set => SetValue(PreviousButtonForegroundBrushProperty, value);
    }

    public Style PreviousButtonStyle
    {
        get => (Style)GetValue(PreviousButtonStyleProperty);
        set => SetValue(PreviousButtonStyleProperty, value);
    }

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty PreviousButtonOpacityProperty =
        DependencyProperty.Register(nameof(PreviousButtonOpacity), typeof(double), typeof(PanelSlideshow),
                                    new PropertyMetadata(1));

    public static readonly DependencyProperty PreviousButtonMarginProperty =
        DependencyProperty.Register(nameof(PreviousButtonMargin), typeof(Thickness), typeof(PanelSlideshow),
                                    new PropertyMetadata(new Thickness(16d)));

    public static readonly DependencyProperty PreviousButtonPaddingProperty =
        DependencyProperty.Register(nameof(PreviousButtonPadding), typeof(Thickness), typeof(PanelSlideshow),
                                    new PropertyMetadata(default(Thickness)));

    public static readonly DependencyProperty PreviousButtonHorizontalAlignmentProperty =
        DependencyProperty.Register(nameof(PreviousButtonHorizontalAlignment), typeof(HorizontalAlignment), typeof(PanelSlideshow),
                                    new PropertyMetadata(HorizontalAlignment.Left));

    public static readonly DependencyProperty PreviousButtonVerticalAlignmentProperty =
        DependencyProperty.Register(nameof(PreviousButtonVerticalAlignment), typeof(VerticalAlignment), typeof(PanelSlideshow),
                                    new PropertyMetadata(VerticalAlignment.Bottom));

    public static readonly DependencyProperty PreviousButtonCornerRadiusProperty =
        DependencyProperty.Register(nameof(PreviousButtonCornerRadius), typeof(CornerRadius), typeof(PanelSlideshow),
                                    new PropertyMetadata(new CornerRadius(16)));

    public static readonly DependencyProperty PreviousButtonWidthProperty =
        DependencyProperty.Register(nameof(PreviousButtonWidth), typeof(double), typeof(PanelSlideshow),
                                    new PropertyMetadata(32d));

    public static readonly DependencyProperty PreviousButtonMaxWidthProperty =
        DependencyProperty.Register(nameof(PreviousButtonMaxWidth), typeof(double), typeof(PanelSlideshow),
                                    new PropertyMetadata(double.PositiveInfinity));

    public static readonly DependencyProperty PreviousButtonMinWidthProperty =
        DependencyProperty.Register(nameof(PreviousButtonMinWidth), typeof(double), typeof(PanelSlideshow),
                                    new PropertyMetadata(0));

    public static readonly DependencyProperty PreviousButtonHeightProperty =
        DependencyProperty.Register(nameof(PreviousButtonHeight), typeof(double), typeof(PanelSlideshow),
                                    new PropertyMetadata(32d));

    public static readonly DependencyProperty PreviousButtonMaxHeightProperty =
        DependencyProperty.Register(nameof(PreviousButtonMaxHeight), typeof(double), typeof(PanelSlideshow),
                                    new PropertyMetadata(double.PositiveInfinity));

    public static readonly DependencyProperty PreviousButtonMinHeightProperty =
        DependencyProperty.Register(nameof(PreviousButtonMinHeight), typeof(double), typeof(PanelSlideshow),
                                    new PropertyMetadata(0));

    public static readonly DependencyProperty PreviousButtonVisibilityModeProperty =
        DependencyProperty.Register(nameof(PreviousButtonVisibilityMode), typeof(NewPipsPagerNavigationMode), typeof(PanelSlideshow),
                                    new PropertyMetadata(NewPipsPagerNavigationMode.Visible));

    public static readonly DependencyProperty PreviousButtonBackgroundBrushProperty =
        DependencyProperty.Register(nameof(PreviousButtonBackgroundBrush), typeof(Brush), typeof(PanelSlideshow),
                                    new PropertyMetadata(null));

    public static readonly DependencyProperty PreviousButtonForegroundBrushProperty =
        DependencyProperty.Register(nameof(PreviousButtonForegroundBrush), typeof(Brush), typeof(PanelSlideshow),
                                    new PropertyMetadata(null));

    public static readonly DependencyProperty PreviousButtonStyleProperty =
        DependencyProperty.Register(nameof(PreviousButtonStyle), typeof(Style), typeof(PanelSlideshow),
                                    new PropertyMetadata(null));

    #endregion
}
