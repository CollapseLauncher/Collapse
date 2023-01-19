﻿using Hi3Helper.Data;
using Hi3Helper.Screen;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Logger;
using static Hi3Helper.Preset.ConfigV2Store;
using static Hi3Helper.Shared.Region.InstallationManagement;
using static Hi3Helper.Shared.Region.LauncherConfig;


namespace Hi3Helper.Shared.Region
{
    public static class GameConfig
    {
        public static RegistryKey RegKey;
        public static bool IsRegKeyExist = true;
        public static bool RequireWindowExclusivePayload;

        public static void InitRegKey()
        {
            RegKey = Registry.CurrentUser.OpenSubKey(CurrentConfigV2.ConfigRegistryLocation, true);
            IsRegKeyExist = !(RegKey == null);
        }

        public static string SectionName = "SettingsV2";
        public static Dictionary<string, IniValue> GameSettingsTemplate = new Dictionary<string, IniValue>
        {
            { "Fullscreen", new IniValue(true) },
            { "ScreenResolution", new IniValue(ScreenProp.GetScreenSize()) },
            { "FullscreenExclusive", new IniValue(false) },
            { "CustomScreenResolution", new IniValue(false) },
            { "GameGraphicsAPI", new IniValue(1) },

            { "ResolutionQuality", new IniValue(2) },
            { "ShadowLevel", new IniValue(3) },
            { "TargetFrameRateForInLevel", new IniValue(60) },
            { "TargetFrameRateForOthers", new IniValue(60) },
            { "ReflectionQuality", new IniValue(2) },
            { "UseDynamicBone", new IniValue(true) },
            { "UseFXAA", new IniValue(true) },
            { "GlobalIllumination", new IniValue(true) },
            { "AmbientOcclusion", new IniValue(2) },
            { "VolumetricLight", new IniValue(2) },
            { "UsePostFX", new IniValue(true) },
            { "HighQualityPostFX", new IniValue(true) },
            { "UseHDR", new IniValue(true) },
            { "UseDistortion", new IniValue(true) },
            { "LodGrade", new IniValue(0) },

            { "MasterVolume", new IniValue(100f) },
            { "BGMVolume", new IniValue(100f) },
            { "SoundEffectVolume", new IniValue(100f) },
            { "VoiceVolume", new IniValue(100f) },
            { "ElfVolume", new IniValue(100f) },
            { "CGVolume", new IniValue(100f) },
            { "CVLanguage", new IniValue(1) },
            { "MuteVolume", new IniValue(false) },

            { "CustomArgs", new IniValue("") },
        };

        public static void CheckExistingGameSettings()
        {
            string SettingsPath = NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString());
            gameIni.SettingsPath = Path.Combine(SettingsPath, "settings.ini");

            PrepareGameSettings();

            UpdateGameExistingSettings();
        }

        public static void PrepareGameSettings()
        {
            gameIni.Settings = new IniFile();
            if (!File.Exists(gameIni.SettingsPath))
                gameIni.Settings.Save(gameIni.SettingsPath);
            gameIni.Settings.Load(gameIni.SettingsPath);
        }

        private static void UpdateGameExistingSettings()
        {
            if (gameIni.Settings[SectionName] == null)
                gameIni.Settings.Add(SectionName);

            if (CurrentConfigV2.IsGenshin ?? false) return;

            foreach (KeyValuePair<string, IniValue> keyValue in GameSettingsTemplate)
            {
                if (gameIni.Settings[SectionName][keyValue.Key].Value == null)
                {
                    LogWriteLine($"{SectionName} Key: {keyValue.Key} is empty!", LogType.NoTag);

                    switch (keyValue.Key)
                    {
                        // Registry: ScreenSettingData
                        case "Fullscreen":
                        case "ScreenResolution":
                            GetOrCreateWindowValue(keyValue.Key, keyValue.Value);
                            break;

                        // Registry: PersonalGraphicsSetting
                        case "ResolutionQuality":
                        case "ShadowLevel":
                        case "TargetFrameRateForInLevel":
                        case "TargetFrameRateForOthers":
                        case "ReflectionQuality":
                        case "UseDynamicBone":
                        case "UseFXAA":
                        case "GlobalIllumination":
                        case "AmbientOcclusion":
                        case "VolumetricLight":
                        case "UsePostFX":
                        case "HighQualityPostFX":
                        case "UseHDR":
                        case "UseDistortion":
                        case "LodGrade":
                            GetOrCreatePersonalGraphicsSettingsValue(keyValue.Key, keyValue.Value);
                            break;

                        // Registry: PersonalAudioSetting
                        case "MasterVolume":
                        case "BGMVolume":
                        case "SoundEffectVolume":
                        case "VoiceVolume":
                        case "ElfVolume":
                        case "CGVolume":
                        case "CVLanguage":
                        case "MuteVolume":
                            GetOrCreatePersonalAudioSettingsValue(keyValue.Key, keyValue.Value);
                            break;

                        // Unregistered Keys
                        default:
                            GetOrCreateUnregisteredKeys(keyValue.Key);
                            break;
                    }
                }
            }
        }

