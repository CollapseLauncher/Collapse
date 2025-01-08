using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static Hi3Helper.Logger;

namespace CollapseLauncher.ShortcutUtils
{
    public sealed class SteamShortcutParser
    {
        public static Encoding ANSI;
        private string _path;
        private List<SteamShortcut> _shortcuts = [];

        public SteamShortcutParser(string path)
        {
            _path = path;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ANSI = Encoding.GetEncoding(1252);

            Load();
        }

        public bool Contains(SteamShortcut shortcut) => _shortcuts.FindIndex(x => x.preliminaryAppID == shortcut.preliminaryAppID) != -1;

        internal void Insert(PresetConfig preset, bool play = false)
        {
            SteamShortcut shortcut = new SteamShortcut(preset, play);

            if (Contains(shortcut))
            {
                _shortcuts.RemoveAll(x => x.preliminaryAppID == shortcut.preliminaryAppID);
            }

            _shortcuts.Add(shortcut);
            shortcut.MoveImages(_path, preset);
        }

        private void Load()
        {
            _shortcuts.Clear();

            if (!File.Exists(_path))
                return;

            var contents = File.ReadAllText(_path, ANSI);
            contents = string.Concat(contents.Skip(11));

            foreach (string line in contents.Split("\x08\x08"))
            {
                if (line == "") continue;
                SteamShortcut steamShortcut = ParseShortcut(line + '\x08');
                if (steamShortcut == null) continue;
                _shortcuts.Add(steamShortcut);
            }
        }

        public void Save()
        {
            if (File.Exists(_path))
                File.Move(_path, _path + "_old", true);

            FileStream fs = File.OpenWrite(_path);
            StreamWriter sw = new StreamWriter(fs, ANSI);

            sw.Write("\x00shortcuts\x00");

            int entryCount = 0;
            foreach (SteamShortcut st in _shortcuts)
            {
                sw.Write(st.ToEntry(entryCount));
                entryCount++;
            }
            sw.Write("\x08\x08");
            sw.Close();
            fs.Close();
        }

        #region Individual Shortcut Parser
        public SteamShortcut ParseShortcut(string line)
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
                    case ParseType.NameTags:
                        if (ln[i] == 0)
                        {
                            parse = ParseType.ValueTags;
                            buffer = [];
                            continue;
                        }
                        buffer.Add(ln[i]);
                        break;
                    case ParseType.ValueTime:
                        buffer.Add(ln[i]);
                        if (buffer.Count == 4)
                        {
                            newShortcut.LastPlayTime = ANSI.GetString(buffer.ToArray(), 0, buffer.Count);
                            buffer = [];
                            parse = ParseType.FindType;
                        }
                        break;
                    case ParseType.ValueAppid:
                        if (buffer.Count == 4)
                        {
                            newShortcut.appid = ANSI.GetString(buffer.ToArray(), 0, buffer.Count);
                            buffer = [];
                            parse = ParseType.NameStr;
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
                LogWriteLine("Invalid shortcut! Skipping...", LogType.Error);
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

            newShortcut.preliminaryAppID = SteamShortcut.GenerateAppId(newShortcut.Exe, newShortcut.AppName);

            return newShortcut;
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
        #endregion
    }
}
