using Hi3Helper.Data;
using Hi3Helper.Screen;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Logger;
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
            RegKey = Registry.CurrentUser.OpenSubKey($"{CurrentRegion.ConfigRegistryLocation}", true);
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
            { "VolumetricLight", new IniValue(true) },
            { "UsePostFX", new IniValue(true) },
            { "HighQualityPostFX", new IniValue(true) },
            { "UseHDR", new IniValue(true) },
            { "UseDistortion", new IniValue(true) },
            { "LodGrade", new IniValue(0) },

            { "BGMVolume", new IniValue(3) },
            { "SoundEffectVolume", new IniValue(3) },
            { "VoiceVolume", new IniValue(3) },
            { "ElfVolume", new IniValue(3) },
            { "CGVolume", new IniValue(3) },
            { "CVLanguage", new IniValue(1) },

            { "CustomArgs", new IniValue("") },
        };

        public static async Task CheckExistingGameSettings()
        {
            gameIni.SettingsPath = Path.Combine(NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString()), "settings.ini");
            PrepareGameSettings();

            await UpdateGameExistingSettings();
        }

        public static void PrepareGameSettings()
        {
            gameIni.Settings = new IniFile();
            if (!File.Exists(gameIni.SettingsPath))
                gameIni.Settings.Save(gameIni.SettingsPath);
            gameIni.Settings.Load(gameIni.SettingsPath);
        }

        private static async Task UpdateGameExistingSettings() =>
        await Task.Run(() =>
        {
            if (gameIni.Settings[SectionName] == null)
                gameIni.Settings.Add(SectionName);

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
                            GetOrCreateWindowValue(keyValue.Key);
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
                            GetOrCreatePersonalGraphicsSettingsValue(keyValue.Key);
                            break;

                        // Registry: PersonalAudioSetting
                        case "BGMVolume":
                        case "SoundEffectVolume":
                        case "VoiceVolume":
                        case "ElfVolume":
                        case "CGVolume":
                        case "CVLanguage":
                            GetOrCreatePersonalAudioSettingsValue(keyValue.Key);
                            break;

                        // Unregistered Keys
                        default:
                            GetOrCreateUnregisteredKeys(keyValue.Key);
                            break;
                    }
                }
            }
        });

        public static async void SaveGameSettings() =>
        await Task.Run(() =>
        {
            gameIni.Settings.Save(gameIni.SettingsPath);
            gameIni.Settings.Load(gameIni.SettingsPath);

            SaveWindowValue();
            SavePersonalGraphicsSettingsValue();
            SavePersonalAudioSettingsValue();
        });

        public static IniValue GetGameConfigValue(string key)
        {
            try
            {
                if (!(CurrentRegion.IsGenshin ?? false))
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

        private static void GetOrCreateWindowValue(in string key)
        {
            // Set default value before getting assigned
            IniValue value = GameSettingsTemplate[key];
            try
            {
                string RegData = GetRegistryValue(ScreenSettingDataReg);

                if (RegData == null) return;

                ScreenSettingData screenData = JsonConvert.DeserializeObject<ScreenSettingData>(RegData);
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
            catch { }

            gameIni.Settings[SectionName].Add(key, value);
        }

        public static void SaveWindowValue()
        {
            Size resolution = gameIni.Settings[SectionName]["ScreenResolution"].ToSize();

            string data = JsonConvert.SerializeObject(new ScreenSettingData
            {
                width = resolution.Width,
                height = resolution.Height,
                isfullScreen = gameIni.Settings[SectionName]["Fullscreen"].ToBool()
            }) + '\0';

            SaveRegistryValue(CurrentRegion.ConfigRegistryLocation, ScreenSettingDataReg, Encoding.UTF8.GetBytes(data), RegistryValueKind.Binary);
            SaveRegistryValue(CurrentRegion.ConfigRegistryLocation, "Screenmanager Is Fullscreen mode_h3981298716", gameIni.Settings[SectionName]["Fullscreen"].ToBool() ? 1 : 0, RegistryValueKind.DWord);
            SaveRegistryValue(CurrentRegion.ConfigRegistryLocation, "Screenmanager Resolution Width_h182942802", resolution.Width, RegistryValueKind.DWord);
            SaveRegistryValue(CurrentRegion.ConfigRegistryLocation, "Screenmanager Resolution Height_h2627697771", resolution.Height, RegistryValueKind.DWord);
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

        public enum EnumQualityLOD
        {
            High = 0,
            Medium = 1,
            Low = 2
        }

        public enum EnumQualityCloseableOLH
        {
            DISABLED = 0,
            LOW = 1,
            HIGH = 2,
        }

        public class PersonalGraphicsSetting
        {
            public string RecommendGrade { get; set; } = "Off";
            public bool IsUserDefinedGrade { get; set; } = false;
            public bool IsUserDefinedVolatile { get; set; } = false;
            public bool IsEcoMode { get; set; } = false;
            public int RecommendResolutionX { get; set; } = 0;
            public int RecommendResolutionY { get; set; } = 0;
            public EnumQuality ResolutionQuality { get; set; } = EnumQuality.Low;
            public int TargetFrameRateForInLevel { get; set; } = 0;
            public int TargetFrameRateForOthers { get; set; } = 0;
            public float ContrastDelta { get; set; } = 0.0f;
            public bool isBrightnessStandardModeOn { get; set; } = true;
            public _VolatileSetting VolatileSetting { get; set; } = null;
        }

        public class _VolatileSetting
        {
            public EnumQuality PostFXGrade { get; set; }
            public bool UsePostFX { get; set; }
            public bool UseHDR { get; set; }
            public bool UseDistortion { get; set; }
            public bool UseReflection { get; set; }
            public bool UseFXAA { get; set; }
            public string MSAA { get; set; } = "MSAA_OFF";
            public bool UseDynamicBone { get; set; }
            public bool UseStaticCloud { get; set; } = false;
            public EnumQuality ShadowLevel { get; set; }
        }

        public class PersonalGraphicsSettingV2
        {
            public EnumQuality ResolutionQuality { get; set; }
            public EnumQualityCloseable ShadowLevel { get; set; }
            public ushort TargetFrameRateForInLevel { get; set; }
            public ushort TargetFrameRateForOthers { get; set; }
            public EnumQualityCloseableOLH ReflectionQuality { get; set; }
            public bool UseDynamicBone { get; set; }
            public bool UseFXAA { get; set; }
            public EnumQuality GlobalIllumination { get; set; }
            public EnumQualityOLH AmbientOcclusion { get; set; }
            public EnumQuality VolumetricLight { get; set; }
            public bool UsePostFX { get; set; }
            public EnumQuality PostFXGrade { get; set; }
            public bool UseHDR { get; set; }
            public bool UseDistortion { get; set; }
            public ushort LodGrade { get; set; }
        }

        private static void GetOrCreatePersonalGraphicsSettingsValue(in string key)
        {
            // Set default value before getting assigned
            IniValue value = GameSettingsTemplate[key];
            try
            {
                string RegData = GetRegistryValue(PersonalGraphicsSettingV2Reg);

                if (RegData == null) return;

                PersonalGraphicsSettingV2 data = JsonConvert.DeserializeObject<PersonalGraphicsSettingV2>(RegData);
                #region Unused
                /*
                switch (key)
                {
                    case "MaximumCombatFPS":
                        value = new IniValue(data.TargetFrameRateForInLevel);
                        break;
                    case "MaximumMenuFPS":
                        value = new IniValue(data.TargetFrameRateForOthers);
                        break;
                    case "VisualQuality":
                        value = new IniValue(ConvertEnumToInt(data.ResolutionQuality));
                        break;
                    case "ShadowQuality":
                        value = new IniValue(ConvertEnumToInt(data.ShadowLevel));
                        break;
                    case "PostProcessing":
                        value = new IniValue(data.UsePostFX);
                        break;
                    case "ReflectionQuality":
                        value = new IniValue(data.ReflectionQuality);
                        break;
                    case "Physics":
                        value = new IniValue(data.VolatileSetting.UseDynamicBone);
                        break;
                    case "HighQualityBloom":
                        value = new IniValue(ConvertEnumToBool(data.VolatileSetting.PostFXGrade));
                        break;
                    case "DynamicRange":
                        value = new IniValue(data.VolatileSetting.UseHDR);
                        break;
                    case "FXAA":
                        value = new IniValue(data.VolatileSetting.UseFXAA);
                        break;
                    case "Distortion":
                        value = new IniValue(data.VolatileSetting.UseDistortion);
                        break;
                }
                */
                #endregion

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
                        value = new IniValue(ConvertEnumHighLowToBool(data.VolumetricLight));
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
            catch { }

            gameIni.Settings[SectionName].Add(key, value);
        }

        public static void SavePersonalGraphicsSettingsValue()
        {
            string data = JsonConvert.SerializeObject(new PersonalGraphicsSettingV2
            {
                ResolutionQuality = ConvertIntToEnum(gameIni.Settings[SectionName]["ResolutionQuality"].ToInt()),
                ShadowLevel = ConvertIntToEnumCloseable(gameIni.Settings[SectionName]["ShadowLevel"].ToInt()),
                TargetFrameRateForInLevel = checked((ushort)gameIni.Settings[SectionName]["TargetFrameRateForInLevel"].ToInt()),
                TargetFrameRateForOthers = checked((ushort)gameIni.Settings[SectionName]["TargetFrameRateForOthers"].ToInt()),
                ReflectionQuality = ConvertIntToEnumCloseableOLH(gameIni.Settings[SectionName]["ReflectionQuality"].ToInt()),
                UseDynamicBone = gameIni.Settings[SectionName]["UseDynamicBone"].ToBool(),
                UseFXAA = gameIni.Settings[SectionName]["UseFXAA"].ToBool(),
                GlobalIllumination = ConvertBoolToEnumHighLow(gameIni.Settings[SectionName]["GlobalIllumination"].ToBool()),
                AmbientOcclusion = ConvertIntToEnumOLH(gameIni.Settings[SectionName]["AmbientOcclusion"].ToInt()),
                VolumetricLight = ConvertBoolToEnumHighLow(gameIni.Settings[SectionName]["VolumetricLight"].ToBool()),
                UsePostFX = gameIni.Settings[SectionName]["UsePostFX"].ToBool(),
                PostFXGrade = ConvertBoolToEnumHighLow(gameIni.Settings[SectionName]["HighQualityPostFX"].ToBool()),
                UseHDR = gameIni.Settings[SectionName]["UseHDR"].ToBool(),
                UseDistortion = gameIni.Settings[SectionName]["UseDistortion"].ToBool(),
                LodGrade = (ushort)gameIni.Settings[SectionName]["LodGrade"].ToInt(),
            }, new Newtonsoft.Json.Converters.StringEnumConverter()) + '\0';

            SaveRegistryValue(CurrentRegion.ConfigRegistryLocation, GraphicsGradeReg, 6, RegistryValueKind.DWord);
            SaveRegistryValue(CurrentRegion.ConfigRegistryLocation, PersonalGraphicsSettingV2Reg, Encoding.UTF8.GetBytes(data), RegistryValueKind.Binary);
        }

        private static int ConvertEnumToInt(EnumQuality value) => (int)value;
        public static bool ConvertEnumHighLowToBool(EnumQuality input) => input == EnumQuality.High;
        private static int ConvertEnumToIntCloseable(EnumQualityCloseable value) => (int)value;
        private static int ConvertEnumToIntOLH(EnumQualityOLH value) => (int)value;
        private static int ConvertEnumToIntCloseableOLH(EnumQualityCloseableOLH value) => (int)value;


        public static EnumQuality ConvertIntToEnum(int value) => (EnumQuality)value;
        public static EnumQuality ConvertBoolToEnumHighLow(bool input) => input ? (EnumQuality.High) : (EnumQuality.Low);
        public static EnumQualityCloseable ConvertIntToEnumCloseable(int value) => (EnumQualityCloseable)value;
        public static EnumQualityOLH ConvertIntToEnumOLH(int value) => (EnumQualityOLH)value;
        public static EnumQualityCloseableOLH ConvertIntToEnumCloseableOLH(int value) => (EnumQualityCloseableOLH)value;

        #endregion

        #region Registry_PersonalAudioSetting

        public const string PersonalAudioSettingReg = "GENERAL_DATA_V2_PersonalAudioSetting_h3869048096";

        public class PersonalAudioSetting
        {
            public byte BGMVolume { get; set; }
            public byte SoundEffectVolume { get; set; }
            public byte VoiceVolume { get; set; }
            public byte ElfVolume { get; set; }
            public byte CGVolume { get; set; }
            public string CVLanguage { get; set; }
            public string _userCVLanguage { get; set; }
            public bool IsUserDefined { get; set; } = false;
        }

        private static void GetOrCreatePersonalAudioSettingsValue(in string key)
        {
            // Set default value before getting assigned
            IniValue value = GameSettingsTemplate[key];
            try
            {
                string RegData = GetRegistryValue(PersonalAudioSettingReg);

                if (RegData == null) return;

                PersonalAudioSetting data = JsonConvert.DeserializeObject<PersonalAudioSetting>(RegData);

                switch (key)
                {
                    case "BGMVolume":
                        value = new IniValue(data.BGMVolume);
                        break;
                    case "SoundEffectVolume":
                        value = new IniValue(data.SoundEffectVolume);
                        break;
                    case "VoiceVolume":
                        value = new IniValue(data.VoiceVolume);
                        break;
                    case "ElfVolume":
                        value = new IniValue(data.ElfVolume);
                        break;
                    case "CGVolume":
                        value = new IniValue(data.CGVolume);
                        break;
                    case "CVLanguage":
                        value = data.IsUserDefined ? 0 : 1;
                        break;
                }
            }
            catch { }

            gameIni.Settings[SectionName].Add(key, value);
        }

        private static void SavePersonalAudioSettingsValue()
        {
            string data = JsonConvert.SerializeObject(new PersonalAudioSetting
            {
                BGMVolume = (byte)gameIni.Settings[SectionName]["BGMVolume"].ToInt(),
                SoundEffectVolume = (byte)gameIni.Settings[SectionName]["SoundEffectVolume"].ToInt(),
                VoiceVolume = (byte)gameIni.Settings[SectionName]["VoiceVolume"].ToInt(),
                ElfVolume = (byte)gameIni.Settings[SectionName]["ElfVolume"].ToInt(),
                CGVolume = (byte)gameIni.Settings[SectionName]["CGVolume"].ToInt(),
                CVLanguage = "Japanese",
                _userCVLanguage = (byte)gameIni.Settings[SectionName]["CVLanguage"].ToInt() == 0 ? "Chinese(PRC)" : null,
                IsUserDefined = (byte)gameIni.Settings[SectionName]["CVLanguage"].ToInt() == 0
            }) + '\0';

            SaveRegistryValue(CurrentRegion.ConfigRegistryLocation, PersonalAudioSettingReg, Encoding.UTF8.GetBytes(data), RegistryValueKind.Binary);
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
                await CheckExistingGameSettings();

                // parameter.AppendFormat("-screen-fullscreen {0} ", gameIni.Settings[SectionName]["Fullscreen"].ToBool() ? 1 : 0);

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
