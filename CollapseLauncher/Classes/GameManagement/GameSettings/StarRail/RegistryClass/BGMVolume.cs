using CollapseLauncher.Interfaces;
using Hi3Helper;
using Microsoft.Win32;
using System;
using System.Text;
using static CollapseLauncher.GameSettings.Statics;
using static Hi3Helper.Logger;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CollapseLauncher.GameSettings.StarRail
{
    internal class BGMVolume : IGameSettingsValue<BGMVolume>
    {
        #region Fields
        private const string _ValueName = "AudioSettings_BGMVolume_h240914409";
        #endregion

        #region Properties
        /// <summary>
        /// This defines "<c>BGM Volume</c>" slider in-game setting
        /// Range: 0 - 10
        /// Default: 10
        /// </summary>
        public int BGMVol { get; set; } = 10;

        #endregion

        #region Methods
#nullable enable
        public static BGMVolume Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = RegistryRoot.GetValue(_ValueName, null);
                if (value != null)
                {
                    int bgmVolume = (int)value;
#if DEBUG
                    LogWriteLine($"Loaded StarRail Settings: {_ValueName} : {value}", LogType.Debug, true);
#endif
                    return new BGMVolume { BGMVol = bgmVolume };
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {_ValueName}\r\n{ex}", LogType.Error, true);
            }
            return new BGMVolume();
        }

        public void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");
                RegistryRoot?.SetValue(_ValueName, BGMVol, RegistryValueKind.DWord);
#if DEBUG
                LogWriteLine($"Saved StarRail Settings: {_ValueName} : {BGMVol}", LogType.Debug, true);
#endif
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
            }

        }

        public bool Equals(BGMVolume? comparedTo)
        {
            if (ReferenceEquals(this, comparedTo)) return true;
            if (comparedTo == null) return false;

            return comparedTo.BGMVol == this.BGMVol;
        }
#nullable disable
        #endregion
    }
}
