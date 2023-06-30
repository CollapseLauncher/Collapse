#if !DISABLEDISCORD
using Discord;
using Hi3Helper.Data;
using Hi3Helper.Preset;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static Hi3Helper.Locale;

#pragma warning disable CA2007
namespace Hi3Helper.DiscordPresence
{
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

    public class DiscordPresenceManager : IDisposable, IAsyncDisposable
    {
        #region Properties
        private Discord.Discord _client;
        private CancellationTokenSource _clientToken = new CancellationTokenSource();

        private int _updateInterval = 250; // in Milliseconds
        private Activity _activity;
        private Activity _previousActivity;
        private ActivityManager _activityManager;
        private int? _lastUnixTimestamp;
        #endregion

        public DiscordPresenceManager(bool initialStart = true)
        {
            // If it's set to be initially started, then enable the presence
            if (initialStart)
            {
                SetupPresence();
                SetActivity(ActivityType.None);
            }
        }

        // Deconstruct and dispose unmanaged resources
        ~DiscordPresenceManager() => this.Dispose();

        public void Dispose()
        {
            // Run async dispose as sync
            DisposeAsync().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            // Trigger cancellation
            _clientToken.Cancel();

            // Wait until cancellation has been triggered
            while (!_clientToken.IsCancellationRequested)
            {
                await Task.Delay(_updateInterval);
            }

            // Dispose Discord RPC client
            // _client?.Dispose();
            _client?.Dispose();
            _client = null;
        }

        public void EnablePresence(long applicationId = AppDiscordApplicationID)
        {
            // Get the DLL path and check if the dll exist. If yes, then initialize the Discord Presence client.
            string dllPath = Path.Combine(AppFolder, "Lib\\discord_game_sdk.dll");
            if (File.Exists(dllPath))
            {
                try
                {
                    // Initialize Discord Presence client and Activity property
                    _client = new Discord.Discord(applicationId, (ulong)CreateFlags.NoRequireDiscord);
                    _activity = new Activity();

                    // Initialize the Activity Manager instance
                    _activityManager = _client.GetActivityManager();

                    // Initialize the token source if the token is cancelled
                    if (_clientToken.IsCancellationRequested)
                    {
                        _clientToken = new CancellationTokenSource();
                    }

                    // Run .UpdateCallbacks() loop routine, initiate the activity and return
                    UpdateCallbacksRoutine();
                    UpdateActivity();

                    Logger.LogWriteLine($"Discord Presence is Enabled!");
                    return;
                }
                catch (ResultException ex)
                {
                    Logger.LogWriteLine($"Error initializing Discord Presence. Please ensure that Discord is running! ({ex.GetType().Name}: {ex.Message})", LogType.Warning, true);
                }
            }

            Logger.LogWriteLine($"Discord Presence DLL: {dllPath} doesn't exist! The Discord presence feature could not be used.");
        }

        public async void DisablePresence()
        {
            Logger.LogWriteLine($"Discord Presence is Disabled!");
            await DisposeAsync();
        }

        public void SetupPresence()
        {
            string GameCategory = GetAppConfigValue("GameCategory").ToString();
            bool IsGameStatusEnabled = GetAppConfigValue("EnableDiscordGameStatus").ToBool();

            if (IsGameStatusEnabled)
            {
                if (_client != null) _client?.Dispose();

                switch (GameCategory)
                {
                    case "Honkai: Star Rail":
                        EnablePresence(AppDiscordApplicationStarRailID);
                        break;
                    case "Honkai Impact 3rd":
                        EnablePresence(AppDiscordApplicationHonkaiID);
                        break;
                    case "Genshin Impact":
                        EnablePresence(AppDiscordApplicationGenshinID);
                        break;
                    default:
                        Logger.LogWriteLine($"Discord Presence (Unknown Game)");
                        break;
                }
            }
            else
            {
                if (_client != null) _client?.Dispose();
                EnablePresence();
            }
        }

        public async void SetActivity(ActivityType activity, int delay = 500)
        {
            await Task.Delay(delay);
            bool IsGameStatusEnabled = GetAppConfigValue("EnableDiscordGameStatus").ToBool();

            switch (activity)
            {
                case ActivityType.Play:
                    BuildActivityGameStatus(IsGameStatusEnabled ? Lang._Misc.DiscordRP_InGame : Lang._Misc.DiscordRP_Play, IsGameStatusEnabled);
                    break;
                case ActivityType.Update:
                    BuildActivityGameStatus(Lang._Misc.DiscordRP_Update, IsGameStatusEnabled);
                    break;
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
                    _lastUnixTimestamp = null;
                    BuildActivityAppStatus(Lang._Misc.DiscordRP_Idle);
                    break;
                default:
                    _activity = new Activity
                    {
                        Details = Lang._Misc.DiscordRP_Default,
                        Assets = new ActivityAssets
                        {
                            LargeImage = $"launcher-logo"
                        }
                    };
                    break;
            }

            if (!_previousActivity.Equals(_activity) && !_clientToken.IsCancellationRequested)
            {
                UpdateActivity();
                _previousActivity = _activity;
            }
        }

        private void BuildActivityGameStatus(string activityName, bool isGameStatusEnabled)
        {
            _activity = new Activity
            {
                Details = $"{activityName} {(!isGameStatusEnabled ? ConfigV2Store.CurrentConfigV2GameCategory : "")}",
                State = $"Server: {ConfigV2Store.CurrentConfigV2GameRegion}",
                Assets = new ActivityAssets
                {
                    LargeImage = $"game-{ConfigV2Store.CurrentConfigV2.GameType.ToString().ToLower()}-logo",
                    LargeText = $"{ConfigV2Store.CurrentConfigV2GameCategory} - {ConfigV2Store.CurrentConfigV2GameRegion}",
                    SmallImage = $"launcher-logo"
                },
                Timestamps = new ActivityTimestamps
                {
                    Start = GetCachedUnixTimestamp()
                }
            };
        }

        private int GetCachedUnixTimestamp()
        {
            if (_lastUnixTimestamp == null)
            {
                _lastUnixTimestamp = ConverterTool.GetUnixTimestamp(true);
            }

            return _lastUnixTimestamp ?? 0;
        }

        private void BuildActivityAppStatus(string activityName)
        {
            _activity = new Activity
            {
                Details = $"{activityName}",
                State = $"Server: {ConfigV2Store.CurrentConfigV2GameRegion}",
                Assets = new ActivityAssets
                {
                    LargeImage = $"game-{ConfigV2Store.CurrentConfigV2.GameType.ToString().ToLower()}-logo",
                    LargeText = $"{ConfigV2Store.CurrentConfigV2GameCategory}",
                    SmallImage = $"launcher-logo"
                },
            };
        }

        private void UpdateActivity() => _activityManager?.UpdateActivity(_activity, (a) =>
        {
#if DEBUG
            Logger.LogWriteLine($"Activity updated! => {_activity.Details} - {_activity.State}");
#endif
        });

        private async void UpdateCallbacksRoutine()
        {
            while (!_clientToken.IsCancellationRequested)
            {
                // Run the update
                _client?.RunCallbacks();

                // Take 33ms delay before the next update
                await Task.Delay(_updateInterval);
            }
        }
    }
}
#endif