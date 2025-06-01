using CollapseLauncher.Extension;
using CollapseLauncher.Helper.LauncherApiLoader;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Helper.LauncherApiLoader.Legacy;
using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ILauncherApi = CollapseLauncher.Helper.LauncherApiLoader.ILauncherApi;
using LauncherApiBase = CollapseLauncher.Helper.LauncherApiLoader.LauncherApiBase;

namespace CollapseLauncher.Plugins;

#nullable enable
internal class PluginLauncherApiWrapper : ILauncherApi
{
    private readonly PluginPresetConfigWrapper _pluginPresetConfig;
    private readonly IPlugin                   _plugin;
    private readonly IGameManager              _pluginGameManager;
    private readonly ILauncherApiMedia         _pluginMediaApi;
    private readonly ILauncherApiNews          _pluginNewsApi;

    public PluginLauncherApiWrapper(IPlugin plugin, PluginPresetConfigWrapper presetConfig)
    {
        ArgumentNullException.ThrowIfNull(plugin,       nameof(plugin));
        ArgumentNullException.ThrowIfNull(presetConfig, nameof(presetConfig));

        _plugin             = plugin;
        _pluginPresetConfig = presetConfig;
        // _pluginGameManager  = presetConfig.PluginGameManager;
        _pluginMediaApi     = presetConfig.PluginMediaApi;
        _pluginNewsApi      = presetConfig.PluginNewsApi;
    }

    public bool IsPlugin                => true;
    public bool IsForceRedirectToSophon => false;
    public bool IsLoadingCompleted      { get; private set; }

    public string  GameBackgroundImg           { get; private set; } = string.Empty;
    public string? GameBackgroundImgLocal      { get; set; }
    public int     GameBackgroundSequenceCount { get; private set; } = 1;
    public float   GameBackgroundSequenceFps   { get; private set; } = 0;

    public string GameName              => _pluginPresetConfig.GameName;
    public string GameRegion            => _pluginPresetConfig.ZoneName;
    public string GameNameTranslation   => InnerLauncherConfig.GetGameTitleRegionTranslationString(GameName, Locale.Lang._GameClientTitles) ?? GameName;
    public string GameRegionTranslation => InnerLauncherConfig.GetGameTitleRegionTranslationString(GameRegion, Locale.Lang._GameClientRegions) ?? GameRegion;

    public HoYoPlayGameInfoField? LauncherGameInfoField { get; } = new();
    public LauncherGameNews       LauncherGameNews      { get; } = new();
    public RegionResourceProp     LauncherGameResource  => throw new NotImplementedException();

    public HttpClient ApiGeneralHttpClient  => throw new NotImplementedException();
    public HttpClient ApiResourceHttpClient => throw new NotImplementedException();

    public async Task<bool> LoadAsync(OnLoadTaskAction?         beforeLoadRoutine = null,
                                      OnLoadTaskAction?         afterLoadRoutine  = null,
                                      ActionOnTimeOutRetry?     onTimeoutRoutine  = null,
                                      ErrorLoadRoutineDelegate? errorLoadRoutine  = null,
                                      CancellationToken         token             = default)
    {
        _ = beforeLoadRoutine?.Invoke(token) ?? Task.CompletedTask;

        try
        {
            IsLoadingCompleted = false;

            Task[] initTasks = GetApiInitTasks(onTimeoutRoutine, token);
            await Task.WhenAll(initTasks);

            await ConvertNewsApiData(token);

            await (afterLoadRoutine?.Invoke(token) ?? Task.CompletedTask);
            return true;
        }
        catch (Exception ex)
        {
            await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
            errorLoadRoutine?.Invoke(ex);
            return false;
        }
        finally
        {
            IsLoadingCompleted = true;
        }
    }

    private async Task ConvertNewsApiData(CancellationToken token)
    {
        LauncherGameNews.Content = new LauncherGameNewsData();

        LauncherBackgroundFlag backgroundFlags = _pluginMediaApi.GetBackgroundFlag();
        await ConvertNewsBackgroundImageDataEntries(LauncherGameNews.Content, token);
    }

