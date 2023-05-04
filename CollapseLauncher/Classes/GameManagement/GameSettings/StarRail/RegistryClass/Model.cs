using CollapseLauncher.GameSettings.StarRail.Context;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Microsoft.Win32;
using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static CollapseLauncher.GameSettings.Statics;
using static Hi3Helper.Logger;

namespace CollapseLauncher.GameSettings.StarRail
{
    internal class Model : IGameSettingsValue<Model>
    {
        #region Fields
        private const string _ValueName = "GraphicsSettings_Model_h2986158309";
        private short _FPS = 60;
        #endregion

        #region Properties
        /// <summary>
        /// This defines "<c>FPS</c>" combobox In-game settings. <br/>
        /// Options: 30, 60, 120 (EXPERIMENTAL)
        /// Default: 60
        /// </summary>
        public float FPS { get; set; } = 60;

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
        /// Default: 1.0
        /// </summary>
        public int ResolutionQuality { get; set; } = 1;

        /// <summary>
        /// This defines "<c>Shadow Quality</c>" combobox In-game settings. <br/>
        /// Options: Off(0), Low (2), Medium(3), High(4)
        /// Default: Low
        /// </summary>
        public int ShadowQuality { get; set; } = 2;

        /// <summary>
        /// This defines "<c>Light Quality</c>" combobox In-game settings. <br/>
        /// Options: VeryLow (1), Low (2), Medium(3), High(4), VeryHigh(5)
        /// Default: Low
        /// </summary>
        public int LightQuality { get; set; } = 2;

        /// <summary>
        /// This defines "<c>Character Quality</c>" combobox In-game settings. <br/>
        /// Options: Low (2), Medium(3), High(4)
        /// Default: Low
        /// </summary>
        public int CharacterQuality { get; set; } = 2;

        /// <summary>
        /// This defines "<c>Environment Quality</c>" combobox In-game settings. <br/>
        /// Options: VeryLow (1), Low (2), Medium(3), High(4), VeryHigh(5)
        /// Default: Low
        /// </summary>
        public int EnvDetailQuality { get; set; } = 2;

        /// <summary>
        /// This defines "<c>Reflection Quality</c>" combobox In-game settings. <br/>>
        /// Options: VeryLow (1), Low (2), Medium(3), High(4), VeryHigh(5)
        /// Default: Low
        /// </summary>
        public int ReflectionQuality { get; set; } = 2;

        /// <summary>
        /// This defines "<c>Bloom Quality</c>" combobox In-game settings. <br/>
        /// Options: Off(0), VeryLow (1), Low (2), Medium(3), High(4), VeryHigh(5)
        /// Default: Low
        /// </summary>
        public int BloomQuality { get; set; } = 2;

        /// <summary>
        /// This defines "<c>Anti Aliasing</c>" combobox In-game settings. <br/>
        /// Options: Off (0), TAA (1), FXAA (2)
        /// Default: TAA
        /// </summary>
        public int AAMode { get; set; } = 1;

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
                LogWriteLine($"Saved Graphics FPS      = {FPS}", LogType.Default, true);
                LogWriteLine($"Saved Graphics VSync    = {EnableVSync}", LogType.Default, true);
                LogWriteLine($"Saved Graphics RenScal  = {RenderScale}", LogType.Default, true);
                LogWriteLine($"Saved Graphics ResQ     = {ResolutionQuality}", LogType.Default, true);
                LogWriteLine($"Saved Graphics ShadowQ  = {ShadowQuality}", LogType.Default, true);
                LogWriteLine($"Saved Graphics LightQ   = {LightQuality}", LogType.Default, true);
                LogWriteLine($"Saved Graphics CharaQ   = {CharacterQuality}", LogType.Default, true);
                LogWriteLine($"Saved Graphics EnvDetQ  = {EnvDetailQuality}", LogType.Default, true);
                LogWriteLine($"Saved Graphics ReflQ    = {ReflectionQuality}", LogType.Default, true);
                LogWriteLine($"Saved Graphics BloomQ   = {BloomQuality}", LogType.Default, true);
                LogWriteLine($"Saved Graphics AAMode   = {AAMode}", LogType.Default, true);
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