        public static void SaveGameSettings()
        {
            gameIni.Settings.Save(gameIni.SettingsPath);
            gameIni.Settings.Load(gameIni.SettingsPath);

            SaveWindowValue();
            SavePersonalGraphicsSettingsValue();
            SavePersonalAudioSettingsValue();
        }

        public static IniValue GetGameConfigValue(string key)
        {
            try
            {
                if (gameIni.Settings != null)
                    return gameIni.Settings[SectionName][key];
            }
            catch (NullReferenceException) { }

            return null;
        }

        public static void SetAndSaveGameConfigValue(string key, IniValue value)
        {
            SetGameConfigValue(key, value);
            gameIni.Settings.Save(gameIni.SettingsPath);
            gameIni.Settings.Load(gameIni.SettingsPath);
        }

        public static void SetGameConfigValue(string key, IniValue value) => gameIni.Settings[SectionName][key] = value;

        private static string GetRegistryValue(in string key)
        {
            if (RegKey == null) return null;
#nullable enable
            object? data = RegKey.GetValue(key, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
#nullable disable
            return Encoding.UTF8.GetString((byte[])data).Replace("\0", "");
        }

        public static void SaveRegistryValue(in string reglocation, in string key, in object value, in RegistryValueKind kind) => RegKey.SetValue(key, value, kind);

        #region Unregistered_Keys

        private static void GetOrCreateUnregisteredKeys(in string key) =>
            gameIni.Settings[SectionName].Add(key, GameSettingsTemplate[key]);

        #endregion

        #region Registry_ScreenSettingData

        public const string ScreenSettingDataReg = "GENERAL_DATA_V2_ScreenSettingData_h1916288658";
        public class ScreenSettingData
        {
            public int width { get; set; }
            public int height { get; set; }
            public bool isfullScreen { get; set; }
        }

        private static void GetOrCreateWindowValue(in string key, in IniValue fallbackValue)
        {
            // Set default value before getting assigned
            IniValue value = GameSettingsTemplate[key];
            try
            {
                string RegData = GetRegistryValue(ScreenSettingDataReg);

                if (RegData != null)
                {
                    ScreenSettingData screenData = (ScreenSettingData)JsonSerializer.Deserialize(RegData, typeof(ScreenSettingData), ScreenSettingDataContext.Default);

                    switch (key)
                    {
                        case "Fullscreen":
                            value = new IniValue(screenData.isfullScreen);
                            break;
                        case "ScreenResolution":
                            value = new IniValue($"{screenData.width}x{screenData.height}");
                            break;
                    }
                }
                else
                {
                    value = fallbackValue;
                }
            }
            catch { }

            gameIni.Settings[SectionName].Add(key, value);
        }

        public static void SaveWindowValue()
        {
            Size resolution = gameIni.Settings[SectionName]["ScreenResolution"].ToSize();

            string data = JsonSerializer.Serialize(new ScreenSettingData
            {
                width = resolution.Width,
                height = resolution.Height,
                isfullScreen = gameIni.Settings[SectionName]["Fullscreen"].ToBool()
            }, typeof(ScreenSettingData), ScreenSettingDataContext.Default) + '\0';

            SaveRegistryValue(CurrentConfigV2.ConfigRegistryLocation, ScreenSettingDataReg, Encoding.UTF8.GetBytes(data), RegistryValueKind.Binary);
            SaveRegistryValue(CurrentConfigV2.ConfigRegistryLocation, "Screenmanager Is Fullscreen mode_h3981298716", gameIni.Settings[SectionName]["Fullscreen"].ToBool() ? 1 : 0, RegistryValueKind.DWord);
            SaveRegistryValue(CurrentConfigV2.ConfigRegistryLocation, "Screenmanager Resolution Width_h182942802", resolution.Width, RegistryValueKind.DWord);
            SaveRegistryValue(CurrentConfigV2.ConfigRegistryLocation, "Screenmanager Resolution Height_h2627697771", resolution.Height, RegistryValueKind.DWord);
        }

        #endregion

        #region Registry_PersonalGraphicsSetting

        public const string PersonalGraphicsSettingReg = "GENERAL_DATA_V2_PersonalGraphicsSetting_h906361411";
        public const string PersonalGraphicsSettingV2Reg = "GENERAL_DATA_V2_PersonalGraphicsSettingV2_h3480068519";
        public const string GraphicsGradeReg = "GENERAL_DATA_V2_GraphicsGrade_h1073342808";

        public enum EnumQuality
        {
            Low = 0,
            Middle = 1,
            High = 2,
            VHigh = 3
        }

        public enum EnumQualityCloseable
        {
            DISABLED = 0,
            LOW = 1,
            MIDDLE = 2,
            HIGH = 3,
            ULTRA = 4
        }

        public enum EnumQualityOLH
        {
            OFF = 0,
            LOW = 1,
            HIGH = 2
        }

        public enum EnumQualityVolLight
        {
            High = 2,
            Medium = 1,
            Low = 0
        }

        public enum EnumQualityCloseableOLH
        {
            DISABLED = 0,
            LOW = 1,
            HIGH = 2,
        }

        public class PersonalGraphicsSetting
        {
            public PersonalGraphicsSetting()
            {
                RecommendGrade = "Off";
                IsUserDefinedGrade = false;
                IsUserDefinedVolatile = false;
                IsEcoMode = false;
                ResolutionQuality = EnumQuality.Low;
                RecommendResolutionX = 0;
                RecommendResolutionY = 0;
                TargetFrameRateForInLevel = 0;
                TargetFrameRateForOthers = 0;
                ContrastDelta = 0.0f;
                isBrightnessStandardModeOn = true;
                VolatileSetting = null;
            }

            public string RecommendGrade { get; set; }
            public bool IsUserDefinedGrade { get; set; }
            public bool IsUserDefinedVolatile { get; set; }
            public bool IsEcoMode { get; set; }
            public int RecommendResolutionX { get; set; }
            public int RecommendResolutionY { get; set; }
            [JsonConverter(typeof(JsonStringEnumConverter))]
            public EnumQuality ResolutionQuality { get; set; }
            public int TargetFrameRateForInLevel { get; set; }
            public int TargetFrameRateForOthers { get; set; }
            public float ContrastDelta { get; set; }
            public bool isBrightnessStandardModeOn { get; set; }
            public _VolatileSetting VolatileSetting { get; set; }
        }

        public class _VolatileSetting
        {
            public _VolatileSetting()
            {
                MSAA = "MSAA_OFF";
                UseStaticCloud = false;
            }

            [JsonConverter(typeof(JsonStringEnumConverter))]
            public EnumQuality PostFXGrade { get; set; }
            public bool UsePostFX { get; set; }
            public bool UseHDR { get; set; }
            public bool UseDistortion { get; set; }
            public bool UseReflection { get; set; }
            public bool UseFXAA { get; set; }
            public string MSAA { get; set; }
            public bool UseDynamicBone { get; set; }
            public bool UseStaticCloud { get; set; }
            [JsonConverter(typeof(JsonStringEnumConverter))]
            public EnumQuality ShadowLevel { get; set; }
        }

        public class PersonalGraphicsSettingV2
        {
            [JsonConverter(typeof(JsonStringEnumConverter))]
            public EnumQuality ResolutionQuality { get; set; }
            [JsonConverter(typeof(JsonStringEnumConverter))]
            public EnumQualityCloseable ShadowLevel { get; set; }
            public int TargetFrameRateForInLevel { get; set; }
            public int TargetFrameRateForOthers { get; set; }
            [JsonConverter(typeof(JsonStringEnumConverter))]
            public EnumQualityCloseableOLH ReflectionQuality { get; set; }
            public bool UseDynamicBone { get; set; }
            public bool UseFXAA { get; set; }
            [JsonConverter(typeof(JsonStringEnumConverter))]
            public EnumQuality GlobalIllumination { get; set; }
            [JsonConverter(typeof(JsonStringEnumConverter))]
            public EnumQualityOLH AmbientOcclusion { get; set; }
            [JsonConverter(typeof(JsonStringEnumConverter))]
            public EnumQualityVolLight VolumetricLight { get; set; }
            public bool UsePostFX { get; set; }
            [JsonConverter(typeof(JsonStringEnumConverter))]
            public EnumQuality PostFXGrade { get; set; }
            public bool UseHDR { get; set; }
            public bool UseDistortion { get; set; }
            public int LodGrade { get; set; }
        }

        private static void GetOrCreatePersonalGraphicsSettingsValue(in string key, in IniValue fallbackValue)
        {
            // Set default value before getting assigned
            IniValue value = GameSettingsTemplate[key];
            try
            {
                string RegData = GetRegistryValue(PersonalGraphicsSettingV2Reg);

                if (RegData != null)
                {
                    PersonalGraphicsSettingV2 data = (PersonalGraphicsSettingV2)JsonSerializer
                        .Deserialize(RegData, typeof(PersonalGraphicsSettingV2), PersonalGraphicsSettingV2Context.Default);

                    switch (key)
                    {
                        case "ResolutionQuality":
                            value = new IniValue(ConvertEnumToInt(data.ResolutionQuality));
                            break;
                        case "ShadowLevel":
                            value = new IniValue(ConvertEnumToIntCloseable(data.ShadowLevel));
                            break;
                        case "TargetFrameRateForInLevel":
                            value = new IniValue(data.TargetFrameRateForInLevel);
                            break;
                        case "TargetFrameRateForOthers":
                            value = new IniValue(data.TargetFrameRateForOthers);
                            break;
                        case "ReflectionQuality":
                            value = new IniValue(ConvertEnumToIntCloseableOLH(data.ReflectionQuality));
                            break;
                        case "UseDynamicBone":
                            value = new IniValue(data.UseDynamicBone);
                            break;
                        case "UseFXAA":
                            value = new IniValue(data.UseFXAA);
                            break;
                        case "GlobalIllumination":
                            value = new IniValue(ConvertEnumHighLowToBool(data.GlobalIllumination));
                            break;
                        case "AmbientOcclusion":
                            value = new IniValue(ConvertEnumToIntOLH(data.AmbientOcclusion));
                            break;
                        case "VolumetricLight":
                            value = new IniValue(ConvertEnumVolLightToInt(data.VolumetricLight));
                            break;
                        case "UsePostFX":
                            value = new IniValue(data.UsePostFX);
                            break;
                        case "HighQualityPostFX":
                            value = new IniValue(ConvertEnumHighLowToBool(data.PostFXGrade));
                            break;
                        case "UseHDR":
                            value = new IniValue(data.UseHDR);
                            break;
                        case "UseDistortion":
                            value = new IniValue(data.UseDistortion);
                            break;
                        case "LodGrade":
                            value = new IniValue(data.LodGrade);
                            break;
                    }
                }
                else
                {
                    value = fallbackValue;
                }
            }
            catch { }

            gameIni.Settings[SectionName].Add(key, value);
        }

        public static void SavePersonalGraphicsSettingsValue()
        {
            string data = JsonSerializer.Serialize(new PersonalGraphicsSettingV2
            {
                ResolutionQuality = ConvertIntToEnum(gameIni.Settings[SectionName]["ResolutionQuality"].ToInt()),
                ShadowLevel = ConvertIntToEnumCloseable(gameIni.Settings[SectionName]["ShadowLevel"].ToInt()),
                TargetFrameRateForInLevel = gameIni.Settings[SectionName]["TargetFrameRateForInLevel"].ToInt(),
                TargetFrameRateForOthers = gameIni.Settings[SectionName]["TargetFrameRateForOthers"].ToInt(),
                ReflectionQuality = ConvertIntToEnumCloseableOLH(gameIni.Settings[SectionName]["ReflectionQuality"].ToInt()),
                UseDynamicBone = gameIni.Settings[SectionName]["UseDynamicBone"].ToBool(),
                UseFXAA = gameIni.Settings[SectionName]["UseFXAA"].ToBool(),
                GlobalIllumination = ConvertBoolToEnumHighLow(gameIni.Settings[SectionName]["GlobalIllumination"].ToBool()),
                AmbientOcclusion = ConvertIntToEnumOLH(gameIni.Settings[SectionName]["AmbientOcclusion"].ToInt()),
                VolumetricLight = ConvertIntToEnumVolLight(gameIni.Settings[SectionName]["VolumetricLight"].ToInt()),
                UsePostFX = gameIni.Settings[SectionName]["UsePostFX"].ToBool(),
                PostFXGrade = ConvertBoolToEnumHighLow(gameIni.Settings[SectionName]["HighQualityPostFX"].ToBool()),
                UseHDR = gameIni.Settings[SectionName]["UseHDR"].ToBool(),
                UseDistortion = gameIni.Settings[SectionName]["UseDistortion"].ToBool(),
                LodGrade = gameIni.Settings[SectionName]["LodGrade"].ToInt()
            }, typeof(PersonalGraphicsSettingV2), PersonalGraphicsSettingV2Context.Default) + '\0';

            SaveRegistryValue(CurrentConfigV2.ConfigRegistryLocation, GraphicsGradeReg, 6, RegistryValueKind.DWord);
            SaveRegistryValue(CurrentConfigV2.ConfigRegistryLocation, PersonalGraphicsSettingV2Reg, Encoding.UTF8.GetBytes(data), RegistryValueKind.Binary);
        }

        private static int ConvertEnumToInt(EnumQuality value) => (int)value;
        private static int ConvertEnumVolLightToInt(EnumQualityVolLight value) => (int)value;
        public static bool ConvertEnumHighLowToBool(EnumQuality input) => input == EnumQuality.High;
        private static int ConvertEnumToIntCloseable(EnumQualityCloseable value) => (int)value;
        private static int ConvertEnumToIntOLH(EnumQualityOLH value) => (int)value;
        private static int ConvertEnumToIntCloseableOLH(EnumQualityCloseableOLH value) => (int)value;


        public static EnumQuality ConvertIntToEnum(int value) => (EnumQuality)value;
        public static EnumQualityVolLight ConvertIntToEnumVolLight(int value) => (EnumQualityVolLight)value;
        public static EnumQuality ConvertBoolToEnumHighLow(bool input) => input ? (EnumQuality.High) : (EnumQuality.Low);
        public static EnumQualityCloseable ConvertIntToEnumCloseable(int value) => (EnumQualityCloseable)value;
        public static EnumQualityOLH ConvertIntToEnumOLH(int value) => (EnumQualityOLH)value;
        public static EnumQualityCloseableOLH ConvertIntToEnumCloseableOLH(int value) => (EnumQualityCloseableOLH)value;

        #endregion

        #region Registry_PersonalAudioSetting

        public const string PersonalAudioSettingReg = "GENERAL_DATA_V2_PersonalAudioSetting_h3869048096";
        public const string PersonalAudioSettingBeforeMuteReg = "GENERAL_DATA_V2_PersonalAudioSettingBeforeMute_h3867268048";
        public const string PersonalAudioSettingVolumeReg = "GENERAL_DATA_V2_PersonalAudioSettingVolume_h600615720";

        public class PersonalAudioSetting
        {
            public PersonalAudioSetting()
            {
                IsUserDefined = true;
            }

            public PersonalAudioVolumeValueSetting ToVolumeValue()
            {
                return new PersonalAudioVolumeValueSetting
                {
                    MasterVolumeValue = ConvertRangeValue(0f, 100f, MasterVolume, 0f, 3f),
                    BGMVolumeValue = ConvertRangeValue(0f, 100f, BGMVolume, 0f, 3f),
                    SoundEffectVolumeValue = ConvertRangeValue(0f, 100f, SoundEffectVolume, 0f, 3f),
                    VoiceVolumeValue = ConvertRangeValue(0f, 100f, VoiceVolume, 0f, 3f),
                    ElfVolumeValue = ConvertRangeValue(0f, 100f, ElfVolume, 0f, 3f),
                    CGVolumeValue = ConvertRangeValue(0f, 100f, CGVolumeV2, 0f, 1.8f),
                    CreatedByDefault = false
                };
            }

            private float ConvertRangeValue(float sMin, float sMax, float sValue, float tMin, float tMax) => (((sValue - sMin) * (tMax - tMin)) / (sMax - sMin)) + tMin;

            public float MasterVolume { get; set; }
            public float BGMVolume { get; set; }
            public float SoundEffectVolume { get; set; }
            public float VoiceVolume { get; set; }
            public float ElfVolume { get; set; }
            public float CGVolumeV2 { get; set; }
            public string CVLanguage { get; set; }
            public string _userCVLanguage { get; set; }
            public bool Mute { get; set; }
            public bool IsUserDefined { get; set; }
        }

        public class PersonalAudioVolumeValueSetting
        {
            public float MasterVolumeValue { get; set; }
            public float BGMVolumeValue { get; set; }
            public float SoundEffectVolumeValue { get; set; }
            public float VoiceVolumeValue { get; set; }
            public float ElfVolumeValue { get; set; }
            public float CGVolumeValue { get; set; }
            public bool CreatedByDefault { get; set; }
        }

        private static void GetOrCreatePersonalAudioSettingsValue(in string key, in IniValue fallbackValue)
        {
            // Set default value before getting assigned
            IniValue value = GameSettingsTemplate[key];
            try
            {
                string RegData = GetRegistryValue(PersonalAudioSettingReg);

                if (RegData == null)
                {
                    PersonalAudioSetting data = (PersonalAudioSetting)JsonSerializer.Deserialize(RegData, typeof(PersonalAudioSetting), PersonalAudioSettingContext.Default);

                    switch (key)
                    {
                        case "MasterVolume":
                            value = data.MasterVolume;
                            break;
                        case "BGMVolume":
                            value = data.BGMVolume;
                            break;
                        case "SoundEffectVolume":
                            value = data.SoundEffectVolume;
                            break;
                        case "VoiceVolume":
                            value = data.VoiceVolume;
                            break;
                        case "ElfVolume":
                            value = data.ElfVolume;
                            break;
                        case "CGVolume":
                            value = data.CGVolumeV2;
                            break;
                        case "CVLanguage":
                            value = data.IsUserDefined ? 0 : 1;
                            break;
                        case "MuteVolume":
                            value = data.Mute;
                            break;
                    }
                }
                else
                {
                    value = fallbackValue;
                }
            }
            catch { }

            gameIni.Settings[SectionName].Add(key, value);
        }

        private static void SavePersonalAudioSettingsValue()
        {
            PersonalAudioSetting set1 = new PersonalAudioSetting
            {
                MasterVolume = gameIni.Settings[SectionName]["MasterVolume"].ToFloat(),
                BGMVolume = gameIni.Settings[SectionName]["BGMVolume"].ToFloat(),
                SoundEffectVolume = gameIni.Settings[SectionName]["SoundEffectVolume"].ToFloat(),
                VoiceVolume = gameIni.Settings[SectionName]["VoiceVolume"].ToFloat(),
                ElfVolume = gameIni.Settings[SectionName]["ElfVolume"].ToFloat(),
                CGVolumeV2 = gameIni.Settings[SectionName]["CGVolume"].ToFloat(),
                CVLanguage = "Japanese",
                _userCVLanguage = (byte)gameIni.Settings[SectionName]["CVLanguage"].ToInt() == 0 ? "Chinese(PRC)" : null,
                IsUserDefined = (byte)gameIni.Settings[SectionName]["CVLanguage"].ToInt() == 0,
                Mute = gameIni.Settings[SectionName]["MuteVolume"].ToBoolNullable() ?? false
            };

            PersonalAudioVolumeValueSetting set2 = set1.ToVolumeValue();

            string data = JsonSerializer.Serialize(set1, typeof(PersonalAudioSetting), PersonalAudioSettingContext.Default) + '\0';
            SaveRegistryValue(CurrentConfigV2.ConfigRegistryLocation, PersonalAudioSettingReg, Encoding.UTF8.GetBytes(data), RegistryValueKind.Binary);

            string data2 = JsonSerializer.Serialize(set2, typeof(PersonalAudioVolumeValueSetting), PersonalAudioVolumeValueSettingContext.Default) + '\0';
            SaveRegistryValue(CurrentConfigV2.ConfigRegistryLocation, PersonalAudioSettingVolumeReg, Encoding.UTF8.GetBytes(data2), RegistryValueKind.Binary);

            set1.Mute = false;
            data = JsonSerializer.Serialize(set1, typeof(PersonalAudioSetting), PersonalAudioSettingContext.Default) + '\0';
            SaveRegistryValue(CurrentConfigV2.ConfigRegistryLocation, PersonalAudioSettingBeforeMuteReg, Encoding.UTF8.GetBytes(data), RegistryValueKind.Binary);
        }

        public static byte ConvertStringToByte(string value)
        {
            switch (value)
            {
                case "Chinese(PRC)":
                    return 0;
                default:
                case "Japanese":
                    return 1;
            }
        }
        public static string ConvertByteToString(byte value)
        {
            switch (value)
            {
                case 0:
                    return "Chinese(PRC)";
                default:
                case 1:
                    return "Japanese";
            }
        }

        #endregion

        #region LaunchArgumentBuilder

        public static async Task<string> GetLaunchArguments()
        {
            StringBuilder parameter = new StringBuilder();

            if (IsRegKeyExist)
            {
                if (!(CurrentConfigV2.IsGenshin ?? false))
                {
                    await Task.Run(CheckExistingGameSettings);

                    if (GetGameConfigValue("FullscreenExclusive").ToBool())
                    {
                        parameter.Append("-window-mode exclusive ");
                        RequireWindowExclusivePayload = true;
                    }

                    Size screenSize = GetGameConfigValue("ScreenResolution").ToSize();

                    int apiID = GetGameConfigValue("GameGraphicsAPI").ToInt();

                    if (apiID == 4)
                    {
                        LogWriteLine($"You are going to use DX12 mode in your game.\r\n\tUsing CustomScreenResolution or FullscreenExclusive value may break the game!", LogType.Warning);
                        if (GetGameConfigValue("CustomScreenResolution").ToBool() && GetGameConfigValue("Fullscreen").ToBool())
                            parameter.AppendFormat("-screen-width {0} -screen-height {1} ", ScreenProp.GetScreenSize().Width, ScreenProp.GetScreenSize().Height);
                        else
                            parameter.AppendFormat("-screen-width {0} -screen-height {1} ", screenSize.Width, screenSize.Height);
                    }
                    else
                        parameter.AppendFormat("-screen-width {0} -screen-height {1} ", screenSize.Width, screenSize.Height);


                    switch (apiID)
                    {
                        case 0:
                            parameter.Append("-force-feature-level-10-1 ");
                            break;
                        default:
                        case 1:
                            parameter.Append("-force-feature-level-11-0 -force-d3d11-no-singlethreaded ");
                            break;
                        case 2:
                            parameter.Append("-force-feature-level-11-1 ");
                            break;
                        case 3:
                            parameter.Append("-force-feature-level-11-1 -force-d3d11-no-singlethreaded ");
                            break;
                        case 4:
                            parameter.Append("-force-d3d12 ");
                            break;
                    }
                }
            }

            if (!GetAppConfigValue("EnableConsole").ToBool())
                parameter.Append("-nolog ");

            string customArgs = GetGameConfigValue("CustomArgs").ToString();

            if (!string.IsNullOrEmpty(customArgs))
                parameter.Append(customArgs);

            return parameter.ToString();
        }

        #endregion
    }
}
