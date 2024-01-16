using System.IO;
using System.Linq;
using Microsoft.Win32;
using Hi3Helper.Preset;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using CollapseLauncher.ShortcutUtils;


namespace CollapseLauncher.ShortcutsUtils
{
    public static class ShortcutCreator
    {
        public static void CreateShortcut(string path, PresetConfigV2 preset)
        {
            string shortcutName = string.Format("{0} ({1}) - Collapse Launcher.url", preset.GameName, preset.ZoneName).Replace(":", "");
            string url = string.Format("collapse://open -g \"{0}\" -r \"{1}\"", preset.GameName, preset.ZoneName);
            string icon = Path.Combine(Path.GetDirectoryName(AppExecutablePath), "Assets/Images/GameIcon/" + preset.GameType switch
            {
                GameType.StarRail => "icon-starrail.ico",
                GameType.Genshin => "icon-genshin.ico",
                _ => "icon-honkai.ico",
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

                var splitPath = path.Split('\\');
                string userId = splitPath[splitPath.Length - 3];

                if (!parser.Insert(preset, play))
                {
                    LogWriteLine(string.Format("{0} already has this region added to Steam!", userId), Hi3Helper.LogType.Error);
                    return;
                }

                parser.Save();
                LogWriteLine(string.Format("Added shortcut for {0} - {1} for Steam3ID {2} ", preset.GameName, preset.ZoneName, userId));
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
    }
}
