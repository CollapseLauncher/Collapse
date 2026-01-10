using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper.EncTool;
using Hi3Helper.Shared.Region;
using System;
using System.IO;
using System.Net.Http;
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

        HttpClient sharedClient = FallbackCDNUtil.GetGlobalHttpClient(true);
        UrlStatus status = await sharedClient.GetCachedUrlStatus(uri, token);
        status.EnsureSuccessStatusCode();

        if (status.FileSize == 0)
        {
            throw new HttpRequestException($"File URL is reachable but it returns 0 bytes. {uri}");
        }

        if (TryGetDownloadedFile(uri, out FileInfo downloadedFilePath) &&
            downloadedFilePath.Length == status.FileSize)
        {
            return downloadedFilePath.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        downloadedFilePath = downloadedFilePath
                            .EnsureCreationOfDirectory()
                            .EnsureNoReadOnly()
                            .StripAlternateDataStream();

        FileStream downloadedFileStream =
            downloadedFilePath.Open(FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);

        using HttpResponseMessage responseMessage =
            await sharedClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        await using Stream responseStream = await responseMessage.Content.ReadAsStreamAsync(token);
        await responseStream.CopyToAsync(downloadedFileStream, token);

        downloadedFileStream.Position = 0;
        return downloadedFileStream;
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

    private static bool TryGetDownloadedFile(Uri          filePath,
                                             out FileInfo downloadedFilePath)
    {
        string imageDirPath   = LauncherConfig.AppGameImgFolder;
        string fileHashedName = $"{Path.GetFileName(filePath.AbsolutePath)}";

        downloadedFilePath = new FileInfo(Path.Combine(imageDirPath, fileHashedName));
        return downloadedFilePath.Exists;
    }

    private static string GetPlaceholderBackgroundImageFrom(PresetConfig presetConfig)
    {
        string relPath = presetConfig.GameName switch
        {
            "Genshin Impact" => PlaceholderBackgroundImageRelPath[0],
            "Honkai Impact 3rd" => PlaceholderBackgroundImageRelPath[1],
            "Honkai: Star Rail" => PlaceholderBackgroundImageRelPath[2],
            "Zenless Zone Zero" => PlaceholderBackgroundImageRelPath[3],
            _ => GetRandomPlaceholderImage()
        };

        return Path.Combine(LauncherConfig.AppExecutableDir, relPath);
    }

    public static string GetRandomPlaceholderImage() =>
        PlaceholderBackgroundImageRelPath[Random.Shared.Next(0, PlaceholderBackgroundImageRelPath.Length - 1)];
}
