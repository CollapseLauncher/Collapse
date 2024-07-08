using CollapseLauncher.GameSettings.Zenless;
using Hi3Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

#nullable enable
namespace CollapseLauncher.GameSettings.Base
{
    internal class MagicNodeBaseValues<T>
            where T : MagicNodeBaseValues<T>, new()
    {
        [JsonIgnore]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public byte[] Magic { get; protected set; }

        [JsonIgnore]
        protected SettingsGameVersionManager GameVersionManager { get; set; }

        [JsonIgnore]
        public JsonNode? SettingsJsonNode { get; protected set; }

        [JsonIgnore]
        public JsonSerializerContext Context { get; protected set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.


        [Obsolete("Loading settings with Load() is not supported for IGameSettingsValueMagic<T> member. Use LoadWithMagic() instead!", true)]
        public static T Load() => throw new NotSupportedException("Loading settings with Load() is not supported for IGameSettingsValueMagic<T> member. Use LoadWithMagic() instead!");

        public static T LoadWithMagic(byte[] magic, SettingsGameVersionManager versionManager, JsonSerializerContext context)
        {
            if (magic == null || magic.Length == 0)
                throw new NullReferenceException($"Magic cannot be an empty array!");

            try
            {
                string? filePath = versionManager.ConfigFilePath;

                if (!File.Exists(filePath)) throw new FileNotFoundException("MagicNodeBaseValues config file not found!");
                string raw = Sleepy.ReadString(filePath, magic);

#if DEBUG
                Logger.LogWriteLine($"RAW MagicNodeBaseValues Settings: {filePath}\r\n" +
                             $"{raw}", LogType.Debug, true);
#endif
                JsonNode? node = raw.DeserializeAsJsonNode();
                T data = new T();
                data.InjectNodeAndMagic(node, magic, versionManager, context);
                return data;
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"Failed to parse MagicNodeBaseValues settings\r\n{ex}", LogType.Error, true);
                return new T().DefaultValue(magic, versionManager, context);
            }
        }

        public T DefaultValue(byte[] magic, SettingsGameVersionManager versionManager, JsonSerializerContext context)
        {
            // Generate dummy data
            T data = new T();

            // Generate raw JSON string
            string rawJson = data.Serialize(Context, false, false);

            // Deserialize it back to JSON Node and inject
            // the node and magic
            JsonNode? defaultJsonNode = rawJson.DeserializeAsJsonNode();
            data.InjectNodeAndMagic(defaultJsonNode, magic, versionManager, context);

            // Return
            return data;
        }

        public void Save()
        {
            // Get the file and dir path
            string? filePath = GameVersionManager.ConfigFilePath;
            string? fileDirPath = Path.GetDirectoryName(filePath);

            // Create the dir if not exist
            if (string.IsNullOrEmpty(fileDirPath) && !Directory.Exists(fileDirPath))
                Directory.CreateDirectory(fileDirPath!);

            // Write into the file
            string jsonString = SettingsJsonNode.SerializeJsonNode(Context, false, false);
            Sleepy.WriteString(filePath!, jsonString, Magic);
        }

        public bool Equals(GeneralData? other)
        {
            return true;
        }

        protected virtual void InjectNodeAndMagic(JsonNode? jsonNode, byte[] magic, SettingsGameVersionManager versionManager, JsonSerializerContext context)
        {
            SettingsJsonNode = jsonNode;
            Magic = magic;
            Context = context;
        }

        protected virtual string GetValue(JsonNode? node, string keyName, string defaultValue)
        {
            // Get node as object
            JsonObject? jsonObject = node?.AsObject();

            // If node is null, return the default value
            if (jsonObject == null) return defaultValue;

            // Try get node as struct value
            if (jsonObject.TryGetPropertyValue(keyName, out JsonNode? jsonNodeValue) && jsonNodeValue != null)
            {
                string returnValue = jsonNodeValue.AsValue().GetValue<string>();
                return returnValue;
            }

            return defaultValue;
        }

        protected virtual TValue GetValue<TValue>(JsonNode? node, string keyName, TValue defaultValue)
            where TValue : struct
        {
            // Get node as object
            JsonObject? jsonObject = node?.AsObject();

            // If node is null, return the default value
            if (jsonObject == null) return defaultValue;

            // Try get node as struct value
            if (jsonObject.TryGetPropertyValue(keyName, out JsonNode? jsonNodeValue) && jsonNodeValue != null)
            {
                TValue returnValue = jsonNodeValue.AsValue().GetValue<TValue>();
                return returnValue;
            }

            return defaultValue;
        }

        protected virtual void SetValue<TValue>(JsonNode? node, string keyName, TValue value)
        {
            // If node is null, return and ignore
            if (node == null) return;

            // Get node as object
            JsonObject? jsonObject = node.AsObject();

            // If node is null, return and ignore
            if (jsonObject == null) return;

            // Create an instance of the JSON node value
            JsonValue? jsonValue = JsonValue.Create(value);

            // If the node has object, then assign the new value
            if (jsonObject.ContainsKey(keyName))
                node[keyName] = jsonValue;
            // Otherwise, add it
            else
                jsonObject.Add(new KeyValuePair<string, JsonNode?>(keyName, jsonValue));
        }
    }
}
