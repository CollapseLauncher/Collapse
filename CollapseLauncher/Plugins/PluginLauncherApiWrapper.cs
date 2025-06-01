using CollapseLauncher.Extension;
using CollapseLauncher.Helper.LauncherApiLoader;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Helper.LauncherApiLoader.Legacy;
using Hi3Helper;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.SentryHelper;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using LauncherApiBase = CollapseLauncher.Helper.LauncherApiLoader.LauncherApiBase;
using ILauncherApi = CollapseLauncher.Helper.LauncherApiLoader.ILauncherApi;

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
        _pluginGameManager  = presetConfig.PluginGameManager;
        _pluginMediaApi     = presetConfig.PluginMediaApi;
        _pluginNewsApi      = presetConfig.PluginNewsApi;
    }

    public bool IsPlugin                => true;
    public bool IsForceRedirectToSophon => false;
    public bool IsLoadingCompleted      { get; private set; }

    public string  GameBackgroundImg      { get; private set; } = string.Empty;
    public string? GameBackgroundImgLocal { get; set; }

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
