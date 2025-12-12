using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace BackgroundTest.CustomControl.PanelSlideshow;

internal partial class MoreThanZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value.TryGetDouble() > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}