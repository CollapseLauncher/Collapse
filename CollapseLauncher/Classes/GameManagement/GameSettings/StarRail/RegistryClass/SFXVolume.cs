using CollapseLauncher.Extension;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.SentryHelper;
using Microsoft.Win32;
using System;
using System.Text.Json.Serialization;
using static Hi3Helper.Logger;
// ReSharper disable RedundantDefaultMemberInitializer
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable InconsistentNaming

#pragma warning disable CS0659
namespace CollapseLauncher.GameSettings.StarRail
{
    internal class SFXVolume : IGameSettingsValue<SFXVolume>
    {
        #region Fields
        private const string ValueName = "AudioSettings_SFXVolume_h2753520268";

        [JsonIgnore]
        public IGameSettings ParentGameSettings { get; }

        private SFXVolume(IGameSettings gameSettings)
        {
            ParentGameSettings = gameSettings;
        }

        #endregion

        #region Properties
        /// <summary>
        /// This defines "<c>SFX Volume</c>" slider in-game setting
        /// Range: 0 - 10
        /// Default: 10
        /// </summary>
        public int SFXVol { get; set; } = 10;

        #endregion

        #region Methods
#nullable enable
        public static SFXVolume Load(IGameSettings gameSettings)
        {
            try
            {
                if (gameSettings.RegistryRoot == null) throw new NullReferenceException($"Cannot load {ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = gameSettings.RegistryRoot.TryGetValue(ValueName, null, gameSettings.RefreshRegistryRoot);
                if (value != null)
                {
                    int sfxVolume = (int)value;
#if DEBUG
                    LogWriteLine($"Loaded StarRail Settings: {ValueName} : {value}", LogType.Debug, true);
#endif
                    return new SFXVolume(gameSettings) { SFXVol = sfxVolume };
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {ValueName}" +
                             $"\r\n  Please open the game and change any Graphics Settings, then close normally. After that you can use this feature." +
                             $"\r\n  If the issue persist, please report it on GitHub" +
                             $"\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(new Exception(
                    $"Failed when reading game settings {ValueName}\r\n" +
                    $"Please open the game and change any graphics settings, then safely close the game. If the problem persist, report the issue on our GitHub\r\n" +
                    $"{ex}", ex));
            }
            return new SFXVolume(gameSettings);
        }

        public void Save()
        {
            try
            {
                if (ParentGameSettings.RegistryRoot == null) throw new NullReferenceException($"Cannot save {ValueName} since RegistryKey is unexpectedly not initialized!");
                ParentGameSettings.RegistryRoot.SetValue(ValueName, SFXVol, RegistryValueKind.DWord);
#if DEBUG
                LogWriteLine($"Saved StarRail Settings: {ValueName} : {SFXVol}", LogType.Debug, true);
#endif
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {ValueName}!\r\n{ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(new Exception($"Failed to save {ValueName}!", ex), SentryHelper.ExceptionType.UnhandledOther);
            }

        }

        public override bool Equals(object? comparedTo) => comparedTo is SFXVolume toThis && TypeExtensions.IsInstancePropertyEqual(this, toThis);
        #endregion
    }
}
