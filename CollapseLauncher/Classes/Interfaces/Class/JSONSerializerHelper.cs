using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CollapseLauncher
{
    internal static partial class JSONSerializerHelper
    {
        private static readonly JavaScriptEncoder jsonEncoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        private static readonly JsonWriterOptions jsonWriterOptions = new JsonWriterOptions()
        {
            Indented = false,
            Encoder = jsonEncoder
        };
        private static readonly JsonWriterOptions jsonWriterOptionsIndented = new JsonWriterOptions()
        {
            Indented = true,
            Encoder = jsonEncoder
        };
        private static readonly JsonReaderOptions jsonReaderOptions = new JsonReaderOptions()
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        };

        private static readonly ArrayBufferWriter<byte> jsonBufferWriter = new ArrayBufferWriter<byte>(4 << 10);
        private static readonly Utf8JsonWriter jsonWriter = new Utf8JsonWriter(jsonBufferWriter, jsonWriterOptions);
        private static readonly Utf8JsonWriter jsonWriterIndented = new Utf8JsonWriter(jsonBufferWriter, jsonWriterOptionsIndented);

#nullable enable
        internal static T? Deserialize<T>(this string data, JsonSerializerContext context, T? defaultType = null)
            where T : class => InnerDeserializeUTF16(data, context, defaultType);

        internal static T? Deserialize<T>(this string data, JsonSerializerContext context, T? defaultType = null)
            where T : struct => InnerDeserializeUTF16(data, context, defaultType);

        internal static T? Deserialize<T>(this ReadOnlySpan<char> data, JsonSerializerContext context, T? defaultType = null)
            where T : class => InnerDeserializeUTF16(data, context, defaultType);

        internal static T? Deserialize<T>(this ReadOnlySpan<char> data, JsonSerializerContext context, T? defaultType = null)
            where T : struct => InnerDeserializeUTF16(data, context, defaultType);

        internal static T? Deserialize<T>(this ReadOnlySpan<byte> data, JsonSerializerContext context, T? defaultType = null)
            where T : class => InnerDeserializeUTF8(data, context, defaultType);

        internal static T? Deserialize<T>(this ReadOnlySpan<byte> data, JsonSerializerContext context, T? defaultType = null)
            where T : struct => InnerDeserializeUTF8(data, context, defaultType);

        internal static T? Deserialize<T>(this Stream data, JsonSerializerContext context, T? defaultType = null)
            where T : class => InnerDeserializeStream(data, context, defaultType);

        internal static T? Deserialize<T>(this Stream data, JsonSerializerContext context, T? defaultType = null)
            where T : struct => InnerDeserializeStream(data, context, defaultType);

        private static T? InnerDeserializeStream<T>(Stream data, JsonSerializerContext context, T? defaultType)
        {
            // Check if the data length is 0, then return default value
            if (data.Length == 0) return defaultType ?? default;

            // Try deserialize. If it returns a null, then return the default value
            return (T?)JsonSerializer.Deserialize(data, typeof(T), context) ?? defaultType ?? default;
        }

        private static T? InnerDeserializeUTF16<T>(ReadOnlySpan<char> data, JsonSerializerContext context, T? defaultType)
        {
            // Check if the data length is 0, then return default value
            if (data.Length == 0) return defaultType ?? default;

            // Try trimming the \0 char at the end of the data
            ReadOnlySpan<char> dataTrimmed = data.TrimEnd((char)0x00);

            // Try deserialize. If it returns a null, then return the default value
            return (T?)JsonSerializer.Deserialize(dataTrimmed, typeof(T), context) ?? defaultType ?? default;
        }

        private static T? InnerDeserializeUTF8<T>(ReadOnlySpan<byte> data, JsonSerializerContext context, T? defaultType)
        {
            // Check if the data length is 0, then return default value
            if (data.Length == 0) return defaultType ?? default;

            // Try trimming the \0 char at the end of the data
            ReadOnlySpan<byte> dataTrimmed = data.TrimEnd((byte)0x00);
            Utf8JsonReader jsonReader = new Utf8JsonReader(dataTrimmed, jsonReaderOptions);

            // Try deserialize. If it returns a null, then return the default value
            return (T?)JsonSerializer.Deserialize(ref jsonReader, typeof(T), context) ?? defaultType ?? default;
        }

        internal static string Serialize<T>(this T? data, JsonSerializerContext context, bool isIncludeNullEndChar = true, bool isWriteIndented = false)
            where T : class => InnerSerialize(data, context, isIncludeNullEndChar, isWriteIndented);

        internal static string Serialize<T>(this T? data, JsonSerializerContext context, bool isIncludeNullEndChar = true, bool isWriteIndented = false)
            where T : struct => InnerSerialize(data, context, isIncludeNullEndChar, isWriteIndented);

        private static string InnerSerialize<T>(this T? data, JsonSerializerContext context, bool isIncludeNullEndChar, bool isWriteIndented)
        {
            const string _defaultValue = "{}";
            // Check if the data is null, then return default value
            if (data == null) return _defaultValue;

            // Lock the buffer
            lock (jsonBufferWriter)
            {
                // Clear the writer and its buffer
                jsonBufferWriter.Clear();

                // SANITY CHECK: Check if the buffer is already zero-ed
                if (jsonBufferWriter.WrittenCount != 0) throw new InvalidOperationException("Illegal Fault: The buffer hasn't been zeroed!");

                // Reset the writer state
                if (isWriteIndented) jsonWriterIndented.Reset();
                else jsonWriter.Reset();

                // Assign the writer
                Utf8JsonWriter writer = isWriteIndented ? ref jsonWriterIndented : ref jsonWriter;

                // Lock the writer
                lock (writer)
                {
                    // Try serialize the type into JSON string
                    JsonSerializer.Serialize(writer, data, typeof(T), context);

                    // Write the buffer to string
                    ReadOnlySpan<byte> buffer = jsonBufferWriter.WrittenSpan;
                    string returnValue = Encoding.UTF8.GetString(buffer);

                    // If the serialization accepts \0 char at the end of the return, then return it with \0
                    // Otherwise, return as is.
                    return isIncludeNullEndChar ? returnValue + '\0' : returnValue;
                }
            }
        }
#nullable disable
    }
}
