using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper.Data;
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
// ReSharper disable CheckNamespace
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.GameManagement.ImageBackground;

public partial class ImageBackgroundManager
{
    #region Fields

    private static readonly string[] PlaceholderBackgroundImageRelPath = [
        @"Assets\Images\PageBackground\default_genshin.avif",
        @"Assets\Images\PageBackground\default_honkai.avif",
        @"Assets\Images\PageBackground\default_starrail.avif",
        @"Assets\Images\PageBackground\default_zzz.avif"
    ];

    private static readonly string[] PlaceholderBackgroundImagePngRelPath = [
        @"Assets\Images\GamePoster\poster_genshin.avif",
        @"Assets\Images\GamePoster\poster_honkai.avif",
        @"Assets\Images\GamePoster\poster_starrail.avif",
        @"Assets\Images\GamePoster\poster_zzz.avif"
    ];

    #endregion

    public static ValueTask<FileStream> OpenStreamFromFileOrUrl(string? filePath, CancellationToken token)
    {
        return !Uri.TryCreate(filePath, UriKind.RelativeOrAbsolute, out Uri? uri)
            ? throw new InvalidOperationException($"File path or URL is misformed! {filePath}")
            : OpenStreamFromFileOrUrl(uri, token);
    }

    public static async ValueTask<FileStream> OpenStreamFromFileOrUrl(Uri uri, CancellationToken token)
    {
        if (!uri.IsAbsoluteUri && !uri.IsWellFormedOriginalString())
        {
            string localPath = uri.OriginalString.GetFullyQualifiedPath();
            return !File.Exists(localPath)
                ? throw new FileNotFoundException($"File: {localPath} does not exist!")
                : File.Open(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        if (uri.IsFile)
        {
            string localPath = uri.LocalPath.GetFullyQualifiedPath();
            return !File.Exists(localPath)
                ? throw new FileNotFoundException($"File: {localPath} does not exist!")
                : File.Open(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        if (uri.Scheme.StartsWith("ms-appx"))
        {
            string uwpResPath = uri.LocalPath.TrimStart("\\/").GetFullyQualifiedPath();
            return !File.Exists(uwpResPath)
                ? throw new FileNotFoundException($"File: {uwpResPath} does not exist!")
                : File.Open(uwpResPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
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
        status = await sharedClient.GetCachedUrlStatus(uri, token).ConfigureAwait(false);
        status.EnsureSuccessStatusCode();

        downloadedFilePath = downloadedFilePath
                            .EnsureCreationOfDirectory()
                            .EnsureNoReadOnly();
        downloadStampFilePath = downloadStampFilePath
                               .EnsureCreationOfDirectory()
                               .EnsureNoReadOnly();

        FileStream downloadedFileStream =
            downloadedFilePath.Open(FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);

        using HttpResponseMessage responseMessage =
            await sharedClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token)
                              .ConfigureAwait(false);
        await using Stream responseStream =
            await responseMessage.Content
                                 .ReadAsStreamAsync(token)
                                 .ConfigureAwait(false);
        await responseStream.CopyToAsync(downloadedFileStream, token)
                            .ConfigureAwait(false);

        // Write stamp for future cache metadata
        ReadOnlySpan<byte> stampData = AsSpan(in status);
        File.WriteAllBytes(downloadStampFilePath.FullName, stampData);

        downloadedFileStream.Position = 0;
        return downloadedFileStream;

        static unsafe ReadOnlySpan<byte> AsSpan<T>(in T data)
            where T : unmanaged => new(Unsafe.AsPointer(in data), sizeof(T));
    }

    internal static async ValueTask<Uri> GetLocalOrDownloadedFilePath(Uri uri, CancellationToken token)
    {
        await using FileStream stream = await OpenStreamFromFileOrUrl(uri, token);
        return new Uri(stream.Name);
    }

    internal static async ValueTask<string> GetLocalOrDownloadedFilePath(string path, CancellationToken token)
    {
        Uri uri = await GetLocalOrDownloadedFilePath(new Uri(path), token);
        return uri.IsFile
            ? uri.LocalPath
            : uri.ToString();
    }

    internal static bool TryGetCroppedImageFilePath(string     filePath,
                                                   out string croppedFilePath)
    {
        string imageDirPath = LauncherConfig.AppGameImgFolder;
        string fileName = $"cropped_{Path.GetFileNameWithoutExtension(filePath)}.png";

        croppedFilePath = Path.Combine(imageDirPath, fileName);
        return IsFileExistAndNotEmpty(croppedFilePath);
    }

    internal static bool TryGetDecodedTemporaryFile(string     filePath,
                                                    out string decodedFilePath)
    {
        string imageDirPath = LauncherConfig.AppGameImgFolder;
        string fileName = $"decoded_{Path.GetFileNameWithoutExtension(filePath)}.png";

        decodedFilePath = Path.Combine(imageDirPath, fileName);
        return IsFileExistAndNotEmpty(decodedFilePath);
    }

    private static bool IsFileExistAndNotEmpty(string filePath)
    {
        FileInfo fileInfo = new(filePath);
        return fileInfo is { Exists: true, Length: > 0 };
    }

    internal static bool TryGetDownloadedFile(Uri           filePath,
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

    internal static string GetPlaceholderBackgroundImageFrom(PresetConfig? presetConfig)
    {
        string[] source = PlaceholderBackgroundImageRelPath;
        string relPath = presetConfig?.GameName switch
                         {
                             "Genshin Impact" => source[0],
                             "Honkai Impact 3rd" => source[1],
                             "Honkai: Star Rail" => source[2],
                             "Zenless Zone Zero" => source[3],
                             _ => GetRandomPlaceholderImage()
                         };

        return Path.Combine(LauncherConfig.AppExecutableDir, relPath);
    }

    public static string GetRandomPlaceholderImage()
    {
        string[] source = PlaceholderBackgroundImageRelPath;
        return source[new Random(Environment.TickCount).Next(0, source.Length - 1)];
    }
}
