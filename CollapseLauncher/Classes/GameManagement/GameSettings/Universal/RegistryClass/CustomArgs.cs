using CollapseLauncher.Extension;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Microsoft.Win32;
using System;
using System.Text.Json.Serialization;
using static Hi3Helper.Logger;
// ReSharper disable RedundantDefaultMemberInitializer
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo

#pragma warning disable CS0659
namespace CollapseLauncher.GameSettings.Universal
{
    internal class CustomArgs : IGameSettingsValue<CustomArgs>
    {
        #region Fields
        private const string ValueName = "CollapseLauncher_CustomArgs";

        [JsonIgnore]
        public IGameSettings ParentGameSettings { get; }

        public CustomArgs() : this(null)
        {

        }

        private CustomArgs(IGameSettings gameSettings)
        {
            ParentGameSettings = gameSettings;
        }
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
        public static CustomArgs Load(IGameSettings gameSettings)
        {
            try
            {
                if (gameSettings.RegistryRoot == null) throw new NullReferenceException($"Cannot load {ValueName} RegistryKey is unexpectedly not initialized!");
#if DEBUG
                LogWriteLine($"Loaded Collapse Custom Argument Settings:\r\n{(string?)gameSettings.RegistryRoot.TryGetValue(ValueName, null, gameSettings.RefreshRegistryRoot) ?? ""}", LogType.Debug, true);
#endif
                return new CustomArgs(gameSettings) { CustomArgumentValue = (string?)gameSettings.RegistryRoot.TryGetValue(ValueName, null, gameSettings.RefreshRegistryRoot) ?? "" };
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
                if (ParentGameSettings.RegistryRoot == null) throw new NullReferenceException($"Cannot save {ValueName} since RegistryKey is unexpectedly not initialized!");
#if DEBUG
                LogWriteLine($"Saved Collapse Custom Argument Settings:\r\n{CustomArgumentValue}", LogType.Debug, true);
#endif
                ParentGameSettings.RegistryRoot.SetValue(ValueName, CustomArgumentValue, RegistryValueKind.String);
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
