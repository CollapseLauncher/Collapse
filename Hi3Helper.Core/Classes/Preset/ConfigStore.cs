using Newtonsoft.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;

using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using System;
using System.Linq;

namespace Hi3Helper.Preset
{
    public static class ConfigStore
    {
        public static Http.Http http = new Http.Http();
        public static List<PresetConfigClasses> Config = new List<PresetConfigClasses>();
        public static long UpdateFilesTotalSize;
        public static long UpdateFilesTotalDownloaded = 0;
        public static bool UpdateFinished = false;

        public static void LoadConfigTemplate()
        {
            PresetConfigClasses Metadata = JsonConvert.DeserializeObject<PresetConfigClasses>(File.ReadAllText(AppGameConfigStampPath));
            AppGameConfigLastUpdate = Metadata.LastUpdated;
            AppGameConfig = JsonConvert.DeserializeObject<PresetConfigClasses>(File.ReadAllText(AppGameConfigMetadataPath));
            Config = AppGameConfig.Metadata;
            GameConfigName = Config.Select(x => x.ZoneName);
        }

        public static void LoadConfigWithCacheTemplate()
        {
            PresetConfigClasses Metadata = JsonConvert.DeserializeObject<PresetConfigClasses>(File.ReadAllText(AppGameConfigStampPath));
            AppGameConfigLastUpdate = Metadata.LastUpdated;
            AppGameConfig = JsonConvert.DeserializeObject<PresetConfigClasses>(File.ReadAllText(AppGameConfigMetadataPath));
            Config = AppGameConfig.Metadata.Where(i => !string.IsNullOrEmpty(i.CachesListAPIURL)).ToList();
            GameConfigName = Config.Select(x => x.ZoneName);
        }

        public static async Task<bool> CheckForNewMetadata()
        {
            PresetConfigClasses Stamp = null;

            try
            {
                using (MemoryStream Stream = new MemoryStream())
                {
                    string URL = string.Format(AppGameConfigURLPrefix, (IsPreview ? "preview" : "stable") + "stamp");
                    await http.DownloadStream(URL, Stream);
                    string data = Encoding.UTF8.GetString(Stream.GetBuffer());
                    Stamp = JsonConvert.DeserializeObject<PresetConfigClasses>(data);
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while checking for new metadata!\r\n{ex}", LogType.Error, true);
                return false;
            }

            return AppGameConfigLastUpdate < Stamp.LastUpdated;
        }

        public static async Task DownloadMetadataFiles(bool Stamp, bool Content)
        {
            string URL;

            if (!Directory.Exists(AppGameConfigMetadataFolder))
                Directory.CreateDirectory(AppGameConfigMetadataFolder);

            if (Stamp)
            {
                URL = string.Format(AppGameConfigURLPrefix, (IsPreview ? "preview" : "stable") + "stamp");
                if (File.Exists(AppGameConfigStampPath))
                    File.Delete(AppGameConfigStampPath);

                await http.Download(URL, AppGameConfigStampPath);
            }

            if (Content)
            {
                URL = string.Format(AppGameConfigURLPrefix, (IsPreview ? "preview" : "stable") + "content");
                if (File.Exists(AppGameConfigMetadataPath))
                    File.Delete(AppGameConfigMetadataPath);

                await http.Download(URL, AppGameConfigMetadataPath);
            }
        }

        public static bool IsMetadataStampExist() => CheckMetadataStampContent(AppGameConfigStampPath);
        public static bool IsMetadataContentExist() => CheckMetadataStampContent(AppGameConfigMetadataPath);

        private static bool CheckMetadataStampContent(string name)
        {
            FileInfo file = new FileInfo(name);
            if (!file.Exists) return false;
            if (file.Length < 2) return false;
            return true;
        }
    }
}
