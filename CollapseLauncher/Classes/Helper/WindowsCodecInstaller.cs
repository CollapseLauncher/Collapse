using CollapseLauncher.Extension;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.Interfaces;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.Http;
using Hi3Helper.Shared.Region;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable IdentifierTypo
#pragma warning disable IDE0290
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper;

internal sealed partial class WindowsCodecInstaller : ProgressBase, ICodecExtensionInstaller
{
    #region Unused Properties
    public override string GamePath
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }
    #endregion

    public string InstallFolder { get; set; }

    public WindowsCodecInstaller(string?                         installFolder,
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
        string[]   mirrorUrls      = GetMirrorList();
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

        Progress.ProgressAllSizeCurrent = totalToExtractSize;
        Progress.ProgressAllPercentage  = 100d;

        Status.ActivityAll = "Installing extension (Do not close this dialog)...";
        UpdateAll();

        await RunInstallerScript(extractFolder);

        new DirectoryInfo(extractFolder).TryDeleteDirectory(true);
        packageFileInfo.TryDeleteFile();
    }

    private static async Task RunInstallerScript(string packageFolder)
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), @"Misc\InstallMediaExtensionCodec.cmd");

        using Process? process = Process.Start(new ProcessStartInfo
        {
            FileName         = path,
            Arguments        = $"\"{packageFolder}\"",
            WorkingDirectory = Path.GetDirectoryName(path),
            UseShellExecute  = true
        });

        await process!.WaitForExitAsync();
        int errorLevel = process.ExitCode;

        if (errorLevel != 0)
        {
            throw new InvalidOperationException($"Installer script failed with exit code {errorLevel}.");
        }
    }

    private static string[] GetMirrorList()
    {
        // ReSharper disable once StringLiteralTypo
        const string mirrorPath = "metadata/media_codecs/WindowsMinimalMediaExtension.zip";

        return LauncherConfig.CDNList
                             .Select(x => x.URLPrefix.CombineURLFromString(mirrorPath))
                             .ToArray();
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
