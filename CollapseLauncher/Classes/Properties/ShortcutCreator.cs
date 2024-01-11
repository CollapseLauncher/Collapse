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

        public static void AddToSteam(GamePresetProperty preset, bool play)
        {
            var paths = GetShortcutsPath();

            if (paths == null || paths.Length == 0) return;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ANSI = Encoding.GetEncoding(1252);

            LoadFile(paths[0]);
                
            shortcuts.Add(new SteamShortcut(preset, play));

            WriteFile(paths[0]);
        }

        public static void RemoveFromSteam(GamePresetProperty preset)
        {

        }

        public static bool IsAddedToSteam(GamePresetProperty preset)
        {
            if (shortcuts.Count == 0)
            {
                var paths = GetShortcutsPath();

                if (paths == null || paths.Length == 0) return false;

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                ANSI = Encoding.GetEncoding(1252);

                LoadFile(paths[0]);
            }

            var res = shortcuts.Find(x => x.Exe == Path.Combine(AppExecutablePath, AppExecutableName) 
                && (x.LaunchOptions == string.Format("-g \"{0}\" -r \"{1}\"", preset._GamePreset.GameName, preset._GamePreset.ZoneName) ||
                    x.LaunchOptions == string.Format("-g \"{0}\" -r \"{1}\" -p", preset._GamePreset.GameName, preset._GamePreset.ZoneName)
                ));
            return res.entryID != "";
        }

        /// Based on CorporalQuesadilla's documentation on Steam Shortcuts.
        /// 
        /// Source:
        /// https://github.com/CorporalQuesadilla/Steam-Shortcut-Manager/wiki/Steam-Shortcuts-Documentation
        private static List<SteamShortcut> shortcuts = [];
        private struct SteamShortcut
        {
            public string entryID = "";
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
                AppName = preset._GamePreset.ZoneFullname;
                Exe = AppExecutablePath;
                appid = generateAppId(Exe, AppName).ToString();

                StartDir = Path.GetDirectoryName(AppExecutablePath);
                LaunchOptions = string.Format("open -g \"{0}\" -r \"{1}\"", preset._GamePreset.GameName, preset._GamePreset.ZoneName);
                if (play)
                    LaunchOptions += " -p";

                entryID = shortcuts.Count.ToString();
            }

            private char BoolToByte(bool b) => b ? '\x01' : '\x00'; 

            public string ToEntry()
            {
                return    '\x00' + entryID + '\x00'
                        + '\x02' + "appid" + '\x00' + appid
                        + '\x01' + "AppName" + '\x00' + AppName + '\x00'
                        + '\x01' + "Exe" + '\x00' + Exe + '\x00'
                        + '\x01' + "StartDir" + '\x00' + StartDir + '\x00'
                        + '\x01' + "icon" + '\x00' + icon + '\x00'
                        + '\x01' + "ShortcutPath" + '\x00' + ShortcutPath + '\x00'
                        + '\x01' + "LaunchOptions" + '\x00' + LaunchOptions + '\x00'
                        + '\x02' + "IsHidden" + '\x00' + BoolToByte(IsHidden) + "\x00\x00\x00"
                        + '\x02' + "AllowDesktopConfig" + '\x00' + BoolToByte(AllowDesktopConfig) + "\x00\x00\x00"
                        + '\x02' + "AllowOverlay" + '\x00' + BoolToByte(AllowOverlay) + "\x00\x00\x00"
                        + '\x02' + "OpenVR" + '\x00' + BoolToByte(OpenVR) + "\x00\x00\x00"
                        + '\x02' + "Devkit" + '\x00' + BoolToByte(Devkit) + "\x00\x00\x00"
                        + '\x01' + "DevkitGameID" + '\x00' + DevkitGameID + '\x00'
                        + '\x02' + "DevkitOverrideAppID" + '\x00' + BoolToByte(DevkitOverrideAppID) + "\x00\x00\x00"
                        + '\x02' + "LastPlayTime" + '\x00' + LastPlayTime + '\x00'
                        + '\x01' + "FlatpakAppID" + '\x00' + FlatpakAppID + '\x00'
                        + '\x00' + "tags" + '\x00' + tags + "\x08\x08";
            }
        }

        private enum ParseType
        {
            FindType,
            NameStr,
            ValueStr,
            ValueAppid,
            NameBool,
            ValueBool,
            ValueTime,
            NameTags,
            ValueTags
        }

        private static Encoding ANSI;
        private static void LoadFile(string path)
        {
            shortcuts.Clear();

            if (!File.Exists(path))
                return;

            var contents = File.ReadAllText(path, ANSI);
            contents = string.Concat(contents.Skip(11));

            foreach (string line in contents.Split("\x08\x08"))
            {
                SteamShortcut? steamShortcut = parseShortcut(ANSI.GetBytes(line + '\x08'));
                if (steamShortcut == null) continue;
                shortcuts.Add((SteamShortcut)steamShortcut);
            }
        }

        private static SteamShortcut? parseShortcut(byte[] ln)
        {
            SteamShortcut newShortcut = new SteamShortcut();

            List<string> strRes = new List<string>();
            List<bool> boolRes = new List<bool>();

            List<byte> buffer = [];
            ParseType parse = ParseType.NameStr;
            for (int i = 0; i < ln.Length; i++)
            {
                if (ln[0] != 0)
                    return null;
                switch (parse)
                {
                    case ParseType.FindType:
                        if (ln[i] == 0)
                            parse = ParseType.NameTags;
                        if (ln[i] == 1)
                            parse = ParseType.NameStr;
                        if (ln[i] == 2)
                            parse = ParseType.NameBool;
                        continue;
                    case ParseType.NameStr:
                    case ParseType.NameBool:
                        if (ln[i] == 0)
                        {
                            parse = parse == ParseType.NameStr ? ParseType.ValueStr : ParseType.ValueBool;
                            string key = ANSI.GetString(buffer.ToArray(), 0, buffer.Count);
                            if (key == "LastPlayTime")
                                parse = ParseType.ValueTime;
                            if (key == "appid")
                                parse = ParseType.ValueAppid;
                            buffer = [];
                            continue;
                        }
                        buffer.Add(ln[i]);
                        break;
                    case ParseType.ValueTime:
                        if (ln[i] == 0)
                        {
                            newShortcut.LastPlayTime = buffer.Count != 0 ? ANSI.GetString(buffer.ToArray(), 0, buffer.Count) : "\x00\x00\x00";
                            buffer = [];
                            parse = ParseType.FindType;
                            while (i < ln.Length - 1 && ln[i + 1] == 0)
                                i++;
                            continue;
                        }
                        buffer.Add(ln[i]);
                        break;
                    case ParseType.ValueAppid:
                        if (ln[i] == 1)
                        {
                            newShortcut.appid = ANSI.GetString(buffer.ToArray(), 0, buffer.Count);
                            buffer = [];
                            parse = ParseType.NameStr;
                            continue;
                        }
                        buffer.Add(ln[i]);
                        break;
                    case ParseType.NameTags:
                        if (ln[i] == 0)
                        {
                            parse = ParseType.ValueTags;
                            buffer = [];
                            continue;
                        }
                        buffer.Add(ln[i]);
                        break;
                    case ParseType.ValueTags:
                        if (ln[i] == 8)
                        {
                            newShortcut.tags = ANSI.GetString(buffer.ToArray(), 0, buffer.Count);
                            buffer = [];
                            continue;
                        }
                        buffer.Add(ln[i]);
                        break;
                    case ParseType.ValueStr:
                        if (ln[i] == 0)
                        {
                            strRes.Add(ANSI.GetString(buffer.ToArray(), 0, buffer.Count));
                            buffer = [];
                            parse = ParseType.FindType;
                            continue;
                        }
                        buffer.Add(ln[i]);
                        break;
                    case ParseType.ValueBool:
                        boolRes.Add(ln[i] == 1);
                        if (ln[i + 3] == 0)
                            i += 3;
                        parse = ParseType.FindType;
                        break;
                }
            }

            if (strRes.Count != 9 || boolRes.Count != 6)
            {
                LogWriteLine("Invalid shortcut! Skipping...", Hi3Helper.LogType.Error);
                return null;
            }
            
            newShortcut.entryID = strRes[0];
            newShortcut.AppName = strRes[1];
            newShortcut.Exe = strRes[2];
            newShortcut.StartDir = strRes[3];
            newShortcut.icon = strRes[4];
            newShortcut.ShortcutPath = strRes[5];
            newShortcut.LaunchOptions = strRes[6];
            newShortcut.DevkitGameID = strRes[7];
            newShortcut.FlatpakAppID = strRes[8];

            newShortcut.IsHidden = boolRes[0];
            newShortcut.AllowDesktopConfig = boolRes[1];
            newShortcut.AllowOverlay = boolRes[2];
            newShortcut.OpenVR = boolRes[3];
            newShortcut.Devkit = boolRes[4];
            newShortcut.DevkitOverrideAppID = boolRes[5];
            
            return newShortcut;
        }

        private static uint generateAppId(string exe, string appname)
        {
            string key = exe + appname;
            var crc32 = new System.IO.Hashing.Crc32();
            crc32.Append(ANSI.GetBytes(key));
            uint top = BitConverter.ToUInt32(crc32.GetCurrentHash()) | 0x80000000;
            return top << 32 | 0x02000000;
        }

        private static void WriteFile(string path)
        {
            if (File.Exists(path + "_old"))
            {
                File.Delete(path + "_old");

                if (File.Exists(path))
                    File.Move(path, path + "_old");
            }

            FileStream fs = File.OpenWrite(path);
            StreamWriter sw = new StreamWriter(fs, ANSI);

            sw.Write("\x00shortcuts\x00");

            foreach (SteamShortcut st in shortcuts)
            {
                sw.Write(st.ToEntry());
            }
            sw.Write("\x08\x08");
            sw.Close();
            fs.Close();
            shortcuts.Clear();
        }
    }
}
