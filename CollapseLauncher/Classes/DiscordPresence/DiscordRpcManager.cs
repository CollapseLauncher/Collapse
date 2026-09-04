using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.Update;
using CollapseLauncher.Plugins;
using DiscordRPC;
using DiscordRPC.Entities;
using DiscordRPC.Message;
using Hi3Helper;
using Hi3Helper.LocaleSourceGen;
using Hi3Helper.Shared.Region;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;

#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.DiscordPresence;

public partial class DiscordRpcManager : IDisposable
{
    public bool IsDisposed;

    public          DiscordRpcClient?      Client;
    public readonly Thread                 PresenceSetThread;
    public readonly Channel<RichPresence?> PresenceSetChannel;

    private ulong               _currentPresenceId = LauncherConfig.AppDiscordApplicationID;
    private DiscordActivityType _currentActivityStatus;

    private readonly EventWaitHandle _isReadyWaitHandle = new(false, EventResetMode.ManualReset);

    public bool IsGameStatusEnabled
    {
        get => LauncherConfig.GetAppConfigValue("EnableDiscordGameStatus");
        set
        {
            LauncherConfig.SetAndSaveConfigValue("EnableDiscordGameStatus", value);
            SetActivity(_currentActivityStatus); // Refresh activity status
        }
    }

    public bool IsShowOnIdle
    {
        get => LauncherConfig.GetAppConfigValue("EnableDiscordIdleStatus");
        set
        {
            LauncherConfig.SetAndSaveConfigValue("EnableDiscordIdleStatus", value);
            SetActivity(_currentActivityStatus); // Refresh activity status
        }
    }

    public bool IsEnabled
    {
        get => LauncherConfig.GetAppConfigValue("EnableDiscordRPC");
        set
        {
            bool isPreviouslyEnabled = LauncherConfig.GetAppConfigValue("EnableDiscordRPC");
            LauncherConfig.SetAndSaveConfigValue("EnableDiscordRPC", value);

            if (value) Start();
            else Stop();

            // Refresh activity status if it was previously disabled.
            if (!isPreviouslyEnabled &&
                value != isPreviouslyEnabled)
            {
                SetActivity(_currentActivityStatus);
            }
        }
    }

    private readonly ConcurrentDictionary<int, DateTime> _cachedStartTimes = [];
    private readonly ILogger                             _sharedLogger;
    private          PresetConfig?                       _currentPresetConfig;

    public DiscordRpcManager()
    {
        _sharedLogger = ILoggerHelper.GetILogger("DiscordRPC");
        PresenceSetChannel = Channel.CreateUnbounded<RichPresence?>(new UnboundedChannelOptions
        {
            SingleWriter = true
        });

        PresenceSetThread = new Thread(PresenceSetterInvoke)
        {
            IsBackground = true
        };
        PresenceSetThread.Start();

        // Initialize from start if enabled and IsShowOnIdle == true
        if (IsEnabled && IsShowOnIdle)
        {
            Start();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref IsDisposed, true))
            return;

        // Stop presence RPC
        Stop();
        
