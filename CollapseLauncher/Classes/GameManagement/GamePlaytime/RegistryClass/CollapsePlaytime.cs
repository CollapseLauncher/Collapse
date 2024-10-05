using CollapseLauncher.Interfaces;
using CollapseLauncher.Helper.Database;
using Hi3Helper;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static Hi3Helper.Logger;

namespace CollapseLauncher.GamePlaytime
{
    internal class CollapsePlaytime
    {
        #region Fields
        private static DateTime BaseDate => new(2012, 2, 13, 0, 0, 0, DateTimeKind.Utc);

        private const string TotalTimeValueName  = "CollapseLauncher_Playtime";
        private const string LastPlayedValueName = "CollapseLauncher_LastPlayed";
        private const string StatsValueName      = "CollapseLauncher_PlaytimeStats";

        private static HashSet<int>      _isDeserializing = [];
        private static CollapsePlaytime  _playtime;
        private        RegistryKey       _registryRoot;
        private        int               _hashID;
        private        IGameVersionCheck _gameVersion;
        
        #endregion

        #region Properties
        /// <summary>
        /// Represents the total time a game was played.<br/><br/>
        /// Default: TimeSpan.Zero
        /// </summary>
        [JsonIgnore]
        public TimeSpan TotalPlaytime { get; private set; } = TimeSpan.Zero;

        /// <summary>
        /// Represents the daily playtime.<br/>
        /// The ControlDate field is used to check if this value should be reset.<br/><br/>
        /// Default: TimeSpan.Zero
        /// </summary>
        public TimeSpan DailyPlaytime { get; private set; } = TimeSpan.Zero;

        /// <summary>
        /// Represents the weekly playtime.<br/>
        /// The ControlDate field is used to check if this value should be reset.<br/><br/>
        /// Default: TimeSpan.Zero
        /// </summary>
        public TimeSpan WeeklyPlaytime { get; private set; } = TimeSpan.Zero;

        /// <summary>
        /// Represents the monthly playtime.<br/>
        /// The ControlDate field is used to check if this value should be reset.<br/><br/>
        /// Default: TimeSpan.Zero
        /// </summary>
        public TimeSpan MonthlyPlaytime { get; private set; } = TimeSpan.Zero;

