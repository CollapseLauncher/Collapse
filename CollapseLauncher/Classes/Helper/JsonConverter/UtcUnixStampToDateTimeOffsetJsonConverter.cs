using System;
using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130

namespace CollapseLauncher.Helper.JsonConverter;

internal class UtcUnixStampToDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(
        ref Utf8JsonReader    reader,
        Type                  typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.ValueSpan.IsEmpty)
        {
            return default;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return double.TryParse(reader.ValueSpan, out double timestampStr)
                ? Parse(timestampStr)
                : throw new InvalidOperationException("Cannot parse string value to number for DateTimeOffset.");
        }

        if (reader.TryGetDouble(out double unixTimestampDouble))
        {
            return Parse(unixTimestampDouble);
        }

        if (reader.TryGetInt64(out long unixTimestampLong))
        {
            return Parse(unixTimestampLong);
        }

        if (reader.TryGetUInt64(out ulong unixTimestampUlong))
        {
            return Parse(unixTimestampUlong);
        }

        throw new InvalidOperationException("Cannot parse number to DateTimeOffset");
    }

    public override void Write(
        Utf8JsonWriter        writer,
        DateTimeOffset        value,
        JsonSerializerOptions options)
    {
        long number = value.Millisecond != 0
            ? value.ToUnixTimeMilliseconds()
            : value.ToUnixTimeSeconds();

        if (options.NumberHandling.HasFlag(JsonNumberHandling.WriteAsString))
        {
            writer.WriteStringValue($"{number}");
            return;
        }

        writer.WriteNumberValue(number);
    }

    private static DateTimeOffset Parse(double timestamp) => timestamp >= 1000000000000
        ? DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Abs(timestamp))
        : DateTimeOffset.FromUnixTimeSeconds((long)Math.Abs(timestamp));
}
