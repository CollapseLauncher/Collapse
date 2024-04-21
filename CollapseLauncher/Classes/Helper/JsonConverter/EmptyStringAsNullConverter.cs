using System;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable
namespace CollapseLauncher.Helper.JsonConverter
{
    internal class EmptyStringAsNullConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException("Current type token is not a string!");

            return reader.ValueSpan.Length == 0 ? null : Extension.ReturnUnescapedData(reader.ValueSpan);
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
