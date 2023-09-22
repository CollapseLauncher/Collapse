using Hi3Helper;
using System;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace CollapseLauncher
{
    internal static class JSONSerializerHelper
    {
#nullable enable
        internal static T? Deserialize<T>(this ReadOnlySpan<byte> data, IJsonTypeInfoResolver context, T? defaultType = null)
            where T : class => InnerDeserialize(data, context, defaultType);

        internal static T? Deserialize<T>(this string data, IJsonTypeInfoResolver context, T? defaultType = null)
            where T : class => InnerDeserialize(Encoding.UTF8.GetBytes(data), context, defaultType);

        private static T? InnerDeserialize<T>(ReadOnlySpan<byte> data, IJsonTypeInfoResolver context, T? defaultType)
            where T : class
        {
            // Check if the data length is 0, then return default value
            if (data.Length == 0) return defaultType ?? default;

            // Try trimming the \0 char at the end of the data
            ReadOnlySpan<byte> dataTrimmed = data.TrimEnd((byte)0x00);

            // Print the log
            string typeStr = typeof(T).Name;
            Logger.LogWriteLine($"[\u001b[33;1mJSONSerializerHelper\u001b[0m::\u001b[32;1mDeserialize\u001b[0m] Deserializing: {typeStr}", LogType.Default);
            Logger.WriteLog($"[JSONSerializerHelper::Deserialize] Deserializing: {typeStr}", LogType.Default);

            // Try deserialize. If it returns a null, then return the default value
            return (T?)JsonSerializer.Deserialize(dataTrimmed, typeof(T), new JsonSerializerOptions()
            {
                TypeInfoResolver = context,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            }) ?? (defaultType ?? default);
        }

        internal static string Serialize<T>(this T? data, IJsonTypeInfoResolver context, bool isIncludeNullEndChar = true, bool isWriteIndented = false)
            where T : class => InnerSerialize(data, context, isIncludeNullEndChar, isWriteIndented);

        private static string InnerSerialize<T>(this T? data, IJsonTypeInfoResolver context, bool isIncludeNullEndChar, bool isWriteIndented)
            where T : class
        {
            const string _defaultValue = "{}";
            // Check if the data is null, then return default value
            if (data == null) return _defaultValue;

            // Print the log
            string typeStr = typeof(T).Name;
            Logger.LogWriteLine($"[\u001b[33;1mJSONSerializerHelper\u001b[0m::\u001b[32;1mSerialize\u001b[0m] Serializing: {typeStr}", LogType.Debug);
            Logger.WriteLog($"[JSONSerializerHelper::Serialze] Serializing: {typeStr}", LogType.Default);

            // Try Serialize the type into JSON string
            string returnValue = JsonSerializer.Serialize(data, typeof(T), new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                TypeInfoResolver = context,
                WriteIndented = isWriteIndented
            });

            // If the serialization accepts \0 char at the end of the return, then return it with \0
            // Otherwise, return as is.
            return isIncludeNullEndChar ? returnValue + '\0' : returnValue;
        }
#nullable disable
    }
}