        /// <summary>
        /// Represents the total time the last/current session lasted.<br/><br/>
        /// Default: TimeSpan.Zero
        /// </summary>
        public TimeSpan LastSession { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Represents the last time the game was launched.<br/><br/>
        /// Default: null
        /// </summary>
        [JsonIgnore]
        public DateTime? LastPlayed { get; set; }

        /// <summary>
        /// Represents a control date.<br/>
        /// This date is used to check if a specific playtime statistic should be reset.<br/><br/>
        /// Default: DateTime.Today
        /// </summary>
        public DateTime ControlDate { get; private set; } = DateTime.Today;
        #endregion

        #region Methods
#nullable enable
        /// <summary>
        /// Reads from the Registry and deserializes the contents. <br/>
        /// Converts RegistryKey values if they are of type DWORD (that is, if they were saved by the old implementation).
        /// </summary>
        public static CollapsePlaytime Load(RegistryKey root, int hashID, IGameVersionCheck gameVersion)
        {
            try
            {
                _isDeserializing.Add(hashID);
                if (root == null) throw new NullReferenceException($"Cannot load playtime. RegistryKey is unexpectedly not initialized!");

                int? totalTime = (int?)root.GetValue(TotalTimeValueName,null);
                int? lastPlayed = (int?)root.GetValue(LastPlayedValueName,null);
                object? stats = root.GetValue(StatsValueName, null);
                
                if (stats != null)
                {
                    ReadOnlySpan<byte> byteStr = (byte[])stats;
#if DEBUG
                    LogWriteLine($"Loaded Playtime:\r\nTotal: {totalTime}s\r\nLastPlayed: {lastPlayed}\r\nStats: {Encoding.UTF8.GetString(byteStr.TrimEnd((byte)0))}", LogType.Debug, true);
#endif
                    _playtime = byteStr.Deserialize(UniversalPlaytimeJSONContext.Default.CollapsePlaytime, new CollapsePlaytime())!;
                }
                else
                {
                    _playtime = new CollapsePlaytime();
                }

                _playtime._gameVersion  = gameVersion;
                _playtime._registryRoot = root;
                _playtime._hashID       = hashID;
                _playtime.TotalPlaytime = TimeSpan.FromSeconds(totalTime ?? 0);
                _playtime.LastPlayed    = lastPlayed != null ? BaseDate.AddSeconds((int)lastPlayed) : null;

                return _playtime;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading playtime.\r\n{ex}", LogType.Error, true);
            }
            finally
            {
                _isDeserializing.Remove(hashID);
            }

            return new CollapsePlaytime
            { 
                _hashID = hashID,
                _registryRoot = root,
                _gameVersion = gameVersion };
        }

        /// <summary>
        /// Serializes all fields and saves them to the Registry.
        /// </summary>
        public void Save(bool forceUpdateDb = false)
        {
            try
            {
                if (_registryRoot == null) throw new NullReferenceException($"Cannot save playtime since RegistryKey is unexpectedly not initialized!");

                string data = this.Serialize(UniversalPlaytimeJSONContext.Default.CollapsePlaytime, true);
                byte[] dataByte = Encoding.UTF8.GetBytes(data);
#if DEBUG
                LogWriteLine($"Saved Playtime:\r\n{data}", LogType.Debug, true);
#endif
                _registryRoot.SetValue(StatsValueName, dataByte, RegistryValueKind.Binary);
                _registryRoot.SetValue(TotalTimeValueName, TotalPlaytime.TotalSeconds, RegistryValueKind.DWord);
                
                double? lastPlayed = (LastPlayed?.ToUniversalTime() - BaseDate)?.TotalSeconds;
                if (lastPlayed != null)
                    _registryRoot.SetValue(LastPlayedValueName, lastPlayed, RegistryValueKind.DWord);

                // Sync only every 5 minutes to reduce database usage
                if ((DateTime.Now - LastDbUpdate).TotalMinutes >= 5 || forceUpdateDb) 
                {
                    UpdatePlaytime_Database_Push(data, TotalPlaytime.TotalSeconds, lastPlayed);
                }
                
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save playtime!\r\n{ex}", LogType.Error, true);
            }
        }

        /// <summary>
        /// Resets all fields and saves to the Registry.
        /// </summary>
        public void Reset()
        {
            TotalPlaytime   = TimeSpan.Zero;
            LastSession     = TimeSpan.Zero;
            DailyPlaytime   = TimeSpan.Zero;
            WeeklyPlaytime  = TimeSpan.Zero;
            MonthlyPlaytime = TimeSpan.Zero;
            ControlDate     = DateTime.Today;
            LastPlayed      = null;

            if (!_isDeserializing.Contains(_hashID)) Save();
        }

        /// <summary>
        /// Updates the current Playtime TimeSpan to the provided value and saves to the Registry.<br/><br/>
        /// </summary>
        /// <param name="timeSpan">New playtime value</param>
        /// <param name="reset">Reset all other fields</param>
        public void Update(TimeSpan timeSpan, bool reset = true)
        {
            if (reset)
            {
                LastSession     = TimeSpan.Zero;
                DailyPlaytime   = TimeSpan.Zero;
                WeeklyPlaytime  = TimeSpan.Zero;
                MonthlyPlaytime = TimeSpan.Zero;
                ControlDate     = DateTime.Today;
            }
            
            TotalPlaytime = timeSpan;

            if (!_isDeserializing.Contains(_hashID)) Save();
        }

        /// <summary>
        /// Adds a minute to all fields, and checks if any should be reset.<br/>
        /// After it saves to the Registry.<br/><br/>
        /// </summary>
        public void AddMinute()
        {
            TimeSpan minute = TimeSpan.FromMinutes(1);
            DateTime today  = DateTime.Today;

            TotalPlaytime = TotalPlaytime.Add(minute);
            LastSession   = LastSession.Add(minute);
            
            if (ControlDate == today)
            {
                DailyPlaytime   = DailyPlaytime.Add(minute);
                WeeklyPlaytime  = WeeklyPlaytime.Add(minute);
                MonthlyPlaytime = MonthlyPlaytime.Add(minute);
            }
            else
            {
                DailyPlaytime   = minute;
                WeeklyPlaytime  = IsDifferentWeek(ControlDate, today) ? minute : WeeklyPlaytime.Add(minute);
                MonthlyPlaytime = IsDifferentMonth(ControlDate, today) ? minute : MonthlyPlaytime.Add(minute);
                
                ControlDate   = today;
            }

            if (!_isDeserializing.Contains(_hashID)) Save();
        }

        #endregion

        #region Utility
        private static bool IsDifferentMonth(DateTime date1, DateTime date2) => date1.Year != date2.Year || date1.Month != date2.Month;

        private static bool IsDifferentWeek(DateTime date1, DateTime date2) => date1.Year != date2.Year || ISOWeek.GetWeekOfYear(date1) != ISOWeek.GetWeekOfYear(date2);
        #endregion

        #region Database Extension
        // processing flags, prevents double task
        private bool _isDbSyncing;
        private bool _isDbPulling;
        private bool _isDbPullSuccess;

        public DateTime LastDbUpdate = DateTime.MinValue;
        
        // value store
        private string? _jsonDataDb;
        private double? _totalTimeDb;
        private double? _lastPlayedDb;
        private int?    _unixStampDb;
        
        // Key names
        private string KeyPlaytimeJson => $"{_gameVersion.GameType.ToString()}-{_gameVersion.GameRegion}-pt-js";
        private string KeyTotalTime    => $"{_gameVersion.GameType.ToString()}-{_gameVersion.GameRegion}-pt-total";
        private string KeyLastPlayed   => $"{_gameVersion.GameType.ToString()}-{_gameVersion.GameRegion}-pt-lastPlayed";
        private string KeyLastUpdated  => $"{_gameVersion.GameType.ToString()}-{_gameVersion.GameRegion}-pt-lu";
        

        #region Sync Methods
        /// <summary>
        /// Sync from/to DB at init
        /// </summary>
        /// <returns>true if require refresh, false if dont.</returns>
        public async ValueTask<bool> DbSync()
        {
            LogWriteLine("[CollapsePlaytime::DbSync] Starting sync operation...", LogType.Default, true);
            try
            {
                // Pull values from DB
                await UpdatePlaytime_Database_Pull();
                if (!_isDbPullSuccess) 
                {
                    LogWriteLine("[CollapsePlaytime::DbSync] Database pull failed, skipping sync~", LogType.Error);
                    return false; // Return if pull failed
                }

                // Compare unix stamp from config
                var unixStampLocal = Convert.ToInt32(DbConfig.GetConfig(KeyLastUpdated).ToString());
                if (_unixStampDb == unixStampLocal) 
                {
                    LogWriteLine("[CollapsePlaytime::DbSync] Sync stamp equal, nothing needs to be done~", LogType.Default, true);
                    return false; // Do nothing if stamp is equal
                }

                // When Db stamp is newer, sync from Db
                if (_unixStampDb > unixStampLocal)
                {
                    if (string.IsNullOrEmpty(_jsonDataDb))
                    {
                        LogWriteLine("[CollapsePlaytime::DbSync] _jsonDataDb is empty, skipping sync~", default, true);
                        return false;
                    }
                    LogWriteLine("[CollapsePlaytime::DbSync] Database data is newer! Pulling data~", LogType.Default, true);
                    _playtime = _jsonDataDb.Deserialize(UniversalPlaytimeJSONContext.Default.CollapsePlaytime,
                                                        new CollapsePlaytime());

                    _playtime!.TotalPlaytime = TimeSpan.FromSeconds(_totalTimeDb ?? 0);
                    if (_lastPlayedDb != null) _playtime.LastPlayed = BaseDate.AddSeconds((int)_lastPlayedDb);
                    DbConfig.SetAndSaveValue(KeyLastUpdated, _unixStampDb.ToString());
                    LastDbUpdate = DateTime.Now;
                    Save();
                    return true;
                }

                if (_unixStampDb < unixStampLocal)
                {
                    LogWriteLine("[CollapsePlaytime::DbSync] Database data is older! Pushing data~", default, true);
                    Save(true);
                }
                return false;
            }
            catch (Exception ex)
            {
                LogWriteLine($"[CollapsePlaytime::DbSync] Failed when trying to do sync operation\r\n{ex}",
                             LogType.Error, true);
                return false;
            }
        }
        

        #endregion
        
        #region DB Operation Methods
        private async void UpdatePlaytime_Database_Push(string jsonData, double totalTime, double? lastPlayed)
        {
            if (_isDbSyncing) return;
            _isDbSyncing = true;
            try
            {
                var unixStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                await DbHandler.StoreKeyValue(KeyPlaytimeJson, jsonData);
                await DbHandler.StoreKeyValue(KeyTotalTime,    totalTime.ToString(CultureInfo.InvariantCulture));
                await DbHandler.StoreKeyValue(KeyLastPlayed,   lastPlayed != null ? lastPlayed.ToString() : "null");
                await DbHandler.StoreKeyValue(KeyLastUpdated,  unixStamp.ToString());
                DbConfig.SetAndSaveValue(KeyLastUpdated, unixStamp);
                _unixStampDb     = Convert.ToInt32(unixStamp);
                LastDbUpdate     = DateTime.Now;
            }
            catch (Exception e)
            {
                LogWriteLine($"Failed when syncing Playtime to DB!\r\n{e}", LogType.Error, true);
            }
            finally
            {
                _isDbSyncing = false;
            }
        }
        
        private async Task UpdatePlaytime_Database_Pull()
        {
            if (_isDbPulling) return;
            _isDbPullSuccess = false;
            try
            {
                _isDbPulling = true;

                _jsonDataDb   = await DbHandler.QueryKey(KeyPlaytimeJson);
                
                var totalTimeDbStr = await DbHandler.QueryKey(KeyTotalTime);
                _totalTimeDb  = string.IsNullOrEmpty(totalTimeDbStr) ? null : Convert.ToDouble(totalTimeDbStr);
                
                var lpDb = await DbHandler.QueryKey(KeyLastPlayed);
                _lastPlayedDb    = !string.IsNullOrEmpty(lpDb) && !lpDb.Contains("null") ? Convert.ToDouble(lpDb) : null; // if Db data is null, return null

                var stampDbStr = await DbHandler.QueryKey(KeyLastUpdated);
                _unixStampDb     = !string.IsNullOrEmpty(stampDbStr) ? Convert.ToInt32(stampDbStr) : null;
                
                _isDbPullSuccess = true;
            }
            catch (Exception e)
            {
                LogWriteLine($"Failed when syncing Playtime to DB!\r\n{e}", LogType.Error, true);
            }
            finally
            {
                _isDbPulling = false;
            }
        }
        #endregion
        #endregion
    }
}
