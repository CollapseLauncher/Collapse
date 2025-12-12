using BackgroundTest.CustomControl;
using BackgroundTest.CustomControl.LayeredBackgroundImage;
using System;
using System.Buffers;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace BackgroundTest;

internal class TestMediaCacheHandler : IMediaCacheHandler
{
    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        MaxConnectionsPerServer = Environment.ProcessorCount * 2
    }, false);

    public async Task<MediaCacheResult> LoadCachedSource(object? sourceObject)
    {
        Uri? sourceUrl = sourceObject as Uri;

        if (sourceUrl == null &&
            sourceObject is string sourceAsString)
        {
            sourceUrl = sourceAsString.GetStringAsUri();
        }

        string absolutePath = sourceUrl?.AbsoluteUri ?? "";

        if (sourceUrl?.IsFile ?? false)
        {
            string normalizedPath = absolutePath[8..].Replace("/", "\\");
            if (File.Exists(normalizedPath))
            {
                return new MediaCacheResult
                {
                    ForceUseInternalDecoder = false,
                    CachedSource = File.Open(normalizedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite),
                    DisposeStream = true
                };
            }

            return null!;
        }

        string extension          = Path.GetExtension(absolutePath);
        bool   forceInternalCodec = extension is not (".webp" or ".webm" or ".avif" or ".jxl");
        string tempDir            = Path.GetTempPath();
        byte[] nameHash           = MD5.HashData(MemoryMarshal.AsBytes(absolutePath.AsSpan()));
        string tempName           = Convert.ToHexStringLower(nameHash) +
                                    (forceInternalCodec ? ".png" : extension);

        string tempPath = Path.Combine(tempDir, tempName);
        if (File.Exists(tempPath))
        {
            return new MediaCacheResult
            {
                ForceUseInternalDecoder = forceInternalCodec,
                CachedSource            = File.Open(tempPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite),
                DisposeStream           = true
            };
        }

        byte[] buffer = ArrayPool<byte>.Shared.Rent(16 << 10);
        try
        {
            using HttpResponseMessage message =
                await Client.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead);
            await using Stream networkStream = await message.Content.ReadAsStreamAsync();

            FileStream fileCreate = File.Open(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            int        read;
            while ((read = await networkStream.ReadAsync(buffer)) > 0)
            {
                fileCreate.Write(buffer.AsSpan(0, read));
            }

            fileCreate.Flush();
            fileCreate.Position = 0;

            return new MediaCacheResult
            {
                ForceUseInternalDecoder = forceInternalCodec,
                CachedSource            = fileCreate,
                DisposeStream           = true
            };
        }
        catch
        {
            return null!;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
