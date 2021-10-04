using System;
//using System.Collections.Generic;
//using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using System.Net;
#if (!NETFRAMEWORK)
using System.Net.Http;
#endif
using System.IO;
using System.Windows.Controls;
using Newtonsoft.Json;
using Hi3HelperGUI.Preset;

//using static Hi3HelperGUI.Logger;
using static Hi3HelperGUI.Data.ConverterTool;

namespace Hi3HelperGUI.Data
{
    public class UpdateData
    {
        protected internal string LocalPath;

        protected internal _RemoteURL RemoteURL;
        protected internal class _RemoteURL
        {
            internal string Data { get; set; }
            internal string DataDictionary { get; set; }
            internal string Event { get; set; }
            internal string EventDictionary { get; set; }
            internal string Ai { get; set; }
            internal string AiDictionary { get; set; }
        }

        public UpdateData(PresetConfigClasses i)
        {
            string bundleURL = ConfigStore.GetMirrorAddressByIndex(i, ConfigStore.DataType.AssetBundle);
            string dictionaryURL = ConfigStore.GetMirrorAddressByIndex(i, ConfigStore.DataType.DictionaryAddress);
            RemoteURL = new _RemoteURL()
            {
                DataDictionary = $"{dictionaryURL}data/editor_compressed/PackageVersion.txt",
                Data = $"{bundleURL}data/editor_compressed/",
                EventDictionary = $"{dictionaryURL}event/editor_compressed/PackageVersion.txt",
                Event = $"{bundleURL}event/editor_compressed/",
                AiDictionary = $"{dictionaryURL}ai/editor_compressed/PackageVersion.txt",
                Ai = $"{bundleURL}ai/editor_compressed/"
            };
        }

        /*
        public UpdateData(PresetConfigClasses i)
        {
            string bundleURL;
            switch (ConfigStore.AppConfigData.MirrorSelection)
            {
                default:
                case 0:
                    bundleURL = ConfigStore.GetMirrorAddress(i, ConfigStore.DataType.Hi3MirrorAssetBundle);
                    break;
                case 1:
                    bundleURL = ConfigStore.GetMirrorAddress(i, ConfigStore.DataType.miHoYoAssetBundle);
                    break;
            }
            RemoteURL = new _RemoteURL()
            {
                DataDictionary = $"{i.Hi3MirrorAssetBundleAddress}data/editor_compressed/PackageVersion.txt",
                Data = $"{bundleURL}data/editor_compressed/",
                EventDictionary = $"{i.Hi3MirrorAssetBundleAddress}event/editor_compressed/PackageVersion.txt",
                Event = $"{bundleURL}event/editor_compressed/",
                AiDictionary = $"{i.Hi3MirrorAssetBundleAddress}ai/editor_compressed/PackageVersion.txt",
                Ai = $"{bundleURL}ai/editor_compressed/"
            };
        }
        */

        /*
         * dataType
         * 0    = Data
         * 1    = Event
         * 2    = Ai
         */
        public void GetDataDict(PresetConfigClasses i, byte dataType)
        {
            HttpClientTool downloader = new HttpClientTool();
            string LocalDirPath = Path.Combine(Environment.GetEnvironmentVariable("userprofile"), $"AppData\\LocalLow\\miHoYo\\{Path.GetFileName(i.ConfigRegistryLocation)}\\{(dataType > 0 ? "Resources" : "Data")}");
            string RemotePath = dataType == 1 ? RemoteURL.Event : dataType == 2 ? RemoteURL.Ai : RemoteURL.Data;
            string LocalPath;
            MemoryStream memoryData = new MemoryStream();
            // Span<string> DictData = webClient.DownloadString(dataType == 1 ? RemoteURL.EventDictionary : dataType == 2 ? RemoteURL.AiDictionary : RemoteURL.DataDictionary).Split("\n");
            downloader.DownloadToStream(
                dataType == 1 ? RemoteURL.EventDictionary : dataType == 2 ? RemoteURL.AiDictionary : RemoteURL.DataDictionary,
                memoryData,
                $"Fetch to buffer: {Enum.GetName(typeof(ConfigStore.DataType), dataType)} list"
                );

#if NETCOREAPP
            Span<string> DictData = Encoding.UTF8.GetString(memoryData.ToArray()).Split('\n');
#else
            string[] DictData = Encoding.UTF8.GetString(memoryData.ToArray()).Split('\n');
#endif

            for (ushort a = (ushort)(dataType > 0 ? 0 : 1); a < DictData.Length - 1; a++)
            {
                ConfigStore.DataProp = JsonConvert.DeserializeObject<UpdateDataProperties>(DictData[a]);
                if (FilterRegion(ConfigStore.DataProp.N, i.UsedLanguage) > 0)
                {
                    LocalPath = Path.Combine(LocalDirPath, NormalizePath($"{ConfigStore.DataProp.N}_{ConfigStore.DataProp.CRC}.unity3d"));
                    if (!File.Exists(LocalPath))
                    {
                        ConfigStore.UpdateFiles.Add(new UpdateDataProperties()
                        {
                            N = ConfigStore.DataProp.N,
                            CS = ConfigStore.DataProp.CS,
                            CRC = ConfigStore.DataProp.CRC,
                            ECS = ConfigStore.DataProp.CS,
                            HumanizeSize = SummarizeSizeSimple(ConfigStore.DataProp.CS),
                            RemotePath = $"{RemotePath}{ConfigStore.DataProp.N}_{ConfigStore.DataProp.CRC}",
                            ActualPath = LocalPath,
                            ZoneName = i.ZoneName,
                            DataType = Enum.GetName(typeof(ConfigStore.DataType), dataType)
                        });
                    }
                    else if (File.Exists(LocalPath) && new FileInfo(LocalPath).Length != ConfigStore.DataProp.CS)
                    {
                        ConfigStore.UpdateFiles.Add(new UpdateDataProperties()
                        {
                            N = ConfigStore.DataProp.N,
                            CS = ConfigStore.DataProp.CS,
                            CRC = ConfigStore.DataProp.CRC,
                            ECS = ConfigStore.DataProp.CS - new FileInfo(LocalPath).Length,
                            HumanizeSize = SummarizeSizeSimple(ConfigStore.DataProp.CS),
                            RemotePath = $"{RemotePath}{ConfigStore.DataProp.N}_{ConfigStore.DataProp.CRC}",
                            ActualPath = LocalPath,
                            ZoneName = i.ZoneName,
                            DataType = Enum.GetName(typeof(ConfigStore.DataType), dataType),
                            DownloadStatus = $"Uncompleted {100 * new FileInfo(LocalPath).Length / ConfigStore.DataProp.CS}% ({SummarizeSizeSimple(new FileInfo(LocalPath).Length)})"
                        });
                    }
                }
            }
        }

        private static string NormalizePath(string i) => Path.Combine(Path.GetDirectoryName(i), Path.GetFileName(i));

        private static byte FilterRegion(string input, string regionName)
        {
            /* return value
             * 0 -> the file is a regional file but outside user region.
             * 1 -> the file is a regional file but inside user region and downloadable.
             * 2 -> the file is not a regional file and downloadable.
             */
            foreach (string word in ConfigStore.RegionalCheckName)
                if (input.Contains(word))
                    if (input.Contains($"{word}_{regionName}"))
                        return 1;
                    else
                        return 0;

            return 2;
        }
    }
}
