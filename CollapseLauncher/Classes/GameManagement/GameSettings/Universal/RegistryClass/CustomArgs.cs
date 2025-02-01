using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Microsoft.Win32;
using System;
using static Hi3Helper.Logger;
// ReSharper disable RedundantDefaultMemberInitializer
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo

#pragma warning disable CS0659
namespace CollapseLauncher.GameSettings.Universal
{
    public class CustomArgs : IGameSettingsValue<CustomArgs>
    {
        #region Fields
        private const string ValueName = "CollapseLauncher_CustomArgs";

        #endregion

        #region Properties

        /// <summary>
        /// Default: (empty)
        /// </summary>
        public string CustomArgumentValue
        {
            get;
            set
            {
                field = value;
                Save();
            }
        } = "";

        #endregion

        #region Methods
#nullable enable
        public static CustomArgs Load()
        {
            try
            {
                if (SettingsBase.RegistryRoot == null) throw new NullReferenceException($"Cannot load {ValueName} RegistryKey is unexpectedly not initialized!");
#if DEBUG
                LogWriteLine($"Loaded Collapse Custom Argument Settings:\r\n{(string?)SettingsBase.RegistryRoot.GetValue(ValueName, null) ?? ""}", LogType.Debug, true);
#endif
                return new CustomArgs { CustomArgumentValue = (string?)SettingsBase.RegistryRoot.GetValue(ValueName, null) ?? "" };
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {ValueName}\r\n{ex}", LogType.Error, true);
                throw;
            }
        }

        public void Save()
        {
            try
            {
                if (SettingsBase.RegistryRoot == null) throw new NullReferenceException($"Cannot save {ValueName} since RegistryKey is unexpectedly not initialized!");
#if DEBUG
                LogWriteLine($"Saved Collapse Custom Argument Settings:\r\n{CustomArgumentValue}", LogType.Debug, true);
#endif
                SettingsBase.RegistryRoot.SetValue(ValueName, CustomArgumentValue, RegistryValueKind.String);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {ValueName}!\r\n{ex}", LogType.Error, true);
            }
        }

        public override bool Equals(object? comparedTo)
        {
            if (ReferenceEquals(this, comparedTo)) return true;
            if (comparedTo is not CustomArgs customArgsCompare) return false;

            return customArgsCompare.CustomArgumentValue == CustomArgumentValue;
        }
        #endregion
    }
}
