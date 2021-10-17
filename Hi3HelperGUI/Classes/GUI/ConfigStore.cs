using System.Collections.Generic;

namespace Hi3HelperGUI.Preset
{
    public static class ConfigStore
    {
        public static AppSettings AppConfigData = new();
        public static List<PresetConfigClasses> Config = new();
        public static List<UpdateDataProperties> UpdateFiles;
        public static long UpdateFilesTotalSize;
        public static long UpdateFilesTotalDownloaded = 0;
        public static bool UpdateFinished = false;
        public static UpdateDataProperties DataProp = new();

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

        public static string GetMirrorAddressByIndex(PresetConfigClasses h, DataType i)
        {
            return i switch
            {
                DataType.Bigfile => h.MirrorList[AppConfigData.AvailableMirror[AppConfigData.MirrorSelection]].Bigfile
                                    + $"StreamingAsb/{h.GameVersion}/pc/HD/asb/",
                DataType.DictionaryAddress => h.DictionaryHost + h.UpdateDictionaryAddress,
                DataType.BlockDictionaryAddress => h.DictionaryHost + h.BlockDictionaryAddress,
                _ => h.MirrorList[AppConfigData.AvailableMirror[AppConfigData.MirrorSelection]].AssetBundle,
            };
        }

        /*
        public static string GetMirrorAddress(PresetConfigClasses h, DataType j)
        {
            switch (j)
            {
                default:
                case DataType.Hi3MirrorAssetBundle:
                    return h.Hi3MirrorAssetBundleAddress;
                case DataType.Hi3MirrorBigFile:
                    return h.Hi3MirrorAssetBigFileAddress;
                case DataType.miHoYoAssetBundle:
                    return h.miHoYoAssetBundleAddress;
                case DataType.miHoYoBigFile:
                    return h.miHoYoAssetBigFileAddress;
            }
        }
        */
    }
}
