﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
// ReSharper disable GrammarMistakeInComment
// ReSharper disable CommentTypo
// ReSharper disable CheckNamespace

#nullable enable
namespace Hi3Helper.Data
{
    public struct IniValue : IEquatable<IniValue>
    {
        #region Fields
        private string? _value;

        #endregion

        #region Methods
        public string? Value
        {
            get => _value;
            set => IsEmpty = string.IsNullOrEmpty(_value = value);
        }

        public bool IsEmpty { get; private set; } = true;

        #endregion

        #region Constructors
        /// <summary>
        /// Create an empty (default) value
        /// </summary>
        public IniValue() { }

        /// <summary>
        /// Create <seealso cref="IniValue"/> instance from any formattable objects
        /// </summary>
        /// <param name="value">Object to convert as value</param>
        public IniValue(object? value)
        {
            if (value is IFormattable formattableObj)
            {
                Value = formattableObj.ToString(null, IniFile.DefaultCulture);
                return;
            }

            Value = value?.ToString();
        }

        /// <summary>
        /// Create <seealso cref="IniValue"/> instance from <seealso cref="IFormattable"/> members
        /// </summary>
        /// <param name="formattableValue">Value to convert from <seealso cref="IFormattable"/> members</param>
        public IniValue(IFormattable? formattableValue) => Value = formattableValue?.ToString(null, IniFile.DefaultCulture);

        /// <summary>
        /// Create <seealso cref="IniValue"/> instance from a <seealso cref="Boolean"/>
        /// </summary>
        /// <param name="value">Value to convert from <seealso cref="Boolean"/></param>
        public IniValue(bool value) => Value = value ? "True" : "False";

        /// <summary>
        /// Create <seealso cref="IniValue"/> instance from a <seealso cref="string"/>
        /// </summary>
        /// <param name="value">Value to convert from <seealso cref="string"/></param>
        public IniValue(string? value) => Value = value;

        /// <summary>
        /// Create <seealso cref="IniValue"/> instance from <seealso cref="Size"/>
        /// </summary>
        /// <param name="value">Value to convert from <seealso cref="Size"/></param>
        public IniValue(Size value) => Value = $"{value.Width}x{value.Height}";

        /// <summary>
        /// Create <seealso cref="IniValue"/> instance from <seealso cref="Guid"/>
        /// </summary>
        /// <param name="value">Value to convert from <seealso cref="Guid"/></param>
        public IniValue(Guid value)
        {
            string guidAsString = value.ToString(null, IniFile.DefaultCulture);
            Value = guidAsString;
        }
        #endregion

        #region Create Methods
        public static IniValue Create<TEnum>(TEnum value)
            where TEnum : struct, Enum => new IniValue(Enum.GetName(value));

        public static IniValue Create(IFormattable? value) => new IniValue(value);

        public static IniValue Create(object? value) => new IniValue(value);

        public static IniValue Create(string? value) => new IniValue(value);

        public static IniValue Create(bool value) => new IniValue(value);

        public static IniValue Create(Size value) => new IniValue(value);

        public static IniValue CreateEmpty() => new IniValue();
        #endregion

        #region Try Methods
        /// <summary>
        /// Try parse value where it's a member of <seealso cref="ISpanParsable{TSelf}"/>
        /// </summary>
        /// <typeparam name="TNumber">A number value where it's a member of <seealso cref="ISpanParsable{TSelf}"/></typeparam>
        /// <param name="result">The output result of the parsed number</param>
        /// <returns>The number of the value member of <seealso cref="ISpanParsable{TSelf}"/></returns>
        public bool TryParseValue<TNumber>(out TNumber result)
            where TNumber : struct, ISpanParsable<TNumber>
        {
            ReadOnlySpan<char> valueSpan = _value;

            // Assign the default value first
            result = default;

            // If the valueSpan is empty, return false
            if (valueSpan.IsEmpty)
            {
                return false;
            }

            // Try parse value and return
            bool resultReturn = TNumber.TryParse(
                valueSpan,
                IniFile.DefaultCulture,
                out result);
            return resultReturn;
        }
        #endregion

