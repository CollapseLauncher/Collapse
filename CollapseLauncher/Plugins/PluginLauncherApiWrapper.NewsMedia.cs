using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.LauncherApiLoader.Legacy;
using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper.Data;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Shared.Region;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable ConvertIfStatementToReturnStatement

namespace CollapseLauncher.Plugins;

#nullable enable
internal partial class PluginLauncherApiWrapper
{
    private delegate ref LauncherPathEntry            CreateDataAsPathDelegate();
    private delegate     PluginDisposableMemory<byte> CreateDataAsBufferDelegate();

    private async Task ConvertBackgroundImageEntries(LauncherGameNewsData newsData, CancellationToken token)
    {
        // TODO: Handle image sequence format with logo overlay (example: Wuthering Waves)
        string  backgroundFolder     = Path.Combine(LauncherConfig.AppGameImgFolder, "bg");
        string? firstImageSpritePath = null;
        string? firstImageSpriteUrl  = null;

        using PluginDisposableMemory<LauncherPathEntry> backgroundEntries = PluginDisposableMemoryExtension.ToManagedSpan<LauncherPathEntry>(_pluginMediaApi.GetBackgroundEntries);
        int count = backgroundEntries.Length;
        for (int i = 0; i < count; i++)
        {
            using var entry           = backgroundEntries[i];
            string    url             = entry.GetPathString();
            string    fileName        = Path.GetFileNameWithoutExtension(url) + $"_{i}" + Path.GetExtension(url);
            string    spriteLocalPath = Path.Combine(backgroundFolder, fileName);
            FileInfo  fileInfo        = new(spriteLocalPath);

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
            await _pluginMediaApi.DownloadAssetAsync(entry,
                                                     destDownload.SafeFileHandle.DangerousGetHandle(),
                                                     null,
                                                     in cancelTokenPass).WaitFromHandle();

            SaveFileStamp(fileInfo, destDownload.Length);

            firstImageSpriteUrl  ??= url;
            firstImageSpritePath ??= spriteLocalPath;
        }

        // Set props
        GameBackgroundImg           = firstImageSpriteUrl ?? string.Empty;
        GameBackgroundImgLocal      = firstImageSpritePath;
        GameBackgroundSequenceCount = count;
        GameBackgroundSequenceFps   = _pluginMediaApi.GetBackgroundSpriteFps();

        newsData.Background = new LauncherGameNewsBackground
        {
            BackgroundImg = GameBackgroundImg
        };
    }

    private async Task ConvertSocialMediaEntries(LauncherGameNewsData newsData, CancellationToken token)
    {
        string spriteFolder = Path.Combine(LauncherConfig.AppGameImgFolder, "cached");
        newsData.SocialMedia ??= [];
        newsData.SocialMedia.Clear();

        using PluginDisposableMemory<LauncherSocialMediaEntry> socialMediaEntry = PluginDisposableMemoryExtension.ToManagedSpan<LauncherSocialMediaEntry>(_pluginNewsApi.GetSocialMediaEntries);
        int count = socialMediaEntry.Length;
        for (int i = 0; i < count; i++)
        {
            using var                    entry = socialMediaEntry[i];
            LauncherSocialMediaEntryFlag flags = entry.Flags;

            string? iconTitle          = entry.SocialMediaDescription.CreateStringFromNullTerminated();
            string? iconClickUrl       = entry.SocialMediaClickUrl.CreateStringFromNullTerminated();
            string? qrImageDescription = entry.QrImageDescription.CreateStringFromNullTerminated();

            string? iconUrl = await GetSocialMediaIconFromFlags(flags,
                                                                LauncherSocialMediaEntryFlag.IconIsPath,
                                                                LauncherSocialMediaEntryFlag.IconIsDataBuffer,
                                                                spriteFolder,
                                                                entry.GetIconAsPath,
                                                                entry.GetIconAsDataBuffer,
                                                                token);
            string? iconHoverUrl = await GetSocialMediaIconFromFlags(flags,
                                                                     LauncherSocialMediaEntryFlag.IconIsPath,
                                                                     LauncherSocialMediaEntryFlag.IconIsDataBuffer,
                                                                     spriteFolder,
                                                                     entry.GetIconHoverAsPath,
                                                                     entry.GetIconHoverAsDataBuffer,
                                                                     token);
            string? qrImageUrl = flags.HasFlag(LauncherSocialMediaEntryFlag.HasQrImage) ?
                await GetSocialMediaIconFromFlags(flags,
                                                  LauncherSocialMediaEntryFlag.QrImageIsPath,
                                                  LauncherSocialMediaEntryFlag.QrImageIsDataBuffer,
                                                  spriteFolder,
                                                  entry.GetQrImageAsPath,
                                                  entry.GetQrImageAsDataBuffer,
                                                  token) :
                null;

            newsData.SocialMedia.Add(new LauncherGameNewsSocialMedia
            {
                IconId             = "0",
                IconImg            = iconUrl,
                IconImgHover       = iconHoverUrl ?? iconUrl,
                Title              = iconTitle,
                SocialMediaUrl     = iconClickUrl,
                QrImg              = qrImageUrl,
                QrTitle            = qrImageDescription,
                QrLinks            = GetSocialMediaInnerLinks(ref entry.ChildEntryHandle.AsRef<LauncherSocialMediaEntry>()),
                IsImageUrlHashable = false,
            });
        }
    }