        // Complete the writer and dispose the wait handle.
        PresenceSetChannel.Writer.TryComplete();
        _isReadyWaitHandle.Dispose();
    }

    public async void PresenceSetterInvoke(object? ctx)
    {
        try
        {
            ChannelReader<RichPresence?> reader = PresenceSetChannel.Reader;

            while (!IsDisposed && await reader.WaitToReadAsync())
            {
                while (reader.TryRead(out RichPresence? presence))
                {
                    if (IsDisposed)
                    {
                        return;
                    }

                    // Blocks and wait until the ready signal is set.
                    _isReadyWaitHandle.WaitOne();
                    Client?.SetPresence(presence);
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // ignore

            // From @neon-nyan:
            //   The reason why this is ignored, is because the disposed exception will
            //   come from the _isReadyWaitHandle. The EventWaitHandle doesn't have such
            //   property or field to check whether the handle is already disposed or
            //   not anyway, so we just yeet the exception.
        }
        catch (Exception e)
        {
            _sharedLogger.LogError(e, "An error has occurred while setting presence on the RPC client.");
        }
    }

    public void Stop()
    {
        DiscordRpcClient? oldClient = Interlocked.Exchange(ref Client, null);
        if (oldClient == null)
        {
            return;
        }

        // Reset the channel by flushing all pending presence
        while (PresenceSetChannel.Reader.TryRead(out _)) { }

        if (!Volatile.Read(ref IsDisposed))
        {
            // Reset wait handle and block presence update until the
            // client is ready or started.
            _isReadyWaitHandle.Reset();
        }

        oldClient.OnReady          -= EventClientOnReady;
        oldClient.OnPresenceUpdate -= EventClientOnPresenceUpdate;
        oldClient.Dispose();
    }

    public void Start()
    {
        // If not enabled, choose to not initialize the client.
        if (!IsEnabled)
        {
            return;
        }

        ulong presenceId = _currentPresenceId == 0
            ? LauncherConfig.AppDiscordApplicationID
            : _currentPresenceId;

        // Initialize new client and replace the field atomically.
        Interlocked.Exchange(ref Client, new DiscordRpcClient($"{presenceId}", _sharedLogger));
        Client.OnReady          += EventClientOnReady;
        Client.OnPresenceUpdate += EventClientOnPresenceUpdate;
        if (!Client.Initialize())
        {
            _sharedLogger.LogInformation("Failed while trying to initialize the client!");
        }
    }

    private void EventClientOnReady(object sender, ReadyMessage? msg)
    {
        _sharedLogger.LogInformation("Connected to Discord with user {username}", msg?.User?.Username);
        _isReadyWaitHandle.Set(); // Unblock the presence update thread.
    }

    private void EventClientOnPresenceUpdate(object sender, PresenceMessage? msg)
    {
        if (msg?.Presence == null)
        {
            _sharedLogger.LogInformation("Activity cleared!");
        }
        else
        {
            _sharedLogger.LogInformation("Activity updated! => {msg}", msg.Presence.State == null
                                             ? msg.Presence.Details
                                             : $"{msg.Presence.Details} - {msg.Presence.State}");
        }
    }

    public void SetPresence(PresetConfig? config)
    {
        Interlocked.Exchange(ref _currentPresetConfig, config);
        if (config == null)
        {
            Interlocked.Exchange(ref _currentPresenceId, 0);
            return;
        }

        ulong presenceId = GetDiscordPresenceId(config);
        Interlocked.Exchange(ref _currentPresenceId, presenceId);

        // We intentionally stop and start the client to refresh / re-create the client
        // with the new presence ID.
        Stop();
        Start();
    }

    public void SetActivity(DiscordActivityType type = DiscordActivityType.None, DateTime? specifiedStartTime = null)
    {
        Interlocked.Exchange(ref _currentActivityStatus, type);

        // Prevent from exhausting the Presence channel if not enabled.
        if (!IsEnabled) return;

        // If IsShowOnIdle == false and the activity type is None or Idle,
        // tries to stop the RPC for a while to disconnect it from Discord and
        // remove the RPC display.
        if (!IsShowOnIdle && type is DiscordActivityType.None or DiscordActivityType.Idle)
        {
            Stop();
            return;
        }

        // Make sure to re-enable the client if it was previously disposed due to
        // IsShowOnIdle == false and type is None or Idle.
        if (Volatile.Read(ref Client) == null &&
            !IsDisposed &&
            IsEnabled)
        {
            Start();
        }

        LangParamsMisc? langMisc = Locale.Current.Lang?._Misc;
        RichPresence presence = type switch
        {
            DiscordActivityType.Play => PresenceBuilder.BuildTimedState(IsGameStatusEnabled ? langMisc?.DiscordRP_InGame : langMisc?.DiscordRP_Play, this, specifiedStartTime),
            DiscordActivityType.Update => PresenceBuilder.BuildTimedState(langMisc?.DiscordRP_Update, this, specifiedStartTime),
            DiscordActivityType.Idle => PresenceBuilder.BuildIdleState(this),
            DiscordActivityType.Repair => PresenceBuilder.BuildGenericState(langMisc?.DiscordRP_Repair, this),
            DiscordActivityType.Cache => PresenceBuilder.BuildGenericState(langMisc?.DiscordRP_Cache, this),
            DiscordActivityType.GameSettings => PresenceBuilder.BuildGenericState(langMisc?.DiscordRP_GameSettings, this),
            DiscordActivityType.AppSettings => PresenceBuilder.BuildGenericState(langMisc?.DiscordRP_AppSettings, this),
            _ => new RichPresence
            {
                Details = Locale.Current.Lang?._Misc?.DiscordRP_Default,
                Assets = new Assets
                {
                    LargeImageKey  = PresenceBuilder.DefaultLauncherLogo,
                    LargeImageText = PresenceBuilder.DefaultLauncherLogoTooltip
                },
                Timestamps = null!
            }
        };

        PresenceSetChannel.Writer.TryWrite(presence);
    }

    private static ulong GetDiscordPresenceId(PresetConfig presetConfig)
    {
        return presetConfig.GameName switch
        {
            "Honkai: Star Rail" => LauncherConfig.AppDiscordApplicationIDHsr,
            "Honkai Impact 3rd" => LauncherConfig.AppDiscordApplicationIDHi3,
            "Genshin Impact"    => LauncherConfig.AppDiscordApplicationIDGi,
            "Zenless Zone Zero" => LauncherConfig.AppDiscordApplicationIDZzz,
            _                   => TryGetPresenceFromPlugin(presetConfig)
        };

        static ulong TryGetPresenceFromPlugin(PresetConfig presetConfig)
        {
            if (presetConfig is not PluginPresetConfigWrapper { DiscordPresenceContext: { IsFeatureAvailable: true } discordContext } ||
                discordContext.PresenceId == 0)
            {
                return LauncherConfig.AppDiscordApplicationID; // Default
            }

            return discordContext.PresenceId;
        }
    }

    private static class PresenceBuilder
    {
        private const string CollapseLogoExt = "https://collapselauncher.com/img/logo@2x.webp";

        public const string DefaultLauncherLogo = "launcher-logo-new";
        public static readonly string DefaultLauncherLogoTooltip = $"Collapse Launcher v{LauncherUpdateHelper.LauncherCurrentVersionString} "
                                                                 + $"{(LauncherConfig.IsPreview ? "Preview" : "Stable")}";

        public static RichPresence BuildTimedState(string? activityName, DiscordRpcManager manager, DateTime? specifiedStartTime = null)
        {
            bool          isGameStatusEnabled = manager.IsGameStatusEnabled;
            PresetConfig? presetConfig        = manager._currentPresetConfig;

            int presetConfigHashId = presetConfig?.HashID ?? 0;

            // Try to get the existing start offset or create a new one if not exist.
            DateTime startOffset = manager._cachedStartTimes.GetOrAdd(presetConfigHashId, specifiedStartTime ?? DateTime.UtcNow);
            TryGetGameIconsAndTranslatedNames(presetConfig,
                                              out string? largeIconUrl,
                                              out string? largeIconTooltip,
                                              out string? smallIconUrl,
                                              out string? smallIconTooltip,
                                              out string? translatedGameName,
                                              out string? translatedGameRegion);

            return new RichPresence
            {
                Details = $"{activityName} {(!isGameStatusEnabled ? translatedGameName : null)}",
                State   = $"{Locale.Current.Lang?._Misc?.DiscordRP_Region} {translatedGameRegion}",
                Assets = new Assets
                {
                    LargeImageKey  = largeIconUrl,
                    LargeImageText = largeIconTooltip,
                    SmallImageKey  = smallIconUrl,
                    SmallImageText = smallIconTooltip
                },
                Timestamps = new Timestamps
                {
                    Start = startOffset
                }
            };
        }

        public static RichPresence BuildGenericState(string? activityName, DiscordRpcManager manager)
        {
            PresetConfig? presetConfig = manager._currentPresetConfig;
            TryGetGameIconsAndTranslatedNames(presetConfig,
                                              out string? largeIconUrl,
                                              out string? largeIconTooltip,
                                              out string? smallIconUrl,
                                              out string? smallIconTooltip,
                                              out _,
                                              out string? translatedGameRegion);

            return new RichPresence
            {
                Details = activityName,
                State   = $"{Locale.Current.Lang?._Misc?.DiscordRP_Region} {translatedGameRegion}",
                Assets = new Assets
                {
                    LargeImageKey  = largeIconUrl,
                    LargeImageText = largeIconTooltip,
                    SmallImageKey  = smallIconUrl,
                    SmallImageText = smallIconTooltip
                },
                Timestamps = null!
            };
        }

        public static RichPresence BuildIdleState(DiscordRpcManager manager)
        {
            // Try to remove existing cached start time (Reset)
            PresetConfig? presetConfig       = manager._currentPresetConfig;
            int           presetConfigHashId = presetConfig?.GetHashCode() ?? 0;
            manager._cachedStartTimes.TryRemove(presetConfigHashId, out _);
            return BuildGenericState(Locale.Current.Lang?._Misc?.DiscordRP_Idle, manager);
        }

        private static void TryGetGameIconsAndTranslatedNames(
            PresetConfig? presetConfig,
            out string?   largeIconUrl,
            out string?   largeIconTooltip,
            out string?   smallIconUrl,
            out string?   smallIconTooltip,
            out string?   translatedGameName,
            out string?   translatedGameRegion)
        {
            Unsafe.SkipInit(out largeIconUrl);
            Unsafe.SkipInit(out largeIconTooltip);
            Unsafe.SkipInit(out smallIconUrl);
            Unsafe.SkipInit(out smallIconTooltip);

            string? currentGameName   = presetConfig?.GameName;
            string? currentGameRegion = presetConfig?.ZoneName;
            translatedGameName   = MetadataHelper.GetTranslatedTitle(currentGameName);
            translatedGameRegion = MetadataHelper.GetTranslatedRegion(currentGameRegion);

            // Try to get icons from plugin if available.
            TryGetPluginGameIcons(presetConfig,
                                  out largeIconUrl,
                                  out largeIconTooltip,
                                  out smallIconUrl,
                                  out smallIconTooltip,
                                  out bool isPluginGame);

            largeIconUrl ??= isPluginGame ? CollapseLogoExt : $"game-{presetConfig?.GameType.ToString().ToLower()}-logo";
            largeIconTooltip ??= $"{translatedGameName} - {translatedGameRegion}";
            smallIconUrl ??= isPluginGame ? CollapseLogoExt : DefaultLauncherLogo;
            smallIconTooltip ??= DefaultLauncherLogoTooltip;
        }

        private static void TryGetPluginGameIcons(PresetConfig? presetConfig,
                                                  out string?   largeIconUrl,
                                                  out string?   largeIconTooltip,
                                                  out string?   smallIconUrl,
                                                  out string?   smallIconTooltip,
                                                  out bool      isPluginGame)
        {
            Unsafe.SkipInit(out largeIconUrl);
            Unsafe.SkipInit(out largeIconTooltip);
            Unsafe.SkipInit(out smallIconUrl);
            Unsafe.SkipInit(out smallIconTooltip);
            Unsafe.SkipInit(out isPluginGame);

            if (presetConfig is not PluginPresetConfigWrapper asPluginPresetConfig)
            {
                return;
            }

            isPluginGame = true;
            if (!asPluginPresetConfig.DiscordPresenceContext.IsFeatureAvailable)
            {
                return;
            }

            largeIconUrl     = asPluginPresetConfig.DiscordPresenceContext.LargeIconUrl;
            largeIconTooltip = asPluginPresetConfig.DiscordPresenceContext.LargeIconTooltip;
            smallIconUrl     = asPluginPresetConfig.DiscordPresenceContext.SmallIconUrl;
            smallIconTooltip = asPluginPresetConfig.DiscordPresenceContext.SmallIconTooltip;
        }
    }
}