        #region Converter Methods
        /// <summary>
        /// Convert the <seealso cref="IniValue"/> to <seealso cref="Size"/>
        /// </summary>
        /// <returns>Value of <seealso cref="Size"/></returns>
        public Size ToSize()
        {
            const string possibleSeparators = ",x:";

            // Assign value string to span
            ReadOnlySpan<char> valueString = _value;

            // If it's empty due to null or is empty, return default size
            if (valueString.IsEmpty)
            {
                return new Size();
            }

            // Allocate stack buffer for range and try search the splitted ranges
            Span<Range> ranges = stackalloc Range[8];
            valueString.Split(ranges, possibleSeparators);

            // Try split the value and get the default if one of the range
            // doesn't actually include the value.
            bool isWidthParsed = int.TryParse(
                valueString[ranges[0]],
                NumberStyles.Integer,
                IniFile.DefaultCulture,
                out int width);

            bool isHeightParsed = int.TryParse(
                valueString[ranges[1]],
                NumberStyles.Integer,
                IniFile.DefaultCulture,
                out int height);

            // If both parsed, return the value
            if (isWidthParsed && isHeightParsed)
            {
                return new Size(width, height);
            }

            // Get the first index of the separator
            int beginPossibleSeparators = valueString.IndexOfAny(possibleSeparators);

            // If the separator starts with index of 0 (in this case "x1080" for example),
            // then return the value of the height only
            return beginPossibleSeparators == 0 ?
                new Size(0, height) :
                // Otherwise, returns the format for width-only (in this case "1920x" for example).
                new Size(width, 0);
        }

        /// <summary>
        /// Convert the <seealso cref="IniValue"/> into <seealso cref="Guid"/>
        /// </summary>
        /// <returns>Value of <seealso cref="Guid"/>. If <see cref="IsEmpty"/> is true, or the value is not
        /// in a valid <seealso cref="Guid"/> form, then return <see cref="Guid.Empty"/></returns>
        public readonly Guid ToGuid()
        {
            // If the back value is empty, return an empty Guid
            if (IsEmpty)
            {
                return Guid.Empty;
            }

            // If the back value can be parsed to Guid, then return
            return Guid.TryParse(_value, out Guid guid) ? guid :
                // Otherwise, return the empty guid again
                Guid.Empty;
        }

        /// <summary>
        /// Convert the <seealso cref="IniValue"/> to <seealso cref="bool"/>
        /// </summary>
        /// <param name="defaultValue">The default value to be returned if the <seealso cref="IniValue"/> is invalid</param>
        /// <returns>Value of <seealso cref="bool"/></returns>
        public bool ToBool(bool defaultValue = false)
        {
            // Try parse the value
            return bool.TryParse(_value, out bool result) ? result :
                // Otherwise, return the default value if invalid.
                defaultValue;
        }

        /// <summary>
        /// Convert the <seealso cref="IniValue"/> to a nullable <seealso cref="bool"/>
        /// </summary>
        /// <returns>Value of a nullable <seealso cref="bool"/></returns>
        public bool? ToBoolNullable()
        {
            // If the value is empty, return null
            if (IsEmpty)
            {
                return null;
            }

            // Otherwise, return the actual boolean value
            return ToBool();
        }

        /// <summary>
        /// Convert the <seealso cref="IniValue"/> to any member of parsable numbers
        /// </summary>
        /// <typeparam name="TNumber">Type which is member of parsable numbers</typeparam>
        /// <param name="defaultValue">The default value to be returned if the <seealso cref="IniValue"/> is invalid</param>
        /// <returns>The parsed number</returns>
        public TNumber ToNumber<TNumber>(TNumber? defaultValue = null)
            where TNumber : struct, ISpanParsable<TNumber>
        {
            // Try parse the value and if it's valid, return the result
            if (TryParseValue(out TNumber result))
            {
                return result;
            }

            // Otherwise, return the default value
            return defaultValue ?? default;
        }

        /// <summary>
        /// Convert the <seealso cref="IniValue"/> to <seealso cref="sbyte"/>
        /// </summary>
        /// <param name="defaultValue">The default value to be returned if the <seealso cref="IniValue"/> is invalid</param>
        /// <returns>Value of <seealso cref="sbyte"/></returns>
        public sbyte ToSByte(sbyte defaultValue = 0) => ToNumber<sbyte>(defaultValue);

