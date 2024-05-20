using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.EncTool;
using Microsoft.Win32;
using System;
using System.Text;
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Logger;
// ReSharper disable RedundantDefaultMemberInitializer
// ReSharper disable RedundantArgumentDefaultValue

namespace CollapseLauncher.GameSettings.Universal
{
    internal class CollapseMiscSetting : IGameSettingsValue<CollapseMiscSetting>
    {
        #region Fields
        private const string _ValueName = "CollapseLauncher_Misc";

        private bool _UseCustomArguments = true;

        private static bool _IsDeserializing;
        #endregion

        #region Properties
        /// <summary>
        /// This defines if the game's main process should be run in Above Normal priority.<br/><br/>
        /// Default: false
        /// </summary>
        public bool UseGameBoost { get; set; } = false;

        /// <summary>
        /// This defines if the game is launched with user provided Custom Launch Argument.<br/><br/>
        /// Default: true
        /// </summary>
        public bool UseCustomArguments
        {
            get => _UseCustomArguments;
            set
            {
                _UseCustomArguments = value;
                // Stop saving if Load() is not yet done.
                if (!_IsDeserializing) Save();
            }
        }

        /// <summary>
        /// This defines if Advanced Game Settings should be shown in respective GSP and used.<br/><br/>
        /// Default: false
        /// </summary>
        public bool UseAdvancedGameSettings { get; set; } = false;

        /// <summary>
        /// This control if GamePreLaunchCommand is going to be used. <br/><br/>
        /// Default: false
        /// </summary>
        public bool UseGamePreLaunchCommand { get; set; } = false;

        /// <summary>
        /// This sets the command that is going to be launched before the game process is invoked.<br/><br/>
        /// Command is launched as a shell with no window.<br/><br/>
        /// </summary>
        public string GamePreLaunchCommand { get; set; } = "";

        /// <summary>
        /// Close GamePreLaunch process when game is stopped.<br/><br/>
        /// </summary>
        public bool GamePreLaunchExitOnGameStop { get; set; } = false;

        /// <summary>
        /// Delay game launch when using pre-launch command.<br/><br/>
        /// Value in ms.
        /// </summary>
        public int GameLaunchDelay { get; set; } = 0;

        /// <summary>
        /// This control if GamePostLaunchCommand is going to be used. <br/><br/>
        /// Default: false
        /// </summary>
        public bool UseGamePostExitCommand { get; set; } = false;

        /// <summary>
        /// This sets the command that is going to be launched after the game process is closed.<br/><br/>
        /// Command is launched as a shell with no window.<br/><br/>
        /// </summary>
        public string GamePostExitCommand { get; set; } = "";

        /// <summary>
        /// Use mobile layout. Currently only available for Genshin and StarRail.
        /// </summary>
        public bool LaunchMobileMode { get; set; } = false;
        #endregion

        #region Methods
#nullable enable
        public static CollapseMiscSetting Load()
        {
            try
            {
                _IsDeserializing = true;
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = RegistryRoot.GetValue(_ValueName, null);

                if (value != null)
                {
                    ReadOnlySpan<byte> byteStr = (byte[])value;
                    #if DEBUG
                    LogWriteLine($"Loaded Collapse Misc Settings:\r\n{Encoding.UTF8.GetString(byteStr.TrimEnd((byte)0))}", LogType.Debug, true);
                    #endif
                    return byteStr.Deserialize<CollapseMiscSetting>(UniversalSettingsJSONContext.Default) ?? new CollapseMiscSetting();
                }
            }
            catch ( Exception ex )
            {
                LogWriteLine($"Failed while reading {_ValueName}\r\n{ex}", LogType.Error, true);
            }
            finally
            {
                _IsDeserializing = false;
            }

            return new CollapseMiscSetting();
        }

        public void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");

                string data = this.Serialize(UniversalSettingsJSONContext.Default, true);
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