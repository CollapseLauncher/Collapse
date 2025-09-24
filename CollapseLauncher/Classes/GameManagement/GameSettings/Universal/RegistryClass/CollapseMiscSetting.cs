using CollapseLauncher.Extension;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.SentryHelper;
using Microsoft.Win32;
using System;
using System.Text;
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Logger;
// ReSharper disable RedundantDefaultMemberInitializer
// ReSharper disable RedundantArgumentDefaultValue
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable InconsistentNaming
// ReSharper disable CommentTypo

#pragma warning disable CS0659
namespace CollapseLauncher.GameSettings.Universal
{
    internal class CollapseMiscSetting : IGameSettingsValue<CollapseMiscSetting>
    {
        #region Fields
        private const string ValueName = "CollapseLauncher_Misc";

        private static bool _isDeserializing;
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
            get;
            set
            {
                field = value;
                // Stop saving if Load() is not yet done.
                if (!_isDeserializing) Save();
            }
        } = true;

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

        /// <summary>
        /// Set custom background for given region for given game.
        /// </summary>
        public bool UseCustomRegionBG
        {
            get;
            set
            {
                field = value;
                if (!_isDeserializing) Save();
            }
        } = false;

    #nullable enable
        /// <summary>
        /// The path of the custom BG for each region
        /// </summary>
        public string? CustomRegionBGPath { get; set; }

        /// <summary>
        /// Determines if the game playtime should be synced to the database
        /// </summary>
        public bool IsSyncPlaytimeToDatabase { get; set; } = true;
        
        /// <summary>
        /// Set per-region Discord Rich Presence setting for playing status
        /// </summary>
        public bool IsPlayingRpc { get; set; } = true;

        #endregion

        #region Methods

        public static CollapseMiscSetting Load()
        {
            try
            {
                _isDeserializing = true;
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = RegistryRoot.TryGetValue(ValueName, null, RefreshRegistryRoot);

                if (value != null)
                {
                    ReadOnlySpan<byte> byteStr = (byte[])value;
                    #if DEBUG
                    LogWriteLine($"Loaded Collapse Misc Settings:\r\n{Encoding.UTF8.GetString(byteStr.TrimEnd((byte)0))}", LogType.Debug, true);
                    #endif
                    return byteStr.Deserialize(UniversalSettingsJsonContext.Default.CollapseMiscSetting) ?? new CollapseMiscSetting();
                }
            }
            catch ( Exception ex )
            {
                LogWriteLine($"Failed while reading {ValueName}\r\n{ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(new Exception($"Failed to read {ValueName}!", ex), SentryHelper.ExceptionType.UnhandledOther);
            }
            finally
            {
                _isDeserializing = false;
            }

            return new CollapseMiscSetting();
        }

        public void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {ValueName} since RegistryKey is unexpectedly not initialized!");

                string data = this.Serialize(UniversalSettingsJsonContext.Default.CollapseMiscSetting, true);
                byte[] dataByte = Encoding.UTF8.GetBytes(data);
#if DEBUG
                LogWriteLine($"Saved Collapse Misc Settings:\r\n{data}", LogType.Debug, true);
#endif
                RegistryRoot.SetValue(ValueName, dataByte, RegistryValueKind.Binary);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {ValueName}!\r\n{ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(new Exception($"Failed to save {ValueName}!", ex), SentryHelper.ExceptionType.UnhandledOther);
            }
        }

        public override bool Equals(object? comparedTo) => comparedTo is CollapseMiscSetting toThis && TypeExtensions.IsInstancePropertyEqual(this, toThis);
        #endregion
    }
}