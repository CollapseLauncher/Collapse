using CollapseLauncher.GameSettings.Honkai.Context;
using CollapseLauncher.GameSettings.Honkai.Enums;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Microsoft.Win32;
using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static CollapseLauncher.GameSettings.Statics;
using static Hi3Helper.Logger;

namespace CollapseLauncher.GameSettings.Honkai
{
    internal class PersonalGraphicsSettingV2 : IGameSettingsValue<PersonalGraphicsSettingV2>
    {
        #region Fields
        private const string _ValueName = "GENERAL_DATA_V2_PersonalGraphicsSettingV2_h3480068519";
        private short _TargetFrameRateForInLevel = 60;
        private short _TargetFrameRateForOthers = 60;
        #endregion

        #region Properties
        /// <summary>
        /// This defines "<c>Rendering Accuracy</c>" combobox In-game settings -> Video.<br/>
        /// <inheritdoc cref="SelectResolutionQuality"/>
        /// Default: Middle
        /// </summary>
        public SelectResolutionQuality ResolutionQuality { get; set; } = SelectResolutionQuality.Middle;

        /// <summary>
        /// This defines "<c>Shadow</c>" combobox In-game settings -> Video.<br/>
        /// <inheritdoc cref="SelectShadowLevel"/>
        /// Default: MIDDLE
        /// </summary>
        public SelectShadowLevel ShadowLevel { get; set; } = SelectShadowLevel.MIDDLE;

        /// <summary>
        /// This defines "<c>FPS in combat</c>" combobox In-game settings -> Video.<br/>
        /// Range: 1 - 32767<br/>
        /// Default: 60
        /// </summary>
        public short TargetFrameRateForInLevel
        {
            get => _TargetFrameRateForInLevel;
            set => _TargetFrameRateForInLevel = value < 1 ? _TargetFrameRateForInLevel : value;
        }

        /// <summary>
        /// This defines "<c>FPS out of combat</c>" combobox In-game settings -> Video.<br/>
        /// Range: 1 - 32767<br/>
        /// Default: 60
        /// </summary>
        public short TargetFrameRateForOthers
        {
            get => _TargetFrameRateForOthers;
            set => _TargetFrameRateForOthers = value < 1 ? _TargetFrameRateForOthers : value;
        }

        /// <summary>
        /// This defines "<c>Reflection</c>" combobox In-game settings -> Video.<br/>
        /// <inheritdoc cref="SelectReflectionQuality"/>
        /// Default: LOW
        /// </summary>
        public SelectReflectionQuality ReflectionQuality { get; set; } = SelectReflectionQuality.LOW;

        /// <summary>
        /// This defines "<c>Physics</c>" switch In-game settings -> Video.<br/>
        /// Default: true
        /// </summary>
        public bool UseDynamicBone { get; set; } = true;

        /// <summary>
        /// This defines "<c>Anti-Aliasing</c>" checkbox In-game settings -> Video.<br/><br/>
        /// Default: true
        /// </summary>
        public bool UseFXAA { get; set; } = true;

        /// <summary>
        /// This defines "<c>Global Illumination</c>" combobox In-game settings -> Video.<br/><br/>
        /// <inheritdoc cref="SelectGlobalIllumination"/><br/>
        /// Default: Low
        /// </summary>
        public SelectGlobalIllumination GlobalIllumination { get; set; } = SelectGlobalIllumination.Low;

        /// <summary>
        /// This defines "<c>Ambient Occlusion</c>" combobox In-game settings -> Video.<br/><br/>
        /// <inheritdoc cref="SelectAmbientOcclusion"/><br/>
        /// Default: LOW
        /// </summary>
        public SelectAmbientOcclusion AmbientOcclusion { get; set; } = SelectAmbientOcclusion.LOW;

        /// <summary>
        /// This defines "<c>Volumetric Light</c>" combobox In-game settings -> Video.<br/><br/>
        /// <inheritdoc cref="SelectVolumetricLight"/><br/>
        /// Default: Medium
        /// </summary>
        public SelectVolumetricLight VolumetricLight { get; set; } = SelectVolumetricLight.Medium;

        /// <summary>
        /// This defines "<c>Post Processing</c>" checkbox In-game settings -> Video.<br/><br/>
        /// Default: true
        /// </summary>
        public bool UsePostFX { get; set; } = true;

