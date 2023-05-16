using CollapseLauncher.GameSettings.StarRail.Context;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using static CollapseLauncher.GameSettings.Statics;
using static Hi3Helper.Logger;

namespace CollapseLauncher.GameSettings.StarRail
{
    public enum Quality // TypeDefIndex: 11339
    {
        None = 0,
        VeryLow = 1,
        Low = 2,
        Medium = 3,
        High = 4,
        VeryHigh = 5
    }

    public enum AntialiasingMode // TypeDefIndex: 25409
    {
        Off = 0,
        TAA = 1,
        FXAA = 2
    }

    internal class Model : IGameSettingsValue<Model>
    {
        #region Fields
        private const string _ValueName = "GraphicsSettings_Model_h2986158309";
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

        #region Properties
        /// <summary>
        /// This defines "<c>FPS</c>" combobox In-game settings. <br/>
        /// Options: 30, 60, 120 (EXPERIMENTAL)
        /// Default: 60 (or depends on FPSDefaultIndex and FPSIndex content)
        /// </summary>
        public int FPS { get; set; } = FPSIndex[FPSDefaultIndex];

        /// <summary>
        /// This defines "<c>V-Sync</c>" combobox In-game settings. <br/>
        /// Options: true, false
        /// Default: false
        /// </summary>
        public bool EnableVSync { get; set; } = false;

        /// <summary>
        /// This defines "<c>Render Scale</c>" combobox In-game settings. <br/>
        /// Options: 0.6, 0.8, 1.0, 1.2, 1.4
        /// Default: 1.0
        /// </summary>
        public double RenderScale { get; set; } = 1.0;

        /// <summary>
        /// No idea what this is still...
        /// Options: 0, 1, 2, 3, 4
        /// Default: Medium
        /// </summary>
        public Quality ResolutionQuality { get; set; } = Quality.Medium;

        /// <summary>
        /// This defines "<c>Shadow Quality</c>" combobox In-game settings. <br/>
        /// Options: Off(0), Low (2), Medium(3), High(4)
        /// Default: Medium
        /// </summary>
        public Quality ShadowQuality { get; set; } = Quality.Medium;

        /// <summary>
        /// This defines "<c>Light Quality</c>" combobox In-game settings. <br/>
        /// Options: VeryLow (1), Low (2), Medium(3), High(4), VeryHigh(5)
        /// Default: Medium
        /// </summary>
        public Quality LightQuality { get; set; } = Quality.Medium;

        /// <summary>
        /// This defines "<c>Character Quality</c>" combobox In-game settings. <br/>
        /// Options: Low (2), Medium(3), High(4)
        /// Default: Medium
        /// </summary>
        public Quality CharacterQuality { get; set; } = Quality.Medium;

        /// <summary>
        /// This defines "<c>Environment Quality</c>" combobox In-game settings. <br/>
        /// Options: VeryLow (1), Low (2), Medium(3), High(4), VeryHigh(5)
        /// Default: Medium
        /// </summary>
        public Quality EnvDetailQuality { get; set; } = Quality.Medium;

        /// <summary>
        /// This defines "<c>Reflection Quality</c>" combobox In-game settings. <br/>>
        /// Options: VeryLow (1), Low (2), Medium(3), High(4), VeryHigh(5)
        /// Default: Medium
        /// </summary>
        public Quality ReflectionQuality { get; set; } = Quality.Medium;

        /// <summary>
        /// This defines "<c>Bloom Quality</c>" combobox In-game settings. <br/>
        /// Options: Off(0), VeryLow (1), Low (2), Medium(3), High(4), VeryHigh(5)
        /// Default: Medium
        /// </summary>
        public Quality BloomQuality { get; set; } = Quality.Medium;

        /// <summary>
        /// This defines "<c>Anti Aliasing</c>" combobox In-game settings. <br/>
        /// Options: Off (0), TAA (1), FXAA (2)
        /// Default: TAA
        /// </summary>
        public AntialiasingMode AAMode { get; set; } = AntialiasingMode.TAA;

        #endregion

        #region Methods
#nullable enable
        public static Model Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");
                object? value = RegistryRoot.GetValue(_ValueName, null);

                if (value != null)
                {
                    ReadOnlySpan<byte> byteStr = (byte[])value;
                    return (Model?)JsonSerializer.Deserialize(byteStr.Slice(0, byteStr.Length - 1), typeof(Model), ModelContext.Default) ?? new Model();
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {_ValueName}\r\n{ex}", LogType.Error, true);
            }

            return new Model();
        }

        public void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");

                string data = JsonSerializer.Serialize(this, typeof(Model), ModelContext.Default) + '\0';
                byte[] dataByte = Encoding.UTF8.GetBytes(data);
                RegistryRoot.SetValue(_ValueName, dataByte, RegistryValueKind.Binary);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
            }
        }
        public bool Equals(Model? comparedTo)
        {
            if (ReferenceEquals(this, comparedTo)) return true;
            if (comparedTo == null) return false;

            return comparedTo.AAMode == this.AAMode &&
                comparedTo.ShadowQuality == this.ShadowQuality &&
                comparedTo.LightQuality == this.LightQuality &&
                comparedTo.CharacterQuality == this.CharacterQuality &&
                comparedTo.BloomQuality == this.BloomQuality &&
                comparedTo.EnvDetailQuality == this.EnvDetailQuality &&
                comparedTo.ReflectionQuality == this.ReflectionQuality &&
                comparedTo.FPS == this.FPS &&
                comparedTo.EnableVSync == this.EnableVSync &&
                comparedTo.RenderScale == this.RenderScale &&
                comparedTo.ResolutionQuality == this.ResolutionQuality;
#nullable disable
        }
        #endregion
    }
}