    private async Task ConvertNewsBackgroundImageDataEntries(LauncherGameNewsData newsData, CancellationToken token)
    {
        Guid cancelToken = Guid.CreateVersion7();
        token.Register(() => _plugin.CancelAsync(in cancelToken));

        // TODO: Handle image sequence format with logo overlay (example: Wuthering Waves)
        string backgroundFolder     = Path.Combine(LauncherConfig.AppGameImgFolder, "bg");
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
                await using FileStream destCopyOver = fileInfo.Create();

                await fromFileStream.CopyToAsync(destCopyOver, 64 << 10, token).ConfigureAwait(false);

                firstImageSpriteUrl  ??= url;
                firstImageSpritePath ??= spriteLocalPath;
                continue; // Skip further processing for local files
            }

            // Check if the background download is completed
            if (IsBackgroundImageDownloadCompleted(fileInfo))
            {
                firstImageSpriteUrl  ??= url;
                firstImageSpritePath ??= spriteLocalPath;
                continue;
            }

            // Start the download
            Guid cancelTokenPass = _plugin.RegisterCancelToken(token);
            await using FileStream destDownload = fileInfo.Create();
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

    private static void SaveFileStamp(FileInfo fileInfo, long fileLength)
    {
        string  fileNamePrefix = Path.GetFileName(fileInfo.Name);
        string? fileDir        = Path.GetDirectoryName(fileInfo.FullName);

        string stampFileName = Path.Combine(fileDir ?? string.Empty, fileNamePrefix + $"#{fileLength}");
        File.WriteAllText(stampFileName, string.Empty); // Create an empty file to mark completion
    }

    private static bool IsBackgroundImageDownloadCompleted(FileInfo fileInfo)
    {
        if (fileInfo.Exists)
        {
            return false;
        }

        string fileNamePrefix = Path.GetFileName(fileInfo.Name);
        string? fileDir = Path.GetDirectoryName(fileInfo.FullName);

        string? firstMarkFile = Directory.EnumerateFiles(fileDir ?? string.Empty, fileNamePrefix + "#*", SearchOption.TopDirectoryOnly)
                                         .FirstOrDefault();

        if (firstMarkFile == null)
        {
            return false;
        }

        ReadOnlySpan<char> markFileName = Path.GetFileNameWithoutExtension(firstMarkFile.AsSpan());
        Span<Range> range = stackalloc Range[2];
        int lenSplit = markFileName.Split(range, '#', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (lenSplit != 2)
        {
            return false; // Invalid mark file format
        }

        if (!int.TryParse(markFileName[range[1]], out int fileLength))
        {
            return false; // Invalid file length in mark file
        }

        return fileInfo.Length == fileLength;
    }

    private Task[] GetApiInitTasks(ActionOnTimeOutRetry? actionOnTimeoutRetry, CancellationToken cancelToken)
    {
        // GameManager initialization
        /*
        ActionTimeoutTaskCallback retryGameManager = innerToken => _pluginGameManager.InitPluginComAsync(_plugin, innerToken);
        Task initGameManagerTask = retryGameManager.WaitForRetryAsync(LauncherApiBase.ExecutionTimeout,
                                                                      LauncherApiBase.ExecutionTimeoutStep,
                                                                      LauncherApiBase.ExecutionTimeoutAttempt,
                                                                      actionOnTimeoutRetry,
                                                                      cancelToken);
        */

        // Media API initialization
        ActionTimeoutTaskCallback retryMediaApi = innerToken => _pluginMediaApi.InitPluginComAsync(_plugin, innerToken);
        Task initMediaApiTask = retryMediaApi.WaitForRetryAsync(LauncherApiBase.ExecutionTimeout,
                                                                LauncherApiBase.ExecutionTimeoutStep,
                                                                LauncherApiBase.ExecutionTimeoutAttempt,
                                                                actionOnTimeoutRetry,
                                                                cancelToken);

        // News API initialization
        ActionTimeoutTaskCallback retryCallback = innerToken => _pluginNewsApi.InitPluginComAsync(_plugin, innerToken);
        Task initNewsApi = retryCallback.WaitForRetryAsync(LauncherApiBase.ExecutionTimeout,
                                                           LauncherApiBase.ExecutionTimeoutStep,
                                                           LauncherApiBase.ExecutionTimeoutAttempt,
                                                           actionOnTimeoutRetry,
                                                           cancelToken);

        return [Task.CompletedTask, initMediaApiTask, initNewsApi];
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        // Add dispose for GameManager here
        _pluginMediaApi.Dispose();
        _pluginNewsApi.Dispose();
    }

    ~PluginLauncherApiWrapper() => Dispose(disposing: false);

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
