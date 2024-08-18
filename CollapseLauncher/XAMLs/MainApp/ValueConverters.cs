using Hi3Helper.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace CollapseLauncher.Pages
{
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string langInfo)
        {
            if (targetType != typeof(bool))
                throw new InvalidOperationException("The target must be a boolean");

            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string langInfo)
        {
            throw new NotSupportedException();
        }
    }

    public class BooleanVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string input) => (bool)value ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, string input) => new NotImplementedException();
    }

    public class InverseBooleanVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string input) => !(bool)value ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, string input) => new NotImplementedException();
    }

    public class DoubleRound2Converter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string input) => Math.Round((double)value, 2);
        public object ConvertBack(object value, Type targetType, object parameter, string input) => new NotImplementedException();
    }

    public class DoubleRound3Converter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string input) => Math.Round((double)value, 3);
        public object ConvertBack(object value, Type targetType, object parameter, string input) => new NotImplementedException();
    }

    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string input) => (value is string asString) && !string.IsNullOrEmpty(asString) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, string input) => new NotImplementedException();
    }

    public class FileSizeToStringLiteralConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string input) => ConverterTool.SummarizeSizeSimple((long)value);
        public object ConvertBack(object value, Type targetType, object parameter, string input) => new NotImplementedException();
    }
}
