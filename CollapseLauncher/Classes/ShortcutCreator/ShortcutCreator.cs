using CollapseLauncher.Helper.Metadata;
using Microsoft.Win32;
using System.IO;
using System.Linq;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.ShortcutUtils
{
    public static class ShortcutCreator
    {
        internal static void CreateShortcut(string path, PresetConfig preset, bool play = false)
        {
            string shortcutName = string.Format("{0} ({1}) - Collapse Launcher.url", preset.GameName, preset.ZoneName).Replace(":", "");
            string url = string.Format("collapse://open -g \"{0}\" -r \"{1}\"", preset.GameName, preset.ZoneName);

            if (play)
                url += " -p";

            string icon = Path.Combine(Path.GetDirectoryName(AppExecutablePath), "Assets/Images/GameIcon/" + preset.GameType switch
            {
                GameNameType.StarRail => "icon-starrail.ico",
                GameNameType.Genshin => "icon-genshin.ico",
                _ => "icon-honkai.ico",
            });

            string fullPath = Path.Combine(path, shortcutName);

            using (StreamWriter writer = new StreamWriter(fullPath, false))
            {
                writer.WriteLine(string.Format("[InternetShortcut]\nURL={0}\nIconIndex=0\nIconFile={1}", url, icon));
            }
        }

        /// Heavily based on Heroic Games Launcher "Add to Steam" feature.
        /// 
        /// Source:
        /// https://github.com/Heroic-Games-Launcher/HeroicGamesLauncher/blob/8bdee1383446d3b81e240a4300baaf337d48ec92/src/backend/shortcuts/nonesteamgame/nonesteamgame.ts

        internal static bool AddToSteam(PresetConfig preset, bool play)
        {
            var paths = GetShortcutsPath();

            if (paths == null || paths.Length == 0)
                return false;

            foreach (string path in paths)
            {
                SteamShortcutParser parser = new SteamShortcutParser(path);

                var splitPath = path.Split('\\');
                string userId = splitPath[splitPath.Length - 3];

                parser.Insert(preset, play);

                parser.Save();
                LogWriteLine(string.Format("[ShortcutCreator::AddToSteam] Added shortcut for {0} - {1} for Steam3ID {2} ", preset.GameName, preset.ZoneName, userId));
            }

            return true;
        }

        internal static bool IsAddedToSteam(PresetConfig preset)
        {
            var paths = GetShortcutsPath();

            if (paths == null || paths.Length == 0)
                return false;

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

            string steamUserData = steamPath + @"\userdata";

            if (!Directory.Exists(steamUserData))
            {
                LogWriteLine("[ShortcutCreator::GetShortcutsPath] " + steamUserData + " is not a valid folder.", Hi3Helper.LogType.Error);
                return null;
            }

            var res = Directory.GetDirectories(steamUserData)
                .Where(x =>
                {
                    string y = x.Split("\\").Last();
                    return y != "ac" && y != "0" && y != "anonymous";
                }).ToArray();

            for (int i = 0; i < res.Length; i++)
            {
                res[i] = Path.Combine(res[i], @"config\shortcuts.vdf");
                LogWriteLine("[ShortcutCreator::GetShortcutsPath] Found profile: " + res[i], Hi3Helper.LogType.Debug);
            }

            return res;
        }
    }
}
