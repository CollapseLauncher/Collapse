using CollapseLauncher.GameSettings.StarRail.Context;
using CollapseLauncher.GameSettings.StarRail.Enums;
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
        /// This defines "<c>Anti Aliasing</c>" combobox In-game settings. <br/>
        /// <inheritdoc cref="SelectAAMode"/>
        /// Default: TAA
        /// </summary>
        public SelectAAMode AAMode { get; set; } = SelectAAMode.TAA;

        /// <summary>
        /// This defines "<c>Bloom Quality</c>" combobox In-game settings. <br/>
        /// <inheritdoc cref="SelectBloomQuality"/>
        /// Default: Low
        /// </summary>
        public SelectBloomQuality BloomQuality { get; set; } = SelectBloomQuality.Low;

        /// <summary>
        /// This defines "<c>Character Quality</c>" combobox In-game settings. <br/>
        /// <inheritdoc cref="SelectCharacterQuality"/>
        /// Default: Low
        /// </summary>
        public SelectCharacterQuality CharacterQuality { get; set; } = SelectCharacterQuality.Low;

        /// <summary>
        /// This defines "<c>Environment Quality</c>" combobox In-game settings. <br/>
        /// <inheritdoc cref="SelectEnvDetailQuality"/>
        /// Default: Low
        /// </summary>
        public SelectEnvDetailQuality EnvDetailQuality { get; set; } = SelectEnvDetailQuality.Low;

        /// <summary>
        /// This defines "<c>Light Quality</c>" combobox In-game settings. <br/>
        /// <inheritdoc cref="SelectLightQuality"/>
        /// Default: Low
        /// </summary>
        public SelectLightQuality LightQuality { get; set; } = SelectLightQuality.Low;
       
        /// <summary>
        /// This defines "<c>Reflection Quality</c>" combobox In-game settings. <br/>
        /// <inheritdoc cref="SelectReflectionQuality"/>
        /// Default: Low
        /// </summary>
        public SelectReflectionQuality ReflectionQuality { get; set; } = SelectReflectionQuality.Low;

        /// <summary>
        /// This defines "<c>Shadow Quality</c>" combobox In-game settings. <br/>
        /// <inheritdoc cref="SelectShadowQuality"/>
        /// Default: Low
        /// </summary>
        public SelectShadowQuality ShadowQuality { get; set; } = SelectShadowQuality.Low;
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
                comparedTo.ReflectionQuality == this.ReflectionQuality;
#nullable disable
        }
        #endregion
    }
}