        /// <summary>
        /// Convert the <seealso cref="IniValue"/> to <seealso cref="byte"/>
        /// </summary>
        /// <param name="defaultValue">The default value to be returned if the <seealso cref="IniValue"/> is invalid</param>
        /// <returns>Value of <seealso cref="byte"/></returns>
        public byte ToByte(byte defaultValue = 0) => ToNumber<byte>(defaultValue);

        /// <summary>
        /// Convert the <seealso cref="IniValue"/> to <seealso cref="short"/>
        /// </summary>
        /// <param name="defaultValue">The default value to be returned if the <seealso cref="IniValue"/> is invalid</param>
        /// <returns>Value of <seealso cref="short"/></returns>
        public short ToShort(short defaultValue = 0) => ToNumber<short>(defaultValue);

        /// <summary>
        /// Convert the <seealso cref="IniValue"/> to <seealso cref="ushort"/>
        /// </summary>
        /// <param name="defaultValue">The default value to be returned if the <seealso cref="IniValue"/> is invalid</param>
        /// <returns>Value of <seealso cref="ushort"/></returns>
        public ushort ToShort(ushort defaultValue = 0) => ToNumber<ushort>(defaultValue);

        /// <summary>
        /// Convert the <seealso cref="IniValue"/> to <seealso cref="int"/>
        /// </summary>
        /// <param name="defaultValue">The default value to be returned if the <seealso cref="IniValue"/> is invalid</param>
        /// <returns>Value of <seealso cref="int"/></returns>
        public int ToInt(int defaultValue = 0) => ToNumber<int>(defaultValue);

        /// <summary>
        /// Convert the <seealso cref="IniValue"/> to <seealso cref="uint"/>
        /// </summary>
        /// <param name="defaultValue">The default value to be returned if the <seealso cref="IniValue"/> is invalid</param>
        /// <returns>Value of <seealso cref="uint"/></returns>
        public uint ToUInt(uint defaultValue = 0) => ToNumber<uint>(defaultValue);

        /// <summary>
        /// Convert the <seealso cref="IniValue"/> to <seealso cref="long"/>
        /// </summary>
        /// <param name="defaultValue">The default value to be returned if the <seealso cref="IniValue"/> is invalid</param>
        /// <returns>Value of <seealso cref="long"/></returns>
        public long ToLong(long defaultValue = 0) => ToNumber<long>(defaultValue);

        /// <summary>
        /// Convert the <seealso cref="IniValue"/> to <seealso cref="ulong"/>
        /// </summary>
        /// <param name="defaultValue">The default value to be returned if the <seealso cref="IniValue"/> is invalid</param>
        /// <returns>Value of <seealso cref="ulong"/></returns>
        public ulong ToUlong(ulong defaultValue = 0) => ToNumber<ulong>(defaultValue);

        /// <summary>
        /// Convert the <seealso cref="IniValue"/> to <seealso cref="Int128"/>
        /// </summary>
        /// <param name="defaultValue">The default value to be returned if the <seealso cref="IniValue"/> is invalid</param>
        /// <returns>Value of <seealso cref="Int128"/></returns>
        public Int128 ToInt128(Int128? defaultValue = null) => ToNumber(defaultValue);

        /// <summary>
        /// Convert the <seealso cref="IniValue"/> to <seealso cref="UInt128"/>
        /// </summary>
        /// <param name="defaultValue">The default value to be returned if the <seealso cref="IniValue"/> is invalid</param>
        /// <returns>Value of <seealso cref="UInt128"/></returns>
        public UInt128 ToUInt128(UInt128? defaultValue = null) => ToNumber(defaultValue);

        /// <summary>
        /// Convert the <seealso cref="IniValue"/> to <seealso cref="float"/>
        /// </summary>
        /// <param name="defaultValue">The default value to be returned if the <seealso cref="IniValue"/> is invalid</param>
        /// <returns>Value of <seealso cref="float"/></returns>
        public float ToFloat(float defaultValue = 0) => ToNumber<float>(defaultValue);

