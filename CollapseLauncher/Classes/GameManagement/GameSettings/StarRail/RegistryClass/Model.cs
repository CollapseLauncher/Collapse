﻿using CollapseLauncher.GameSettings.StarRail.Context;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.EncTool;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Text;
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Logger;

namespace CollapseLauncher.GameSettings.StarRail
{

    #region Enums
    public enum Quality
    {
        Off = 0,
        Custom = 0,
        VeryLow = 1,
        Low = 2,
        Medium = 3,
        High = 4,
        VeryHigh = 5
    }

    public enum AntialiasingMode
    {
        Off = 0,
        TAA = 1,
        FXAA = 2
    }
    #endregion

    internal class Model : IGameSettingsValue<Model>
    {
        #region Fields

        public const  string _ValueName       = "GraphicsSettings_Model_h2986158309";
        private const string _GraphicsQuality = "GraphicsSettings_GraphicsQuality_h523255858";
        
        public static readonly int[] FPSIndex = new int[] { 30, 60, 120 };
        public const int FPSDefaultIndex = 1; // 60 in FPSIndex[]
        public static Dictionary<int, int> FPSIndexDict = GenerateStaticFPSIndexDict();
        private static Dictionary<int, int> GenerateStaticFPSIndexDict()
        {
            Dictionary<int, int> ret = new Dictionary<int, int>();
            for (int i = 0; i < FPSIndex.Length; i++)
            {
                ret.Add(FPSIndex[i], i);
            }
            return ret;
        }
        #endregion

        #region Presets
        private static Model _VeryLowPreset = new()
        {
            FPS               = 60,
            EnableVSync       = false,
            RenderScale       = 0.8,
            ResolutionQuality = Quality.VeryLow,
            ShadowQuality     = Quality.Low,
            LightQuality      = Quality.VeryLow,
            CharacterQuality  = Quality.Low,
            EnvDetailQuality  = Quality.VeryLow,
            ReflectionQuality = Quality.VeryLow,
            SFXQuality        = Quality.VeryLow,
            BloomQuality      = Quality.VeryLow,
            AAMode            = AntialiasingMode.TAA,
            EnableMetalFXSU   = false,
        };

        private static Model _LowPreset = new()
        {
            FPS               = 60,
            EnableVSync       = true,
            RenderScale       = 1.0,
            ResolutionQuality = Quality.Low,
            ShadowQuality     = Quality.Low,
            LightQuality      = Quality.Low,
            CharacterQuality  = Quality.Low,
            EnvDetailQuality  = Quality.Low,
            ReflectionQuality = Quality.Low,
            SFXQuality        = Quality.Low,
            BloomQuality      = Quality.Low,
            AAMode            = AntialiasingMode.TAA,
            EnableMetalFXSU   = false,
        };

        private static Model _MediumPreset = new()
        {
            FPS               = 60,
            EnableVSync       = true,
            RenderScale       = 1.0,
            ResolutionQuality = Quality.Medium,
            ShadowQuality     = Quality.Medium,
            LightQuality      = Quality.Medium,
            CharacterQuality  = Quality.Medium,
            EnvDetailQuality  = Quality.Medium,
            ReflectionQuality = Quality.Medium,
            SFXQuality        = Quality.Medium,
            BloomQuality      = Quality.Medium,
            AAMode            = AntialiasingMode.TAA,
            EnableMetalFXSU   = false,
        };

        private static Model _HighPreset = new()
        {
            FPS               = 60,
            EnableVSync       = true,
            RenderScale       = 1.2,
            ResolutionQuality = Quality.High,
            ShadowQuality     = Quality.High,
            LightQuality      = Quality.High,
            CharacterQuality  = Quality.High,
            EnvDetailQuality  = Quality.High,
            ReflectionQuality = Quality.High,
            SFXQuality        = Quality.High,
            BloomQuality      = Quality.High,
            AAMode            = AntialiasingMode.TAA,
            EnableMetalFXSU   = false,
        };

        private static Model _VeryHighPreset = new()
        {
            FPS               = 60,
            EnableVSync       = true,
            RenderScale       = 1.4,
            ResolutionQuality = Quality.VeryHigh,
            ShadowQuality     = Quality.High,
            LightQuality      = Quality.VeryHigh,
            CharacterQuality  = Quality.High,
            EnvDetailQuality  = Quality.VeryHigh,
            ReflectionQuality = Quality.VeryHigh,
            SFXQuality        = Quality.High,
            BloomQuality      = Quality.VeryHigh,
            AAMode            = AntialiasingMode.TAA,
            EnableMetalFXSU   = false,
        };
        #endregion

        #region Properties
        /// <summary>
        /// This defines "<c>FPS</c>" combobox In-game settings. <br/>
        /// Options: 30, 60, 120 (EXPERIMENTAL) <br/>
        /// Default: 60 (or depends on FPSDefaultIndex and FPSIndex content)
        /// </summary>
        public int FPS { get; set; } = FPSIndex[FPSDefaultIndex];

        /// <summary>
        /// This defines "<c>V-Sync</c>" combobox In-game settings. <br/>
        /// Options: true, false <br/>
        /// Default: false
        /// </summary>
        public bool EnableVSync { get; set; } = false;

        /// <summary>
        /// This defines "<c>Render Scale</c>" combobox In-game settings. <br/>
        /// Options: 0.6, 0.8, 1.0, 1.2, 1.4, 1.6, 1.8, 2.0 <br/>
        /// Default: 1.0
        /// </summary>
        public double RenderScale { get; set; } = 1.0;

        /// <summary>
        /// Deprecated for now. <br/>
        /// Options: Custom (0), VeryLow (1), Low (2), Medium(3), High(4), VeryHigh(5) <br/>
        /// Default: Custom
        /// </summary>
        public Quality ResolutionQuality { get; set; } = Quality.Custom;

