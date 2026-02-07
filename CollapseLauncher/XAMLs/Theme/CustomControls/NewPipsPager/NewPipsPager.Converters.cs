using Microsoft.UI.Xaml.Data;
using System;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.NewPipsPager;

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