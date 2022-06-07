using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace Hi3Helper.Preset
{
    public static class ConfigStore
    {
        public static AppSettings AppConfigData = new AppSettings();
        public static List<PresetConfigClasses> Config = new List<PresetConfigClasses>();
        public static List<UpdateDataProperties> UpdateFiles;
        public static long UpdateFilesTotalSize;
        public static long UpdateFilesTotalDownloaded = 0;
        public static bool UpdateFinished = false;
        public static UpdateDataProperties DataProp = new UpdateDataProperties();

        public static readonly string[] RegionalCheckName = new string[] { "TextMap", "RandomDialogData", "sprite" };
        public enum DataType
        {
            Data = 0,
            Event = 1,
            Ai = 2,
            Block = 3,
            Video = 4,
            Subtitle = 5,
            DictionaryAddress = 6,
            Bigfile = 7,
            AssetBundle = 8,
            BlockDictionaryAddress = 9,
        }

        public static void LoadConfigFromFile(string input) => Config = JsonConvert.DeserializeObject<List<PresetConfigClasses>>(File.ReadAllText(input));
        public static void LoadConfigTemplate() => Config = GameConfigurationTemplate.GameConfigTemplate;

        public static string GetMirrorAddressByIndex(PresetConfigClasses h, DataType i)
        {
            switch (i)
            {
                case DataType.Bigfile:
                    return h.MirrorList[AppConfigData.AvailableMirror[AppConfigData.MirrorSelection]].Bigfile
                                    + $"StreamingAsb/{h.GameVersion}/pc/HD/asb/";
                case DataType.DictionaryAddress:
                    return h.DictionaryHost + h.UpdateDictionaryAddress;
                case DataType.BlockDictionaryAddress:
                    return h.DictionaryHost + h.BlockDictionaryAddress;
                default:
                    return h.MirrorList[AppConfigData.AvailableMirror[AppConfigData.MirrorSelection]].AssetBundle;
            }
        }
    }
}
