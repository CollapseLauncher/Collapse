using BackgroundTest.CustomControl;
using BackgroundTest.CustomControl.LayeredBackgroundImage;
using System;
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
        try
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
                return new MediaCacheResult()
                {
                    CachedSource = sourceUrl,
                    DisposeStream = true
                };
            }

            string extension = Path.GetExtension(absolutePath);
            bool forceInternalCodec = extension is not (".webp" or ".webm" or ".avif" or ".jxl");
            string tempDir = Path.GetTempPath();
            byte[] nameHash = MD5.HashData(MemoryMarshal.AsBytes(absolutePath.AsSpan()));
            string tempName = Convert.ToHexStringLower(nameHash) +
                                        (forceInternalCodec ? ".png" : extension);

            string tempPath = Path.Combine(tempDir, tempName);
            if (File.Exists(tempPath))
            {
                return new MediaCacheResult
                {
                    ForceUseInternalDecoder = forceInternalCodec,
                    CachedSource = new Uri(tempPath),
                    DisposeStream = true
                };
            }

            FileStream? fileCreate = null;
            try
            {
                using HttpResponseMessage message =
                    await Client.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead);
                await using Stream networkStream = await message.Content.ReadAsStreamAsync();

                fileCreate = File.Open(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                await networkStream.CopyToAsync(fileCreate, 16 << 10);
                fileCreate.Position = 0;

                return new MediaCacheResult
                {
                    ForceUseInternalDecoder = forceInternalCodec,
                    CachedSource = fileCreate,
                    DisposeStream = true
                };
            }
            catch
            {
                if (fileCreate != null)
                {
                    await fileCreate.DisposeAsync();
                }
                return null!;
            }
        }
        catch (Exception)
        {
            return null!;
        }
    }
}
