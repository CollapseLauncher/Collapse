using CollapseLauncher.Interfaces;
using Hi3Helper;
using Microsoft.Win32;
using System;
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
        public int MasterVol { get; set; } = 10;

        #endregion

        #region Methods
#nullable enable
        public static MasterVolume Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = RegistryRoot.GetValue(_ValueName, null);
                if (value != null)
                {
                    int masterVolume = (int)value;
#if DEBUG
                    LogWriteLine($"Loaded StarRail Settings: {_ValueName} : {value}", LogType.Debug, true);
#endif
                    return new MasterVolume { MasterVol = masterVolume };
                }
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
#if DEBUG
                LogWriteLine($"Saved StarRail Settings: {_ValueName} : {MasterVol}", LogType.Debug, true);
#endif
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
            }

        }

        public bool Equals(MasterVolume? comparedTo) => TypeExtensions.IsInstancePropertyEqual(this, comparedTo);
        #endregion
    }
}
