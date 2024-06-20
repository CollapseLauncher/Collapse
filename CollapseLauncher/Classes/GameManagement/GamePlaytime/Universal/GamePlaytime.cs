using CollapseLauncher.GamePlaytime.Base;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Timers;
using static Hi3Helper.Logger;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static CollapseLauncher.InnerLauncherConfig;

namespace CollapseLauncher.GamePlaytime.Universal
{
    internal class GamePlaytime : GamePlaytimeBase, IGamePlaytime
    {
        #region Properties
        public event EventHandler<Playtime> PlaytimeUpdated;
        #endregion

        public GamePlaytime(IGameVersionCheck GameVersionManager) : base(GameVersionManager)
        {
            string registryPath = Path.Combine($"Software\\{GameVersionManager.VendorTypeProp.VendorType}", GameVersionManager.GamePreset.InternalGameNameInConfig);
            RegistryRoot = Registry.CurrentUser.OpenSubKey(registryPath, true);

            RegistryRoot ??= Registry.CurrentUser.CreateSubKey(registryPath, true, RegistryOptions.None);

            _gameVersionManager = GameVersionManager;

            _playtime = Playtime.Load(RegistryRoot);
        }

        public void ForceUpdate()
        {
            PlaytimeUpdated?.Invoke(this, _playtime);
        }

        public void Update(TimeSpan timeSpan)
        {
            TimeSpan oldTimeSpan = _playtime.CurrentPlaytime;

            _playtime.Update(timeSpan);
            PlaytimeUpdated?.Invoke(this, _playtime);

            LogWriteLine($"Playtime counter changed to {TimeSpanToString(timeSpan)}m. (Previous value: {TimeSpanToString(oldTimeSpan)})", writeToLog: true);
        }

        public void Reset()
        {
            TimeSpan oldTimeSpan = _playtime.CurrentPlaytime;

            _playtime.Reset();
            PlaytimeUpdated?.Invoke(this, _playtime);

            LogWriteLine($"Playtime counter was reset! (Previous value: {TimeSpanToString(oldTimeSpan)})", writeToLog: true);
        }

        public async void StartSession(Process proc)
        {
            DateTime begin          = DateTime.Now;
            TimeSpan initialTimeSpan = _playtime.CurrentPlaytime;

            _playtime.LastPlayed = begin;
            _playtime.Save();

#if DEBUG
            LogWriteLine($"{_gameVersionManager.GamePreset.ProfileName} - Started session at {begin.ToLongTimeString()}.");
#endif
            int elapsedSeconds = 0;

            using (var inGameTimer = new Timer())
            {
                inGameTimer.Interval = 60000;
                inGameTimer.Elapsed += (_, _) => 
                {
                    elapsedSeconds += 60;
                    DateTime now = DateTime.Now;

                    _playtime.Add(TimeSpan.FromMinutes(1));
                    PlaytimeUpdated?.Invoke(this, _playtime);

#if DEBUG
                    LogWriteLine($"{_gameVersionManager.GamePreset.ProfileName} - {elapsedSeconds}s elapsed. ({now.ToLongTimeString()})");
#endif
                };

                inGameTimer.Start();
                await proc.WaitForExitAsync();
                inGameTimer.Stop();
            }

            DateTime end = DateTime.Now;
            double totalElapsedSeconds = (end - begin).TotalSeconds;
            if (totalElapsedSeconds < 0)
            {
                LogWriteLine($"[HomePage::StartPlaytimeCounter] Date difference cannot be lower than 0. ({elapsedSeconds}s)", LogType.Error);
                Dialog_InvalidPlaytime(m_mainPage?.Content, elapsedSeconds);
                totalElapsedSeconds = elapsedSeconds;
            }

            TimeSpan totalTimeSpan = TimeSpan.FromSeconds(totalElapsedSeconds);
            LogWriteLine($"Added {totalElapsedSeconds}s [{totalTimeSpan.Hours}h {totalTimeSpan.Minutes}m {totalTimeSpan.Seconds}s] " +
                         $"to {_gameVersionManager.GamePreset.ProfileName} playtime.", LogType.Default, true);
            
            _playtime.Update(initialTimeSpan.Add(totalTimeSpan), false);
            PlaytimeUpdated?.Invoke(this, _playtime);
        }
    }
}
