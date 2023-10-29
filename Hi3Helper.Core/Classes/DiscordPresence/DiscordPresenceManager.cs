#if !DISABLEDISCORD
using Discord;
using Hi3Helper.Data;
using Hi3Helper.Preset;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
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
        private readonly object _sdkLock = new object();

        private int _updateInterval = 250; // in Milliseconds
        private Activity _activity;
        private Activity _previousActivity;
        private ActivityManager _activityManager;
        private ActivityType _activityType;
        private int? _lastUnixTimestamp;
        #endregion

        public DiscordPresenceManager(bool initialStart = true)
        {
            // If it's set to be initially started, then enable the presence
            if (initialStart)
            {
                SetupPresence(true);
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
            UpdateCallbacksRoutine();
            _client?.Dispose();
            _client = null;
        }

        public void EnablePresence(bool isInitialStart, long applicationId = AppDiscordApplicationID)
        {
            // Get the DLL path and check if the dll exist. If yes, then initialize the Discord Presence client.
            string dllPath = Path.Combine(AppFolder, "Lib\\discord_game_sdk.dll");
            if (File.Exists(dllPath))
            {
                try
                {
                    lock (_sdkLock)
                    {
                        // Initialize Discord Presence client and Activity property
                        _client = new Discord.Discord(applicationId, (ulong)CreateFlags.NoRequireDiscord);
                        if (isInitialStart) _activity = new Activity();
                        else SetActivity(_activityType);

                        // Initialize the Activity Manager instance
                        _activityManager = _client.GetActivityManager();

                        // Initialize the token source if the token is cancelled
                        if (_clientToken.IsCancellationRequested)
                        {
                            _clientToken = new CancellationTokenSource();
                        }
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

        public void SetupPresence(bool isInitialStart = false)
        {
            string GameCategory = GetAppConfigValue("GameCategory").ToString();
            bool IsGameStatusEnabled = GetAppConfigValue("EnableDiscordGameStatus").ToBool();

            if (IsGameStatusEnabled)
            {
                lock (_sdkLock)
                {
                    if (_client != null) Dispose();

                    switch (GameCategory)
                    {
                        case "Honkai: Star Rail":
                            EnablePresence(isInitialStart, AppDiscordApplicationID_HSR);
                            break;
                        case "Honkai Impact 3rd":
                            EnablePresence(isInitialStart, AppDiscordApplicationID_HI3);
                            break;
                        case "Genshin Impact":
                            EnablePresence(isInitialStart, AppDiscordApplicationID_GI);
                            break;
                        default:
                            Logger.LogWriteLine($"Discord Presence (Unknown Game)");
                            break;
                    }
                }
            }
            else
            {
                lock (_sdkLock)
                {
                    if (_client != null) Dispose();
                    EnablePresence(isInitialStart);
                }
            }
        }

        public async void SetActivity(ActivityType activity, int delay = 500)
        {
            await Task.Delay(delay);
            bool IsGameStatusEnabled = GetAppConfigValue("EnableDiscordGameStatus").ToBool();

            _activityType = activity;

            lock (_sdkLock)
            {
                switch (activity)
                {
                    case ActivityType.Play:
                        BuildActivityGameStatus(IsGameStatusEnabled ? Lang._Misc.DiscordRP_InGame : Lang._Misc.DiscordRP_Play, IsGameStatusEnabled);
                        break;
                    case ActivityType.Update:
                        BuildActivityGameStatus(Lang._Misc.DiscordRP_Update, IsGameStatusEnabled);
                        break;
                    case ActivityType.Repair:
                        BuildActivityAppStatus(Lang._Misc.DiscordRP_Repair, IsGameStatusEnabled);
                        break;
                    case ActivityType.Cache:
                        BuildActivityAppStatus(Lang._Misc.DiscordRP_Cache, IsGameStatusEnabled);
                        break;
                    case ActivityType.GameSettings:
                        BuildActivityAppStatus(Lang._Misc.DiscordRP_GameSettings, IsGameStatusEnabled);
                        break;
                    case ActivityType.AppSettings:
                        BuildActivityAppStatus(Lang._Misc.DiscordRP_AppSettings, IsGameStatusEnabled);
                        break;
                    case ActivityType.Idle:
                        _lastUnixTimestamp = null;
                        BuildActivityAppStatus(Lang._Misc.DiscordRP_Idle, IsGameStatusEnabled);
                        break;
                    default:
                        _activity = new Activity
                        {
                            Details = StrToByteUtf8(Lang._Misc.DiscordRP_Default),
                            Assets = new ActivityAssets
                            {
                                LargeImage = StrToByteUtf8($"launcher-logo")
                            }
                        };
                        break;
                }
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
                Details = StrToByteUtf8($"{activityName} {(!isGameStatusEnabled ? ConfigV2Store.CurrentConfigV2GameCategory : Lang._Misc.DiscordRP_Ad)}"),
                State = StrToByteUtf8($"{Lang._Misc.DiscordRP_Region} {ConfigV2Store.CurrentConfigV2GameRegion}"),
                Assets = new ActivityAssets
                {
                    LargeImage = StrToByteUtf8($"game-{ConfigV2Store.CurrentConfigV2.GameType.ToString().ToLower()}-logo"),
                    LargeText = StrToByteUtf8($"{ConfigV2Store.CurrentConfigV2GameCategory} - {ConfigV2Store.CurrentConfigV2GameRegion}"),
                    SmallImage = StrToByteUtf8($"launcher-logo"),
                    SmallText = StrToByteUtf8($"Collapse Launcher v{AppCurrentVersionString} {(IsPreview ? "Preview" : "Stable")}")
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

        private void BuildActivityAppStatus(string activityName, bool isGameStatusEnabled)
        {
            _activity = new Activity
            {
                Details = StrToByteUtf8($"{activityName} {(!isGameStatusEnabled ? string.Empty : Lang._Misc.DiscordRP_Ad)}"),
                State = StrToByteUtf8($"{Lang._Misc.DiscordRP_Region} {ConfigV2Store.CurrentConfigV2GameRegion}"),
                Assets = new ActivityAssets
                {
                    LargeImage = StrToByteUtf8($"game-{ConfigV2Store.CurrentConfigV2.GameType.ToString().ToLower()}-logo"),
                    LargeText = StrToByteUtf8($"{ConfigV2Store.CurrentConfigV2GameCategory}"),
                    SmallImage = StrToByteUtf8($"launcher-logo"),
                    SmallText = StrToByteUtf8($"Collapse Launcher v{AppCurrentVersionString} {(IsPreview ? "Preview" : "Stable")}")
                },
            };
        }

        private void UpdateActivity() => _activityManager?.UpdateActivity(_activity, (a) =>
        {
            Logger.LogWriteLine($"Activity updated! => {ReadUtf8Byte(_activity.Details)} - {ReadUtf8Byte(_activity.State)}");
        });

        private void UpdateCallbacksRoutine()
        {
            Task.Run(async () =>
            {
                while (!_clientToken.IsCancellationRequested)
                {
                    lock (_sdkLock)
                    {
                        try
                        {
                            _client?.RunCallbacks();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWriteLine($"Discord Presence: {ex.Message}", LogType.Error);
                        }
                    }

                    await Task.Delay(_updateInterval);
                }
            }, _clientToken.Token);
        }

        private string ReadUtf8Byte(byte[] input) => input == null || input.Length == 0 ? string.Empty : Encoding.UTF8.GetString(input);

        private byte[] StrToByteUtf8(string s)
        {
            // Use fixed width (128 bytes) as defined in field's SizeConst
            byte[] bufferOut = new byte[128];
            // Get the UTF-8 bytes (converting 16-bit (UTF-16) to 8-bit (UTF-8) char (as byte))
            Encoding.UTF8.GetBytes(s, bufferOut);
            // return the buffer
            return bufferOut;
        }
    }
}
#endif