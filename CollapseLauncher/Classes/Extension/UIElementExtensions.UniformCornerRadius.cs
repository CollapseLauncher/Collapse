using Hi3Helper.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Extension;

public static partial class UIElementExtensions
{
    private static readonly Size ZeroSize = default;

    /// <summary>
    /// Sets uniform corner radius size. If value is set to -1, the rounded corner radius will be applied.
    /// </summary>
    public static readonly DependencyProperty UniformCornerRadiusProperty =
        DependencyProperty.RegisterAttached("UniformCornerRadius", typeof(double),
                                            typeof(FrameworkElement), new PropertyMetadata(0));

    public static double GetUniformCornerRadius(DependencyObject obj) => obj.GetValue(CursorTypeProperty).TryGetDouble();

    public static void SetUniformCornerRadius(DependencyObject obj, double value)
    {
        if (obj is not FrameworkElement asElement)
        {
            return;
        }

        UnsetUniformCornerRadiusEvent(asElement);
        if (value < 0)
        {
            ApplyUniformCornerRadiusSize(asElement, asElement.RenderSize);
            SetUniformCornerRadiusEvent(asElement);
            return;
        }

        ApplyUniformCornerRadiusSize(asElement, new CornerRadius(value));
    }

    private static void UnsetUniformCornerRadiusEvent(FrameworkElement element)
    {
        element.SizeChanged -= UniformCornerRadius_ElementOnSizeChanged;
    }

    private static void SetUniformCornerRadiusEvent(FrameworkElement element)
    {
        element.SizeChanged += UniformCornerRadius_ElementOnSizeChanged;
    }

    private static void ApplyUniformCornerRadiusSize(FrameworkElement element, Size size)
    {
        double preferredSize = size.Width < size.Height
            ? size.Width
            : size.Height;

        if (preferredSize < 0)
        {
            preferredSize = 0;
        }

        ApplyUniformCornerRadiusSize(element, new CornerRadius(preferredSize / 2));
    }

    private static void ApplyUniformCornerRadiusSize(FrameworkElement element, CornerRadius cornerRadius)
    {
        switch (element)
        {
            case Control asControl:
                asControl.CornerRadius = cornerRadius;
                break;
            case StackPanel asStackPanel:
                asStackPanel.CornerRadius = cornerRadius;
                break;
            case Grid asGrid:
                asGrid.CornerRadius = cornerRadius;
                break;
        }
    }

    private static void UniformCornerRadius_ElementOnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            e.NewSize == ZeroSize)
        {
            return;
        }

        ApplyUniformCornerRadiusSize(element, e.NewSize);
    }
}
