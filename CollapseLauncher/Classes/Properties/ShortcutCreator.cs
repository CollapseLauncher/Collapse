using Hi3Helper.Preset;
using static Hi3Helper.Shared.Region.LauncherConfig;
using System.IO;
using Microsoft.Win32;
using System.Linq;
using static Hi3Helper.Logger;
using CollapseLauncher.Statics;
using System.Text;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace CollapseLauncher
{
    internal static class ShortcutCreator
    {
        public static void CreateShortcut(string path, PresetConfigV2 preset)
        {
            string shortcutName = preset.ZoneFullname + " - Collapse Launcher" + ".lnk";
            IWshRuntimeLibrary.WshShell shell = new IWshRuntimeLibrary.WshShell();
            IWshRuntimeLibrary.IWshShortcut shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(Path.Combine(path, shortcutName));
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

            try
            {
                LoadFile(a[0]);
            } catch (Exception e)
            {
                LogWriteLine(e.Message, Hi3Helper.LogType.Error);
            }


        }

        public static void RemoveFromSteam(string zoneFullName)
        {

        }

        public static bool IsAddedToSteam(string zoneFullName)
        {
            return false;
        }

        /// Based on CorporalQuesadilla's documentation on Steam Shortcuts.
        /// 
        /// Source:
        /// https://github.com/CorporalQuesadilla/Steam-Shortcut-Manager/wiki/Steam-Shortcuts-Documentation
        private static int entryCount = 0;
        private static List<SteamShortcut> shortcuts = [];
        private struct SteamShortcut
        {
            public int entryID = entryCount;
            public string appid = "";
            public string AppName = "";
            public string Exe = "";
            public string StartDir = "";
            public string icon = "";
            public string ShortcutPath = "";
            public string LaunchOptions = "";
            public bool IsHidden = false;
            public bool AllowDesktopConfig = false;
            public bool AllowOverlay = false;
            public bool OpenVR = false;
            public bool Devkit = false;
            public string DevkitGameID = "";
            public bool DevkitOverrideAppID = false;
            public string LastPlayTime = "\x00\x00\x00\x00";
            public string FlatpakAppID = "";
            public string tags = "";

            public SteamShortcut() { }

            public SteamShortcut(GamePresetProperty preset, bool play = false)
            {
                AppName = preset._GamePreset.ZoneFullname + " - Collapse Launcher";
                Exe = Path.Combine(AppExecutablePath, AppExecutableName);
                appid = generateAppId(Exe, AppName).ToString();

                StartDir = AppExecutablePath;
                LaunchOptions = string.Format("-g {0} -r {1}", preset._GamePreset.GameName, preset._GamePreset.ZoneName);
                if (play)
                    LaunchOptions += " -p";
                
                entryCount++;
                shortcuts.Add(this);
            }

            public string ToEntry()
            {
                return    '\x00' + entryID + '\x00'
                        + '\x02' + "appid" + '\x00' + appid + '\x00'
                        + '\x01' + "AppName" + '\x00' + AppName + '\x00'
                        + '\x01' + "Exe" + '\x00' + Exe + '\x00'
                        + '\x01' + "StartDir" + '\x00' + StartDir + '\x00' +
                        + '\x01' + "icon" + '\x00' + icon + '\x00' +
                        + '\x01' + "ShortcutPath" + '\x00' + ShortcutPath + '\x00' +
                        + '\x01' + "LaunchOptions" + '\x00' + LaunchOptions + '\x00'
                        + '\x02' + "IsHidden" + '\x00' + IsHidden + "\x00\x00\x00"
                        + '\x02' + "AllowDesktopConfig" + '\x00' + AllowDesktopConfig + "\x00\x00\x00"
                        + '\x02' + "AllowOverlay" + '\x00' + AllowOverlay + "\x00\x00\x00"
                        + '\x02' + "OpenVR" + '\x00' + OpenVR + "\x00\x00\x00"
                        + '\x02' + "Devkit" + '\x00' + Devkit + "\x00\x00\x00"
                        + '\x01' + "DevkitGameID" + '\x00' + DevkitGameID + '\x00'
                        + '\x02' + "DevkitOverrideAppID" + '\x00' + DevkitOverrideAppID + '\x00'
                        + '\x02' + "LastPlayTime" + '\x00' + LastPlayTime + '\x00'
                        + '\x01' + "FlatpakAppID" + '\x00' + FlatpakAppID + '\x00'
                        + '\x00' + "tags" + '\x00' + tags + "\x08\x08";
            }
        }

        private static void LoadFile(string path)
        {
            if (!File.Exists(path))
                return;

            var contents = File.ReadAllText(path);
            contents = string.Concat(contents.Skip(11));

            foreach (string line in contents.Split("\x08\x08"))
            {
                int pos = 0;
                SteamShortcut newShortcut = new SteamShortcut();
                Queue<char> ln = new Queue<char> {};
                foreach (char c in line)
                    ln.Enqueue(c);
                ln.Enqueue('\x08');
                newShortcut.entryID = (int)parseValue(ref ln, null);
                newShortcut.appid = (string)parseValue(ref ln, "appid");
                newShortcut.AppName = (string)parseValue(ref ln, "AppName");
                shortcuts.Add(newShortcut);
            }
            

            foreach (SteamShortcut st in  shortcuts)
            {
                LogWriteLine(st.AppName, Hi3Helper.LogType.Warning);
            }
            
        }

        private static object parseValue(ref Queue<char> line, string expectedName, int leadingNulls = 1)
        {
            if (line.Count == 0)
                return null;
            
            string name = "";
            int quoteCount = 0;
            char type = 'a';
            while (line.First() != '\x08' && line.Count > 0)
            {
                char c = line.Dequeue();

                if ((c == '\x01' || c == '\x02') && quoteCount == 0)
                {
                    quoteCount++;
                    type = c;
                    continue;
                }

                if ((c == '\x00' || c == '\x01') && quoteCount == 3 && type == '\x02')
                {
                    while (line.Peek() == '\x00')
                        line.Dequeue();
                    return c == '\x01';
                }

                if (c == '\x08')
                        return type == '\x03' ? name : null;

                if (c == '\x00')
                {
                    switch (quoteCount)
                    {
                        case 0:
                            type = c;
                            break;
                        case 1:
                            if (name != "tags" && type == '\x00')
                            {
                                _ = int.TryParse(name, out int res);
                                return res;
                            }
                            break;
                        case 2:
                            if (name != expectedName)
                                return null;
                            if (name == "tags")
                                type = '\x03';
                            if (name == "appid")
                                type = '\x01';
                            name = "";
                            break;
                        case 3:
                            return name;
                    }
                    quoteCount++;
                    continue;
                }
                name += c;
            }
            return null;
        }

        private static ulong generateAppId(string exe, string appname)
        {
            string key = exe + appname;
            var crc32 = new System.IO.Hashing.Crc32();
            crc32.Append(Encoding.UTF8.GetBytes(key));
            ulong top = BitConverter.ToUInt64(crc32.GetCurrentHash()) | 0x80000000;
            return top << 32 | 0x02000000;
        }

        private static void WriteFile(string path)
        {
            FileStream fs = File.OpenWrite(path + "2");
            StreamWriter sw = new StreamWriter(fs);

            sw.Write("\x00shortcuts\x00");

            foreach (SteamShortcut st in shortcuts)
            {
                sw.Write(st.ToEntry());
            }
            sw.Write("\x08\x08");

            sw.Flush();
        }
    }
}