        /// <summary>
        /// Convert the <seealso cref="IniValue"/> to <seealso cref="double"/>
        /// </summary>
        /// <param name="defaultValue">The default value to be returned if the <seealso cref="IniValue"/> is invalid</param>
        /// <returns>Value of <seealso cref="double"/></returns>
        public double ToDouble(double defaultValue = 0) => ToNumber<double>(defaultValue);

        /// <summary>
        /// Convert the <seealso cref="IniValue"/> to <seealso cref="decimal"/>
        /// </summary>
        /// <param name="defaultValue">The default value to be returned if the <seealso cref="IniValue"/> is invalid</param>
        /// <returns>Value of <seealso cref="decimal"/></returns>
        public decimal ToDecimal(decimal defaultValue = 0) => ToNumber<decimal>(defaultValue);
        #endregion

        #region Get String Methods
        private string? GetStringValue() => _value;

        public override string? ToString() => _value;
        #endregion

        #region Implicit Cast Operators
        public static implicit operator IniValue(sbyte o) => new IniValue(o);

        public static implicit operator IniValue(byte o) => new IniValue(o);

        public static implicit operator IniValue(short o) => new IniValue(o);

        public static implicit operator IniValue(ushort o) => new IniValue(o);

        public static implicit operator IniValue(int o) => new IniValue(o);

        public static implicit operator IniValue(uint o) => new IniValue(o);

        public static implicit operator IniValue(long o) => new IniValue(o);

        public static implicit operator IniValue(ulong o) => new IniValue(o);

        public static implicit operator IniValue(Int128 o) => new IniValue(o);

        public static implicit operator IniValue(UInt128 o) => new IniValue(o);

        public static implicit operator IniValue(float o) => new IniValue(o);

        public static implicit operator IniValue(double o) => new IniValue(o);

        public static implicit operator IniValue(decimal o) => new IniValue(o);

        public static implicit operator IniValue(bool o) => new IniValue(o);

        public static implicit operator IniValue(string? o) => new IniValue(o);

        public static implicit operator IniValue(nint o) => new IniValue(o);

        public static implicit operator IniValue(nuint o) => new IniValue(o);

        public static implicit operator IniValue(Size o) => new IniValue(o);

        public static implicit operator IniValue(Guid o) => new IniValue(o);

        public static implicit operator sbyte(IniValue value) => value.ToNumber<sbyte>();

        public static implicit operator byte(IniValue value) => value.ToNumber<byte>();

        public static implicit operator short(IniValue value) => value.ToNumber<short>();

        public static implicit operator ushort(IniValue value) => value.ToNumber<ushort>();

        public static implicit operator int(IniValue value) => value.ToNumber<int>();

        public static implicit operator uint(IniValue value) => value.ToNumber<uint>();

        public static implicit operator long(IniValue value) => value.ToNumber<long>();

        public static implicit operator ulong(IniValue value) => value.ToNumber<ulong>();

        public static implicit operator Int128(IniValue value) => value.ToNumber<Int128>();

        public static implicit operator UInt128(IniValue value) => value.ToNumber<UInt128>();

        public static implicit operator float(IniValue value) => value.ToNumber<float>();

        public static implicit operator double(IniValue value) => value.ToNumber<double>();

        public static implicit operator decimal(IniValue value) => value.ToNumber<decimal>();

        public static implicit operator bool(IniValue value) => value.ToBool();

        public static implicit operator string?(IniValue value) => value.GetStringValue();

        public static implicit operator nint(IniValue value) => value.ToNumber<nint>();

        public static implicit operator nuint(IniValue value) => value.ToNumber<nuint>();

        public static implicit operator Size(IniValue value) => value.ToSize();

        public static implicit operator Guid(IniValue value) => value.ToGuid();

        public static bool operator ==(IniValue valueA, IniValue valueB) => Equals(valueA, valueB);

        public static bool operator !=(IniValue valueA, IniValue valueB) => !(valueA == valueB);

        public bool Equals(IniValue compareTo) => this == compareTo;

        public override bool Equals([NotNullWhen(true)] object? obj) => obj is IniValue iniValue && GetHashCode() == iniValue.GetHashCode();

        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        #endregion
    }
}