        /// <summary>
        /// This defines "<c>Shadow Quality</c>" combobox In-game settings. <br/>
        /// Options: Off(0), Low (2), Medium(3), High(4)
        /// Default: Off
        /// </summary>
        public Quality ShadowQuality { get; set; } = Quality.Off;

        /// <summary>
        /// This defines "<c>Light Quality</c>" combobox In-game settings. <br/>
        /// Options: VeryLow (1), Low (2), Medium(3), High(4), VeryHigh(5) <br/>
        /// Default: VeryLow
        /// </summary>
        public Quality LightQuality { get; set; } = Quality.VeryLow;

        /// <summary>
        /// This defines "<c>Character Quality</c>" combobox In-game settings. <br/>
        /// Options: Low (2), Medium(3), High(4) <br/>
        /// Default: Low
        /// </summary>
        public Quality CharacterQuality { get; set; } = Quality.Low;

        /// <summary>
        /// This defines "<c>Environment Quality</c>" combobox In-game settings. <br/>
        /// Options: VeryLow (1), Low (2), Medium(3), High(4), VeryHigh(5) <br/>
        /// Default: VeryLow
        /// </summary>
        public Quality EnvDetailQuality { get; set; } = Quality.VeryLow;

        /// <summary>
        /// This defines "<c>Reflection Quality</c>" combobox In-game settings. <br/>>
        /// Options: VeryLow (1), Low (2), Medium(3), High(4), VeryHigh(5) <br/>
        /// Default: VeryLow
        /// </summary>
        public Quality ReflectionQuality { get; set; } = Quality.VeryLow;

        /// <summary>
        /// This defines "<c>SFX Quality</c>" combobox In-game settings. <br/>>
        /// Options: VeryLow (1), Low (2), Medium(3), High(4) <br/>
        /// Default: High (I believe it's a mistake by miHoYo)
        /// </summary>
        public Quality SFXQuality { get; set; } = Quality.High;

        /// <summary>
        /// This defines "<c>Bloom Quality</c>" combobox In-game settings. <br/>
        /// Options: Off(0), VeryLow (1), Low (2), Medium(3), High(4), VeryHigh(5) <br/>
        /// Default: Off
        /// </summary>
        public Quality BloomQuality { get; set; } = Quality.Off;

        /// <summary>
        /// This defines "<c>Anti Aliasing</c>" combobox In-game settings. <br/>
        /// Options: Off (0), TAA (1), FXAA (2) <br/>
        /// Default: Off
        /// </summary>
        public AntialiasingMode AAMode { get; set; } = AntialiasingMode.Off;

        /// <summary>
        /// MetalFX config for Apple devices. Should not be used under Windows. <br/>
        /// Options: true, false <br/>
        /// Default: false
        /// </summary>
        public bool EnableMetalFXSU { get; set; }

        #endregion

        #region Methods
#nullable enable
        public static Model Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_GraphicsQuality} RegistryKey is unexpectedly not initialized!");
                var graphicsQuality = (Quality)RegistryRoot.GetValue(_GraphicsQuality, Quality.Medium);
                return graphicsQuality switch
                {
                    Quality.Custom => LoadCustom(),
                    Quality.VeryLow => _VeryLowPreset,
                    Quality.Low => _LowPreset,
                    Quality.Medium => _MediumPreset,
                    Quality.High => _HighPreset,
                    Quality.VeryHigh => _VeryHighPreset,
                    _ => new Model()
                };
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {_GraphicsQuality}" +
                             $"\r\n  Please open the game and change any settings, then close normally. After that you can use this feature." +
                             $"\r\n  If the issue persist, please report it on GitHub" +
                             $"\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(new Exception(
                    $"Failed when reading game settings {_GraphicsQuality}\r\n" +
                    $"Please open the game and change any settings, then safely close the game. If the problem persist, report the issue on our GitHub\r\n" +
                    $"{ex}", ex));
            }

            return new Model();
        }

        private static Model LoadCustom()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");
                object? value = RegistryRoot.GetValue(_ValueName, null);

                if (value != null)
                {
                    ReadOnlySpan<byte> byteStr = (byte[])value;
#if DEBUG
                    LogWriteLine($"Loaded StarRail Settings: {_ValueName}\r\n{Encoding.UTF8.GetString((byte[])value, 0, ((byte[])value).Length - 1)}", LogType.Debug, true);
#endif

                    return byteStr.Deserialize<Model>(StarRailSettingsJSONContext.Default) ?? new Model();
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {_ValueName}" +
                             $"\r\n  Please open the game and change any settings, then close normally. After that you can use this feature." +
                             $"\r\n  If the issue persist, please report it on GitHub" +
                             $"\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(new Exception(
                    $"Failed when reading game settings {_ValueName}\r\n" +
                    $"Please open the game and change any settings, then safely close the game. If the problem persist, report the issue on our GitHub\r\n" +
                    $"{ex}", ex));
            }

            return _MediumPreset;
        }

        public void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");

                RegistryRoot.SetValue(_GraphicsQuality, Quality.Custom, RegistryValueKind.DWord);

                string data = this.Serialize(StarRailSettingsJSONContext.Default);
                byte[] dataByte = Encoding.UTF8.GetBytes(data);
                RegistryRoot.SetValue(_ValueName, dataByte, RegistryValueKind.Binary);
#if DEBUG
                LogWriteLine($"Saved StarRail Settings: {_ValueName}\r\n{data}", LogType.Debug, true);
#endif
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
            }
        }

        public bool Equals(Model? comparedTo) => TypeExtensions.IsInstancePropertyEqual(this, comparedTo);
        #endregion
    }
}
