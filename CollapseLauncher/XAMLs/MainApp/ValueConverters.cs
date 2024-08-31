using Hi3Helper;
using Hi3Helper.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using Windows.Globalization.NumberFormatting;

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

    public class DownloadSpeedLimitToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double asDouble)
            {
                long valBfromM = (long)(asDouble * (1 << 20));
                return valBfromM > 0 ?
                    string.Format(Locale.Lang._Misc.IsBytesMoreThanBytes, valBfromM, string.Format(Locale.Lang._Misc.SpeedPerSec, ConverterTool.SummarizeSizeSimple(valBfromM))) :
                    string.Format(Locale.Lang._Misc.IsBytesUnlimited);
            }
            return Locale.Lang._Misc.IsBytesNotANumber;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class DownloadChunkSizeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double asDouble)
            {
                int valBfromM = (int)(asDouble * (1 << 20));
                return valBfromM > 0 ?
                    string.Format(Locale.Lang._Misc.IsBytesMoreThanBytes, valBfromM, ConverterTool.SummarizeSizeSimple(valBfromM)) :
                    string.Format(Locale.Lang._Misc.IsBytesUnlimited);
            }
            return Locale.Lang._Misc.IsBytesNotANumber;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class BooleanToOpacityDimmConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool asBoolean)
                return asBoolean ? 1 : 0.45;

            return 0.45;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class DoubleFormatter : INumberFormatter2, INumberParser
    {
        private const string Format = "{0:F5}";
        public string FormatDouble(double value) => string.Format(Format, value);
        public double? ParseDouble(string text) => double.TryParse(text, out var dbl) ? dbl : null;

        public string FormatInt(long value) => throw new NotSupportedException();
        public string FormatUInt(ulong value) => throw new NotSupportedException();
        public long? ParseInt(string text) => throw new NotSupportedException();
        public ulong? ParseUInt(string text) => throw new NotSupportedException();
    }
}
