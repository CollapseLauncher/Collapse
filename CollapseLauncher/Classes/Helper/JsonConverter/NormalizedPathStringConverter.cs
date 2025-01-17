using Hi3Helper.Data;
using System;
using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable
namespace CollapseLauncher.Helper.JsonConverter
{
    internal class NormalizedPathStringConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException("Current type token is not a string!");

            int spanLen = reader.ValueSpan.Length;
            if (spanLen == 0)
            {
                return null;
            }

            char[] buffer = ArrayPool<char>.Shared.Rent(spanLen);
            try
            {
                Encoding.UTF8.TryGetChars(reader.ValueSpan, buffer, out int charsWritten);
                ConverterTool.NormalizePathInplaceNoTrim(buffer.AsSpan(0, charsWritten));
                return new string(buffer, 0, charsWritten);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
