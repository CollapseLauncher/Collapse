using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher
{
    internal static partial class JSONSerializerHelper
    {
        internal static async ValueTask<T?> DeserializeAsync<T>(this Stream data, JsonSerializerContext context, CancellationToken token = default, T? defaultType = null)
            where T : class => await InnerDeserializeStreamAsync(data, context, token, defaultType);

        internal static async ValueTask<T?> DeserializeAsync<T>(this Stream data, JsonSerializerContext context, CancellationToken token = default, T? defaultType = null)
            where T : struct => await InnerDeserializeStreamAsync(data, context, token, defaultType);

        internal static async Task<JsonNode?> DeserializeAsNodeAsync(this Stream data, CancellationToken token = default)
            => await InnerDeserializeStreamAsNodeAsync(data, token);

        private static async ValueTask<T?> InnerDeserializeStreamAsync<T>(Stream data, JsonSerializerContext context, CancellationToken token, T? defaultType) =>
        // Check if the data cannot be read, then throw
        !data.CanRead ? throw new NotSupportedException("Stream is not readable! Cannot deserialize the stream to JSON!") :
        // Try deserialize. If it returns a null, then return the default value
            (T?)await JsonSerializer.DeserializeAsync(data, typeof(T), context, token) ?? defaultType;

        private static async Task<JsonNode?> InnerDeserializeStreamAsNodeAsync(Stream data, CancellationToken token) =>
        // Check if the data cannot be read, then throw
        !data.CanRead ? throw new NotSupportedException("Stream is not readable! Cannot deserialize the stream to JSON!") :
        // Try deserialize to JSON Node
            await JsonNode.ParseAsync(data, jsonNodeOptions, jsonDocumentOptions, token);

        internal static async ValueTask SerializeAsync<T>(this T? value, Stream targetStream, JsonSerializerContext context, CancellationToken token = default)
            where T : class => await InnerSerializeStreamAsync(value, targetStream, context, token);

        internal static async ValueTask SerializeAsync<T>(this T? value, Stream targetStream, JsonSerializerContext context, CancellationToken token = default)
            where T : struct => await InnerSerializeStreamAsync(value, targetStream, context, token);

        private static async ValueTask InnerSerializeStreamAsync<T>(this T? value, Stream targetStream, JsonSerializerContext context, CancellationToken token)
        {
            if (!targetStream.CanWrite)
                throw new NotSupportedException("Stream is not writeable! Cannot serialize the object into Stream!");

            JsonTypeInfo<T?>? typeInfo = (JsonTypeInfo<T?>?)context.GetTypeInfo(typeof(T));
            if (typeInfo == null)
                throw new NotSupportedException($"Context does not contain a type info of type {typeof(T).Name}!");

            await JsonSerializer.SerializeAsync(targetStream, value, typeInfo, token);
        }
    }
}
