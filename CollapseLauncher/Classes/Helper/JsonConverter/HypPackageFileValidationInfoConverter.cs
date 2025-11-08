using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CollapseLauncher.Helper.JsonConverter
{
    public class HypPackageFileValidationInfoConverter : JsonConverter<List<HypPackageFileValidationInfo>>
    {
        public override bool CanConvert(Type type)
        {
            return true;
        }

        public override List<HypPackageFileValidationInfo> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            string valueString = EmptiedBackslash(reader.ValueSpan);
            List<HypPackageFileValidationInfo> returnList = valueString.Deserialize(RegionResourcePluginValidateJsonContext.Default.ListHypPackageFileValidationInfo);

            return returnList;
        }

        private static unsafe string EmptiedBackslash(ReadOnlySpan<byte> span)
        {
            Span<byte> buffer = new byte[span.Length];
            int indexIn = 0;
            int indexOut = 0;
            while (indexIn < span.Length)
            {
                if (span[indexIn] == '\\')
                {
                    ++indexIn;
                    continue;
                }

                buffer[indexOut] = span[indexIn];
                ++indexIn;
                ++indexOut;
            }

            fixed (byte* bufferPtr = buffer)
            {
                return Encoding.UTF8.GetString(bufferPtr, indexOut);
            }
        }

        public override void Write(
                Utf8JsonWriter writer,
                List<HypPackageFileValidationInfo> baseType,
                JsonSerializerOptions options)
        {

            throw new JsonException("Serializing is not supported!");
        }
    }
}
