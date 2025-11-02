using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.Update;
using CollapseLauncher.Plugins;
using DiscordRPC;
using DiscordRPC.Message;
using Hi3Helper;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Dataflow;
using static Hi3Helper.Locale;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable StringLiteralTypo
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.DiscordPresence
{
    #region Enums

    public enum ActivityType
    {
        None,
        Idle,
        Play,
        Update,
        Repair,
        Cache,
        GameSettings,
        AppSettings
    }

    #endregion

    public sealed partial class DiscordPresenceManager : IDisposable
    {
        #region Properties
        private const string CollapseLogoExt = "https://collapselauncher.com/img/logo@2x.webp";

        private DiscordRpcClient? _client;

        private          RichPresence?             _presence;
        private          ActivityType              _activityType;
        private          DateTime?                 _lastPlayTime;
        private          bool                      _firstTimeConnect = true;
        private readonly ActionBlock<RichPresence> _presenceUpdateQueue;

        private bool _cachedIsIdleEnabled = true;

        public bool IdleEnabled
        {
            get
            {
                bool value = GetAppConfigValue("EnableDiscordIdleStatus");
                _cachedIsIdleEnabled = value;
                return value;
            }
            set
            {
                SetAndSaveConfigValue("EnableDiscordIdleStatus", value);
                _cachedIsIdleEnabled = value;
            }
        }

        #endregion

        public DiscordPresenceManager(bool initialStart = true)
        {
            _presenceUpdateQueue = new ActionBlock<RichPresence>(_ => _client?.SetPresence(_presence),
                                                                 new ExecutionDataflowBlockOptions
                                                                 {
                                                                     MaxMessagesPerTask     = 1,
                                                                     MaxDegreeOfParallelism = 1,
                                                                     EnsureOrdered          = true
                                                                 });

            if (!initialStart)
            {
                return;
            }

            // Prepare idle cached setting
            Logger.LogWriteLine($"Doing initial start for Discord RPC!\r\n\tIdle status : {IdleEnabled}",
                                LogType.Scheme);
        }

        // Deconstruct and dispose unmanaged resources
        ~DiscordPresenceManager()
        {
            Dispose();
        }

        public void Dispose()
        {
            // Dispose Discord RPC client
            DisablePresence();

            // Suppress the GC from finalization
            GC.SuppressFinalize(this);
        }

        private void EnablePresence(ulong applicationId)
        {
            _firstTimeConnect = true;

            // Flush and dispose the session
            DisablePresence();

            // Initialize Discord RPC client
            _client = new DiscordRpcClient(applicationId.ToString(), ILoggerHelper.GetILogger("DiscordRPC"));

            _client.OnReady          += OnReady;
            _client.OnPresenceUpdate += OnPresenceUpdate;

            if (!_client.Initialize())
            {
                Logger.LogWriteLine("Error initializing Discord Presence.", LogType.Warning, true);
                return;
            }

            Logger.LogWriteLine("Discord Presence is Enabled!");
        }

        private void OnReady(object? _, ReadyMessage msg)
        {
            Logger.LogWriteLine($"Connected to Discord with user {msg.User.Username}");
            if (!_firstTimeConnect)
            {
                // Restart Discord RPC client
                _firstTimeConnect = true;
                SetupPresence();
            }
            else
            {
                // Restore our last activity
                if (!(!_cachedIsIdleEnabled &&
                      _activityType is ActivityType.Idle or ActivityType.None))
                {
                    SetActivity(_activityType);
                }

                _firstTimeConnect = false;
            }
        }

        private static void OnPresenceUpdate(object? _, PresenceMessage msg)
        {
            if (msg.Presence == null)
            {
                Logger.LogWriteLine("Activity cleared!");
            }
            else
            {
                Logger.LogWriteLine(msg.Presence.State == null
                                        ? $"Activity updated! => {msg.Presence.Details}"
                                        : $"Activity updated! => {msg.Presence.Details} - {msg.Presence.State}");
            }
        }

        public void DisablePresence()
        {
            _client?.SetPresence(null);
            _client?.Dispose();
            _client = null;
        }

        public void SetupPresence()
        {
            string? gameCategory        = GetAppConfigValue("GameCategory").ToString();
            bool    isGameStatusEnabled = GetAppConfigValue("EnableDiscordGameStatus").ToBool();

            if (isGameStatusEnabled)
            {
                switch (gameCategory)
                {
                    case "Honkai: Star Rail":
                        EnablePresence(AppDiscordApplicationID_HSR);
                        return;
                    case "Honkai Impact 3rd":
                        EnablePresence(AppDiscordApplicationID_HI3);
                        return;
                    case "Genshin Impact":
                        EnablePresence(AppDiscordApplicationID_GI);
                        return;
                    case "Zenless Zone Zero":
                        EnablePresence(AppDiscordApplicationID_ZZZ);
                        return;
                    default:
                        if (TryEnablePresenceIfPlugin())
                        {
                            return;
                        }
                        Logger.LogWriteLine("Discord Presence (Unknown Game)", LogType.Error, true);
                        break;
                }
            }

            EnablePresence(AppDiscordApplicationID);
        }

        private bool TryEnablePresenceIfPlugin()
        {
            if (LauncherMetadataHelper.CurrentMetadataConfig
                is not PluginPresetConfigWrapper asPluginPresetConfig)
            {
                return false;
            }

            if (!asPluginPresetConfig.DiscordPresenceContext.IsFeatureAvailable ||
                asPluginPresetConfig.DiscordPresenceContext.PresenceId == 0)
            {
                return false;
            }

            EnablePresence(asPluginPresetConfig.DiscordPresenceContext.PresenceId);
            return true;
        }

        public void SetActivity(ActivityType activity, DateTime? activityOffset = null)
        {
            if (!GetAppConfigValue("EnableDiscordRPC").ToBool())
            {
                return;
            }

            //_lastAttemptedActivityType = activity;
            _activityType = activity;

            switch (activity)
            {
                case ActivityType.Play:
                    {
                        bool isGameStatusEnabled = GetAppConfigValue("EnableDiscordGameStatus").ToBool();
                        BuildActivityGameStatus(isGameStatusEnabled ? Lang._Misc.DiscordRP_InGame : Lang._Misc.DiscordRP_Play,
                                                isGameStatusEnabled, activityOffset);
                        break;
                    }
                case ActivityType.Update:
                    {
                        bool isGameStatusEnabled = GetAppConfigValue("EnableDiscordGameStatus").ToBool();
                        BuildActivityGameStatus(Lang._Misc.DiscordRP_Update, isGameStatusEnabled);
                        break;
                    }
                case ActivityType.Repair:
                    BuildActivityAppStatus(Lang._Misc.DiscordRP_Repair);
                    break;
                case ActivityType.Cache:
                    BuildActivityAppStatus(Lang._Misc.DiscordRP_Cache);
                    break;
                case ActivityType.GameSettings:
                    BuildActivityAppStatus(Lang._Misc.DiscordRP_GameSettings);
                    break;
                case ActivityType.AppSettings:
                    BuildActivityAppStatus(Lang._Misc.DiscordRP_AppSettings);
                    break;
                case ActivityType.Idle:
                    _lastPlayTime = null;
                    if (_cachedIsIdleEnabled)
                    {
                        BuildActivityAppStatus(Lang._Misc.DiscordRP_Idle);
                    }
                    else
                    {
                        _presence = null; // Clear presence
                    }

                    break;
                default:
                    _presence = new RichPresence
                    {
                        Details = Lang._Misc.DiscordRP_Default,
                        Assets = new Assets
                        {
                            LargeImageKey = "launcher-logo-new",
                            LargeImageText =
                                $"Collapse Launcher v{LauncherUpdateHelper.LauncherCurrentVersionString} {(IsPreview ? "Preview" : "Stable")}"
                        },
                        Timestamps = null!
                    };
                    break;
            }

            UpdateActivity();
        }

        private void BuildActivityGameStatus(string activityName, bool isGameStatusEnabled, DateTime? activityOffset = null)
        {
            var curGameName   = LauncherMetadataHelper.CurrentMetadataConfigGameName;
            var curGameRegion = LauncherMetadataHelper.CurrentMetadataConfigGameRegion;

            if (string.IsNullOrEmpty(curGameName) || string.IsNullOrEmpty(curGameRegion))
                return;

            var curGameNameTranslate =
                InnerLauncherConfig.GetGameTitleRegionTranslationString(curGameName, Lang._GameClientTitles);
            var curGameRegionTranslate =
                InnerLauncherConfig.GetGameTitleRegionTranslationString(curGameRegion, Lang._GameClientRegions);

            if (TryBuildActivityGameStatusFromPlugin(activityName,
                                                     curGameNameTranslate,
                                                     curGameRegionTranslate,
                                                     isGameStatusEnabled,
                                                     activityOffset,
                                                     out _presence))
            {
                return;
            }

            _presence = new RichPresence
            {
                Details = $"{activityName} {(!isGameStatusEnabled ? curGameNameTranslate : null)}",
                State   = $"{Lang._Misc.DiscordRP_Region} {curGameRegionTranslate}",
                Assets = new Assets
                {
                    LargeImageKey  = $"game-{LauncherMetadataHelper.CurrentMetadataConfig?.GameType.ToString().ToLower()}-logo",
                    LargeImageText = $"{curGameNameTranslate} - {curGameRegionTranslate}",
                    SmallImageKey  = "launcher-logo-new",
                    SmallImageText = $"Collapse Launcher v{LauncherUpdateHelper.LauncherCurrentVersionString} "
                                     + $"{(IsPreview ? "Preview" : "Stable")}"
                },
                Timestamps = new Timestamps
                {
                    Start = GetCachedStartPlayTime(activityOffset)
                }
            };
        }

        private bool TryBuildActivityGameStatusFromPlugin(
            string                                activityName,
            string?                               translatedGameName,
            string?                               translatedRegionName,
            bool                                  isGameStatusEnabled,
            DateTime?                             activityOffset,
            [NotNullWhen(true)] out RichPresence? presence)
        {
            Unsafe.SkipInit(out presence);

            if (LauncherMetadataHelper.CurrentMetadataConfig
                is not PluginPresetConfigWrapper asPluginPresetConfig ||
                !asPluginPresetConfig.DiscordPresenceContext.IsFeatureAvailable)
            {
                return false;
            }

            string? largeIconUrl     = asPluginPresetConfig.DiscordPresenceContext.LargeIconUrl;
            string? largeIconTooltip = asPluginPresetConfig.DiscordPresenceContext.LargeIconTooltip;
            string? smallIconUrl     = asPluginPresetConfig.DiscordPresenceContext.SmallIconUrl;
            string? smallIconTooltip = asPluginPresetConfig.DiscordPresenceContext.SmallIconTooltip;

            presence = new RichPresence
            {
                Details = $"{activityName} {(!isGameStatusEnabled ? translatedGameName : null)}",
                State   = $"{Lang._Misc.DiscordRP_Region} {translatedRegionName}",
                Assets = new Assets
                {
                    LargeImageKey  = largeIconUrl ?? CollapseLogoExt,
                    LargeImageText = largeIconTooltip ?? $"{translatedGameName} - {translatedRegionName}",
                    SmallImageKey  = smallIconUrl ?? CollapseLogoExt,
                    SmallImageText = smallIconTooltip ??
                                     $"Collapse Launcher v{LauncherUpdateHelper.LauncherCurrentVersionString} "
                                     + $"{(IsPreview ? "Preview" : "Stable")}"
                },
                Timestamps = new Timestamps
                {
                    Start = GetCachedStartPlayTime(activityOffset)
                }
            };

            return true;
        }

        private DateTime GetCachedStartPlayTime(DateTime? activityOffset)
        {
            _lastPlayTime ??= activityOffset;
            _lastPlayTime ??= DateTime.UtcNow;
            return _lastPlayTime.Value;
        }

        private void BuildActivityAppStatus(string activityName)
        {
            var curGameName = LauncherMetadataHelper.CurrentMetadataConfigGameName;
            var curGameRegion = LauncherMetadataHelper.CurrentMetadataConfigGameRegion;

            if (string.IsNullOrEmpty(curGameName) || string.IsNullOrEmpty(curGameRegion))
                return;

            var curGameNameTranslate =
                InnerLauncherConfig.GetGameTitleRegionTranslationString(curGameName, Lang._GameClientTitles);
            var curGameRegionTranslate =
                InnerLauncherConfig.GetGameTitleRegionTranslationString(curGameRegion, Lang._GameClientRegions);

            if (TryBuildActivityAppStatusFromPlugin(activityName,
                                                    curGameNameTranslate,
                                                    curGameRegionTranslate,
                                                    out _presence))
            {
                return;
            }

            _presence = new RichPresence
            {
                Details = activityName,
                State   = $"{Lang._Misc.DiscordRP_Region} {curGameRegionTranslate}",
                Assets  = new Assets
                {
                    LargeImageKey  = $"game-{LauncherMetadataHelper.CurrentMetadataConfig?.GameType.ToString().ToLower()}-logo",
                    LargeImageText = curGameNameTranslate,
                    SmallImageKey  = "launcher-logo-new",
                    SmallImageText = $"Collapse Launcher v{LauncherUpdateHelper.LauncherCurrentVersionString} "
                                     + $"{(IsPreview ? "Preview" : "Stable")}"
                },
                Timestamps = null!
            };
        }

        private static bool TryBuildActivityAppStatusFromPlugin(
            string                                activityName,
            string?                               translatedGameName,
            string?                               translatedRegionName,
            [NotNullWhen(true)] out RichPresence? presence)
        {
            Unsafe.SkipInit(out presence);

            if (LauncherMetadataHelper.CurrentMetadataConfig
                is not PluginPresetConfigWrapper asPluginPresetConfig ||
                !asPluginPresetConfig.DiscordPresenceContext.IsFeatureAvailable)
            {
                return false;
            }

            string? largeIconUrl     = asPluginPresetConfig.DiscordPresenceContext.LargeIconUrl;
            string? largeIconTooltip = asPluginPresetConfig.DiscordPresenceContext.LargeIconTooltip;
            string? smallIconUrl     = asPluginPresetConfig.DiscordPresenceContext.SmallIconUrl;
            string? smallIconTooltip = asPluginPresetConfig.DiscordPresenceContext.SmallIconTooltip;

            presence = new RichPresence
            {
                Details = activityName,
                State   = $"{Lang._Misc.DiscordRP_Region} {translatedRegionName}",
                Assets  = new Assets
                {
                    LargeImageKey  = largeIconUrl ?? CollapseLogoExt,
                    LargeImageText = largeIconTooltip ?? translatedGameName,
                    SmallImageKey  = smallIconUrl ?? CollapseLogoExt,
                    SmallImageText = smallIconTooltip ??
                                     $"Collapse Launcher v{LauncherUpdateHelper.LauncherCurrentVersionString} "
                                     + $"{(IsPreview ? "Preview" : "Stable")}"
                },
                Timestamps = null!
            };

            return true;
        }

        private void UpdateActivity()
        {
            if (_presence != null)
            {
                _presenceUpdateQueue.Post(_presence);
            }
        }
    }
}