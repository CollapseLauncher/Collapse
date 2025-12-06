using CollapseLauncher.Extension;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Shared.Region;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher.Plugins;

#nullable enable

internal sealed partial class PluginLauncherApiWrapper
{
    private async Task ConvertBackgroundImageEntries(HypLauncherBackgroundList newsData, CancellationToken token)
    {
        // TODO: Handle image sequence format with logo overlay (example: Wuthering Waves)
        string  backgroundFolder     = Path.Combine(LauncherConfig.AppGameImgFolder, "bg");
        string? firstImageSpritePath = null;
        string? firstImageSpriteUrl  = null;

        using PluginDisposableMemory<LauncherPathEntry> backgroundEntries = PluginDisposableMemoryExtension.ToManagedSpan<LauncherPathEntry>(_pluginMediaApi.GetBackgroundEntries);
        int count = backgroundEntries.Length;

        newsData.GameContentList.Clear();
        if (count == 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            using LauncherPathEntry entry = backgroundEntries[i];
            string?                 url   = entry.Path;
            if (string.IsNullOrEmpty(url))
            {
                continue;
            }

            string    fileName        = Path.GetFileNameWithoutExtension(url) + $"_{i}" + Path.GetExtension(url);
            string    spriteLocalPath = Path.Combine(backgroundFolder, fileName);
            FileInfo  fileInfo        = new(spriteLocalPath);
            fileInfo.Directory?.Create();

            // Check if the background download is completed
            if (IsFileDownloadCompleted(fileInfo))
            {
                firstImageSpriteUrl  ??= url;
                firstImageSpritePath ??= spriteLocalPath;
                continue;
            }

            // Use local file as its url and do Copy Over instead.
            if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                // Remove "file://" prefix and normalize the path
                string localUrl = url.AsSpan()[7..].TrimStart("/\\").ToString().NormalizePath();

                if (string.IsNullOrEmpty(localUrl) || !File.Exists(localUrl))
                {
                    continue; // Skip if the file does not exist
                }

                await using FileStream fromFileStream = new(localUrl, FileMode.Open, FileAccess.Read, FileShare.Read);
                await using FileStream destCopyOver   = fileInfo.Create();

                await fromFileStream.CopyToAsync(destCopyOver, 64 << 10, token).ConfigureAwait(false);

                firstImageSpriteUrl  ??= url;
                firstImageSpritePath ??= spriteLocalPath;
                continue; // Skip further processing for local files
            }

            // Start the download
            Guid                   cancelTokenPass = _plugin.RegisterCancelToken(token);
            await using FileStream destDownload    = fileInfo.Create();
            _pluginMediaApi.DownloadAssetAsync(entry,
                                               destDownload.SafeFileHandle.DangerousGetHandle(),
                                               null,
                                               in cancelTokenPass,
                                               out nint asyncDownloadAssetResult);
            await asyncDownloadAssetResult.AsTask();

            SaveFileStamp(fileInfo, destDownload.Length);

            firstImageSpriteUrl  ??= url;
            firstImageSpritePath ??= spriteLocalPath;
        }

        // Set props
        _pluginMediaApi.GetBackgroundSpriteFps(out float fps);
        GameBackgroundImg           = firstImageSpriteUrl ?? string.Empty;
        GameBackgroundImgLocal      = firstImageSpritePath;
        GameBackgroundSequenceCount = count;
        GameBackgroundSequenceFps   = fps;
    }
}
