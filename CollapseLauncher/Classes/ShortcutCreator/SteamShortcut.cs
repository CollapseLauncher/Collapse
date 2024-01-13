using System;
using System.IO;
using Hi3Helper.Preset;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.ShortcutsUtils
{
    public sealed class SteamShortcut
    {
        /// Based on CorporalQuesadilla's documentation on Steam Shortcuts.
        /// 
        /// Source:
        /// https://github.com/CorporalQuesadilla/Steam-Shortcut-Manager/wiki/Steam-Shortcuts-Documentation

        public string preliminaryAppID = "";
        private GameType gameType = GameType.Unknown;

        #region Shortcut fields
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
        public string LastPlayTime = "\x00\x00\x00";
        public string FlatpakAppID = "";
        public string tags = "";
        #endregion

        public SteamShortcut() { }

        public SteamShortcut(int count, PresetConfigV2 preset, bool play = false)
        {
            AppName = string.Format("{0} - {1}", preset.GameName, preset.ZoneName);
            Exe = AppExecutablePath;
            var id = BitConverter.GetBytes(GenerateAppId(Exe, AppName));
            appid = ShortcutCreator.ANSI.GetString(id, 0, id.Length);

            icon = Path.Combine(Path.GetDirectoryName(AppExecutablePath), "Assets/Images/SteamShortcuts/" + preset.GameType switch
            {
                GameType.StarRail => "starrail/icon.ico",
                GameType.Genshin => "genshin/icon.ico",
                _ => "honkai/icon.ico",
            });

            preliminaryAppID = GeneratePreliminaryId(Exe, AppName).ToString();

            StartDir = Path.GetDirectoryName(AppExecutablePath);

            LaunchOptions = string.Format("open -g \"{0}\" -r \"{1}\"", preset.GameName, preset.ZoneName);
            if (play)
                LaunchOptions += " -p";

            entryID = count.ToString();
            gameType = preset.GameType;
        }

        private static char BoolToByte(bool b) => b ? '\x01' : '\x00';

        public string ToEntry()
        {
            return '\x00' + entryID + '\x00'
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


        private uint GeneratePreliminaryId(string exe, string appname)
        {
            string key = exe + appname;
            var crc32 = new System.IO.Hashing.Crc32();
            crc32.Append(ShortcutCreator.ANSI.GetBytes(key));
            uint top = BitConverter.ToUInt32(crc32.GetCurrentHash()) | 0x80000000;
            return (top << 32) | 0x02000000;
        }

        private uint GenerateAppId(string exe, string appname)
        {
            uint appId = GeneratePreliminaryId(exe, appname);

            return appId >> 32;
        }

        /*private uint GenerateGridId(string exe, string appname)
        {
            uint appId = GeneratePreliminaryId(exe, appname);

            return (appId >> 32) - 0x10000000;
        }*/

        public void MoveImages(string path)
        {
            if (gameType == GameType.Unknown)
                return;

            path = Path.GetDirectoryName(path);
            string gridPath = Path.Combine(path, "grid");
            if (!Directory.Exists(gridPath))
                Directory.CreateDirectory(gridPath);

            // Game background
            CopyImage(gridPath, "hero", "_hero");

            // Game logo
            CopyImage(gridPath, "logo", "_logo");

            // Vertical banner
            // Shows when viewing all games of category or in the Home page
            CopyImage(gridPath, "banner", "p");

            // Horizontal banner
            // Appears in Big Picture mode when the game is the most recently played
            CopyImage(gridPath, "preview", "");
        }

        private void CopyImage(string gridPath, string type, string steamSuffix)
        {
            string game = gameType switch
            {
                GameType.StarRail => "starrail",
                GameType.Genshin => "genshin",
                _ => "honkai",
            };

            string originalPath = Path.Combine(Path.GetDirectoryName(AppExecutablePath), 
                string.Format("Assets/Images/SteamShortcuts/{0}/{1}.png", game, type));

            string steamPath = Path.Combine(gridPath, preliminaryAppID + steamSuffix + ".png");
            
            File.Copy(originalPath, steamPath, true);
        }
    }
}
