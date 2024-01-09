using IWshRuntimeLibrary;
using Hi3Helper.Preset;
using static Hi3Helper.Shared.Region.LauncherConfig;
using System.IO;
using Microsoft.Win32;
using System.Linq;
using static Hi3Helper.Logger;
using CollapseLauncher.Statics;

namespace CollapseLauncher
{
    internal static class ShortcutCreator
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

        private static string[] GetShortcutsPath() {

            RegistryKey reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam", false);
            if (reg == null)
                return null;

            string steamPath = (string)reg.GetValue("InstallPath", null);

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

        private static void WriteShortcutsFile(string path)
        {

        }

        public static void AddToSteam(GamePresetProperty preset)
        {
            var a = GetShortcutsPath();

            if (a == null || a.Length == 0) return;

            foreach (string b in a)
                LogWriteLine(b, Hi3Helper.LogType.Error);
        }

        public static void RemoveFromSteam(string zoneFullName)
        {

        }

        public static bool IsAddedToSteam(string zoneFullName)
        {
            return false;
        }

        private static int entryCount = 0;
        private struct SteamShortcut
        {
            public int entryID = entryCount;
            public string appName;
            public string unquotedPath = AppExecutablePath;
            public string startDir;
            public string iconPath;
            public string shortcutPath = AppExecutablePath;
            public string launchOptions;
            public bool isHidden = false;
            public bool allowDeskConf = false;
            public bool allowOverlay = false;
            public bool openVR = false;
            public string lastPlayTime = "";
            public string tags = "";

            public SteamShortcut(GamePresetProperty preset, bool play = false)
            {
                startDir = preset._GameVersion.GameDirPath;
                launchOptions = string.Format("-g {0} -r {1}", preset._GamePreset.GameName, preset._GamePreset.ZoneName);
                if (play)
                    launchOptions += " -p";
                appName = preset._GamePreset.ZoneFullname + " - Collapse Launcher";
            }

            public string ToEntry()
            {
                return    '\x00' + entryID.ToString() + '\x00'
                        + '\x01' + "appname" + '\x00' + appName + '\x00'
                        + '\x01' + "exe" + '\x00' + unquotedPath + '\x00'
                        + '\x01' + "StartDir" + '\x00' + startDir + '\x00' +
                        + '\x01' + "icon" + '\x00' + iconPath + '\x00' +
                        + '\x01' + "ShortcutPath" + '\x00' + shortcutPath + '\x00' +
                        + '\x01' + "LaunchOptions" + '\x00' + launchOptions + '\x00'
                        + '\x02' + "IsHidden" + '\x00' + isHidden + "\x00\x00\x00"
                        + '\x02' + "AllowDesktopConfig" + '\x00' + allowDeskConf + "\x00\x00\x00"
                        + '\x02' + "AllowOverlay" + '\x00' + allowOverlay + "\x00\x00\x00"
                        + '\x02' + "OpenVR" + '\x00' + openVR + "\x00\x00\x00"
                        + '\x02' + "LastPlayTime" + '\x00' + lastPlayTime
                        + '\x00' + "tags" + '\x00' + tags + "\x08\x08";
            }
        }
    }
}
