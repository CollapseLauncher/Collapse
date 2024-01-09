using IWshRuntimeLibrary;
using Hi3Helper.Preset;
using static Hi3Helper.Shared.Region.LauncherConfig;
using System.IO;

namespace CollapseLauncher
{
    public static class ShortcutCreator
    {
        public static void CreateShortcut(string path, PresetConfigV2 preset)
        {
            string shortcutName = preset.ZoneFullname + " - Collapse Launcher" + ".lnk";
            WshShell shell = new WshShell();
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(Path.Combine(path, shortcutName));
            shortcut.Description = string.Format("Launches {0} using Collapse Launcher.", preset.ZoneFullname);
            shortcut.TargetPath = AppExecutablePath;
            shortcut.Arguments = string.Format("open -g \"{0}\" -r \"{1}\"", preset.GameName, preset.ZoneName);
            shortcut.Save();
        }

        /// Heavily based on Heroic Games Launcher "Add to Steam" feature.
        /// 
        /// Source:
        /// https://github.com/Heroic-Games-Launcher/HeroicGamesLauncher/blob/8bdee1383446d3b81e240a4300baaf337d48ec92/src/backend/shortcuts/nonesteamgame/nonesteamgame.ts

        public static void AddToSteam(PresetConfigV2 preset)
        {

        }

        public static void RemoveFromSteam(string zoneFullName)
        {

        }

        public static bool IsAddedToSteam(string zoneFullName)
        {
            return false;
        }
    }
}
