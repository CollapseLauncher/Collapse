using CollapseLauncher.Helper;
using CollapseLauncher.Plugins;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Update;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Buffers;
using System.Collections;
using System.IO;
using System.Net.Http;
using System.Numerics;
using System.Threading;
using Windows.Globalization.NumberFormatting;

// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable CheckNamespace

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

    public partial class CountSingleToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is ICollection asCollection)
            {
                return asCollection.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            }

            return value.TryGetDouble() < 2 ? Visibility.Collapsed : Visibility.Visible;
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

    public partial class BooleanToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double thisValue = value is not bool boolean || boolean ? 1.0d : 0d;
            return typeof(float) == targetType ? (float)thisValue : thisValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class FloorFloatingValueConverter : IValueConverter
    {
        public virtual object Convert(object value, Type targetType, object parameter, string language)
        {
            double valEvaluated = Math.Floor((double)value);
            if (!double.IsFinite(valEvaluated) ||
                double.IsNaN(valEvaluated) ||
                double.IsInfinity(valEvaluated))
            {
                return null;
            }

            return valEvaluated;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class PercentageValueConverter : FloorFloatingValueConverter
    {
        public override object Convert(object value, Type targetType, object parameter, string language)
        {
            double result = (double)base.Convert(value, targetType, parameter, language);
            return $"{result}%";
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

#nullable enable
    public partial class UrlToCachedImagePathConverter : IValueConverter
    {
        internal static         CDNCache   CacheManager;
        private static readonly HttpClient Client;

        static UrlToCachedImagePathConverter()
        {
            Client = new HttpClientBuilder()
                    .UseLauncherConfig()
                    .Create();
            CacheManager = new CDNCache
            {
                IsUseAggressiveMode        = true,
                CurrentCacheDir            = LauncherConfig.AppGameImgCachedFolder,
                Logger                     = ILoggerHelper.GetILogger(nameof(UrlToCachedImagePathConverter)),
                MaxAcceptedCacheExpireTime = TimeSpan.FromDays(7)
            };
            LauncherConfig.AppGameFolderChanged += AppGameFolderChanged;
        }

        private static void AppGameFolderChanged(string path)
        {
            string changedCacheFolder = LauncherConfig.AppGameImgCachedFolder;
            CacheManager.CurrentCacheDir = changedCacheFolder;

            CacheManager.Logger?.LogDebug("App game folder has been changed to: {path}", path);
            CacheManager.Logger?.LogDebug("Sprite cache folder has been changed to: {path}", changedCacheFolder);
        }

        public object? Convert(object? value, Type targetType, object parameter, string language)
        {
            Uri?    sourceAsUri    = value as Uri;
            string? sourceAsString = value as string;

            if ((sourceAsUri == null &&
                !string.IsNullOrEmpty(sourceAsString) &&
                !Uri.TryCreate(sourceAsString, UriKind.Absolute, out sourceAsUri)) ||
                sourceAsUri == null)
            {
                return value;
            }

            // Return if it's already a local path
            if (sourceAsUri.IsFile)
            {
                return sourceAsUri;
            }

            // Check if the file is already cached
            if (CacheManager.TryGetAggressiveCachedFilePath(sourceAsUri, out sourceAsUri))
            {
                return sourceAsUri;
            }

            // Otherwise, download the file in background while also returns the origin URL temporarily.
            new Thread(() => BeginBackgroundDownload(sourceAsUri))
            {
                IsBackground = true
            }.Start();
            return sourceAsUri;
        }

        private static async void BeginBackgroundDownload(Uri? url)
        {
            try
            {
                if (url == null)
                {
                    return;
                }

                CDNCacheResult result = await CacheManager.TryGetCachedStreamFrom(Client, url, token: CancellationToken.None);
                await using Stream stream = result.Stream;
                await stream.CopyToAsync(Stream.Null); // Copy over with CopyToStream in the background.
            }
            catch (Exception ex)
            {
                CacheManager.Logger?.LogError(ex, "An error has occurred while trying to download content from: {url}", url);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class StringIsNullOrEmptyToBooleanConverter : IValueConverter
    {
        public virtual object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string asString &&
                !string.IsNullOrEmpty(asString))
            {
                return false;
            }

            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class InverseStringIsNullOrEmptyToBooleanConverter : StringIsNullOrEmptyToBooleanConverter
    {
        public override object Convert(object value, Type targetType, object parameter, string language)
            => !(bool)base.Convert(value, targetType, parameter, language);
    }

    public partial class IsNumberEqualToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double number          = value.TryGetDouble();
            double parameterNumber = parameter.TryGetDouble();

            if (double.IsNaN(number) &&
                double.IsNaN(parameterNumber))
            {
                return true;
            }

            double absTolerance = Math.Abs(number - parameterNumber);
            return absTolerance < 1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            double parameterValue = parameter.TryGetDouble();
            return GetDoubleValueTo(parameterValue, targetType);

            static object GetDoubleValueTo(double value, Type targetType)
            {
                if (targetType == typeof(byte))
                {
                    return value.TryGetInteger<double, byte>();
                }

                if (targetType == typeof(sbyte))
                {
                    return value.TryGetInteger<double, sbyte>();
                }

                if (targetType == typeof(ushort))
                {
                    return value.TryGetInteger<double, ushort>();
                }

                if (targetType == typeof(short))
                {
                    return value.TryGetInteger<double, short>();
                }

                if (targetType == typeof(uint))
                {
                    return value.TryGetInteger<double, uint>();
                }

                if (targetType == typeof(int))
                {
                    return value.TryGetInteger<double, int>();
                }

                if (targetType == typeof(ulong))
                {
                    return value.TryGetInteger<double, ulong>();
                }

                if (targetType == typeof(long))
                {
                    return value.TryGetInteger<double, long>();
                }

                if (targetType == typeof(float))
                {
                    return (float)value;
                }

                return value;
            }
        }
    }

    public partial class IsContainsAnyNumbersToBooleanConverter : IValueConverter
    {
        private const           string             SeparatorsStr = ",}-/\\#;";
        private static readonly SearchValues<char> Separators    = SearchValues.Create(SeparatorsStr);

        public virtual object Convert(object value, Type targetType, object parameter, string language)
        {
            double valueAsDouble = value.TryGetDouble();
            if (parameter is not string asStringToSplit)
            {
                return false;
            }

            ReadOnlySpan<char> stringSpan      = asStringToSplit;
            int                entriesCountMin = stringSpan.CountAny(Separators) + 1;

            Span<Range> entriesSpan = stackalloc Range[entriesCountMin];
            int entriesCount = stringSpan.SplitAny(entriesSpan,
                                                   SeparatorsStr,
                                                   StringSplitOptions.RemoveEmptyEntries |
                                                   StringSplitOptions.TrimEntries);

            if (entriesCount == 0)
            {
                return false;
            }

            Span<double> entries = stackalloc double[entriesCount];
            for (int i = 0; i < entriesCount; i++)
            {
                if (!double.TryParse(stringSpan[entriesSpan[i]], out double result))
                {
                    return false;
                }

                entries[i] = result;
            }

            return entries.Contains(valueAsDouble);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class InverseIsContainsAnyNumbersToBooleanConverter : IsContainsAnyNumbersToBooleanConverter
    {
        public override object Convert(object value, Type targetType, object parameter, string language)
            => !(bool)base.Convert(value, targetType, parameter, language);
    }

    public partial class TimeSpanToTimeStampStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (parameter is not string format)
            {
                throw new
                    InvalidOperationException("ConverterParameter must be in form of valid format string for TimeSpan.ToString()");
            }

            int        len              = (int)BitOperations.RoundUpToPowerOf2((uint)format.Length * 2);
            Span<char> charFormatBuffer = stackalloc char[len];

            if (value is not TimeSpan asTimeSpan)
            {
                double asDoubleMilliseconds = value.TryGetDouble();
                if (!double.IsFinite(asDoubleMilliseconds))
                {
                    throw new InvalidOperationException("Value must be in forms of number and finite!");
                }

                asTimeSpan = TimeSpan.FromMilliseconds(asDoubleMilliseconds);
            }

            if (!asTimeSpan.TryFormat(charFormatBuffer, out int bytesWritten, format))
            {
                throw new
                    InvalidOperationException("Cannot format the TimeSpan as either the format string or value might be invalid");
            }

            return new string(charFormatBuffer[..bytesWritten]);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    public partial class TimeSpanToNumberMillisecondsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not TimeSpan asTimeSpan)
            {
                throw new InvalidOperationException("Type of the value must be a TimeSpan!");
            }

            double asValue = asTimeSpan.TotalMilliseconds;
            return asValue.GetDoubleValueTo(targetType);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            double valueAsDouble = value.TryGetDouble();
            if (!double.IsFinite(valueAsDouble))
            {
                throw new InvalidOperationException("Cannot get actual finite number");
            }

            return TimeSpan.FromMilliseconds(valueAsDouble);
        }
    }

    public partial class MediaAutoplayWindowOverrideConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (!WindowUtility.CurrentWindowIsVisible)
            {
                return false;
            }

            return value is true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class BackgroundParallaxPixelToComboBoxIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            int amount = (int)value.TryGetDouble();
            return amount switch
            {
                2 => 0,
                4 => 1,
                8 => 2,
                _ => 3
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
