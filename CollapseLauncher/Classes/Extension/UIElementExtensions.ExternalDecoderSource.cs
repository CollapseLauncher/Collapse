using CollapseLauncher.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Extension;

public static partial class UIElementExtensions
{
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
        obj.SetValue(ExternalDecoderSourceProperty, value);
    }

    public static readonly DependencyProperty ExternalDecoderImageExSourceProperty =
        DependencyProperty.RegisterAttached("ExternalDecoderImageExSourceSource",
                                            typeof(object),
                                            typeof(ImageEx.ImageEx),
                                            new PropertyMetadata(null));

    public static readonly DependencyProperty ExternalDecoderImageExPlaceholderSourceProperty =
        DependencyProperty.RegisterAttached("ExternalDecoderImageExPlaceholderSource",
                                            typeof(object),
                                            typeof(ImageEx.ImageEx),
                                            new PropertyMetadata(null));

    public static object? GetExternalDecoderImageExSource(DependencyObject obj) => obj.GetValue(ExternalDecoderImageExSourceProperty);

    public static void SetExternalDecoderImageExSource(DependencyObject obj, object? value)
    {
        if (obj is not ImageEx.ImageEx image)
        {
            return;
        }

        image.Source = ImageDecoderValueConverter.Shared.Convert(value, typeof(object), null, null) as ImageSource;
        obj.SetValue(ExternalDecoderImageExSourceProperty, value);
    }

    public static object? GetExternalDecoderImageExPlaceholderSource(DependencyObject obj) => obj.GetValue(ExternalDecoderImageExPlaceholderSourceProperty);

    public static void SetExternalDecoderImageExPlaceholderSource(DependencyObject obj, object? value)
    {
        if (obj is not ImageEx.ImageEx image)
        {
            return;
        }

        image.PlaceholderSource = ImageDecoderValueConverter.Shared.Convert(value, typeof(object), null, null) as ImageSource;
        obj.SetValue(ExternalDecoderImageExPlaceholderSourceProperty, value);
    }
}
