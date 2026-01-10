using Microsoft.UI.Xaml.Data;
using System;

namespace BackgroundTest.CustomControl.NewPipsPager;

internal partial class IndexAddOneConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int asInt)
        {
            return asInt + 1;
        }

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}