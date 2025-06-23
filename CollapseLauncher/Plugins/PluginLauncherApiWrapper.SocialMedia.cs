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
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable ConvertIfStatementToReturnStatement

namespace CollapseLauncher.Plugins;

#nullable enable
internal partial class PluginLauncherApiWrapper
{
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

            string? iconTitle          = entry.Description;
            string? iconClickUrl       = entry.ClickUrl;
            string? qrImageDescription = entry.QrDescription;

            string? iconDataMarshal      = entry.IconPath;
            string? iconHoverDataMarshal = entry.IconHoverPath;
            string? qrImageDataMarshal   = entry.QrPath;

            string? iconUrl = await GetSocialMediaIconFromFlags(flags,
                                                                LauncherSocialMediaEntryFlag.IconIsPath,
                                                                LauncherSocialMediaEntryFlag.IconIsDataBuffer,
                                                                spriteFolder,
                                                                iconDataMarshal,
                                                                token);
            string? iconHoverUrl = await GetSocialMediaIconFromFlags(flags,
                                                                     LauncherSocialMediaEntryFlag.IconIsPath,
                                                                     LauncherSocialMediaEntryFlag.IconIsDataBuffer,
                                                                     spriteFolder,
                                                                     iconHoverDataMarshal,
                                                                     token);
            string? qrImageUrl = await GetSocialMediaIconFromFlags(flags,
                                                                   LauncherSocialMediaEntryFlag.QrImageIsPath,
                                                                   LauncherSocialMediaEntryFlag.QrImageIsDataBuffer,
                                                                   spriteFolder,
                                                                   qrImageDataMarshal,
                                                                   token);

