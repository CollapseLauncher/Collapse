using System.IO;
using System.Linq;
using Microsoft.Win32;
using Hi3Helper.Preset;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using CollapseLauncher.ShortcutUtils;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;


namespace CollapseLauncher.ShortcutsUtils
{
    public static class ShortcutCreator
    {
        public static void CreateShortcut(string path, PresetConfigV2 preset)
        {
            string shortcutName = string.Format("{0} ({1}) - Collapse Launcher.url", preset.GameName, preset.ZoneName).Replace(":", "");
            string url = string.Format("collapse://open -g \"{0}\" -r \"{1}\"", preset.GameName, preset.ZoneName);
            string icon = Path.Combine(Path.GetDirectoryName(AppExecutablePath), "Assets/Images/SteamShortcuts/" + preset.GameType switch
            {
                GameType.StarRail => "starrail/icon.ico",
                GameType.Genshin => "genshin/icon.ico",
                _ => "honkai/icon.ico",
            });

            StreamWriter writer = new StreamWriter(Path.Combine(path, shortcutName));
            writer.WriteLine(string.Format("[InternetShortcut]\nURL={0}\nIconIndex=0\nIconFile={1}", url, icon));
            writer.Close();
        }

        /// Heavily based on Heroic Games Launcher "Add to Steam" feature.
        /// 
        /// Source:
        /// https://github.com/Heroic-Games-Launcher/HeroicGamesLauncher/blob/8bdee1383446d3b81e240a4300baaf337d48ec92/src/backend/shortcuts/nonesteamgame/nonesteamgame.ts

        public static void AddToSteam(PresetConfigV2 preset, bool play)
        {
            var paths = GetShortcutsPath();

            if (paths == null || paths.Length == 0) return;

            foreach (string path in paths)
            {
                SteamShortcutParser parser = new SteamShortcutParser(path);

                if (!parser.Insert(new SteamShortcut(preset, play)))
                {
                    LogWriteLine("Already added to Steam!", Hi3Helper.LogType.Error);
                    return;
                }

                parser.Save();
                LogWriteLine("Steam Shortcut created for " + preset.ZoneFullname + "!");
            }
        }

        public static bool IsAddedToSteam(PresetConfigV2 preset)
        {
            var paths = GetShortcutsPath();

            if (paths == null || paths.Length == 0) return false;

            foreach (string path in paths)
            {
                SteamShortcutParser parser = new SteamShortcutParser(path);

                if (!parser.Contains(new SteamShortcut(preset)))
                    return false;
            }

            return true;
        }

        private static string[] GetShortcutsPath()
        {
            RegistryKey reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam", false);
            if (reg == null)
                return null;

            string steamPath = (string)reg.GetValue("InstallPath", "C:\\Program Files (x86)\\Steam");

            var res = Directory.GetDirectories(steamPath + @"\userdata")
                .Where(x =>
                    !(x.EndsWith("ac") || x.EndsWith("0") || x.EndsWith("anonymous"))
                    ).ToArray();

            for (int i = 0; i < res.Length; i++)
            {
                res[i] = Path.Combine(res[i], @"config\shortcuts.vdf");
            }

            return res;
        }

        /*public static Dictionary<string, string> GetSteamIDName()
        {
            Dictionary<string, string> result = new Dictionary<string, string>() { };

            foreach (string id in GetSteamID3())
            {
                long id64 = ConvertSteamID3ToID64(id);
                using (HttpClient wc = new HttpClient())
                {
                    string key = "42E55E051B80627D2DF6F63B47523ED3";
                    var json = wc.GetStringAsync(string.Format("http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={0}&steamids={1}", key, id64.ToString()));
                    string name = Regex.Match(json.Result, "(\"personaname\":\".*?\")").Value.Split("\"")[3];
                    result.Add(id, name);
                }
            }
            return result;
        }

        private static string[] GetSteamID3()
        {
            RegistryKey reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam", false);
            if (reg == null)
                return null;

            string steamPath = (string)reg.GetValue("InstallPath", "C:\\Program Files (x86)\\Steam");

            var res = Directory.GetDirectories(steamPath + @"\userdata")
                .Where(x =>
                    !(x.EndsWith("ac") || x.EndsWith("0") || x.EndsWith("anonymous"))
                    ).ToArray();

            for (int i = 0; i < res.Length; i++)
            {
                res[i] = res[i].Split("\\").Last();
            }

            return res;
        }

        private static long ConvertSteamID3ToID64(string steamID3)
        {
            if (!long.TryParse(steamID3, out long id))
                return -1;

            long acc_type = id % 2;
            long acc_id = (id - acc_type) / 2;
            return 76561197960265728 + (acc_id * 2) + acc_type;
        }*/
    }
}
