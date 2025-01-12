using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.EncTool;
using Microsoft.Win32;
using System;
using Hi3Helper.SentryHelper;
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Logger;
// ReSharper disable RedundantDefaultMemberInitializer
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo

#pragma warning disable CS0659
namespace CollapseLauncher.GameSettings.StarRail
{
    internal class SFXVolume : IGameSettingsValue<SFXVolume>
    {
        #region Fields
        private const string _ValueName = "AudioSettings_SFXVolume_h2753520268";
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
        public static SFXVolume Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = RegistryRoot.GetValue(_ValueName, null);
                if (value != null)
                {
                    int sfxVolume = (int)value;
#if DEBUG
                    LogWriteLine($"Loaded StarRail Settings: {_ValueName} : {value}", LogType.Debug, true);
#endif
                    return new SFXVolume { SFXVol = sfxVolume };
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
            return new SFXVolume();
        }

        public void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");
                RegistryRoot.SetValue(_ValueName, SFXVol, RegistryValueKind.DWord);
#if DEBUG
                LogWriteLine($"Saved StarRail Settings: {_ValueName} : {SFXVol}", LogType.Debug, true);
#endif
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(new Exception($"Failed to save {_ValueName}!", ex), SentryHelper.ExceptionType.UnhandledOther);
            }

        }

        public override bool Equals(object? comparedTo) => comparedTo is SFXVolume toThis && TypeExtensions.IsInstancePropertyEqual(this, toThis);
        #endregion
    }
}
