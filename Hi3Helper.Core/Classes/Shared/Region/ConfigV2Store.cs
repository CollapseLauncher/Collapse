﻿using System;
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
        public static PresetConfigV2 CurrentConfigV2;
        public static string CurrentConfigV2GameCategory;
        public static string CurrentConfigV2GameRegion;

        public static long ConfigV2LastUpdate;

        public static bool IsGameCategoryLocked = false;
        public static bool IsGameRegionsLocked = false;
        public static List<string> ConfigV2GameCategory;
        public static List<string> ConfigV2GameRegions;

        public static void LoadCurrentConfigV2(string Key, string Value)
        {
            CurrentConfigV2 = ConfigV2.MetadataV2[Key][Value];
            CurrentConfigV2GameCategory = Key;
            CurrentConfigV2GameRegion = Value;
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
            string content = File.ReadAllText(AppGameConfigV2MetadataPath);
            if (string.IsNullOrEmpty(stamp)) throw new NullReferenceException("stampv2.json file seems to be empty. Please remove it and restart the launcher!");
            if (string.IsNullOrEmpty(content)) throw new NullReferenceException("metadatav2.json file seems to be empty. Please remove it and restart the launcher!");

            ConfigV2 = (Metadata)JsonSerializer
                .Deserialize(content, typeof(Metadata), MetadataContext.Default);

            if (ConfigV2 is null) throw new NullReferenceException("Metadata config is broken");

            ConfigV2GameCategory = ConfigV2.MetadataV2.Keys.ToList();

            ConfigV2LastUpdate = ((Stamp)JsonSerializer
                .Deserialize(stamp, typeof(Stamp), StampContext.Default)).LastUpdated;
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

        public static async Task<bool> CheckForNewConfigV2()
        {
            Stamp ConfigStamp = null;

            try
            {
                using (Http.Http _http = new Http.Http())
                using (MemoryStream Stream = new MemoryStream())
                {
                    string URL = string.Format(AppGameConfigV2URLPrefix, (IsPreview ? "preview" : "stable") + "stamp");
                    await _http.Download(URL, Stream).ConfigureAwait(false);
                    Stream.Position = 0;
                    ConfigStamp = (Stamp)JsonSerializer.Deserialize(Stream, typeof(Stamp), StampContext.Default);
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"[ConfigV2Store] Failed while checking for new metadata!\r\n{ex}\r\nAttemping to use fallback metadata URL.", LogType.Warning, true);
                try
                {
                    using (Http.Http _http = new Http.Http())
                    using (MemoryStream Stream = new MemoryStream())
                    {
                        string URL = string.Format(AppGameConfigV2URLPrefixFallback, (IsPreview ? "preview" : "stable") + "stamp");
                        await _http.Download(URL, Stream).ConfigureAwait(false);
                        Stream.Position = 0;
                        ConfigStamp = (Stamp)JsonSerializer.Deserialize(Stream, typeof(Stamp), StampContext.Default);
                    }
                } catch (Exception fallbackFailedException)
                {
                    LogWriteLine($"[ConfigV2Store] Failed while checking for new metadata using fallback CDN.\r\n{fallbackFailedException}", LogType.Error, true);
                }
                return false;
            }
            return ConfigV2LastUpdate != ConfigStamp?.LastUpdated;
        }

        public static async Task DownloadConfigV2Files(bool Stamp, bool Content)
        {
            string URL;

            using (Http.Http _httpClient = new Http.Http())
            {
                if (!Directory.Exists(AppGameConfigMetadataFolder))
                    Directory.CreateDirectory(AppGameConfigMetadataFolder);

                if (Stamp)
                {
                    try
                    {
                        URL = string.Format(AppGameConfigV2URLPrefix, (IsPreview ? "preview" : "stable") + "stamp");
                        if (File.Exists(AppGameConfigV2StampPath))
                            File.Delete(AppGameConfigV2StampPath);

                        await _httpClient.Download(URL, AppGameConfigV2StampPath, true, null, null).ConfigureAwait(false);
                    } catch (Exception ex)
                    {
                        LogWriteLine($"[ConfigV2Store] Failed to download new Config files!\r\n{ex}\r\nAttemping to use fallback URL.", LogType.Warning, true);
                        try
                        {
                            URL = string.Format(AppGameConfigV2URLPrefixFallback, (IsPreview ? "preview" : "stable") + "stamp");
                            if (File.Exists(AppGameConfigV2StampPath))
                                File.Delete(AppGameConfigV2StampPath);

                            await _httpClient.Download(URL, AppGameConfigV2StampPath, true, null, null).ConfigureAwait(false);
                        } catch (Exception fallbackFailedException)
                        {
                            LogWriteLine($"[ConfigV2Store] Failed to download new Config files using fallback CDN!\r\n{fallbackFailedException}", LogType.Error, true);
                        }
                    }
                }

                if (Content)
                {
                    try {
                        URL = string.Format(AppGameConfigV2URLPrefix, (IsPreview ? "preview" : "stable") + "config");
                        if (File.Exists(AppGameConfigV2MetadataPath))
                            File.Delete(AppGameConfigV2MetadataPath);

                        await _httpClient.Download(URL, AppGameConfigV2MetadataPath, true, null, null).ConfigureAwait(false);
                    } catch (Exception ex)
                    {
                        LogWriteLine($"[ConfigV2Store] Failed to download new Config files!\r\n{ex}\r\nAttemping to use fallback URL.", LogType.Warning, true);
                        try
                        {
                            URL = string.Format(AppGameConfigV2URLPrefixFallback, (IsPreview ? "preview" : "stable") + "config");
                            if (File.Exists(AppGameConfigV2MetadataPath))
                                File.Delete(AppGameConfigV2MetadataPath);

                            await _httpClient.Download(URL, AppGameConfigV2MetadataPath, true, null, null).ConfigureAwait(false);
                        } catch (Exception fallbackFailedException)
                        {
                            LogWriteLine($"[ConfigV2Store] Failed to download new Config files using fallback CDN!\r\n{fallbackFailedException}", LogType.Error, true);
                        }
                    }
                    
                }
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
    }
}
