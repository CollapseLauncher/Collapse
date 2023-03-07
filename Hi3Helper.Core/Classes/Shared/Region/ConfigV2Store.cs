using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace Hi3Helper.Preset
{
    public static class ConfigV2Store
    {
        public static Metadata ConfigV2 = new Metadata();
        internal static PresetConfigV2 CurrentConfigV2;
        public static string CurrentConfigV2GameCategory;
        public static string CurrentConfigV2GameRegion;

        public static long ConfigV2LastUpdate;

        public static bool IsGameCategoryLocked = false;
        public static bool IsGameRegionsLocked = false;
        public static List<string> ConfigV2GameCategory;
        public static List<string> ConfigV2GameRegions;

        public static PresetConfigV2 LoadCurrentConfigV2(string Key, string Value)
        {
            CurrentConfigV2 = ConfigV2.MetadataV2[Key][Value];
            CurrentConfigV2GameCategory = Key;
            CurrentConfigV2GameRegion = Value;

            return CurrentConfigV2;
        }

        public static void LoadConfigV2CacheOnly()
        {
            LoadConfigV2();
            Dictionary<string, Dictionary<string, PresetConfigV2>> res = new Dictionary<string, Dictionary<string, PresetConfigV2>>();
            Dictionary<string, PresetConfigV2> phase1 = new Dictionary<string, PresetConfigV2>();

            foreach (KeyValuePair<string, Dictionary<string, PresetConfigV2>> a in ConfigV2.MetadataV2)
            {
                phase1 = a.Value
                    .Where(b => b.Value.IsCacheUpdateEnabled ?? false)
                    .ToDictionary(b => b.Key, b => b.Value);

                if (phase1.Count > 0) res.Add(a.Key, phase1);
            }

            ConfigV2.MetadataV2 = res;
            ConfigV2.DecryptStrings();
            ConfigV2GameCategory = ConfigV2.MetadataV2.Keys.ToList();
        }

        public static void LoadConfigV2()
        {
            string stamp = File.ReadAllText(AppGameConfigV2StampPath);
            string content = File.ReadAllText(AppGameConfigV2MetadataPath);
            if (string.IsNullOrEmpty(stamp)) throw new NullReferenceException($"{AppGameConfigV2StampPath} file seems to be empty. Please remove it and restart the launcher!");
            if (string.IsNullOrEmpty(content)) throw new NullReferenceException($"{AppGameConfigV2MetadataPath} file seems to be empty. Please remove it and restart the launcher!");

            ConfigV2 = (Metadata)JsonSerializer
                .Deserialize(content, typeof(Metadata), MetadataContext.Default);

            if (ConfigV2 is null) throw new NullReferenceException("Metadata config is broken");

            ConfigV2GameCategory = ConfigV2.MetadataV2.Keys.ToList();

            ConfigV2LastUpdate = ((Stamp)JsonSerializer
                .Deserialize(stamp, typeof(Stamp), StampContext.Default)).LastUpdated;

            ConfigV2.DecryptStrings();
        }

        public static bool GetConfigV2Regions(string GameCategoryName)
        {
            if (!ConfigV2.MetadataV2.ContainsKey(GameCategoryName))
            {
                ConfigV2GameRegions = ConfigV2.MetadataV2.FirstOrDefault().Value.Keys.ToList();
                LogWriteLine($"Game category \"{GameCategoryName}\" isn't found!", LogType.Error, true);
                return false;
            }

            ConfigV2GameRegions = ConfigV2.MetadataV2[GameCategoryName].Keys.ToList();
            return true;
        }

        public static bool IsConfigV2StampExist() => CheckConfigV2StampContent(AppGameConfigV2StampPath);
        public static bool IsConfigV2ContentExist() => CheckConfigV2StampContent(AppGameConfigV2MetadataPath);

        private static bool CheckConfigV2StampContent(string name)
        {
            FileInfo file = new FileInfo(name);
            if (!file.Exists) return false;
            if (file.Length < 2) return false;
            return true;
        }
    }
}
