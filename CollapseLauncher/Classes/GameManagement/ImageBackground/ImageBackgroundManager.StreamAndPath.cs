using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper.EncTool;
using Hi3Helper.Shared.Region;
using System;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable StringLiteralTypo
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.GameManagement.ImageBackground;

public partial class ImageBackgroundManager
{
    #region Fields

    private static readonly string[] PlaceholderBackgroundImageRelPath = [
        @"Assets\Images\PageBackground\default_genshin.webp",
        @"Assets\Images\PageBackground\default_honkai.webp",
        @"Assets\Images\PageBackground\default_starrail.webp",
        @"Assets\Images\PageBackground\default_zzz.webp",
    ];

    private static readonly string[] PlaceholderBackgroundImagePngRelPath = [
        @"Assets\Images\GamePoster\poster_genshin.png",
        @"Assets\Images\GamePoster\poster_honkai.png",
        @"Assets\Images\GamePoster\poster_starrail.png",
        @"Assets\Images\GamePoster\poster_zzz.png",
    ];

    #endregion

    private static Task<FileStream> OpenStreamFromFileOrUrl(string? filePath, CancellationToken token)
    {
        if (!Uri.TryCreate(filePath, UriKind.Absolute, out Uri? uri))
        {
            throw new InvalidOperationException($"File path or URL is misformed! {filePath}");
        }

        return OpenStreamFromFileOrUrl(uri, token);
    }

    private static async Task<FileStream> OpenStreamFromFileOrUrl(Uri uri, CancellationToken token)
    {
        if (uri.IsFile)
        {
            string localPath = uri.LocalPath;
            return !File.Exists(localPath)
                ? throw new FileNotFoundException($"File: {localPath} does not exist!")
                : File.Open(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        // Find stamp file. Try read the metadata in it and just use locally cached files if matches.
        if (TryGetDownloadedFile(uri,
                                 out FileInfo downloadedFilePath,
                                 out FileInfo downloadStampFilePath,
                                 out UrlStatus status))
        {
            return downloadedFilePath.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        HttpClient sharedClient = FallbackCDNUtil.GetGlobalHttpClient(true);
        status = await sharedClient.GetCachedUrlStatus(uri, token);
        status.EnsureSuccessStatusCode();

        if (status.FileSize == 0)
        {
            throw new HttpRequestException($"File URL is reachable but it returns 0 bytes. {uri}");
        }

        downloadedFilePath = downloadedFilePath
                            .EnsureCreationOfDirectory()
                            .EnsureNoReadOnly();
        downloadStampFilePath = downloadStampFilePath
                               .EnsureCreationOfDirectory()
                               .EnsureNoReadOnly();

        FileStream downloadedFileStream =
            downloadedFilePath.Open(FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);

        using HttpResponseMessage responseMessage =
            await sharedClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        await using Stream responseStream = await responseMessage.Content.ReadAsStreamAsync(token);
        await responseStream.CopyToAsync(downloadedFileStream, token);

        // Write stamp for future cache metadata
        ReadOnlySpan<byte> stampData = AsSpan(in status);
        File.WriteAllBytes(downloadStampFilePath.FullName, stampData);

        downloadedFileStream.Position = 0;
        return downloadedFileStream;

        static unsafe ReadOnlySpan<byte> AsSpan<T>(in T data)
            where T : unmanaged => new(Unsafe.AsPointer(in data), sizeof(T));
    }

    private static async Task<Uri> GetLocalOrDownloadedFilePath(Uri uri, CancellationToken token)
    {
        await using FileStream stream = await OpenStreamFromFileOrUrl(uri, token);
        return new Uri(stream.Name);
    }

    private static bool TryGetCroppedImageFilePath(string filePath,
                                                   out string croppedFilePath)
    {
        string imageDirPath = LauncherConfig.AppGameImgFolder;
        string fileName = $"cropped_{Path.GetFileNameWithoutExtension(filePath)}.png";

        croppedFilePath = Path.Combine(imageDirPath, fileName);
        return File.Exists(croppedFilePath);
    }

    private static bool TryGetDecodedTemporaryFile(string filePath,
                                                   out string decodedFilePath)
    {
        string imageDirPath = LauncherConfig.AppGameImgFolder;
        string fileName = $"decoded_{Path.GetFileNameWithoutExtension(filePath)}.png";

        decodedFilePath = Path.Combine(imageDirPath, fileName);
        return File.Exists(decodedFilePath);
    }

    private static bool TryGetDownloadedFile(Uri           filePath,
                                             out FileInfo  downloadedFilePath,
                                             out FileInfo  downloadStampFilePath,
                                             out UrlStatus urlStatusStamp)
    {
        string imageDirPath   = LauncherConfig.AppGameImgFolder;
        string fileHashedName = $"{Path.GetFileName(filePath.AbsolutePath)}";
        string fileStampName  = $"{Path.GetFileName(filePath.AbsolutePath)}.downloadedStamp";

        downloadedFilePath    = new FileInfo(Path.Combine(imageDirPath, fileHashedName));
        downloadStampFilePath = new FileInfo(Path.Combine(imageDirPath, fileStampName));
        urlStatusStamp        = default;

        return downloadStampFilePath.Exists &&
               downloadedFilePath.Exists &&
               TryReadDownloadStampFile(downloadStampFilePath, out urlStatusStamp) &&
               downloadedFilePath.Length == urlStatusStamp.FileSize;
    }

    private static unsafe bool TryReadDownloadStampFile(
        FileInfo      downloadStampFilePath,
        out UrlStatus urlStatusStamp)
    {
        urlStatusStamp = default;

        if (!downloadStampFilePath.Exists)
        {
            return false;
        }

        scoped Span<byte> buffer = stackalloc byte[sizeof(UrlStatus)];
        using FileStream  stream = downloadStampFilePath.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        int               read   = stream.ReadAtLeast(buffer, buffer.Length, false);

        if (read != sizeof(UrlStatus))
        {
            return false;
        }

        urlStatusStamp = MemoryMarshal.Read<UrlStatus>(buffer);
        return true;
    }

    internal static string GetPlaceholderBackgroundImageFrom(PresetConfig? presetConfig, bool usePngVersion = false)
    {
        string[] source = usePngVersion
            ? PlaceholderBackgroundImagePngRelPath
            : PlaceholderBackgroundImageRelPath;

        string relPath = presetConfig?.GameName ??
                         LauncherConfig.GetAppConfigValue("GameCategory").Value switch
                         {
                             "Genshin Impact" => source[0],
                             "Honkai Impact 3rd" => source[1],
                             "Honkai: Star Rail" => source[2],
                             "Zenless Zone Zero" => source[3],
                             _ => GetRandomPlaceholderImage(usePngVersion)
                         };

        return Path.Combine(LauncherConfig.AppExecutableDir, relPath);
    }

    public static string GetRandomPlaceholderImage(bool usePngVersion = false)
    {
        string[] source = usePngVersion
            ? PlaceholderBackgroundImagePngRelPath
            : PlaceholderBackgroundImageRelPath;

        return source[Random.Shared.Next(0, source.Length - 1)];
    }
}
