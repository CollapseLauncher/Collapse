using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.EncTool;
using Microsoft.Win32;
using System;
using System.Text;
using System.Text.Json;
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Logger;

namespace CollapseLauncher.GameSettings.Universal
{ 
    internal class CollapseMiscSetting : IGameSettingsValue<CollapseMiscSetting>
    {
        #region Fields
        private const string _ValueName = "CollapseLauncher_Misc";
        #endregion

        #region Properties
        /// <summary>
        /// This defines if the game's main process should be run in Above Normal priority.<br/><br/>
        /// Default: false
        /// </summary>
        public bool UseGameBoost { get; set; } = false;
        #endregion

        #region Methods
#nullable enable
        public static CollapseMiscSetting Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = RegistryRoot.GetValue(_ValueName, null);

                if (value != null)
                {
                    ReadOnlySpan<byte> byteStr = (byte[])value;
#if DEBUG
                    LogWriteLine($"Loaded Collapse Misc Settings:\r\n{Encoding.UTF8.GetString((byte[])value, 0, ((byte[])value).Length - 1)}", LogType.Debug, true);
#endif
                    return (CollapseMiscSetting?)JsonSerializer.Deserialize(byteStr.Slice(0, byteStr.Length - 1), typeof(CollapseMiscSetting), UniversalSettingsJSONContext.Default) ?? new CollapseMiscSetting();
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {_ValueName}\r\n{ex}", LogType.Error, true);
            }

            return new CollapseMiscSetting();
        }

        public void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");

                string data = JsonSerializer.Serialize(this, typeof(CollapseMiscSetting), UniversalSettingsJSONContext.Default) + '\0';
                byte[] dataByte = Encoding.UTF8.GetBytes(data);
#if DEBUG
                LogWriteLine($"Saved Collapse Misc Settings:\r\n{data}", LogType.Debug, true);
#endif
                RegistryRoot.SetValue(_ValueName, dataByte, RegistryValueKind.Binary);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
            }
        }

        public bool Equals(CollapseMiscSetting? comparedTo) => TypeExtensions.IsInstancePropertyEqual(this, comparedTo);
        #endregion
    }
}
