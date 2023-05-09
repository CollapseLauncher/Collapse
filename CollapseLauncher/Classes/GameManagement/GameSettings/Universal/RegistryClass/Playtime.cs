using CollapseLauncher.Interfaces;
using Hi3Helper;
using Microsoft.Win32;
using System;
using static CollapseLauncher.GameSettings.Statics;
using static Hi3Helper.Logger;

namespace CollapseLauncher.GameSettings.Universal
{
    public class Playtime : IGameSettingsValue<Playtime>
    {
        #region Fields
        private const string _ValueName = "CollapseLauncher_Playtime";
        private string _PlaytimeValue = "0h:0m";
        #endregion

        #region Properties
        /// <summary>
        /// Default: (empty)
        /// </summary>
        public string PlaytimeValue
        {
            get => _PlaytimeValue;
            set
            {
                _PlaytimeValue = value;
                Save();
            }
        }
        #endregion

        #region Methods
#nullable enable
        public static Playtime Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");

                return new Playtime { PlaytimeValue = (string?)RegistryRoot.GetValue(_ValueName, null) ?? "" };
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {_ValueName}\r\n{ex}", LogType.Error, true);
                throw;
            }
        }

        public void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");

                RegistryRoot.SetValue(_ValueName, PlaytimeValue, RegistryValueKind.String);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
            }
        }

        public bool Equals(Playtime? comparedTo)
        {
            if (ReferenceEquals(this, comparedTo)) return true;
            if (comparedTo == null) return false;

            return comparedTo.PlaytimeValue == this.PlaytimeValue;
        }
#nullable disable
        #endregion
    }
}