            newsData.SocialMedia.Add(new LauncherGameNewsSocialMedia
            {
                IconId             = Guid.CreateVersion7().ToString(),
                IconImg            = iconUrl,
                IconImgHover       = iconHoverUrl ?? iconUrl,
                Title              = iconTitle,
                SocialMediaUrl     = iconClickUrl,
                QrImg              = qrImageUrl,
                QrTitle            = qrImageDescription,
                QrLinks            = GetSocialMediaInnerLinks(ref entry.ChildEntryHandle),
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
                string? description    = innerEntry.Description;
                string? descriptionUrl = innerEntry.ClickUrl;

                innerEntry = ref innerEntry.ChildEntryHandle;

                if (string.IsNullOrEmpty(description) ||
                    string.IsNullOrEmpty(descriptionUrl))
                {
                    continue;
                }

                links.Add(new LauncherGameNewsSocialMediaQrLinks
                {
                    Title = description,
                    Url   = descriptionUrl,
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
                                                            string?                      embeddedDataOrPath,
                                                            CancellationToken            token)
    {
        if (string.IsNullOrEmpty(embeddedDataOrPath))
        {
            return null;
        }

        if (flags.HasFlag(flagIfUrl))
        {
            return await CopyOverUrlData(outputDir, embeddedDataOrPath, token);
        }

        if (flags.HasFlag(flagIfBuffer))
        {
            return await CopyOverEmbeddedData(outputDir, embeddedDataOrPath, token);
        }

        return string.Empty;
    }

    private async Task<string?> CopyOverUrlData(string            outputDir,
                                                string?           dataUrl,
                                                CancellationToken token)
    {
        if (string.IsNullOrEmpty(dataUrl))
        {
            return null;
        }

        // Safeguard: Check if the data is actually base64. If so, redirect.
        if (IsDataActuallyBase64(dataUrl))
        {
            return await CopyOverEmbeddedData(outputDir, dataUrl, token);
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
        await _pluginNewsApi.DownloadAssetAsync(dataUrl,
                                                fileStream.SafeFileHandle.DangerousGetHandle(),
                                                null,
                                                in cancelToken).WaitFromHandle();

        SaveFileStamp(fileInfo, fileStream.Length);

        return filePath;
    }

    private static bool IsDataActuallyBase64(string data)
    {
        if (Base64Url.IsValid(data))
        {
            return true;
        }

        if (!Base64.IsValid(data))
        {
            return false;
        }

        return true;
    }

    private delegate bool WriteEmbeddedBase64DataToBuffer(ReadOnlySpan<char> chars, Span<byte> buffer, out int dataDecoded);

    private static async Task<string?> CopyOverEmbeddedData(string            outputDir,
                                                            string?           dataBase64,
                                                            CancellationToken token)
    {

        if (string.IsNullOrEmpty(dataBase64))
        {
            return null;
        }

        WriteEmbeddedBase64DataToBuffer writeToDelegate;
        if (Base64Url.IsValid(dataBase64, out int bufferLen))
        {
            writeToDelegate = WriteBufferFromBase64Url;
        }
        else if (Base64.IsValid(dataBase64, out bufferLen))
        {
            writeToDelegate = WriteBufferFromBase64Raw;
        }
        else
        {
            return null;
        }

        byte[] dataBuffer = ArrayPool<byte>.Shared.Rent(bufferLen);
        try
        {
            if (!writeToDelegate(dataBase64, dataBuffer, out int dataDecoded) || dataDecoded == 0)
            {
                return null;
            }

            ReadOnlySpan<byte> headerSpan    = dataBuffer.AsSpan(0, Math.Min(1 << 10, dataDecoded));
            string             fileExtension = DecideEmbeddedDataExtension(headerSpan);

            string fileBaseNameSignature = GetHashString(headerSpan);
            string filePath = Path.Combine(outputDir, $"embeddedPluginAssets-{fileBaseNameSignature}" + fileExtension);

            FileInfo fileInfo = new FileInfo(filePath)
                               .EnsureCreationOfDirectory()
                               .EnsureNoReadOnly();

            if (IsFileDownloadCompleted(fileInfo))
            {
                return filePath;
            }

            await using UnmanagedMemoryStream unmanagedStream = ToStream(dataBuffer.AsSpan(0, dataDecoded));
            await using FileStream            fileStream      = fileInfo.Create();

            await unmanagedStream.CopyToAsync(fileStream, token);
            SaveFileStamp(fileInfo, fileStream.Length);

            return filePath;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(dataBuffer);
        }

        unsafe UnmanagedMemoryStream ToStream(Span<byte> buffer)
        {
            ref byte dataRef = ref MemoryMarshal.AsRef<byte>(buffer);
            return new UnmanagedMemoryStream((byte*)Unsafe.AsPointer(ref dataRef), buffer.Length);
        }

        bool WriteBufferFromBase64Url(ReadOnlySpan<char> chars, Span<byte> buffer, out int dataDecoded)
        {
            if (Base64Url.TryDecodeFromChars(chars, buffer, out dataDecoded))
            {
                return true;
            }

            dataDecoded = 0;
            return false;
        }

        bool WriteBufferFromBase64Raw(ReadOnlySpan<char> chars, Span<byte> buffer, out int dataDecoded)
        {
            int    tempBufferToUtf8Len = Encoding.UTF8.GetByteCount(chars);
            byte[] tempBufferToUtf8    = ArrayPool<byte>.Shared.Rent(tempBufferToUtf8Len);
            try
            {
                if (!Encoding.UTF8.TryGetBytes(chars, tempBufferToUtf8, out int utf8StrWritten))
                {
                    dataDecoded = 0;
                    return false;
                }

                OperationStatus decodeStatus = Base64.DecodeFromUtf8(tempBufferToUtf8.AsSpan(0, utf8StrWritten), buffer, out _, out dataDecoded);
                if (decodeStatus == OperationStatus.Done)
                {
                    return true;
                }

                dataDecoded = 0;
            #if DEBUG
                throw new InvalidOperationException($"Cannot decode data string from Base64 as it returns with status: {decodeStatus}");
            #else
                return false;
            #endif
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(tempBufferToUtf8);
            }
        }
    }

    private static string GetHashString(ReadOnlySpan<byte> headerData)
    {
        byte[] data = Hash.GetHashFromBytes<XxHash64>(headerData);
        string hashString = HexTool.BytesToHexUnsafe(data)!;
        return hashString;
    }

    internal static string DecideEmbeddedDataExtension(ReadOnlySpan<byte> headerData)
    {
        ReadOnlySpan<byte> pngMagic   = [0x89, 0x50, 0x4E, 0x47];
        ReadOnlySpan<byte> jpgMagic   = "JFIF\0"u8;
        ReadOnlySpan<byte> svgMagic   = "<svg"u8;
        ReadOnlySpan<byte> webp1Magic = "RIFF"u8;
        ReadOnlySpan<byte> webp2Magic = "WEBP"u8;

        if (headerData.StartsWith(svgMagic))
        {
            return ".svg";
        }

        if (headerData.StartsWith(pngMagic))
        {
            return ".png";
        }

        if (headerData.IndexOf(jpgMagic) > 0)
        {
            return ".jpg";
        }

        if (headerData.StartsWith(webp1Magic) &&
            headerData.IndexOf(webp2Magic) > 7)
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
