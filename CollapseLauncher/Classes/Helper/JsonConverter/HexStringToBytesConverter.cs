using Hi3Helper.Data;
using System;
using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.Helper.JsonConverter
{
    internal class HexStringToBytesConverter : JsonConverter<byte[]>
    {
        public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("Current type token is not a string!");
            }

            ReadOnlySpan<byte> charSpan = reader.ValueSpan;
            if (charSpan.Length % 2 != 0)
            {
                throw new JsonException($"String does not have an even length! (Length: {charSpan.Length} characters)");
            }

            int    bufferAllocLen = charSpan.Length >> 1;
            bool   useRent        = charSpan.Length <= 64 << 10;
            byte[] returnBuffer   = GC.AllocateUninitializedArray<byte>(bufferAllocLen, true);
            char[] stringBuffer   = useRent ? ArrayPool<char>.Shared.Rent(charSpan.Length) : new char[charSpan.Length];

            try
            {
                if (!(Encoding.UTF8.TryGetChars(reader.ValueSpan, stringBuffer, out int charsWritten) &&
                      charsWritten % 2 == 0))
                {
                    throw new JsonException("Failed while converting JSON UTF-8 string to .NET/Unicode string");
                }

                if (!HexTool.TryHexToBytesUnsafe(stringBuffer, returnBuffer))
                {
                    throw new JsonException("Failed while converting string to hex");
                }

                return returnBuffer;
            }
            finally
            {
                if (useRent)
                {
                    ArrayPool<char>.Shared.Return(stringBuffer);
                }
            }
        }

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
        {
            if (value.Length % 2 != 0)
            {
                throw new
                    JsonException($"Bytes Array does not have an even length! (Length: {value.Length} characters)");
            }

            int    bufferAllocLen = value.Length << 1;
            char[] stringBuffer   = GC.AllocateUninitializedArray<char>(bufferAllocLen, true);
            if (!HexTool.TryBytesToHexUnsafe(value, stringBuffer))
            {
                throw new JsonException("Failed while converting bytes to hex");
            }

            writer.WriteStringValue(stringBuffer);
        }
    }
}