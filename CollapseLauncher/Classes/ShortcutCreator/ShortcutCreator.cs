using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Microsoft.Win32;
using System.IO;
using System.Linq;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.ShortcutUtils
{
    public static class ShortcutCreator
    {
        public static string GetIconName(GameNameType gameType)
        {
            return gameType switch
            {
                GameNameType.Genshin => "icon-genshin.ico",
                GameNameType.StarRail => "icon-starrail.ico",
                GameNameType.Zenless => "icon-zenless.ico",
                _ => "icon-honkai.ico",
            };
        }

        internal static void CreateShortcut(string path, PresetConfig preset, bool play = false)
        {
            string translatedGameTitle =
                InnerLauncherConfig.GetGameTitleRegionTranslationString(preset.GameName,
                                                                        Locale.Lang._GameClientTitles)!;
            string translatedGameRegion =
                InnerLauncherConfig.GetGameTitleRegionTranslationString(preset.ZoneName,
                                                                        Locale.Lang._GameClientRegions);
            string shortcutName = $"{translatedGameTitle} ({translatedGameRegion}) - Collapse Launcher.url".Replace(":", "");
            string url = $"collapse://open -g \"{preset.GameName}\" -r \"{preset.ZoneName}\"";

            if (play) url += " -p";

            string icon = Path.Combine(Path.GetDirectoryName(AppExecutablePath)!,
                                       $"Assets/Images/GameIcon/{GetIconName(preset.GameType)}");

            string fullPath = Path.Combine(path, shortcutName);

            using (StreamWriter writer = new StreamWriter(fullPath, false))
            {
                writer.WriteLine($"[InternetShortcut]\nURL={url}\nIconIndex=0\nIconFile={icon}");
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
                LogWriteLine($"[ShortcutCreator::AddToSteam] Added shortcut for {preset.GameName} - {preset.ZoneName} for Steam3ID {userId}");
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
                LogWriteLine("[ShortcutCreator::GetShortcutsPath] " + steamUserData + " is not a valid folder.", LogType.Error);
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
                LogWriteLine("[ShortcutCreator::GetShortcutsPath] Found profile: " + res[i], LogType.Debug);
            }

            return res;
        }
    }
}
