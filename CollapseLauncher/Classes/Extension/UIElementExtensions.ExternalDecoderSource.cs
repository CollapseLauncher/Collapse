using CollapseLauncher.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Extension;

public static partial class UIElementExtensions
{
    /// <summary>
    /// Sets uniform corner radius size. If value is set to -1, the rounded corner radius will be applied.
    /// </summary>
    public static readonly DependencyProperty ExternalDecoderSourceProperty =
        DependencyProperty.RegisterAttached("ExternalDecoderSource",
                                            typeof(object),
                                            typeof(Image),
                                            new PropertyMetadata(null));

    public static object? GetExternalDecoderSource(DependencyObject obj) => obj.GetValue(ExternalDecoderSourceProperty);

    public static void SetExternalDecoderSource(DependencyObject obj, object? value)
    {
        if (obj is not Image image)
        {
            return;
        }

        image.Source = ImageDecoderValueConverter.Shared.Convert(value, typeof(object), null, null) as ImageSource;
    }
}
