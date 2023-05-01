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
    internal class BGMVolume : IGameSettingsValue<BGMVolume>
    {
        #region Fields
        private const string _ValueName = "AudioSettings_BGMVolume_h240914409";
        private const string _ValueNameMasterVolume = "AudioSettings_MasterVolume_h1622207037";
        private const string _ValueNameSFXVolume = "AudioSettings_SFXVolume_h2753520268";
        private const string _ValueNameVOVolume = "AudioSettings_VOVolume_h805685304";
        #endregion

        #region Properties
        /// <summary>
        /// This defines "<c>BGM Volume</c>" slider in-game setting
        /// Range: 0 - 10
        /// Default: 10
        /// </summary>
        public int bgmvol { get; set; } = 10;

        #endregion

        #region Methods
#nullable enable
        public static BGMVolume Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = RegistryRoot.GetValue(_ValueName, null);
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
                RegistryRoot?.SetValue(_ValueName, bgmvol, RegistryValueKind.DWord);
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

            return comparedTo.bgmvol == this.bgmvol;
        }
#nullable disable
        #endregion
    }
}
