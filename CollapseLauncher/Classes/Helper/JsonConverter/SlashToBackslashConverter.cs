using Hi3Helper.Data;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable
namespace CollapseLauncher.Helper.JsonConverter
{
    internal class SlashToBackslashConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException("Current type token is not a string!");

            string? str = reader.GetString();
            return ConverterTool.NormalizePath(str);
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            string replaced = value.Replace('\\', '/');
            writer.WriteStringValue(replaced);
        }
    }
}
