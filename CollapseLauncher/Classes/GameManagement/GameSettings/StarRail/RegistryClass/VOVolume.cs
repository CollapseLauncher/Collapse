using CollapseLauncher.Interfaces;
using Hi3Helper;
using Microsoft.Win32;
using System;
using static CollapseLauncher.GameSettings.Statics;
using static Hi3Helper.Logger;

namespace CollapseLauncher.GameSettings.StarRail
{
    internal class VOVolume : IGameSettingsValue<VOVolume>
    {
        #region Fields
        private const string _ValueName = "AudioSettings_VOVolume_h805685304";
        #endregion

        #region Properties
        /// <summary>
        /// This defines "<c>Voice Over Volume</c>" slider in-game setting
        /// Range: 0 - 10
        /// Default: 10
        /// </summary>
        public int VOVol { get; set; } = 10;

        #endregion

        #region Methods
#nullable enable
        public static VOVolume Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = RegistryRoot.GetValue(_ValueName, null);
                if (value != null)
                {
                    int voVolume = (int)value;
#if DEBUG
                    LogWriteLine($"Loaded StarRail Settings: {_ValueName} : {value}", LogType.Debug, true);
#endif
                    return new VOVolume { VOVol = voVolume };
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {_ValueName}\r\n{ex}", LogType.Error, true);
            }
            return new VOVolume();
        }

        public void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");
                RegistryRoot?.SetValue(_ValueName, VOVol, RegistryValueKind.DWord);
#if DEBUG
                LogWriteLine($"Saved StarRail Settings: {_ValueName} : {VOVol}", LogType.Debug, true);
#endif
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
            }

        }

        public bool Equals(VOVolume? comparedTo) => TypeExtensions.IsInstancePropertyEqual(this, comparedTo);
        #endregion
    }
}
