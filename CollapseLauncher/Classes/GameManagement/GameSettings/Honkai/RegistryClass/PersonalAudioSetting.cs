using CollapseLauncher.GameSettings.Honkai.Context;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.EncTool;
using Microsoft.Win32;
using System;
using System.Text;
using System.Text.Json.Serialization;
using Hi3Helper.SentryHelper;
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Logger;
// ReSharper disable RedundantDefaultMemberInitializer
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

#pragma warning disable CS0659
namespace CollapseLauncher.GameSettings.Honkai
{
    internal class PersonalAudioSetting : IGameSettingsValue<PersonalAudioSetting>
    {
        #region Fields
        internal const   string                     ValueName    = "GENERAL_DATA_V2_PersonalAudioSetting_h3869048096";
        private readonly PersonalAudioSettingVolume _volumeValue = PersonalAudioSettingVolume.Load();

        #endregion

        #region Properties

        /// <summary>
        /// This defines "<c>Master Volume</c>" slider In-game settings -> Audio.<br/><br/>
        /// Range: 0 - 100<br/>
        /// Default: 100
        /// </summary>
        public int MasterVolume
        {
            get;
            set
            {
                field                          = value;
                _volumeValue.MasterVolumeValue = value;
            }
        } = 100;

        /// <summary>
        /// This defines "<c>BGM</c>" slider In-game settings -> Audio -> Volume Balance.<br/><br/>
        /// Range: 0 - 100<br/>
        /// Default: 100
        /// </summary>
        public int BGMVolume
        {
            get;
            set
            {
                field                       = value;
                _volumeValue.BGMVolumeValue = ConvertRangeValue(0.0f, 100.0f, value, 0.0f, 3.0f);
            }
        } = 100;

        /// <summary>
        /// This defines "<c>SFX</c>" slider In-game settings -> Audio -> Volume Balance.<br/><br/>
        /// Range: 0 - 100<br/>
        /// Default: 100
        /// </summary>
        public int SoundEffectVolume
        {
            get;
            set
            {
                field                               = value;
                _volumeValue.SoundEffectVolumeValue = ConvertRangeValue(0.0f, 100.0f, value, 0.0f, 3.0f);
            }
        } = 100;

        /// <summary>
        /// This defines "<c>Voice Acting</c>" slider In-game settings -> Audio -> Volume Balance.<br/><br/>
        /// Range: 0 - 100<br/>
        /// Default: 100
        /// </summary>
        public int VoiceVolume
        {
            get;
            set
            {
                field                         = value;
                _volumeValue.VoiceVolumeValue = ConvertRangeValue(0.0f, 100.0f, value, 0.0f, 3.0f);
            }
        } = 100;

        /// <summary>
        /// This defines "<c>ELF VO</c>" slider In-game settings -> Audio -> Volume Balance.<br/><br/>
        /// Range: 0 - 100<br/>
        /// Default: 100
        /// </summary>
        public int ElfVolume
        {
            get;
            set
            {
                field                       = value;
                _volumeValue.ElfVolumeValue = ConvertRangeValue(0.0f, 100.0f, value, 0.0f, 3.0f);
            }
        } = 100;

        /// <summary>
        /// This defines "<c>CG</c>" slider In-game settings -> Audio -> Volume Balance.<br/><br/>
        /// Range: 0 - 100<br/>
        /// Default: 100
        /// </summary>
        public int CGVolumeV2
        {
            get;
            set
            {
                field                      = value;
                _volumeValue.CGVolumeValue = ConvertRangeValue(0.0f, 100.0f, value, 0.0f, 1.8f);
            }
        } = 100;

        /// <summary>
        /// This defines "<c>Mute</c>" switch In-game settings -> Audio.<br/><br/>
        /// Range: 0 - 100<br/>
        /// Default: 100
        /// </summary>
        public bool Mute { get; set; } = false;

        /// <summary>
        /// This stays as "<c>Japanese</c>" whenever <c>_userCVLanguage</c> has set to any value.<br/><br/>
        /// Default: "Japanese"
        /// </summary>
        public string CVLanguage { get; set; } = "Japanese";

        /// <summary>
        /// This defines "<c>Voice-over</c>" radio box In-game settings -> Audio.<br/><br/>
        /// Values:<br/>
        ///     - 1 = Japanese<br/>
        ///     - 0 = Chinese(PRC)<br/><br/>
        /// Default: 1 (Japanese)
        /// </summary>
        [JsonIgnore]
        public int _userCVLanguageInt
        {
            get => _userCVLanguage switch
            {
                "Japanese" => 1,
                _ => 0
            };
            set => _userCVLanguage = value switch
            {
                1 => "Japanese",
                _ => "Chinese(PRC)"
            };
        }

        /// <summary>
        /// This defines "<c>Voice-over</c>" radio box In-game settings -> Audio.<br/><br/>
        /// Values:<br/>
        ///     - Japanese<br/>
        ///     - Chinese(PRC)<br/><br/>
        /// Default: "Japanese"
        /// </summary>
        public string _userCVLanguage { get; set; } = "Japanese";

        /// <summary>
        /// Default: true
        /// </summary>
        public bool IsUserDefined { get; set; } = true;
        #endregion

        #region Methods
#nullable enable
        public static PersonalAudioSetting Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = RegistryRoot.GetValue(ValueName, null);

                if (value != null)
                {
                    ReadOnlySpan<byte> byteStr = (byte[])value;
#if DEBUG
                    LogWriteLine($"Loaded HI3 Settings: {ValueName}\r\n{Encoding.UTF8.GetString(byteStr.TrimEnd((byte)0))}", LogType.Debug, true);
#endif
                    return byteStr.Deserialize(HonkaiSettingsJsonContext.Default.PersonalAudioSetting) ?? new PersonalAudioSetting();
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {ValueName}" +
                             $"\r\n  Please open the game and change any settings, then close normally. After that you can use this feature." +
                             $"\r\n  If the issue persist, please report it on GitHub" +
                             $"\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(new Exception(
                    $"Failed when reading game settings {ValueName}\r\n" +
                    $"Please open the game and change any settings, then safely close the game. If the problem persist, report the issue on our GitHub\r\n" +
                    $"{ex}", ex));
            }

            return new PersonalAudioSetting();
        }

        public void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {ValueName} since RegistryKey is unexpectedly not initialized!");

                string data = this.Serialize(HonkaiSettingsJsonContext.Default.PersonalAudioSetting);
                byte[] dataByte = Encoding.UTF8.GetBytes(data);

                RegistryRoot.SetValue(ValueName, dataByte, RegistryValueKind.Binary);
#if DEBUG
                LogWriteLine($"Saved HI3 Settings: {ValueName}\r\n{data}", LogType.Debug, true);
#endif
                _volumeValue.Save();
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {ValueName}!\r\n{ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(new Exception($"Failed to save {ValueName}!", ex), SentryHelper.ExceptionType.UnhandledOther);
            }
        }

        public override bool Equals(object? comparedTo) => comparedTo is PersonalAudioSetting toThis && TypeExtensions.IsInstancePropertyEqual(this, toThis);
        #endregion
    }
}
