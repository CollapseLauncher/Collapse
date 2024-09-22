// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// ReSharper disable PartialTypeWithSinglePart
namespace Hi3Helper.CommunityToolkit.WinUI.Controls;

/// <summary>
/// The <see cref="ImageCropperThumb"/> control is used for <see cref="ImageCropper"/>.
/// </summary>
public partial class ImageCropperThumb : Control
{
    private readonly TranslateTransform _layoutTransform = new();
    internal const string NormalState = "Normal";
    internal const string PointerOverState = "PointerOver";
    internal const string PressedState = "Pressed";
    internal const string DisabledState = "Disabled";
    internal ThumbPosition Position { get; set; }

    /// <summary>
    /// Gets or sets the X coordinate of the ImageCropperThumb.
    /// </summary>
    public double X
    {
        get { return (double)GetValue(XProperty); }
        set { SetValue(XProperty, value); }
    }

    /// <summary>
    /// Gets or sets the Y coordinate of the ImageCropperThumb.
    /// </summary>
    public double Y
    {
        get { return (double)GetValue(YProperty); }
        set { SetValue(YProperty, value); }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageCropperThumb"/> class.
    /// </summary>
    public ImageCropperThumb()
    {
        DefaultStyleKey = typeof(ImageCropperThumb);
        RenderTransform = _layoutTransform;
        ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY;
        SizeChanged += ImageCropperThumb_SizeChanged;
    }

    protected override void OnApplyTemplate()
    {
        PointerEntered -= Control_PointerEntered;
        PointerExited -= Control_PointerExited;
        PointerCaptureLost -= Control_PointerCaptureLost;
        PointerCanceled -= Control_PointerCanceled;

        PointerEntered += Control_PointerEntered;
        PointerExited += Control_PointerExited;
        PointerCaptureLost += Control_PointerCaptureLost;
        PointerCanceled += Control_PointerCanceled;
    }

    private void ImageCropperThumb_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (_layoutTransform != null)
        {
            _layoutTransform.X = X - (this.ActualWidth / 2);
            _layoutTransform.Y = Y - (this.ActualHeight / 2);
        }
    }

    private static void OnXChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var target = (ImageCropperThumb)d;
        target.UpdatePosition();
    }



    private static void OnYChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var target = (ImageCropperThumb)d;
        target.UpdatePosition();
    }

    /// <summary>
    /// Identifies the <see cref="X"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty XProperty =
        DependencyProperty.Register(nameof(X), typeof(double), typeof(ImageCropperThumb), new PropertyMetadata(0d, OnXChanged));

    /// <summary>
    /// Identifies the <see cref="Y"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty YProperty =
        DependencyProperty.Register(nameof(Y), typeof(double), typeof(ImageCropperThumb), new PropertyMetadata(0d, OnYChanged));

    public void Control_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);
        VisualStateManager.GoToState(this, PointerOverState, true);
    }

    public void Control_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        base.OnPointerExited(e);
        VisualStateManager.GoToState(this, NormalState, true);
    }

    private void Control_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        VisualStateManager.GoToState(this, NormalState, true);
    }

    private void Control_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        base.OnPointerCanceled(e);
        VisualStateManager.GoToState(this, NormalState, true);
    }

    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        base.OnPointerPressed(e);
        VisualStateManager.GoToState(this, PressedState, true);
    }

    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        base.OnPointerReleased(e);
        VisualStateManager.GoToState(this, NormalState, true);
    }
}
