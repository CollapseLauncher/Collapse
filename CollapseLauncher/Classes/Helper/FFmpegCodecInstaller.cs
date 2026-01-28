using CollapseLauncher.Extension;
using CollapseLauncher.GameManagement.ImageBackground;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.Interfaces;
using Hi3Helper.EncTool;
using Hi3Helper.Http;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable IdentifierTypo
#pragma warning disable IDE0290
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper;

[JsonSerializable(typeof(string[]))]
internal sealed partial class FFmpegMirrorListJsonContext : JsonSerializerContext;

internal interface ICodecExtensionInstaller
{
    Task Start();
}

internal sealed partial class FFmpegCodecInstaller : ProgressBase, ICodecExtensionInstaller
{
    #region Unused Properties
    public override string GamePath
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }
    #endregion

    public string InstallFolder { get; set; }

    public FFmpegCodecInstaller(string?                         installFolder,
                                CancellationTokenSourceWrapper? tokenSource = null)
        : base(null!, null!, null!, null!, null!)
    {
        InstallFolder = installFolder ?? string.Empty;
        Token         = tokenSource ?? new CancellationTokenSourceWrapper();
    }

    public async Task Start()
    {
        Status.ActivityAll = "Retrieving mirror list...";

        CancellationToken token = Token!.Token;

        HttpClient client          = FallbackCDNUtil.GetGlobalHttpClient(true);
        string[]   mirrorUrls      = await GetMirrorList(token);
        UrlStatus  availableMirror = await FindAvailableMirrors(client, mirrorUrls);

        ProgressAllSizeCurrent          = 0;
        ProgressAllSizeTotal            = availableMirror.FileSize;
        Progress.ProgressAllSizeCurrent = ProgressAllSizeCurrent;
        Progress.ProgressAllSizeTotal   = ProgressAllSizeTotal;

        Status.ActivityAll = "Downloading package...";
        UpdateAll();

        string mirrorUrl     = availableMirror.Url;
        string packageFile   = Path.Combine(InstallFolder, Path.GetFileName(mirrorUrl));
        string extractFolder = Path.Combine(InstallFolder, Path.GetFileNameWithoutExtension(mirrorUrl));

        FileInfo packageFileInfo = new(packageFile);

        DownloadClient downloadClient = DownloadClient.CreateInstance(client);
        await RunDownloadTask(availableMirror.FileSize,
                              packageFileInfo,
                              mirrorUrl,
                              downloadClient,
                              HttpClientDownloadProgressAdapter,
                              token);

        packageFileInfo.Refresh();
        bool isZip = Path.GetExtension(packageFile).Equals(".zip", StringComparison.OrdinalIgnoreCase);

        long totalToExtractSize = 0;
        await using (FileStream fileStream = packageFileInfo.OpenRead())
        {
            totalToExtractSize += !isZip
                ? GetArchiveUncompressedSizeNative7Zip(fileStream)
                : GetArchiveUncompressedSizeManaged(fileStream);
        }

        ProgressAllSizeCurrent          = 0;
        ProgressAllSizeTotal            = totalToExtractSize;
        Progress.ProgressAllSizeCurrent = ProgressAllSizeCurrent;
        Progress.ProgressAllSizeTotal   = ProgressAllSizeTotal;

        Status.ActivityAll = "Extracting package...";
        UpdateAll();

        InstallPackageExtractorDelegate extractDelegate =
            !isZip
                ? ExtractUsingNative7Zip
                : ExtractUsingManagedZip;

        await extractDelegate(() => File.Open(packageFile,
                                              FileMode.Open,
                                              FileAccess.Read,
                                              FileShare.Read),
                              extractFolder,
                              token);

        string innerFfmpegDir = ImageBackgroundManager.FindFfmpegInstallFolder(extractFolder) ??
                                throw new FileNotFoundException("Library files are not found!");

        foreach (string dllFile in Directory.EnumerateFiles(innerFfmpegDir, "*.dll", SearchOption.TopDirectoryOnly))
        {
            string dllFileName    = Path.GetFileName(dllFile);
            string targetFilePath = Path.Combine(InstallFolder, dllFileName);

            File.Move(dllFile, targetFilePath, true);
        }

        new DirectoryInfo(extractFolder).TryDeleteDirectory(true);
        packageFileInfo.TryDeleteFile();

        Progress.ProgressAllSizeCurrent = totalToExtractSize;
        Progress.ProgressAllPercentage  = 100d;
        UpdateAll();
    }

    private static async Task<string[]> GetMirrorList(CancellationToken token)
    {
        // ReSharper disable once StringLiteralTypo
        const string mirrorPath = "metadata/media_codecs/ffmpeg_mirrorlist.json";

        await using Stream mirrorListStream = await FallbackCDNUtil.TryGetCDNFallbackStream(mirrorPath, token: token);
        return await mirrorListStream.DeserializeAsync(FFmpegMirrorListJsonContext.Default.StringArray,
                                                       token: token) ??
               throw new NullReferenceException("Mirror list is empty!");
    }

    private async Task<UrlStatus> FindAvailableMirrors(HttpClient client, params string[] urls)
    {
        foreach (string url in urls)
        {
            Status.ActivityAll = $"Checking Mirror URL: {url}...";

            UrlStatus status = await client.GetCachedUrlStatus(url, Token!.Token);
            if (status.IsSuccessStatusCode)
            {
                return status;
            }
        }

        throw new HttpRequestException("No available mirrors are reachable!");
    }
}
