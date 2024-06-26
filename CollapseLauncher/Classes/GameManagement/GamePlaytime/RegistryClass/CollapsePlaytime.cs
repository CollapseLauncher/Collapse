using Hi3Helper;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using static Hi3Helper.Logger;

namespace CollapseLauncher.GamePlaytime
{
    internal class CollapsePlaytime
    {
        #region Fields
        private static DateTime BaseDate => new(2012, 2, 13, 0, 0, 0, DateTimeKind.Utc);

        private const string _ValueName              = "CollapseLauncher_Playtime";
        private const string _OldLastPlayedValueName = "CollapseLauncher_LastPlayed";

        private static Dictionary<int, bool> _IsDeserializing = [];
        private        RegistryKey           _registryRoot;
        private        int                   _hashID;

        #endregion

        #region Properties
        /// <summary>
        /// Represents the total time a game was played.<br/><br/>
        /// Default: TimeSpan.Zero
        /// </summary>
        public TimeSpan TotalPlaytime { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Represents the total time the last/current session lasted.<br/><br/>
        /// Default: TimeSpan.Zero
        /// </summary>
        public TimeSpan LastSession { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Represents the daily playtime.<br/>
        /// The ControlDate field is used to check if this value should be reset.<br/><br/>
        /// Default: TimeSpan.Zero
        /// </summary>
        public TimeSpan DailyPlaytime { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Represents the weekly playtime.<br/>
        /// The ControlDate field is used to check if this value should be reset.<br/><br/>
        /// Default: TimeSpan.Zero
        /// </summary>
        public TimeSpan WeeklyPlaytime { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Represents the monthly playtime.<br/>
        /// The ControlDate field is used to check if this value should be reset.<br/><br/>
        /// Default: TimeSpan.Zero
        /// </summary>
        public TimeSpan MonthlyPlaytime { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Represents the last time the game was launched.<br/><br/>
        /// Default: null
        /// </summary>
        public DateTime? LastPlayed { get; set; } = null;

        /// <summary>
        /// Represents a control date.<br/>
        /// This date is used to check if a specific playtime statistic should be reset.<br/><br/>
        /// Default: DateTime.Today
        /// </summary>
        public DateTime ControlDate { get; set; } = DateTime.Today;
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
                if (root == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = root.GetValue(_ValueName, null);

                // The value being an int means this value was saved by the old implementaion and represents the total number of seconds played.
                if (value is int oldPlaytime)
                {
                    object? lastPlayed = root.GetValue(_OldLastPlayedValueName, null);
                    root.DeleteValue(_OldLastPlayedValueName, false);

                    LogWriteLine($"Found old Playtime RegistryKey! Converting to the new format... (Playtime: {oldPlaytime} | Last Played: {lastPlayed})", writeToLog: true);
                    _IsDeserializing[hashID] = false;
                    
                    CollapsePlaytime playtime = new CollapsePlaytime()
                    {
                        TotalPlaytime = TimeSpan.FromSeconds(oldPlaytime),
                        LastPlayed = lastPlayed != null ? BaseDate.AddSeconds((int)lastPlayed) : null,
                        _registryRoot = root,
                        _hashID = hashID
                    };
                    playtime.Save();

                    return playtime;
                }

                if (value != null)
                {
                    ReadOnlySpan<byte> byteStr = (byte[])value;
#if DEBUG
                    LogWriteLine($"Loaded Playtime:\r\n{Encoding.UTF8.GetString(byteStr.TrimEnd((byte)0))}", LogType.Debug, true);
#endif
                    CollapsePlaytime playtime = byteStr.Deserialize<CollapsePlaytime>(UniversalPlaytimeJSONContext.Default) ?? new CollapsePlaytime();
                    playtime._registryRoot = root;
                    playtime._hashID = hashID;

                    return playtime;
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {_ValueName}\r\n{ex}", LogType.Error, true);
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
                if (_registryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");

                string data = this.Serialize(UniversalPlaytimeJSONContext.Default, true);
                byte[] dataByte = Encoding.UTF8.GetBytes(data);
#if DEBUG
                LogWriteLine($"Saved Playtime:\r\n{data}", LogType.Debug, true);
#endif
                _registryRoot.SetValue(_ValueName, dataByte, RegistryValueKind.Binary);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
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
            
            if (today == DateTime.Today)
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
