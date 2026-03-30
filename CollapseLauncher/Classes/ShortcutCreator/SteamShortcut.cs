using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Loading;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Data;
using System;
using System.Buffers;
using System.Collections.Generic;
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

            string translatedGameTitle  = MetadataHelper.GetTranslatedTitle(preset.GameName);
            string translatedGameRegion = MetadataHelper.GetTranslatedRegion(preset.ZoneName);
            AppName = $"{translatedGameTitle} - {translatedGameRegion}";

            string stubPath = VelopackLocatorExtension.FindCollapseStubPath();
            Exe = $"\"{stubPath}\"";
            StartDir = $"{Path.GetDirectoryName(stubPath)}";

            string appNameInternal = $"{preset.GameName} - {preset.ZoneName}";
            AppID = GenerateAppId(Exe, appNameInternal);

            string gridPath = Path.Combine(_path!, "grid");
            string iconName = ShortcutCreator.GetIconName(_preset);
            Icon = Path.Combine(gridPath, iconName);

            LaunchOptions = $"open -g \"{preset.GameName}\" -r \"{preset.ZoneName}\"";
            if (play) LaunchOptions += " -p";
        }

        private static uint GenerateAppId(string exe, string appName)
        {
            // Actually the appid generation algorithm for custom apps has been changed.
            // It's now a completely random number instead of the crc32.
            // But to be able to track the target app, we still use crc32 as appid.
            Crc32 crc32 = new Crc32();
            crc32.Append(Encoding.UTF8.GetBytes(exe + appName));
            return BitConverter.ToUInt32(crc32.GetCurrentHash()) | 0x80000000;
        }

        internal async Task MoveImages(CancellationToken token)
        {
            if (!string.IsNullOrEmpty(_path)) Directory.CreateDirectory(_path);

            string gridPath = Path.Combine(_path!, "grid");
            if (!Directory.Exists(gridPath)) Directory.CreateDirectory(gridPath);

            string iconAssetPath = ShortcutCreator.GetIconPath(_preset);

            if (!Path.Exists(Icon) && Path.Exists(iconAssetPath))
            {
                File.Copy(iconAssetPath, Icon);
                LogWriteLine($"[SteamShortcut::MoveImages] Copied icon from {iconAssetPath} to {Icon}.");
            }

            await CacheImages(token);
            if (token.IsCancellationRequested) return;

            // Game background
            CopyImageFromCache(gridPath, "_hero");

            // Game logo
            CopyImageFromCache(gridPath, "_logo");

            // Vertical banner
            // Shows when viewing all games of category or in the Home page
            CopyImageFromCache(gridPath, "p");

            // Horizontal banner
            // Appears in Big Picture mode when the game is the most recently played
            CopyImageFromCache(gridPath, "");
        }

        private void CopyImageFromCache(string gridPath, string steamSuffix)
        {
            try
            {
                File.Copy(Path.Combine(AppGameImgCachedFolder, AppID + steamSuffix),
                          Path.Combine(gridPath, AppID + steamSuffix + ".png"), true);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private async Task CacheImages(CancellationToken token)
        {
            Dictionary<string, SteamGameProp> assets = _preset.ZoneSteamAssets;
            if (assets == null || assets.Count == 0) return;

            (string, string)[] images =
            [
                ("Hero", "_hero"),
                ("Logo", "_logo"),
                ("Banner", "p"),
                ("Preview", "")
            ];
            List<(string, string)> cacheImageList = [];

            for (int index = images.Length - 1; index >= 0; index--)
            {
                (string, string) image       = images[index];
                SteamGameProp    asset       = assets[image.Item1];
                string           steamSuffix = image.Item2;

                string cachePath = Path.Combine(AppGameImgCachedFolder, AppID + steamSuffix);
                string hash      = MD5Hash(cachePath);
                if (!hash.Equals(asset.MD5, StringComparison.OrdinalIgnoreCase))
                    cacheImageList.Add(image);
            }

            if (cacheImageList.Count == 0) return;

            LoadingMessageHelper.ShowLoadingFrame();

            for (int i = 0; i < cacheImageList.Count; i++)
            {
                (string, string) image = cacheImageList[i];
                string progressString = string.Format(Locale.Current.Lang?._Dialogs?.SteamShortcutDownloadingImages ?? "", i + 1, cacheImageList.Count);
                LoadingMessageHelper.SetMessage(Locale.Current.Lang?._Dialogs?.SteamShortcutTitle, progressString);
                await CacheImageFromUrl(assets[image.Item1], image.Item2, token);
            }

            LoadingMessageHelper.HideLoadingFrame();
        }

        private async Task CacheImageFromUrl(SteamGameProp asset, string steamSuffix, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            string cachePath = Path.Combine(AppGameImgCachedFolder, AppID + steamSuffix);

            string hash = MD5Hash(cachePath);
            if (hash.Equals(asset.MD5, StringComparison.OrdinalIgnoreCase)) return;

            string cdnURL = FallbackCDNUtil.TryGetAbsoluteToRelativeCDNURL(asset.URL, "metadata/");

            for (int i = 0; i < 3; i++)
            {
                FileInfo info = new FileInfo(cachePath);
                await DownloadImage(info, cdnURL, token);

                if (token.IsCancellationRequested)
                {
                    File.Delete(cachePath);
                    return;
                }

                hash = MD5Hash(cachePath);

                if (hash.Equals(asset.MD5, StringComparison.OrdinalIgnoreCase)) return;

                File.Delete(cachePath);

                LogWriteLine($"[SteamShortcut::GetImageFromUrl] Invalid checksum for file {cachePath}! {hash} does not match {asset.MD5}.", LogType.Error);
            }

            LogWriteLine($"[SteamShortcut::GetImageFromUrl] After 3 tries, {cdnURL} could not be downloaded successfully.", LogType.Error);
        }

        private static async ValueTask DownloadImage(FileInfo fileInfo, string url, CancellationToken token)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(4 << 10);
            try
            {
                // Try to get the remote stream and download the file
                await using Stream netStream = await FallbackCDNUtil.GetHttpStreamFromResponse(url, token);
                await using FileStream outStream = fileInfo.Open(new FileStreamOptions
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
                    await outStream.WriteAsync(buffer.AsMemory(0, read), token);

                LogWriteLine($"Downloading resource from: {url} has been completed and stored locally into:"
                             + $"\"{fileInfo.FullName}\" with size: {ConverterTool.SummarizeSizeSimple(fileLength)} ({fileLength} bytes)",
                             LogType.Default, true);
            }
            catch (TaskCanceledException)
            {
                // ignored
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
