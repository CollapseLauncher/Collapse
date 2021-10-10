//using System;
using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

namespace Hi3HelperGUI.Preset
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

        public static string GetMirrorAddressByIndex(PresetConfigClasses h, DataType i)
        {
            switch (i)
            {
                default:
                case DataType.AssetBundle:
                    return h.MirrorList[AppConfigData.AvailableMirror[AppConfigData.MirrorSelection]].AssetBundle;
                case DataType.Bigfile:
                    return h.MirrorList[AppConfigData.AvailableMirror[AppConfigData.MirrorSelection]].Bigfile;
                case DataType.DictionaryAddress:
                    return h.DictionaryHost + h.UpdateDictionaryAddress;
                case DataType.BlockDictionaryAddress:
                    return h.DictionaryHost + h.BlockDictionaryAddress;
            }
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
