using CollapseLauncher.Helper.Image;
using CollapseLauncher.Plugins;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Update;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections;
using System.IO;
using Windows.Globalization.NumberFormatting;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable ConvertIfStatementToSwitchStatement

namespace CollapseLauncher.Pages
{
    public partial class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string langInfo)
        {
            if (targetType != typeof(bool))
                throw new InvalidOperationException("The value must be a boolean");

            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string langInfo)
        {
            if (targetType != typeof(bool))
                throw new NotSupportedException("The value must be a boolean");

            return !(bool)value;
        }
    }

    public partial class BooleanVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string input) => (bool)value ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, string input) => (Visibility)value == Visibility.Visible;
    }

    public partial class InverseBooleanVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string input) => !(bool)value ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, string input) => (Visibility)value == Visibility.Collapsed;
    }

    public partial class DoubleRound2Converter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string input) => Math.Round((double)value, 2);
        public object ConvertBack(object value, Type targetType, object parameter, string input) => new NotImplementedException();
    }

    public partial class DoubleRound3Converter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string input) => Math.Round((double)value, 3);
        public object ConvertBack(object value, Type targetType, object parameter, string input) => new NotImplementedException();
    }

    public partial class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string input) => value is string asString && !string.IsNullOrEmpty(asString) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, string input) => new NotImplementedException();
    }

    public partial class FileSizeToStringLiteralConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string input)
        {
            if (value is double asDouble)
            {
                return ConverterTool.SummarizeSizeSimple(asDouble);
            }

            if (value is float asFloat)
            {
                return ConverterTool.SummarizeSizeSimple(asFloat);
            }

            return ConverterTool.SummarizeSizeSimple((long)value);
        }
        public object ConvertBack(object value, Type targetType, object parameter, string input) => new NotImplementedException();
    }

    public partial class DownloadSpeedLimitToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not double asDouble)
            {
                return Locale.Lang._Misc.IsBytesNotANumber;
            }

            long valBFromM = (long)(asDouble * (1 << 20));
            return valBFromM > 0 ?
                string.Format(Locale.Lang._Misc.IsBytesMoreThanBytes, valBFromM, string.Format(Locale.Lang._Misc.SpeedPerSec, ConverterTool.SummarizeSizeSimple(valBFromM))) :
                string.Format(Locale.Lang._Misc.IsBytesUnlimited);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class DownloadChunkSizeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not double asDouble)
            {
                return Locale.Lang._Misc.IsBytesNotANumber;
            }

            int valBFromM = (int)(asDouble * (1 << 20));
            return valBFromM > 0 ?
                string.Format(Locale.Lang._Misc.IsBytesMoreThanBytes, valBFromM, ConverterTool.SummarizeSizeSimple(valBFromM)) :
                string.Format(Locale.Lang._Misc.IsBytesUnlimited);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class DoubleFormatter : INumberFormatter2, INumberParser
    {
        private const string Format = "{0:F5}";
        public string FormatDouble(double value) => string.Format(Format, value);
        public double? ParseDouble(string text) => double.TryParse(text, out var dbl) ? dbl : null;

        public string FormatInt(long value) => throw new NotSupportedException();
        public string FormatUInt(ulong value) => throw new NotSupportedException();
        public long? ParseInt(string text) => throw new NotSupportedException();
        public ulong? ParseUInt(string text) => throw new NotSupportedException();
    }

    public partial class CapsuleCornerRadiusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not double d)
            {
                return 0;
            }

            double padding = 0;
            if (parameter is double asDouble)
            {
                padding = asDouble;
            }
            return (d - padding) / 2.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is ICollection asCollection)
            {
                return asCollection.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            
            return value.Equals(0) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class InverseCountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
            => value.Equals(0) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class InverseNumberToBoolConverter : NumberToBoolConverter
    {
        public override object Convert(object value, Type targetType, object parameter, string language)
        {
            return !(bool)base.Convert(value, targetType, parameter, language);
        }
    }

    public partial class NumberToBoolConverter : IValueConverter
    {
        public virtual object Convert(object value, Type targetType, object parameter, string language)
        {
            return value switch
                   {
                       double asDouble => asDouble > 0,
                       float asFloat => asFloat > 0,
                       long asLong => asLong > 0,
                       ulong asULong => asULong > 0,
                       int asInt => asInt > 0,
                       uint asUInt => asUInt > 0,
                       short asShort => asShort > 0,
                       ushort asUShort => asUShort > 0,
                       byte asByte => asByte > 0,
                       sbyte asSByte => asSByte > 0,
                       _ => throw new InvalidDataException()
                   };
        }

        public virtual object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class GameNameLocaleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string asString)
            {
                return InnerLauncherConfig.GetGameTitleRegionTranslationString(asString, Locale.Lang._GameClientTitles);
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class GameRegionLocaleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string asString)
            {
                return InnerLauncherConfig.GetGameTitleRegionTranslationString(asString, Locale.Lang._GameClientRegions);
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class ByStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string asString)
            {
                return string.Format(Locale.Lang._SettingsPage.Plugin_AuthorBy, asString);
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class GamePluginIconConverter : IValueConverter
    {
#nullable enable
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not PluginInfo asPluginInfo)
            {
                return GetDefault();
            }

            try
            {
                string?  iconUrl = null;
                if (asPluginInfo.Instance is { } pluginInstance)
                {
                    pluginInstance.GetPluginAppIconUrl(out iconUrl);
                }

                if (string.IsNullOrEmpty(iconUrl))
                {
                    iconUrl = asPluginInfo.PluginManifest.PluginAlternativeIcon;
                }

                if (string.IsNullOrEmpty(iconUrl))
                {
                    return GetDefault();
                }

                Uri? url;
                if (PluginLauncherApiWrapper.IsDataActuallyBase64(iconUrl))
                {
                    string  spriteFolder = Path.Combine(LauncherConfig.AppGameImgFolder, "cached");
                    string? urlStr       = PluginLauncherApiWrapper.CopyOverEmbeddedData(spriteFolder, iconUrl);

                    if (string.IsNullOrEmpty(urlStr))
                    {
                        return GetDefault();
                    }

                    url = new Uri(urlStr);
                }
                else if (!Uri.TryCreate(iconUrl, UriKind.Absolute, out url))
                {
                    return GetDefault();
                }

                return new BitmapIcon
                {
                    UriSource = url,
                    ShowAsMonochrome = false
                };
            }
            catch (Exception ex)
            {
                string message = $"[GamePluginIconConverter::Convert()] Cannot get icon from plugin: {asPluginInfo.Name}";
                Logger.LogWriteLine(message, LogType.Error, true);
                SentryHelper.ExceptionHandler(ex);
            }

            return GetDefault();

            FontIcon GetDefault() => new()
            {
                Glyph    = "\uE74C",
                FontSize = 20,
                Width    = 20,
                Height   = 20
            };
        }
#nullable restore

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class LocalFullDateTimeConverter : IValueConverter
    {
        public const string FullFormat = "dddd, MMMM dd, yyyy hh:mm tt (zzz)";

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value switch
                   {
                       DateTime asDateTime => GetFullFormat(asDateTime.ToLocalTime()),
                       DateTimeOffset asDateTimeOffset => GetFullFormat(asDateTimeOffset.ToLocalTime()),
                       _ => value
                   };

            string GetFullFormat(DateTimeOffset offset) => offset.ToString(FullFormat);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class TimeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not double asDouble)
            {
                return Locale.Lang._Misc.IsBytesNotANumber;
            }

            TimeSpan span = TimeSpan.FromSeconds(asDouble);
            return span.ToString("mm\\:ss");
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class BooleanToIsEnabledOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double thisValue = value is not bool boolean || boolean ? 1.0d : 0.25d;
            return typeof(float) == targetType ? (float)thisValue : thisValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /*
    public partial class LocaleCodeToFlagUrlConverter : IValueConverter
    {
        private const string Separator = "-_";
        private const StringSplitOptions Options = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not string asString)
            {
                return null;
            }

            Span<Range>        splitRange = stackalloc Range[2];
            ReadOnlySpan<char> asSpan     = asString.AsSpan();
            int                rangeLen   = asSpan.SplitAny(splitRange, Separator, Options);

            if (rangeLen != 2)
            {
                return null;
            }

            ReadOnlySpan<char> countryId = asSpan[splitRange[1]];
            if (countryId.Equals("419", StringComparison.OrdinalIgnoreCase))
            {
                countryId = "es";
            }

            return $"https://flagcdn.com/{countryId}.svg";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    */

    public partial class UpdateToVersionStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
            => string.Format(Locale.Lang._PluginManagerPage.ListViewItemUpdateStatusAvailableButton,
                             value switch
                             {
                                 PluginManifest asManifest => asManifest.PluginVersion,
                                 null => GameVersion.Empty,
                                 _ => (GameVersion)value
                             });

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class UpdatingPercentageStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
            => string.Format(Locale.Lang._PluginManagerPage.ListViewItemUpdateStatusAvailableButtonUpdating,
                             Math.Round((double)value, 2));

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class PluginUpdatedToVersionStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
            => string.Format(Locale.Lang._PluginManagerPage.ListViewItemUpdateStatusCompleted,
                             value switch
                             {
                                 PluginManifest asManifest => asManifest.PluginVersion,
                                 null => GameVersion.Empty,
                                 _ => (GameVersion)value
                             });

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class NullableVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
            => value is null ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class GetCachedUrlDataConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string stringUrl &&
                stringUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return ImageLoaderHelper.GetCachedSprites(stringUrl);
            }

            if (value is Uri uri &&
                uri.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return ImageLoaderHelper.GetCachedSprites(uri.AbsolutePath);
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
