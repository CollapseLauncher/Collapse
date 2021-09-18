//using System;
using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

namespace Hi3HelperGUI.Preset
{
    public static class ConfigStore
    {
        public static List<PresetConfigClasses> Config = new List<PresetConfigClasses>();
        public static List<UpdateDataProperties> UpdateFiles;
        public static long UpdateFilesTotalSize;
        public static long UpdateFilesTotalDownloaded = 0;
        public static bool UpdateFinished = false;
        public static UpdateDataProperties DataProp = new UpdateDataProperties();

        public static readonly List<string> RegionalCheckName = new List<string>() { "TextMap", "RandomDialogData", "sprite" };
        public static bool UseHi3Mirror = true;
        public enum DataType
        {
            Hi3MirrorBigFile = 3,
            Hi3MirrorAssetBundle = 4,
            miHoYoBigFile = 5,
            miHoYoAssetBundle = 6,
            Data = 0,
            Event = 1,
            Ai = 2,
            Block = 7,
            Video = 8,
            Subtitle = 9
        }

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
    }
}
