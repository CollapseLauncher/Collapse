using Discord;
using Hi3Helper.Data;
using Hi3Helper.Preset;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Shared.Region.LauncherConfig;

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
                EnablePresence();
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
            _client?.Dispose();
        }

        public void EnablePresence()
        {
            // Get the DLL path and check if the dll exist. If yes, then initialize the Discord Presence client.
            string dllPath = Path.Combine(AppFolder, "Lib\\discord_game_sdk.dll");
            if (File.Exists(dllPath))
            {
                try
                {
                    // Initialize Discord Presence client and Activity property
                    _client = new Discord.Discord(AppDiscordApplicationID, (ulong)CreateFlags.NoRequireDiscord);
                    _activity = new Activity();

                    // Initialize the Activity Manager instance
                    _activityManager = _client.GetActivityManager();

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

        public async void SetActivity(ActivityType activity, int delay = 500)
        {
            await Task.Delay(delay);
            switch (activity)
            {
                case ActivityType.Play:
                    BuildActivityGameStatus("Playing");
                    break;
                case ActivityType.Update:
                    BuildActivityGameStatus("Updating");
                    break;
                case ActivityType.Repair:
                    BuildActivityAppStatus("Repairing Game");
                    break;
                case ActivityType.Cache:
                    BuildActivityAppStatus("Updating Cache");
                    break;
                case ActivityType.GameSettings:
                    BuildActivityAppStatus("Changing Game Settings");
                    break;
                case ActivityType.AppSettings:
                    BuildActivityAppStatus("Changing App Settings");
                    break;
                case ActivityType.Idle:
                    _lastUnixTimestamp = null;
                    BuildActivityAppStatus("Idle");
                    break;
                default:
                    _activity = new Activity
                    {
                        Details = $"No Activity",
                        Assets = new ActivityAssets
                        {
                            LargeImage = $"launcher-logo"
                        }
                    };
                    break;
            }

            if (!_previousActivity.Equals(_activity))
            {
                UpdateActivity();
                _previousActivity = _activity;
            }
        }

        private void BuildActivityGameStatus(string activityName)
        {
            _activity = new Activity
            {
                Details = $"{activityName} {ConfigV2Store.CurrentConfigV2GameCategory}",
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
            while (true)
            {
                // Take 33ms delay before the next update
                await Task.Delay(_updateInterval);

                // If a cancel is requested, then return
                if (_clientToken.IsCancellationRequested) return;

                // Run the update
                _client?.RunCallbacks();
            }
        }
    }
}
