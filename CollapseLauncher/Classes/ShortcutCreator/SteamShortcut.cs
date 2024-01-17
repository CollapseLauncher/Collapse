using CollapseLauncher.ShortcutUtils;
using Hi3Helper.Data;
using Hi3Helper.Preset;
using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Logger;
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

        public SteamShortcut(PresetConfigV2 preset, bool play = false)
        {
            AppName = string.Format("{0} - {1}", preset.GameName, preset.ZoneName);
            Exe = AppExecutablePath;
            var id = BitConverter.GetBytes(GenerateAppId(Exe, AppName));
            appid = SteamShortcutParser.ANSI.GetString(id, 0, id.Length);

            icon = Path.Combine(Path.GetDirectoryName(AppExecutablePath), "Assets/Images/GameIcon/" + preset.GameType switch
            {
                GameType.StarRail => "icon-starrail.ico",
                GameType.Genshin => "icon-genshin.ico",
                _ => "icon-honkai.ico",
            });

            preliminaryAppID = GeneratePreliminaryId(Exe, AppName).ToString();

            StartDir = Path.GetDirectoryName(AppExecutablePath);

            LaunchOptions = string.Format("open -g \"{0}\" -r \"{1}\"", preset.GameName, preset.ZoneName);
            if (play)
                LaunchOptions += " -p";
        }

        private static char BoolToByte(bool b) => b ? '\x01' : '\x00';

        public string ToEntry(int entryID = -1)
        {
            return '\x00' + (entryID >= 0 ? entryID.ToString() : this.entryID) + '\x00'
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


        private static uint GeneratePreliminaryId(string exe, string appname)
        {
            string key = exe + appname;
            var crc32 = new System.IO.Hashing.Crc32();
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

        public void MoveImages(string path, PresetConfigV2 preset)
        {
            if (preset == null) return;

            path = Path.GetDirectoryName(path);
            string gridPath = Path.Combine(path, "grid");
            if (!Directory.Exists(gridPath))
                Directory.CreateDirectory(gridPath);

            SteamAssetCollection assets = preset.ZoneSteamAssets;

            // Game background
            GetImageFromUrl(gridPath, assets.Hero, "_hero");

            // Game logo
            GetImageFromUrl(gridPath, assets.Logo, "_logo");

            // Vertical banner
            // Shows when viewing all games of category or in the Home page
            GetImageFromUrl(gridPath, assets.Banner, "p");

            // Horizontal banner
            // Appears in Big Picture mode when the game is the most recently played
            GetImageFromUrl(gridPath, assets.Preview, "");
        }

        private static string MD5Hash(string path)
        {
            if (!File.Exists(path))
                return "";
            FileStream stream = File.OpenRead(path);
            var hash = System.Security.Cryptography.MD5.Create().ComputeHash(stream);
            stream.Close();
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
        }

        private async void GetImageFromUrl(string gridPath, SteamAsset asset, string steamSuffix)
        {
            string steamPath = Path.Combine(gridPath, preliminaryAppID + steamSuffix + ".png");

            string hash = MD5Hash(steamPath);

            if (hash.ToLower() == asset.MD5) return;

            for (int i = 0; i < 3; i++)
            {
                FileInfo info = new FileInfo(steamPath);
                await DownloadImage(info, asset.URL, new CancellationToken());

                hash = MD5Hash(steamPath);

                if (hash.ToLower() == asset.MD5) return;

                File.Delete(steamPath);

                LogWriteLine(string.Format("Invalid checksum for file {0}! {1} does not match {2}.", steamPath, hash, asset.MD5), Hi3Helper.LogType.Error);
            }
            
            LogWriteLine("After 3 tries, " + asset.URL + " could not be downloaded successfully.", Hi3Helper.LogType.Error);
            return;
        }

        private static async ValueTask DownloadImage(FileInfo fileInfo, string url, CancellationToken token)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(4 << 10);
            try
            {
                // Try get the remote stream and download the file
                using Stream netStream = await FallbackCDNUtil.GetHttpStreamFromResponse(url, token);
                using Stream outStream = fileInfo.Open(new FileStreamOptions()
                {
                    Access = FileAccess.Write,
                    Mode = FileMode.Create,
                    Share = FileShare.ReadWrite,
                    Options = FileOptions.Asynchronous
                });

                // Get the file length
                long fileLength = netStream.Length;

                // Copy (and download) the remote streams to local
                LogWriteLine($"Start downloading resource from: {url}", Hi3Helper.LogType.Default, true);
                int read = 0;
                while ((read = await netStream.ReadAsync(buffer, token)) > 0)
                    await outStream.WriteAsync(buffer, 0, read, token);

                LogWriteLine($"Downloading resource from: {url} has been completed and stored locally into:"
                    + $"\"{fileInfo.FullName}\" with size: {ConverterTool.SummarizeSizeSimple(fileLength)} ({fileLength} bytes)", Hi3Helper.LogType.Default, true);
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
