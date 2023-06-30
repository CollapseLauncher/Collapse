﻿using CollapseLauncher.Interfaces;
using Hi3Helper;
using Microsoft.Win32;
using System;
using static CollapseLauncher.GameSettings.Statics;
using static Hi3Helper.Logger;

namespace CollapseLauncher.GameSettings.Universal
{
    public class CustomArgs : IGameSettingsValue<CustomArgs>
    {
        #region Fields
        private const string _ValueName = "CollapseLauncher_CustomArgs";
        private string _CustomArgumentValue = "";
        #endregion

        #region Properties
        /// <summary>
        /// Default: (empty)
        /// </summary>
        public string CustomArgumentValue
        {
            get => _CustomArgumentValue;
            set
            {
                _CustomArgumentValue = value;
                Save();
            }
        }
        #endregion

        #region Methods
#nullable enable
        public static CustomArgs Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");
#if DEBUG
                LogWriteLine($"Loaded Collapse Custom Argument Settings:\r\n{(string?)RegistryRoot.GetValue(_ValueName, null) ?? ""}", LogType.Debug, true);
#endif
                return new CustomArgs { CustomArgumentValue = (string?)RegistryRoot.GetValue(_ValueName, null) ?? "" };
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
#if DEBUG
                LogWriteLine($"Saved Collapse Custom Argument Settings:\r\n{CustomArgumentValue}", LogType.Debug, true);
#endif
                RegistryRoot.SetValue(_ValueName, CustomArgumentValue, RegistryValueKind.String);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
            }
        }

        public bool Equals(CustomArgs? comparedTo)
        {
            if (ReferenceEquals(this, comparedTo)) return true;
            if (comparedTo == null) return false;

            return comparedTo.CustomArgumentValue == this.CustomArgumentValue;
        }
#nullable disable
        #endregion
    }
}
