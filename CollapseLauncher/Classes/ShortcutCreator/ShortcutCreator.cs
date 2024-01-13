using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Microsoft.Win32;
using Hi3Helper.Preset;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.ShortcutsUtils
{
    public static class ShortcutCreator
    {
        #region ANSI
        private static List<SteamShortcut> shortcuts = [];
        public static Encoding ANSI { get; private set; }

        private static void RegisterANSIEncoding()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ANSI = Encoding.GetEncoding(1252);
        }
        #endregion

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

            RegisterANSIEncoding();

            LoadFile(paths[0]);
            
            if (IsAddedToSteam(preset))
            {
                LogWriteLine("Already added to Steam!", Hi3Helper.LogType.Error);
                return;
            }

            SteamShortcut shortcut = new SteamShortcut(shortcuts.Count, preset, play);
            shortcuts.Add(shortcut);
            shortcut.MoveImages(paths[0]);
            WriteFile(paths[0]);

            LogWriteLine("Steam Shortcut created for " + preset.ZoneFullname + "!");
        }

        public static bool IsAddedToSteam(PresetConfigV2 preset)
        {
            if (shortcuts.Count == 0)
            {
                var paths = GetShortcutsPath();

                if (paths == null || paths.Length == 0) return false;

                RegisterANSIEncoding();

                LoadFile(paths[0]);
            }

            var res = shortcuts.FindIndex(x => x.Exe == AppExecutablePath 
                && x.AppName == string.Format("{0} - {1}", preset.GameName, preset.ZoneName));
            return res != -1;
        }

        #region File Handling Methods
        private static void LoadFile(string path)
        {
            shortcuts.Clear();

            if (!File.Exists(path))
                return;

            var contents = File.ReadAllText(path, ANSI);
            contents = string.Concat(contents.Skip(11));

            foreach (string line in contents.Split("\x08\x08"))
            {
                if (line == "") continue;
                SteamShortcut steamShortcut = parseShortcut(line + '\x08');
                if (steamShortcut == null) continue;
                shortcuts.Add(steamShortcut);
            }
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

        private static string[] GetShortcutsPath()
        {
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
        #endregion

        #region Shortcut Parser
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

        public static SteamShortcut parseShortcut(string line)
        {
            byte[] ln = ANSI.GetBytes(line);
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
        #endregion
    }
}
