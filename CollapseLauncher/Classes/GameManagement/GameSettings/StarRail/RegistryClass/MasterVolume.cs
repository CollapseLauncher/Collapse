using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.StarRail;
using CollapseLauncher.GameSettings.StarRail.Context;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Screen;
using Microsoft.Win32;
using System;
using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static CollapseLauncher.GameSettings.Statics;
using static Hi3Helper.Logger;

namespace CollapseLauncher.GameSettings.StarRail
{
    internal class MasterVolume : IGameSettingsValue<MasterVolume>
    {
        #region Fields
        private const string _ValueName = "AudioSettings_MasterVolume_h1622207037";
        #endregion

        #region Properties
        /// <summary>
        /// This defines "<c>Master Volume</c>" slider in-game setting
        /// Range: 0 - 10
        /// Default: 10
        /// </summary>
        public float MasterVol { get; set; } = 10;

        #endregion

        #region Methods
#nullable enable
        public static MasterVolume Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = RegistryRoot.GetValue(_ValueName);
                LogWriteLine($"Loaded {_ValueName} with the value of {value}");
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {_ValueName}\r\n{ex}", LogType.Error, true);
            }
            return new MasterVolume();
        }

        public void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");
                RegistryRoot?.SetValue(_ValueName, MasterVol, RegistryValueKind.DWord);
                LogWriteLine($"Saved {_ValueName} with {MasterVol}", LogType.Default, true);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
            }

        }

        public bool Equals(MasterVolume? comparedTo)
        {
            if (ReferenceEquals(this, comparedTo)) return true;
            if (comparedTo == null) return false;

            return comparedTo.MasterVol == this.MasterVol;
        }
#nullable disable
        #endregion
    }
}
