using CollapseLauncher.GameSettings.Honkai.Context;
using CollapseLauncher.Interfaces;
using Microsoft.Win32;
using System;
using System.Text;
using System.Text.Json;
using static CollapseLauncher.GameSettings.Statics;
using static Hi3Helper.Data.ConverterTool;

namespace CollapseLauncher.GameSettings.Honkai
{
    internal class PersonalAudioSettingVolume : IGameSettingsValue<PersonalAudioSettingVolume>
    {
        #region Fields
        private const string _ValueName = "GENERAL_DATA_V2_PersonalAudioSettingVolume_h600615720";
        #endregion

        #region Properties
        /// <summary>
        /// This defines "<c>Master Volume</c>" slider In-game settings -> Audio.<br/><br/>
        /// Range: 0.0f - 100.0f<br/>
        /// Default: 100.0f
        /// </summary>
        public float MasterVolumeValue { get; set; } = 100.0f;

        /// <summary>
        /// This defines "<c>BGM</c>" slider In-game settings -> Audio -> Volume Balance.<br/><br/>
        /// Range: 0.0f - 3.0f<br/>
        /// Default: 3.0f
        /// </summary>
        public float BGMVolumeValue { get; set; } = 3.0f;

        /// <summary>
        /// This defines "<c>SFX</c>" slider In-game settings -> Audio -> Volume Balance.<br/><br/>
        /// Range: 0.0f - 3.0f<br/>
        /// Default: 3.0f
        /// </summary>
        public float SoundEffectVolumeValue { get; set; } = 3.0f;

        /// <summary>
        /// This defines "<c>Voice Acting</c>" slider In-game settings -> Audio -> Volume Balance.<br/><br/>
        /// Range: 0.0f - 3.0f<br/>
        /// Default: 3.0f
        /// </summary>
        public float VoiceVolumeValue { get; set; } = 3.0f;

        /// <summary>
        /// This defines "<c>ELF VO</c>" slider In-game settings -> Audio -> Volume Balance.<br/><br/>
        /// Range: 0.0f - 3.0f<br/>
        /// Default: 3.0f
        /// </summary>
        public float ElfVolumeValue { get; set; } = 3.0f;

        /// <summary>
        /// This defines "<c>CG</c>" slider In-game settings -> Audio -> Volume Balance.<br/><br/>
        /// Range: 0.0f - 3.0f<br/>
        /// Default: 3.0f
        /// </summary>
        public float CGVolumeValue { get; set; } = 1.8f;

        /// <summary>
        /// Default: false
        /// </summary>
        public bool CreateByDefault { get; set; } = false;
        #endregion

        #region Methods
#nullable enable
        public static PersonalAudioSettingVolume Load()
        {
            if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");

            object? value = RegistryRoot.GetValue(_ValueName, null);

            if (value != null)
            {
                ReadOnlySpan<byte> byteStr = (byte[])value;
                return (PersonalAudioSettingVolume?)JsonSerializer.Deserialize(byteStr.Slice(0, byteStr.Length - 1), typeof(PersonalAudioSettingVolume), PersonalAudioSettingVolumeContext.Default) ?? new PersonalAudioSettingVolume();
            }

            return new PersonalAudioSettingVolume();
        }

        public void Save()
        {
            if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");

            string data = JsonSerializer.Serialize(this, typeof(PersonalAudioSettingVolume), PersonalAudioSettingVolumeContext.Default) + '\0';
            byte[] dataByte = Encoding.UTF8.GetBytes(data);

            RegistryRoot.SetValue(_ValueName, dataByte, RegistryValueKind.Binary);
        }
#nullable disable
        #endregion
    }
}
