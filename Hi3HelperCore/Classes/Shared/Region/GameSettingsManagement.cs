using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Threading.Tasks;

using Microsoft.Win32;
using Newtonsoft.Json;

using Hi3Helper.Screen;
using Hi3Helper.Data;

using static Hi3Helper.Logger;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Shared.Region.InstallationManagement;

using static Hi3Helper.Shared.Region.LauncherConfig;


namespace Hi3Helper.Shared.Region
{
    public static class GameSettingsManagement
    {
        public const string SectionName = "Settings";
        public static Dictionary<string, IniValue> GameSettingsTemplate = new Dictionary<string, IniValue>
        {
            { "Fullscreen", new IniValue(true) },
            { "ScreenResolution", new IniValue(ScreenProp.GetScreenSize()) },
            { "MaximumCombatFPS", new IniValue(60) },
            { "MaximumMenuFPS", new IniValue(60) },
            { "VisualQuality", new IniValue(2) },
            { "ShadowQuality", new IniValue(2) },
            { "PostProcessing", new IniValue(false) },
            { "Reflection", new IniValue(false) },
            { "Physics", new IniValue(false) },
            { "HighQualityBloom", new IniValue(false) },
            { "DynamicRange", new IniValue(false) },
            { "FXAA", new IniValue(false) },
            { "Distortion", new IniValue(false) },
            { "FullscreenExclusive", new IniValue(false) },
            { "CustomScreenResolution", new IniValue(false) },
            { "GameGraphicsAPI", new IniValue(3) }
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
            gameIni.SettingsStream = new FileStream(gameIni.SettingsPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            gameIni.Settings.Load(gameIni.SettingsStream);
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
                        case "MaximumCombatFPS":
                        case "MaximumMenuFPS":
                        case "VisualQuality":
                        case "ShadowQuality":
                        case "PostProcessing":
                        case "Reflection":
                        case "Physics":
                        case "HighQualityBloom":
                        case "DynamicRange":
                        case "FXAA":
                        case "Distortion":
                            GetOrCreatePersonalGraphicsSettingsValue(keyValue.Key);
                            break;

                        // Unregistered Keys
                        default:
                            GetOrCreateUnregisteredKeys(keyValue.Key);
                            break;
                    }
                }
            }
        });

        public static async Task SaveGameSettings() =>
        await Task.Run(() =>
        {
            SaveWindowValue();
            SavePersonalGraphicsSettingsValue();

            gameIni.Settings.Save(gameIni.SettingsStream = new FileStream(gameIni.SettingsPath, FileMode.OpenOrCreate, FileAccess.ReadWrite));
            gameIni.Settings.Load(gameIni.SettingsStream = new FileStream(gameIni.SettingsPath, FileMode.Open, FileAccess.Read)); 
        });

        private static string GetRegistryValue(in string key)
        {
            var parentkey = Registry.CurrentUser;
            var reg = parentkey.OpenSubKey($"{CurrentRegion.ConfigRegistryLocation}", false);
            var subkey = reg.GetValue(key, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            return Encoding.UTF8.GetString((byte[])subkey).Replace("\0","");
        }

        private static void SaveRegistryValue(in string key, in object value, in RegistryValueKind kind) =>
            Registry.SetValue($"HKEY_CURRENT_USER\\{CurrentRegion.ConfigRegistryLocation}", key, value, kind);

        #region Unregistered_Keys

        private static void GetOrCreateUnregisteredKeys(in string key) =>
            gameIni.Settings[SectionName].Add(key, GameSettingsTemplate[key]);

        #endregion

        #region Registry_ScreenSettingData

        const string ScreenSettingDataReg = "GENERAL_DATA_V2_ScreenSettingData_h1916288658";
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
                ScreenSettingData screenData = JsonConvert.DeserializeObject<ScreenSettingData>
                                               (GetRegistryValue(ScreenSettingDataReg));
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

        private static void SaveWindowValue()
        {
            Size resolution = gameIni.Settings[SectionName]["ScreenResolution"].ToSize();

            string data = JsonConvert.SerializeObject(new ScreenSettingData
            {
                width = resolution.Width,
                height = resolution.Height,
                isfullScreen = gameIni.Settings[SectionName]["Fullscreen"].ToBool()
            }) + '\0';

            SaveRegistryValue(ScreenSettingDataReg, Encoding.UTF8.GetBytes(data), RegistryValueKind.Binary);
            SaveRegistryValue("Screenmanager Is Fullscreen mode_h3981298716", gameIni.Settings[SectionName]["Fullscreen"].ToBool() ? 1 : 0, RegistryValueKind.DWord);
            SaveRegistryValue("Screenmanager Resolution Width_h182942802", resolution.Width, RegistryValueKind.DWord);
            SaveRegistryValue("Screenmanager Resolution Height_h2627697771", resolution.Height, RegistryValueKind.DWord);
        }

        #endregion

        #region Registry_PersonalGraphicsSetting

        const string PersonalGraphicsSettingReg = "GENERAL_DATA_V2_PersonalGraphicsSetting_h906361411";
        public enum EnumQuality { VHigh, High, Middle, Low, DISABLED, LOW, HIGH }

        public class PersonalGraphicsSetting
        {
            public string RecommendGrade { get; set; } = "Off";
            public bool IsUserDefinedGrade { get; set; } = false;
            public bool IsUserDefinedVolatile { get; set; } = true;
            public bool IsEcoMode { get; set; } = false;
            public int RecommendResolutionX { get; set; }
            public int RecommendResolutionY { get; set; }
            public EnumQuality ResolutionQuality { get; set; }
            public int TargetFrameRateForInLevel { get; set; }
            public int TargetFrameRateForOthers { get; set; }
            public float ContrastDelta { get; set; } = 0.0f;
            public bool isBrightnessStandardModeOn { get; set; } = true;
            public _VolatileSetting VolatileSetting { get; set; }
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

        private static void GetOrCreatePersonalGraphicsSettingsValue(in string key)
        {
            // Set default value before getting assigned
            IniValue value = GameSettingsTemplate[key];
            try
            {
                PersonalGraphicsSetting data = JsonConvert.DeserializeObject<PersonalGraphicsSetting>
                                               (GetRegistryValue(PersonalGraphicsSettingReg));
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
                        value = new IniValue(ConvertEnumToInt(data.VolatileSetting.ShadowLevel));
                        break;
                    case "PostProcessing":
                        value = new IniValue(data.VolatileSetting.UsePostFX);
                        break;
                    case "Reflection":
                        value = new IniValue(data.VolatileSetting.UseReflection);
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
            }
            catch { }

            gameIni.Settings[SectionName].Add(key, value);
        }

        private static void SavePersonalGraphicsSettingsValue()
        {
            Size resolution = gameIni.Settings[SectionName]["ScreenResolution"].ToSize();

            string data = JsonConvert.SerializeObject(new PersonalGraphicsSetting
            {
                RecommendGrade = "Off",
                RecommendResolutionX = resolution.Width,
                RecommendResolutionY = resolution.Height,
                ResolutionQuality = ConvertIntToEnum(gameIni.Settings[SectionName]["VisualQuality"].ToInt(), false),
                TargetFrameRateForInLevel = gameIni.Settings[SectionName]["MaximumCombatFPS"].ToInt(),
                TargetFrameRateForOthers = gameIni.Settings[SectionName]["MaximumMenuFPS"].ToInt(),
                VolatileSetting = new _VolatileSetting
                {
                    PostFXGrade = ConvertBoolToEnum(gameIni.Settings[SectionName]["HighQualityBloom"].ToBool(), false),
                    UsePostFX = gameIni.Settings[SectionName]["PostProcessing"].ToBool(),
                    UseHDR = gameIni.Settings[SectionName]["DynamicRange"].ToBool(),
                    UseDistortion = gameIni.Settings[SectionName]["Distortion"].ToBool(),
                    UseReflection = gameIni.Settings[SectionName]["Reflection"].ToBool(),
                    UseFXAA = gameIni.Settings[SectionName]["FXAA"].ToBool(),
                    UseDynamicBone = gameIni.Settings[SectionName]["Physics"].ToBool(),
                    ShadowLevel = ConvertIntToEnum(gameIni.Settings[SectionName]["ShadowQuality"].ToInt(), true),
                }
            }) + '\0';

            SaveRegistryValue(PersonalGraphicsSettingReg, Encoding.UTF8.GetBytes(data), RegistryValueKind.Binary);
        }

        private static int ConvertEnumToInt(EnumQuality value)
        {
            switch (value)
            {
                case EnumQuality.VHigh:
                    return 3;
                case EnumQuality.High:
                    return 2;
                case EnumQuality.Middle:
                    return 1;
                case EnumQuality.Low:
                    return 0;
                case EnumQuality.HIGH:
                    return 2;
                case EnumQuality.LOW:
                    return 1;
                case EnumQuality.DISABLED:
                    return 0;
                default:
                    return 0;
            }
        }

        private static bool ConvertEnumToBool(EnumQuality value)
        {
            switch (value)
            {
                case EnumQuality.VHigh:
                case EnumQuality.High:
                case EnumQuality.Middle:
                case EnumQuality.HIGH:
                    return true;
                default:
                    return false;
            }
        }

        private static EnumQuality ConvertBoolToEnum(bool input, bool upper) =>
            input ? (upper ? EnumQuality.HIGH : EnumQuality.High) : (upper ? EnumQuality.DISABLED : EnumQuality.Low);

        private static EnumQuality ConvertIntToEnum(int input, bool upper)
        {
            switch (input)
            {
                case 0:
                    return upper ? EnumQuality.DISABLED : EnumQuality.Low;
                case 1:
                    return upper ? EnumQuality.LOW : EnumQuality.Middle;
                case 2:
                    return upper ? EnumQuality.HIGH : EnumQuality.High;
                case 3:
                    return EnumQuality.VHigh;
                default:
                    return EnumQuality.DISABLED;
            }
        }

        #endregion
    }
}
