using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Data;
using System;
using System.Buffers;
using System.IO;
using System.IO.Hashing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.MainEntryPoint;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.ShortcutUtils
{
    public sealed class SteamShortcut
    {
        #region Shortcut fields
        // We don't care about any other fields.
        public readonly uint AppID;
        public readonly string AppName;
        public readonly string Exe;
        public readonly string StartDir;
        public readonly string Icon;
        public readonly string LaunchOptions;
        #endregion

        private readonly string _path;
        private readonly PresetConfig _preset;

        internal SteamShortcut(string path, PresetConfig preset, bool play = false)
        {
            _path = Path.GetDirectoryName(path);
            _preset = preset;

            var translatedGameTitle =
                InnerLauncherConfig.GetGameTitleRegionTranslationString(preset.GameName,
                                                                        Locale.Lang._GameClientTitles)!;
            var translatedGameRegion =
                InnerLauncherConfig.GetGameTitleRegionTranslationString(preset.ZoneName,
                                                                        Locale.Lang._GameClientRegions);
            AppName = $"{translatedGameTitle} - {translatedGameRegion}";

            var stubPath = FindCollapseStubPath();
            Exe = $"\"{stubPath}\"";
            StartDir = $"{Path.GetDirectoryName(stubPath)}";

            var appNameInternal = $"{preset.GameName} - {preset.ZoneName}";
            AppID = GenerateAppId(Exe, appNameInternal);

            var gridPath = Path.Combine(_path!, "grid");
            var iconName = ShortcutCreator.GetIconName(_preset.GameType);
            Icon = Path.Combine(gridPath, iconName);

            LaunchOptions = $"open -g \"{preset.GameName}\" -r \"{preset.ZoneName}\"";
            if (play) LaunchOptions += " -p";
        }

        private static uint GenerateAppId(string exe, string appname)
        {
            // Actually the appid generation algorithm for custom apps has been changed.
            // It's now a completely random number instead of the crc32.
            // But to be able to track the target app, we still use crc32 as appid.
            var crc32 = new Crc32();
            crc32.Append(Encoding.UTF8.GetBytes(exe + appname));
            return BitConverter.ToUInt32(crc32.GetCurrentHash()) | 0x80000000;
        }

        internal void MoveImages()
        {
            if (!string.IsNullOrEmpty(_path)) Directory.CreateDirectory(_path);

            var gridPath = Path.Combine(_path!, "grid");
            if (!Directory.Exists(gridPath)) Directory.CreateDirectory(gridPath);

            var iconName = ShortcutCreator.GetIconName(_preset.GameType);
            var iconAssetPath = Path.Combine(Path.GetDirectoryName(AppExecutablePath)!, "Assets\\Images\\GameIcon\\" + iconName);

            if (!Path.Exists(Icon) && Path.Exists(iconAssetPath))
            {
                File.Copy(iconAssetPath, Icon);
                LogWriteLine($"[SteamShortcut::MoveImages] Copied icon from {iconAssetPath} to {Icon}.");
            }

            var assets = _preset.ZoneSteamAssets;
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
            string steamPath = Path.Combine(gridPath, AppID + steamSuffix + ".png");

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
