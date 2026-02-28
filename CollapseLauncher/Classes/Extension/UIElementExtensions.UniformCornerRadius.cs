using Hi3Helper.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Extension;

public static partial class UIElementExtensions
{
    /// <summary>
    /// Sets uniform corner radius size. If value is set to -1, the rounded corner radius will be applied.
    /// </summary>
    public static readonly DependencyProperty UniformCornerRadiusProperty =
        DependencyProperty.RegisterAttached("UniformCornerRadius", typeof(double),
                                            typeof(Control), new PropertyMetadata(0));

    public static double GetUniformCornerRadius(DependencyObject obj) => obj.GetValue(CursorTypeProperty).TryGetDouble();

    public static void SetUniformCornerRadius(DependencyObject obj, double value)
    {
        if (obj is not Control element)
        {
            return;
        }

        UnsetUniformCornerRadiusEvent(element);
        if (value < 0)
        {
            ApplyUniformCornerRadiusSize(element, element.RenderSize);
            SetUniformCornerRadiusEvent(element);
            return;
        }

        element.CornerRadius = new CornerRadius(value);
    }

    private static void UnsetUniformCornerRadiusEvent(Control element)
    {
        element.SizeChanged -= UniformCornerRadius_ElementOnSizeChanged;
    }

    private static void SetUniformCornerRadiusEvent(Control element)
    {
        element.SizeChanged += UniformCornerRadius_ElementOnSizeChanged;
    }

    private static void ApplyUniformCornerRadiusSize(Control element, Size size)
    {
        double preferredSize = size.Width < size.Height
            ? size.Width
            : size.Height;

        if (preferredSize < 0)
        {
            preferredSize = 0;
        }
        element.CornerRadius = new CornerRadius(preferredSize / 2);
    }

    private static readonly Size ZeroSize = default;

    private static void UniformCornerRadius_ElementOnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Control element ||
            e.NewSize == ZeroSize)
        {
            return;
        }

        ApplyUniformCornerRadiusSize(element, e.NewSize);
    }
}
