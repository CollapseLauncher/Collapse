using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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
            ConfigV2GameCategory = ConfigV2.MetadataV2.Keys.ToList();
        }

        public static void LoadConfigV2()
        {
            string stamp = File.ReadAllText(AppGameConfigV2StampPath);
            ConfigV2GameCategory = new();
            foreach (string s in AppGameConfigV2MetadataPath)
            {
                string content = File.ReadAllText(s);
                if (string.IsNullOrEmpty(content))
                    throw new NullReferenceException($"{AppGameConfigV2MetadataPath} file seems to be empty. Please remove it and restart the launcher!");
                Metadata tempConfig = (Metadata)JsonSerializer
                    .Deserialize(content, typeof(Metadata), CoreLibraryJSONContext.Default);
                if (tempConfig is null) throw new NullReferenceException("Metadata config is broken");
                ConfigV2GameCategory.AddRange(tempConfig.MetadataV2.Keys.ToList());
                ConfigV2.AddStrings(tempConfig);
            }
            if (string.IsNullOrEmpty(stamp)) throw new NullReferenceException($"{AppGameConfigV2StampPath} file seems to be empty. Please remove it and restart the launcher!");

            ConfigV2LastUpdate = ((Stamp)JsonSerializer
                .Deserialize(stamp, typeof(Stamp), CoreLibraryJSONContext.Default)).LastUpdated;

            //ConfigV2.DecryptStrings();//
            ConfigV2.GenerateHashID();
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

        public static int GetPreviousGameRegion(string GameCategoryName)
        {
            string iniKeyName = $"LastRegion_{GameCategoryName.Replace(" ", string.Empty)}";
            string regionName;

            if (!IsConfigKeyExist(iniKeyName))
            {
                regionName = ConfigV2GameRegions.FirstOrDefault();
                SetAndSaveConfigValue(iniKeyName, regionName);
                return ConfigV2GameRegions.IndexOf(regionName);
            }

            regionName = GetAppConfigValue(iniKeyName).ToString();
            if (ConfigV2GameRegions.Contains(regionName))
            {
                return ConfigV2GameRegions.IndexOf(regionName);
            }

            regionName = ConfigV2GameRegions.FirstOrDefault();
            return ConfigV2GameRegions.IndexOf(regionName);
        }

        public static void SetPreviousGameRegion(string GameCategoryName, string RegionName, bool isSave = true)
        {
            string iniKeyName = $"LastRegion_{GameCategoryName.Replace(" ", string.Empty)}";

            if (isSave)
            {
                SetAndSaveConfigValue(iniKeyName, RegionName);
            }
            else
            {
                SetAppConfigValue(iniKeyName, RegionName);
            }
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
        private static bool CheckConfigV2StampContent(string[] name)
        {
            foreach (string s in name)
            {
                FileInfo file = new(s);
                if (!file.Exists) return false;
                if (file.Length < 2) return false;
            }
            return true;
        }
    }
}
