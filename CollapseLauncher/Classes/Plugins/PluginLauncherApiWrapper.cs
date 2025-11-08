using CollapseLauncher.Extension;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using Hi3Helper;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.SentryHelper;
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
internal partial class PluginLauncherApiWrapper : ILauncherApi
{
    private readonly PluginPresetConfigWrapper _pluginPresetConfig;
    private readonly IPlugin                   _plugin;
    private readonly IGameManager              _pluginGameManager;
    private readonly ILauncherApiMedia         _pluginMediaApi;
    private readonly ILauncherApiNews          _pluginNewsApi;

    public PluginLauncherApiWrapper(IPlugin plugin, PluginPresetConfigWrapper presetConfig)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(presetConfig);

        _plugin             = plugin;
        _pluginPresetConfig = presetConfig;
        _pluginGameManager  = presetConfig.PluginGameManager;
        _pluginMediaApi     = presetConfig.PluginMediaApi;
        _pluginNewsApi      = presetConfig.PluginNewsApi;
    }

    public bool IsPlugin => true;

    public bool IsForceRedirectToSophon => false;
    public bool IsLoadingCompleted      { get; private set; }

    public string  GameBackgroundImg           { get; private set; } = string.Empty;
    public string? GameBackgroundImgLocal      { get; set; }

    // ReSharper disable UnusedAutoPropertyAccessor.Global
    public int   GameBackgroundSequenceCount { get; private set; } = 1;
    public float GameBackgroundSequenceFps   { get; private set; }
    // ReSharper enable UnusedAutoPropertyAccessor.Global

    public string GameName              => _pluginPresetConfig.GameName;
    public string GameRegion            => _pluginPresetConfig.ZoneName;
    public string GameNameTranslation   => InnerLauncherConfig.GetGameTitleRegionTranslationString(GameName, Locale.Lang._GameClientTitles) ?? GameName;
    public string GameRegionTranslation => InnerLauncherConfig.GetGameTitleRegionTranslationString(GameRegion, Locale.Lang._GameClientRegions) ?? GameRegion;

    public HypLauncherResourceWpfApi LauncherGameResourceWpf    { get; } = new();
    public HypGameInfoData           LauncherGameInfoField      { get; } = new();
    public HypLauncherGameInfoApi    LauncherGameResourceSophon { get; } = new();
    public HypLauncherBackgroundApi  LauncherGameBackground     { get; } = new();
    public HypLauncherContentApi     LauncherGameContent        { get; } = new();
    public RegionResourceProp        LauncherGameResource       { get; } = new();

    public HttpClient ApiGeneralHttpClient  => throw new NotImplementedException();
    public HttpClient ApiResourceHttpClient => throw new NotImplementedException();

    public async ValueTask<bool> LoadAsync(
        Func<CancellationToken, ValueTask>? beforeLoadRoutineAsync = null,
        Func<CancellationToken, ValueTask>? afterLoadRoutineAsync  = null,
        ActionOnTimeOutRetry?               onTimeoutRoutine       = null,
        Action<Exception>?                  errorLoadRoutine       = null,
        CancellationToken                   token                  = default)
    {
        _ = beforeLoadRoutineAsync?.Invoke(token) ?? ValueTask.CompletedTask;

        try
        {
            IsLoadingCompleted = false;

            Task[] initTasks = GetApiInitTasks(onTimeoutRoutine, token);
            await Task.WhenAll(initTasks);

            LauncherGameBackground.Data = new();

            await ConvertBackgroundImageEntries(LauncherGameBackground.Data, token);
            await ConvertSocialMediaEntries(LauncherGameContent, token);
            await ConvertNewsAndCarouselEntries(LauncherGameContent, token);

            await(afterLoadRoutineAsync?.Invoke(token) ?? ValueTask.CompletedTask);
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

    private static bool IsFileDownloadCompleted(FileInfo fileInfo)
    {
        if (!fileInfo.Exists)
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

        Span<Range> range = stackalloc Range[16];
        int lenSplit = firstMarkFile.Split(range, '#', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (lenSplit < 2 || lenSplit > range.Length)
        {
            return false; // Invalid mark file format
        }

        if (!int.TryParse(firstMarkFile[range[..lenSplit][^1]], out int fileLength))
        {
            return false; // Invalid file length in mark file
        }

        return fileInfo.Length == fileLength;
    }

    private Task[] GetApiInitTasks(ActionOnTimeOutRetry? actionOnTimeoutRetry, CancellationToken cancelToken)
    {
        // GameManager initialization
        ActionTimeoutTaskCallback retryGameManager = innerToken => _pluginGameManager.InitPluginComAsync(_plugin, innerToken);
        Task initGameManagerTask = retryGameManager.WaitForRetryAsync(LauncherApiBase.ExecutionTimeout,
                                                                      LauncherApiBase.ExecutionTimeoutStep,
                                                                      LauncherApiBase.ExecutionTimeoutAttempt,
                                                                      actionOnTimeoutRetry,
                                                                      cancelToken);

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

        return [initGameManagerTask, initMediaApiTask, initNewsApi];
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        // Add dispose for GameManager here
        _pluginMediaApi.Free();
        _pluginNewsApi.Free();
    }

    ~PluginLauncherApiWrapper() => Dispose(disposing: false);

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
