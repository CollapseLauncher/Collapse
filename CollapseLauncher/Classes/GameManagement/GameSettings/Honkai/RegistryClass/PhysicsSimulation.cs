﻿using CollapseLauncher.Interfaces;
using Hi3Helper;
using System;
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Logger;

namespace CollapseLauncher.GameSettings.Honkai
{
    internal class PhysicsSimulation : IGameSettingsValue<PhysicsSimulation>
    {
        #region Fields
        private const string _ValueName = "GENERAL_DATA_V2_UsePhysicsSimulation_h1109146915";
        #endregion

        #region Properties
        /// <summary>
        /// This defines if "<c>Physics</c>" toggle in "More" settings is enabled or not.<br/>
        /// </summary>
        public int UsePhysicsSimulation { get; set; }

        public bool PhysicsSimulationBool
        {
            get => UsePhysicsSimulation == 1;
            set => UsePhysicsSimulation = value ? 1 : 2;
        }
        #endregion
        
        #region Methods
        #nullable enable
        public static PhysicsSimulation Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = RegistryRoot.GetValue(_ValueName, null);
                if (value != null)
                {
                    int physicsSimulation = (int)value;
                    #if DEBUG
                    LogWriteLine($"Loaded HI3 Settings: {_ValueName} : {value}", LogType.Debug, true);
                    #endif

                    return new PhysicsSimulation { UsePhysicsSimulation = physicsSimulation };
                }
            }
            catch ( Exception ex )
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

            return new PhysicsSimulation();
        }

        public void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");
                RegistryRoot.SetValue(_ValueName, UsePhysicsSimulation, Microsoft.Win32.RegistryValueKind.DWord);
                #if DEBUG
                LogWriteLine($"Saved HI3 Settings: {_ValueName} : {UsePhysicsSimulation}", LogType.Debug, true);
                #endif
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
            }
        }

        public bool Equals(PhysicsSimulation? comparedTo) => UsePhysicsSimulation == comparedTo?.UsePhysicsSimulation;

        #endregion
    }
}