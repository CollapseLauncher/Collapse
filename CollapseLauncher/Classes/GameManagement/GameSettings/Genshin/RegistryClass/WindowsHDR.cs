﻿using Hi3Helper;
using Hi3Helper.EncTool;
using Microsoft.Win32;
using System;
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Logger;

namespace CollapseLauncher.GameSettings.Genshin
{
    internal class WindowsHDR
    {
        #region Fields
        private const string _ValueName = "WINDOWS_HDR_ON_h3132281285";
        #endregion

        #region Properties
        /// <summary>
        /// This defines "<c>HDR</c>" native settings. No in-game switch available yet.<br/><br/>
        /// Range: 0 - 1
        /// Default: 0
        /// </summary>
        public int HDR { get; set; } = 0;

        /// <summary>
        /// Converted value from HDR integer inside WINDOWS_HDR_ON registry to usable boolean.
        /// </summary>
        public bool isHDR { get => HDR == 1; set => HDR = value ? 1 : 0; }
        #endregion

        #region Methods
#nullable enable
        public static WindowsHDR Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} since RegistryRoot is unexpectedly not initialized!");

                object? value = RegistryRoot.GetValue(_ValueName, null);
                if (value != null)
                {
                    int hdr = (int)value;
#if DEBUG
                    LogWriteLine($"Loaded Genshin Settings: {_ValueName} : {value}", LogType.Debug, true);
#endif 
                    return new WindowsHDR { HDR = hdr };
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {_ValueName}" +
                             $"\r\n  Please open the game and change any Graphics Settings, then close normally. After that you can use this feature." +
                             $"\r\n  If the issue persist, please report it on GitHub" +
                             $"\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(new Exception(
                    $"Failed when reading game settings {_ValueName}\r\n" +
                    $"Please open the game and change any graphics settings, then safely close the game. If the problem persist, report the issue on our GitHub\r\n" +
                    $"{ex}", ex));
            }
            return new WindowsHDR();
        }

        public void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");
                RegistryRoot?.SetValue(_ValueName, HDR, RegistryValueKind.DWord);
#if DEBUG
                LogWriteLine($"Saved Genshin Settings: {_ValueName} : {HDR}", LogType.Debug, true);
#endif
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
            }
        }

        public bool Equals(WindowsHDR? comparedTo) => TypeExtensions.IsInstancePropertyEqual(this, comparedTo);
        #endregion
    }
}