    private static List<LauncherGameNewsSocialMediaQrLinks>? GetSocialMediaInnerLinks(ref LauncherSocialMediaEntry innerEntry)
    {
        if (Unsafe.IsNullRef(ref innerEntry))
        {
            return null;
        }

        List<LauncherGameNewsSocialMediaQrLinks> links = [];
        while (!Unsafe.IsNullRef(ref innerEntry))
        {
            try
            {
                using PluginDisposableMemory<byte> descriptionSpan = innerEntry.SocialMediaDescription;
                using PluginDisposableMemory<byte> descriptionUrl  = innerEntry.SocialMediaClickUrl;

                innerEntry = ref innerEntry.ChildEntryHandle.AsRef<LauncherSocialMediaEntry>();

                if (descriptionSpan.IsEmpty ||
                    descriptionUrl.IsEmpty)
                {
                    continue;
                }

                string? description = descriptionSpan.CreateStringFromNullTerminated();
                string? url         = descriptionUrl.CreateStringFromNullTerminated();

                links.Add(new LauncherGameNewsSocialMediaQrLinks
                {
                    Title = description,
                    Url   = url,
                });
            }
            finally
            {
                innerEntry.Dispose();
            }
        }

        return links;
    }

    private async Task<string?> GetSocialMediaIconFromFlags(LauncherSocialMediaEntryFlag flags,
                                                            LauncherSocialMediaEntryFlag flagIfUrl,
                                                            LauncherSocialMediaEntryFlag flagIfBuffer,
                                                            string                       outputDir,
                                                            CreateDataAsPathDelegate     dataAsUrlCreate,
                                                            CreateDataAsBufferDelegate   dataAsBufferCreate,
                                                            CancellationToken            token)
    {
        if (flags.HasFlag(flagIfUrl))
        {
            return await CopyOverUrlData(outputDir, dataAsUrlCreate, token);
        }

        if (flags.HasFlag(flagIfBuffer))
        {
            return await CopyOverEmbeddedData(outputDir, dataAsBufferCreate, token);
        }

        return string.Empty;
    }

    private async Task<string?> CopyOverUrlData(string                   outputDir,
                                                CreateDataAsPathDelegate urlCreate,
                                                CancellationToken        token)
    {
        using LauncherPathEntry urlData = urlCreate.Invoke();
        string                  dataUrl = urlData.GetPathString();

        if (string.IsNullOrEmpty(dataUrl))
        {
            return null;
        }

        string fileName = Path.GetFileName(dataUrl);
        string filePath = Path.Combine(outputDir, fileName);

        FileInfo fileInfo = new FileInfo(filePath)
                           .EnsureCreationOfDirectory()
                           .EnsureNoReadOnly();

        if (IsFileDownloadCompleted(fileInfo))
        {
            return filePath;
        }

        await using FileStream fileStream = fileInfo.Create();

        Guid cancelToken = _plugin.RegisterCancelToken(token);
        await _pluginNewsApi.DownloadAssetAsync(urlData,
                                                fileStream.SafeFileHandle.DangerousGetHandle(),
                                                null,
                                                in cancelToken).WaitFromHandle();

        SaveFileStamp(fileInfo, fileStream.Length);

        return filePath;
    }

    private static async Task<string?> CopyOverEmbeddedData(string                     outputDir,
                                                            CreateDataAsBufferDelegate bufferCreate,
                                                            CancellationToken          token)
    {
        using PluginDisposableMemory<byte> dataBuffer = bufferCreate.Invoke();
        if (dataBuffer.IsEmpty)
        {
            return null;
        }

        ReadOnlySpan<byte> headerSpan            = dataBuffer.AsSpan(0, Math.Min(1 << 10, dataBuffer.Length));
        string             fileExtension         = DecideEmbeddedDataExtension(headerSpan);
        string             fileBaseNameSignature = GetHashString(headerSpan);
        string             filePath              = Path.Combine(outputDir, $"embeddedPluginAssets-{fileBaseNameSignature}" + fileExtension);

        FileInfo fileInfo = new FileInfo(filePath)
            .EnsureCreationOfDirectory()
            .EnsureNoReadOnly();

        if (IsFileDownloadCompleted(fileInfo))
        {
            return filePath;
        }

        await using UnmanagedMemoryStream unmanagedStream = dataBuffer.AsStream();
        await using FileStream fileStream = fileInfo.Create();

        await unmanagedStream.CopyToAsync(fileStream, token);
        SaveFileStamp(fileInfo, fileStream.Length);

        return filePath;
    }

    private static string GetHashString(ReadOnlySpan<byte> headerData)
    {
        byte[] data = Hash.GetHashFromBytes<XxHash64>(headerData);
        string hashString = HexTool.BytesToHexUnsafe(data)!;
        return hashString;
    }

    private static string DecideEmbeddedDataExtension(ReadOnlySpan<byte> headerData)
    {
        if (headerData.StartsWith("<svg"u8))
        {
            return ".svg";
        }

        if (headerData.StartsWith("‰PNG"u8))
        {
            return ".png";
        }

        if (headerData.IndexOf("JFIF\0"u8) > 0)
        {
            return ".jpg";
        }

        if (headerData.StartsWith("RIFF"u8) &&
            headerData.IndexOf("WEBP"u8) > 7)
        {
            return ".webp";
        }

        return ".bin";
    }

    private static void SaveFileStamp(FileInfo fileInfo, long fileLength)
    {
        string  fileNamePrefix = Path.GetFileName(fileInfo.Name);
        string? fileDir        = Path.GetDirectoryName(fileInfo.FullName);

        string stampFileName = Path.Combine(fileDir ?? string.Empty, fileNamePrefix + $"#{fileLength}");
        File.WriteAllText(stampFileName, string.Empty); // Create an empty file to mark completion
    }
}
