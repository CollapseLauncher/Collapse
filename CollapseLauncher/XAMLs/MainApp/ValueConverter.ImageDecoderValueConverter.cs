using CollapseLauncher.XAMLs.Theme.CustomControls;
using Hi3Helper;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Data;
using System;
// ReSharper disable AccessToDisposedClosure

#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Pages;

public partial class ImageDecoderValueConverter : IValueConverter
{
    public static readonly ImageDecoderValueConverter Shared = new();

    private static readonly ILogger Logger = ILoggerHelper.GetILogger("ImageDecoderValueConverter");

    public object? Convert(object? value, Type targetType, object? parameter, string? language)
        => LayeredBackgroundImageExtensions.GetImageSource(value, Logger) ?? value;

    public object ConvertBack(object? value, Type targetType, object? parameter, string? language)
    {
        throw new NotSupportedException();
    }
}