        /// <summary>
        /// This defines "<c>High Quality</c>" checkbox In-game settings -> Video -> Post Processing.<br/><br/>
        /// This value is referenced to PostFXGrade.<br/>
        /// - If true, then it will set PostFXGrade to High.<br/>
        /// - If false, then it will set PostFXGrade to Low.<br/><br/>
        /// See also: <seealso cref="SelectPostFXGrade"/><br/><br/>
        /// Default: false -> Low
        /// </summary>
        [JsonIgnore]
        public bool PostFXGradeBool
        {
            get => PostFXGrade == SelectPostFXGrade.High;
            set => PostFXGrade = value ? SelectPostFXGrade.High : SelectPostFXGrade.Low;
        }

        /// <summary>
        /// This defines "<c>High Quality</c>" checkbox In-game settings -> Video -> Post Processing.<br/><br/>
        /// <inheritdoc cref="SelectPostFXGrade"/><br/>
        /// Default: Low
        /// </summary>
        public SelectPostFXGrade PostFXGrade { get; set; } = SelectPostFXGrade.Low;

        /// <summary>
        /// This defines "<c>HDR</c>" checkbox In-game settings -> Video -> Post Processing.<br/><br/>
        /// Default: true
        /// </summary>
        public bool UseHDR { get; set; } = true;

        /// <summary>
        /// This defines "<c>Distortion</c>" checkbox In-game settings -> Video -> Post Processing.<br/><br/>
        /// Default: true
        /// </summary>
        public bool UseDistortion { get; set; } = true;

        /// <summary>
        /// This defines "<c>Setting Details</c>" combobox In-game settings -> Video.<br/><br/>
        /// <inheritdoc cref="SelectLodGrade"/><br/>
        /// Default: Medium
        /// </summary>
        public SelectLodGrade LodGrade { get; set; } = SelectLodGrade.Medium;
        #endregion

        #region Methods
#nullable enable
        public static PersonalGraphicsSettingV2 Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = RegistryRoot.GetValue(_ValueName, null);

                if (value != null)
                {
                    ReadOnlySpan<byte> byteStr = (byte[])value;
                    return (PersonalGraphicsSettingV2?)JsonSerializer.Deserialize(byteStr.Slice(0, byteStr.Length - 1), typeof(PersonalGraphicsSettingV2), PersonalGraphicsSettingV2Context.Default) ?? new PersonalGraphicsSettingV2();
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {_ValueName}\r\n{ex}", LogType.Error, true);
            }

            return new PersonalGraphicsSettingV2();
        }

        public void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");

                string data = JsonSerializer.Serialize(this, typeof(PersonalGraphicsSettingV2), PersonalGraphicsSettingV2Context.Default) + '\0';
                byte[] dataByte = Encoding.UTF8.GetBytes(data);

                RegistryRoot.SetValue(_ValueName, dataByte, RegistryValueKind.Binary);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
            }
        }

        public bool Equals(PersonalGraphicsSettingV2? comparedTo)
        {
            if (ReferenceEquals(this, comparedTo)) return true;
            if (comparedTo == null) return false;

            return comparedTo.UseHDR == this.UseHDR &&
                comparedTo.UseFXAA == this.UseFXAA &&
                comparedTo.UsePostFX == this.UsePostFX &&
                comparedTo.ResolutionQuality == this.ResolutionQuality &&
                comparedTo.ReflectionQuality == this.ReflectionQuality &&
                comparedTo.ShadowLevel == this.ShadowLevel &&
                comparedTo.AmbientOcclusion == this.AmbientOcclusion &&
                comparedTo.GlobalIllumination == this.GlobalIllumination &&
                comparedTo.LodGrade == this.LodGrade &&
                comparedTo.PostFXGrade == this.PostFXGrade &&
                comparedTo.TargetFrameRateForInLevel == this.TargetFrameRateForInLevel &&
                comparedTo.TargetFrameRateForOthers == this.TargetFrameRateForOthers &&
                comparedTo.UseDistortion == this.UseDistortion &&
                comparedTo.UseDynamicBone == this.UseDynamicBone &&
                comparedTo.VolumetricLight == this.VolumetricLight;
        }
#nullable disable
        #endregion
    }
}
