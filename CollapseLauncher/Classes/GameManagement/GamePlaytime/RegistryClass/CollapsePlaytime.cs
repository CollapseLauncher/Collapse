using Hi3Helper;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using static Hi3Helper.Logger;

namespace CollapseLauncher.GamePlaytime
{
    internal class CollapsePlaytime
    {
        #region Fields
        private static DateTime BaseDate => new(2012, 2, 13, 0, 0, 0, DateTimeKind.Utc);

        private const string _TotalTimeValueName  = "CollapseLauncher_Playtime";
        private const string _LastPlayedValueName = "CollapseLauncher_LastPlayed";
        private const string _StatsValueName      = "CollapseLauncher_PlaytimeStats";

        private static Dictionary<int, bool> _IsDeserializing = [];
        private        RegistryKey           _registryRoot;
        private        int                   _hashID;

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
        public static CollapsePlaytime Load(RegistryKey root, int hashID)
        {
            try
            {
                _IsDeserializing[hashID] = true;
                if (root == null) throw new NullReferenceException($"Cannot load playtime. RegistryKey is unexpectedly not initialized!");

                int? totalTime = (int?)root.GetValue(_TotalTimeValueName,null);
                int? lastPlayed = (int?)root.GetValue(_LastPlayedValueName,null);
                object? stats = root.GetValue(_StatsValueName, null);

                CollapsePlaytime playtime;

                if (stats != null)
                {
                    ReadOnlySpan<byte> byteStr = (byte[])stats;
#if DEBUG
                    LogWriteLine($"Loaded Playtime:\r\nTotal: {totalTime}s\r\nLastPlayed: {lastPlayed}\r\nStats: {Encoding.UTF8.GetString(byteStr.TrimEnd((byte)0))}", LogType.Debug, true);
#endif
                    playtime = byteStr.Deserialize<CollapsePlaytime>(UniversalPlaytimeJSONContext.Default) ?? new CollapsePlaytime();
                }
                else
                {
                    playtime = new CollapsePlaytime();
                }

                playtime._registryRoot = root;
                playtime._hashID       = hashID;
                playtime.TotalPlaytime = TimeSpan.FromSeconds(totalTime ?? 0);
                playtime.LastPlayed    = lastPlayed != null ? BaseDate.AddSeconds((int)lastPlayed) : null;

                return playtime;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading playtime.\r\n{ex}", LogType.Error, true);
            }
            finally
            {
                _IsDeserializing[hashID] = false;
            }

            return new CollapsePlaytime() { _hashID = hashID, _registryRoot = root };
        }

        /// <summary>
        /// Serializes all fields and saves them to the Registry.
        /// </summary>
        public void Save()
        {
            try
            {
                if (_registryRoot == null) throw new NullReferenceException($"Cannot save playtime since RegistryKey is unexpectedly not initialized!");

                string data = this.Serialize(UniversalPlaytimeJSONContext.Default, true);
                byte[] dataByte = Encoding.UTF8.GetBytes(data);
#if DEBUG
                LogWriteLine($"Saved Playtime:\r\n{data}", LogType.Debug, true);
#endif
                _registryRoot.SetValue(_StatsValueName, dataByte, RegistryValueKind.Binary);
                _registryRoot.SetValue(_TotalTimeValueName, TotalPlaytime.TotalSeconds, RegistryValueKind.DWord);
                
                double? lastPlayed = (LastPlayed?.ToUniversalTime() - BaseDate)?.TotalSeconds;
                if (lastPlayed != null)
                    _registryRoot.SetValue(_LastPlayedValueName, lastPlayed, RegistryValueKind.DWord);
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

            if (!_IsDeserializing[_hashID]) Save();
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

            if (!_IsDeserializing[_hashID]) Save();
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

            if (!_IsDeserializing[_hashID]) Save();
        }

        #endregion

        #region Utility
        private static bool IsDifferentMonth(DateTime date1, DateTime date2) => date1.Year != date2.Year || date1.Month != date2.Month;

        private static bool IsDifferentWeek(DateTime date1, DateTime date2) => date1.Year != date2.Year || ISOWeek.GetWeekOfYear(date1) != ISOWeek.GetWeekOfYear(date2);
        #endregion
    }
}
