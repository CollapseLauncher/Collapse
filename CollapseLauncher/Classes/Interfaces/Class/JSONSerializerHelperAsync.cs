using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher
{
    internal static partial class JSONSerializerHelper
    {
#nullable enable
        internal static async ValueTask<T?> DeserializeAsync<T>(this Stream data, JsonSerializerContext context, CancellationToken token = default, T? defaultType = null)
            where T : class => await InnerDeserializeStreamAsync(data, context, token, defaultType);

        internal static async ValueTask<T?> DeserializeAsync<T>(this Stream data, JsonSerializerContext context, CancellationToken token = default, T? defaultType = null)
            where T : struct => await InnerDeserializeStreamAsync(data, context, token, defaultType);

        private static async ValueTask<T?> InnerDeserializeStreamAsync<T>(Stream data, JsonSerializerContext context, CancellationToken token, T? defaultType) =>
        // Check if the data cannot be read, then throw
        !data.CanRead ? throw new NotSupportedException("Stream is not readable! Cannot deserialize the stream to JSON!") :
        // Try deserialize. If it returns a null, then return the default value
            (T?)await JsonSerializer.DeserializeAsync(data, typeof(T), context, token) ?? defaultType;
#nullable disable
    }
}
