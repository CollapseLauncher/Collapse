using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Data;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.MainEntryPoint;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.ShortcutUtils
{
    public sealed class SteamShortcut
    {
        /// Based on CorporalQuesadilla's documentation on Steam Shortcuts.
        /// 
        /// Source:
        /// https://github.com/CorporalQuesadilla/Steam-Shortcut-Manager/wiki/Steam-Shortcuts-Documentation

        public string preliminaryAppID = "";

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
        public bool AllowDesktopConfig = true;
        public bool AllowOverlay = true;
        public bool OpenVR = false;
        public bool Devkit = false;
        public string DevkitGameID = "";
        public bool DevkitOverrideAppID = false;
        public string LastPlayTime = "\x00\x00\x00\x00";
        public string FlatpakAppID = "";
        public string tags = "";
        #endregion

        internal SteamShortcut() { }

        internal SteamShortcut(PresetConfig preset, bool play = false)
        {
            AppName = $"{preset.GameName} - {preset.ZoneName}";
            
            string stubPath = FindCollapseStubPath();
            Exe = $"\"{stubPath}\"";
            StartDir = $"\"{Path.GetDirectoryName(stubPath)}\"";

            var id = BitConverter.GetBytes(GenerateAppId(Exe, AppName));
            appid = SteamShortcutParser.ANSI.GetString(id, 0, id.Length);

            preliminaryAppID = GeneratePreliminaryId(Exe, AppName).ToString();

            LaunchOptions = $"open -g \"{preset.GameName}\" -r \"{preset.ZoneName}\"";
            if (play) LaunchOptions += " -p";
        }

        private static char BoolToByte(bool b) => b ? '\x01' : '\x00';

        public string ToEntry(int id = -1)
        {
            return '\x00' + (id >= 0 ? id.ToString() : entryID) + '\x00'
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
                    + '\x02' + "LastPlayTime" + '\x00' + LastPlayTime
                    + '\x01' + "FlatpakAppID" + '\x00' + FlatpakAppID + '\x00'
                    + '\x00' + "tags" + '\x00' + tags + "\x08\x08";
        }


        private static uint GeneratePreliminaryId(string exe, string appname)
        {
            string key = exe + appname;

            var crc32 = new Crc32();
            crc32.Append(SteamShortcutParser.ANSI.GetBytes(key));

            uint top = BitConverter.ToUInt32(crc32.GetCurrentHash()) | 0x80000000;

            return (top << 32) | 0x02000000;
        }

        public static uint GenerateAppId(string exe, string appname)
        {
            uint appId = GeneratePreliminaryId(exe, appname);

            return appId >> 32;
        }

        private static uint GenerateGridId(string exe, string appname)
        {
            uint appId = GeneratePreliminaryId(exe, appname);

            return (appId >> 32) - 0x10000000;
        }

        internal void MoveImages(string path, PresetConfig preset)
        {
            if (preset == null) return;
            var parentDir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parentDir)) Directory.CreateDirectory(parentDir);
            
            path = Path.GetDirectoryName(path);
            string gridPath = Path.Combine(path!, "grid");
            if (!Directory.Exists(gridPath)) Directory.CreateDirectory(gridPath);

            string iconName = ShortcutCreator.GetIconName(preset.GameType);

            icon = Path.Combine(gridPath, iconName);
            string iconAssetPath = Path.Combine(Path.GetDirectoryName(AppExecutablePath)!, "Assets\\Images\\GameIcon\\" + iconName);

            if (!Path.Exists(icon) && Path.Exists(iconAssetPath))
            {
                File.Copy(iconAssetPath, icon);
                LogWriteLine($"[SteamShortcut::MoveImages] Copied icon from {iconAssetPath} to {icon}.");
            }

            Dictionary<string, SteamGameProp> assets = preset.ZoneSteamAssets;
            if (assets == null) return;

            // Game background
            GetImageFromUrl(gridPath, assets["Hero"], "_hero");

            // Game logo
            GetImageFromUrl(gridPath, assets["Logo"], "_logo");

            // Vertical banner
            // Shows when viewing all games of category or in the Home page
            GetImageFromUrl(gridPath, assets["Banner"], "p");

            // Horizontal banner
            // Appears in Big Picture mode when the game is the most recently played
            GetImageFromUrl(gridPath, assets["Preview"], "");
        }

        private async void GetImageFromUrl(string gridPath, SteamGameProp asset, string steamSuffix)
        {
            string steamPath = Path.Combine(gridPath, preliminaryAppID + steamSuffix + ".png");

            string hash = MD5Hash(steamPath);

            if (hash.ToLower() == asset.MD5) return;

            string cdnURL = FallbackCDNUtil.TryGetAbsoluteToRelativeCDNURL(asset.URL, "metadata/");

            for (int i = 0; i < 3; i++)
            {
                FileInfo info = new FileInfo(steamPath);
                await DownloadImage(info, cdnURL, new CancellationToken());

                hash = MD5Hash(steamPath);

                if (hash.ToLower() == asset.MD5) return;

                File.Delete(steamPath);

                LogWriteLine($"[SteamShortcut::GetImageFromUrl] Invalid checksum for file {steamPath}! {hash} does not match {asset.MD5}.", LogType.Error);
            }
            
            LogWriteLine($"[SteamShortcut::GetImageFromUrl] After 3 tries, {cdnURL} could not be downloaded successfully.", LogType.Error);
        }

        private static async ValueTask DownloadImage(FileInfo fileInfo, string url, CancellationToken token)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(4 << 10);
            try
            {
                // Try to get the remote stream and download the file
                using Stream netStream = await FallbackCDNUtil.GetHttpStreamFromResponse(url, token);
                using Stream outStream = fileInfo.Open(new FileStreamOptions()
                {
                    Access = FileAccess.Write,
                    Mode = FileMode.Create,
                    Share = FileShare.ReadWrite
                });

                // Get the file length
                long fileLength = netStream.Length;

                // Copy (and download) the remote streams to local
                LogWriteLine($"Start downloading resource from: {url}", LogType.Default, true);
                int read;
                while ((read = await netStream.ReadAsync(buffer, token)) > 0)
                    await outStream.WriteAsync(buffer, 0, read, token);

                LogWriteLine($"Downloading resource from: {url} has been completed and stored locally into:"
                    + $"\"{fileInfo.FullName}\" with size: {ConverterTool.SummarizeSizeSimple(fileLength)} ({fileLength} bytes)", LogType.Default, true);
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex, ErrorType.Connection);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
