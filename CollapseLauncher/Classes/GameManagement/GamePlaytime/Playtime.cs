using CollapseLauncher.Extension;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Timers;
using static Hi3Helper.Logger;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static CollapseLauncher.InnerLauncherConfig;

namespace CollapseLauncher.GamePlaytime
{
    internal class Playtime : IGamePlaytime
    {
        #region Properties
        public event EventHandler<CollapsePlaytime> PlaytimeUpdated;
#nullable enable
        public CollapsePlaytime CollapsePlaytime => _playtime;

        private static HashSet<int>      _activeSessions = []; 
        private        RegistryKey?      _registryRoot;
        private        CollapsePlaytime  _playtime;
        private        IGameVersionCheck _gameVersionManager;

        private CancellationTokenSourceWrapper _token = new();
        #endregion

        public Playtime(IGameVersionCheck GameVersionManager)
        {
            string registryPath = Path.Combine($"Software\\{GameVersionManager.VendorTypeProp.VendorType}", GameVersionManager.GamePreset.InternalGameNameInConfig!);
            _registryRoot = Registry.CurrentUser.OpenSubKey(registryPath, true);

            _registryRoot ??= Registry.CurrentUser.CreateSubKey(registryPath, true, RegistryOptions.None);

            _gameVersionManager = GameVersionManager;

            _playtime = CollapsePlaytime.Load(_registryRoot, _gameVersionManager.GamePreset.HashID, _gameVersionManager);
            
            CheckDb();
        }
#nullable disable

        public async void CheckDb()
        {
            var needUpdate = await _playtime.DbSync();
            if (needUpdate.IsUpdated && needUpdate.PlaytimeData != null)
            {
                _playtime = needUpdate.PlaytimeData;
                PlaytimeUpdated?.Invoke(this, _playtime);
            }
        }
        
        public void Update(TimeSpan timeSpan)
        {
            TimeSpan oldTimeSpan = _playtime.TotalPlaytime;

            _playtime.Update(timeSpan);
            PlaytimeUpdated?.Invoke(this, _playtime);

            LogWriteLine($"Playtime counter changed to {TimeSpanToString(timeSpan)}. (Previous value: {TimeSpanToString(oldTimeSpan)})", writeToLog: true);
        }

        public void Reset()
        {
            TimeSpan oldTimeSpan = _playtime.TotalPlaytime;

            _playtime.Reset();
            PlaytimeUpdated?.Invoke(this, _playtime);

            LogWriteLine($"Playtime counter was reset! (Previous value: {TimeSpanToString(oldTimeSpan)})", writeToLog: true);
        }

        public async void StartSession(Process proc, DateTime? begin = null)
        {
            int hashId = _gameVersionManager.GamePreset.HashID;

            // If a playtime HashSet has already tracked, then return (do not track the playtime more than once)
            if (_activeSessions.Contains(hashId)) return;

            // Otherwise, add it to track list
            _activeSessions.Add(hashId);

            begin ??= DateTime.Now;

            TimeSpan initialTimeSpan = _playtime.TotalPlaytime;

            _playtime.LastPlayed  = begin;
            _playtime.LastSession = TimeSpan.Zero;
            _playtime.Save();
            PlaytimeUpdated?.Invoke(this, _playtime);

#if DEBUG
            LogWriteLine($"{_gameVersionManager.GamePreset.ProfileName} - Started session at {begin.Value.ToLongTimeString()}.");
#endif
            int elapsedSeconds = 0;

            using (var inGameTimer = new Timer())
            {
                inGameTimer.Interval = 60000;
                inGameTimer.Elapsed += (_, _) => 
                {
                    elapsedSeconds += 60;
                    DateTime now = DateTime.Now;

                    _playtime.AddMinute();
                    PlaytimeUpdated?.Invoke(this, _playtime);
#if DEBUG
                    LogWriteLine($"{_gameVersionManager.GamePreset.ProfileName} - {elapsedSeconds}s elapsed. ({now.ToLongTimeString()})");
#endif
                };

                inGameTimer.Start();
                await proc.WaitForExitAsync(_token.Token);
                inGameTimer.Stop();
            }

            DateTime end = DateTime.Now;
            double totalElapsedSeconds = (end - begin.Value).TotalSeconds;
            if (totalElapsedSeconds < 0)
            {
                LogWriteLine($"[HomePage::StartPlaytimeCounter] Date difference cannot be lower than 0. ({totalElapsedSeconds}s)", LogType.Error);
                Dialog_InvalidPlaytime(m_mainPage?.Content, elapsedSeconds);
                totalElapsedSeconds = elapsedSeconds;
            }

            TimeSpan totalTimeSpan = TimeSpan.FromSeconds(totalElapsedSeconds);
            LogWriteLine($"Added {totalElapsedSeconds}s [{totalTimeSpan.Hours}h {totalTimeSpan.Minutes}m {totalTimeSpan.Seconds}s] " +
                         $"to {_gameVersionManager.GamePreset.ProfileName} playtime.", LogType.Default, true);
            
            _playtime.Update(initialTimeSpan.Add(totalTimeSpan), false);
            PlaytimeUpdated?.Invoke(this, _playtime);

            _activeSessions.Remove(hashId);
        }

        private static string TimeSpanToString(TimeSpan timeSpan) => $"{timeSpan.Days * 24 + timeSpan.Hours}h {timeSpan.Minutes}m";

        public void Dispose()
        {
            _token.Cancel();
            _playtime.Save(true);
            _playtime.LastDbUpdate = DateTime.MinValue;
            _registryRoot           = null;
        }
    }
}
