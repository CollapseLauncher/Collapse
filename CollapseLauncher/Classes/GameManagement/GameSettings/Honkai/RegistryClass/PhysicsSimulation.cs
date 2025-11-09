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

#pragma warning disable CS0659
namespace CollapseLauncher.GameSettings.Honkai
{
    internal class PhysicsSimulation : IGameSettingsValue<PhysicsSimulation>
    {
        #region Fields
        private const string ValueName = "GENERAL_DATA_V2_UsePhysicsSimulation_h1109146915";

        [JsonIgnore]
        public IGameSettings ParentGameSettings { get; }

        private PhysicsSimulation(IGameSettings gameSettings)
        {
            ParentGameSettings = gameSettings;
        }
        #endregion

        #region Properties
        /// <summary>
        /// This defines if "<c>Physics</c>" toggle in "More" settings is enabled or not.<br/>
        /// </summary>
        public int UsePhysicsSimulation { get; private set; }

        public bool PhysicsSimulationBool
        {
            get => UsePhysicsSimulation == 1;
            set => UsePhysicsSimulation = value ? 1 : 2;
        }
        #endregion
        
        #region Methods
        #nullable enable
        public static PhysicsSimulation Load(IGameSettings gameSettings)
        {
            try
            {
                if (gameSettings.RegistryRoot == null) throw new NullReferenceException($"Cannot load {ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = gameSettings.RegistryRoot.TryGetValue(ValueName, null, gameSettings.RefreshRegistryRoot);
                if (value != null)
                {
                    int physicsSimulation = (int)value;
                    #if DEBUG
                    LogWriteLine($"Loaded HI3 Settings: {ValueName} : {value}", LogType.Debug, true);
                    #endif

                    return new PhysicsSimulation(gameSettings) { UsePhysicsSimulation = physicsSimulation };
                }
            }
            catch ( Exception ex )
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

            return new PhysicsSimulation(gameSettings);
        }

        public void Save()
        {
            try
            {
                if (ParentGameSettings.RegistryRoot == null) throw new NullReferenceException($"Cannot save {ValueName} since RegistryKey is unexpectedly not initialized!");
                ParentGameSettings.RegistryRoot.SetValue(ValueName, UsePhysicsSimulation, RegistryValueKind.DWord);
                #if DEBUG
                LogWriteLine($"Saved HI3 Settings: {ValueName} : {UsePhysicsSimulation}", LogType.Debug, true);
                #endif
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {ValueName}!\r\n{ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(new Exception($"Failed to save {ValueName}!", ex), SentryHelper.ExceptionType.UnhandledOther);
            }
        }

        public override bool Equals(object? comparedTo) => comparedTo is PhysicsSimulation toThis && TypeExtensions.IsInstancePropertyEqual(this, toThis);
        #endregion
    }
}