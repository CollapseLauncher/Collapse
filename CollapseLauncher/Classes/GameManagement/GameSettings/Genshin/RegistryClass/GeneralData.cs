using CollapseLauncher.GameSettings.Genshin.Context;
using Hi3Helper;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using static CollapseLauncher.GameSettings.Statics;
using static Hi3Helper.Logger;

namespace CollapseLauncher.GameSettings.Genshin
{
    internal class GeneralData
    {
        #region Fields
        private const string _ValueName = "GENERAL_DATA_h2389025596";
        #endregion

        #region Properties
        //in need of help,,
        //Using guide from https://github.com/Myp3a/GenshinConfigurator/wiki/Config-format
        //Thanks Myp3a!

        public int mtrAbortTimeOutCount { get; set; } = 3;

        /// <summary>
        /// Could be for PlayStation multiplayer stuff
        /// </summary>
        public bool onlyPlayWithPSPlayer { get; set; } = false;

        public int urlCheckErrorTraceCount { get; set; } = 30;

        public decimal uiPaperWhite { get; set; } = 0.0m;
        public decimal scenePaperWhite { get; set; } = 0.0m;

        public int localLevelIndex { get; set; } = 0;
        public bool disableRewiredDelayInit { get; set; } = false;
        public bool enableAudioChangeAndroidMinimumBufferCapacity { get; set; } = true;

        /// <summary>
        /// This define "<c>Voice Volume</c>" slider in-game.<br/>
        /// Valid values: 0-10
        /// Default: 10
        /// </summary>
        public int volumeVoice { get; set; } = 10;

        public int mtrTraceCDEachReason { get; set; } = 600;
        public bool mtrCached { get; set; } = true;
        public List<object> urlCheckBanReasons { get; set; }

        /// <summary>
        /// This define "<c>Server Name</c>" last selected in loading menu. <br/>
        /// </summary>
        public string selectedServerName { get; set; } = "os_usa";

        public int urlCheckTimeInterval { get; set; } = 1000;
        public int lastSeenPreDownloadTime { get; set; } = 0;

        /// <summary>
        /// This holds settings that holds Graphics Settings settings. YEP!<br/>
        /// Needs to be serialized again with its keys and values in dictionary<br/>
        /// https://github.com/Myp3a/GenshinConfigurator/wiki/Config-format#graphics-data-format
        /// </summary>
        [JsonPropertyName("graphicsData")]
        public string _graphicsData { get; set; }

        [JsonIgnore]
        public GraphicsData graphicsData { get; set; }

        public bool completionPlayGoPkg { get; set; } = false;

        /// <summary>
        /// This defines "<c>Gamma</c>" slider in-game. <br/>
        /// This implementation is quite janky but hear me out <br/>
        /// The value is directly controlled by Gamma Slider, which linked with GammaValue (NumberBox) <br/>
        /// Since the value is flipped, math function of y = -x + 4.4 is used (Refer GenshinGameSettingsPage.Ext.cs Line 122)
        /// </summary>
        public double gammaValue { get; set; } = 2.2f;

        public bool mtrIsOpen { get; set; } = true;

        /// <summary>
        /// This defines "<c>Vibration Intensity</c>" slider in-game. <br/>
        /// Valid Values: 1-5
        /// Default: 5
        /// </summary>
        public int vibrationIntensity { get; set; } = 5;

        public bool needPlayGoFullPkgPatch { get; set; } = false;
        public bool exploreNotification { get; set; } = true;
        public int mtrMaxTTL { get; set; } = 32;
        public bool urlCheckCached { get; set; } = false;

        /// <summary>
        /// deviceUUID<br/>
        /// This is supposed to be empty
        /// </summary>
        public string deviceUUID { get; set; } = "";

        /// <summary>
        /// This defines "<c>Smart combat camera</c>" whatever that is.<br/>
        /// Default: true
        /// </summary>
        public bool enableCameraCombatLock { get; set; } = true;

        public bool enableEffectAssembleInEditor { get; set; } = true;
        public bool resinNotification { get; set; } = true;

        /// <summary>
        /// This define "<c>Voice Language</c>" in-game settings. <br/>
        /// Valid values: 0-3
        /// Default: 1
        /// Chinese(0)
        /// English(1)
        /// Japanese(2)
        /// Korean(3)
        /// </summary>
        public int deviceVoiceLanguageType { get; set; } = 1;

        public string curAccountName { get; set; } = "";
        public bool _audioSuccessInit { get; set; } = true;
        public bool urlCheckAllIP { get; set; } = false;
        public bool rewiredDisableKeyboard { get; set; } = false;

        /// <summary>
        /// This defines "<c>Audio Output</c>" combo box in-game.
        /// Valid Values: 0 (stereo), 1 (surround)
        /// Default: 0
        /// </summary>
        public int audioOutput { get; set; } = 0;

        public int miniMapConfig { get; set; } = 1;

        /// <summary>
        /// This  defines "<c>Automatic View Height</c>" in-game settings.<br/>
        /// Default: true
        /// </summary>
        public bool enableCameraSlope { get; set; } = true;

        /// <summary>
        /// This define vibration level for certain controller probably ?
        /// Valid Values: 0 (Full), 1 (Partial), 2 (Off)
        /// Default: 0
        /// </summary>
        public int vibrationLevel { get; set; } = 0;

        public string deviceID { get; set; } = "";
        public string uiSaveData { get; set; }
        public bool gyroAiming { get; set; } = false;
        public bool motionBlur { get; set; } = true;
        public bool completionPkg { get; set; } = false;
        public int audioAndroidMiniumBufferCapacity { get; set; } = 2048;

        /// <summary>
        /// This is a dict that keeps track of graphics settings changes.<br/>
        /// Save to ignore (?)
        /// </summary>
        [JsonIgnore]
        public GlobalPerfData globalPerfData { get; set; }

        [JsonPropertyName("globalPerfData")]
        public string _globalPerfData { get; set; }

        public int urlCheckTimeOut { get; set; } = 5000;
        public int audioAPI { get; set; } = -1;

        /// <summary>
        /// This defines "<c>Dynamic Range</c>" audio combo box in-game.
        /// Valid values: 0 (full), 1 (limited)
        /// Default: 0
        /// </summary>
        public int audioDynamicRange { get; set; } = 0;

        public bool firstHDRSetting { get; set; } = true;
        public int mtrTraceCount { get; set; } = 5;
        public bool disableRewiredInitProtection { get; set; } = false;
        public string greyTestDeviceUniqueId { get; set; } = "";
        public bool mtrUseOldWinVersion { get; set; } = false;
        public List<int> _serializedCodeSwitches { get; set; }
        public int urlCheckAbortTimeOutCount { get; set; } = 3;
        public int urlCheckSueecssTraceCount { get; set; } = 5; //yes, that is actually the class name, its not a typo by us...

        /// <summary>
        /// This define "<c>Music Volume</c>" slider in-game.<br/>
        /// Valid values: 0-10
        /// Default: 10
        /// </summary>
        public int volumeMusic { get; set; } = 10;

        /// <summary>
        /// This holds value for controllers input that has been customized.
        /// </summary>
        public List<string> _overrideControllerMapKeyList { get; set; }

        public bool urlCheckIsOpen { get; set; } = false;
        public int urlCheckCDEachReason { get; set; } = 600;
        public List<string> _customDataValueList { get; set; }

        /// <summary>
        /// This define "<c>Text Language</c>" in-game settings. <br/>
        /// Valid values: 1-13 
        /// Default: 1
        /// English(1)
        /// Simplified Chinese(2)
        /// Traditional Chinese(3)
        /// French(4)
        /// German(5)
        /// Spanish(6)
        /// Portugese(7)
        /// Russian(8)
        /// Japanese(9)
        /// Korean(10)
        /// Thai(11)
        /// Vietnamese(12)
        /// Indonesian(13)
        /// Turkish(14)
        /// Italy(15)
        /// </summary>
        public int deviceLanguageType { get; set; } = 1;

        public List<string> _customDataKeyList { get; set; }
        public List<object> mtrBanReasons { get; set; }
        public int mtrTimeInterval { get; set; } = 1000;

        /// <summary>
        /// This define "<c>SFX Volume</c>" slider in-game.<br/>
        /// Valid values: 0-10
        /// Default: 10
        /// </summary>
        public int volumeSFX { get; set; } = 10;

        public int mtrAutoTraceInterval { get; set; } = 3600;
        public bool needReportQuestResourceDeleteStatusFiles { get; set; } = false;

        /// <summary>
        /// This holds said override for those controllers.
        /// </summary>
        // Temporary for fallback before the implementation is made
        public List<string> _overrideControllerMapValueList { get; set; }

        public int mtrTimeOut { get; set; } = 5000;

        /// <summary>
        /// This define "<c>Global Volume</c>" slider in-game.<br/>
        /// Valid values: 0-10
        /// Default: 10
        /// </summary>
        public int volumeGlobal { get; set; } = 10;

        public decimal maxLuminosity { get; set; } = 0.0m;

        /// <summary>
        /// userLocalDataVersionId<br/>
        /// This should be static<br/>
        /// Empty value
        /// </summary>
        public string userLocalDataVersionId { get; set; } = "";

        public bool forceDisableQuestResourceManagement { get; set; } = false;
        public bool rewiredEnableEDS { get; set; } = false;
        public bool usingNewVibrationSetting { get; set; } = true;

        /// <summary>
        /// This holds settings for input sens and stuff.<br/>
        /// Please look at https://github.com/Myp3a/GenshinConfigurator/wiki/Config-format#input-data-format
        /// </summary>
        public string inputData { get; set; }

        public string targetUID { get; set; } = "";
        public bool rewiredEnableKeyboard { get; set; } = false;

        #endregion

        #region Methods
#nullable enable
        public static GeneralData Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");
                object? value = RegistryRoot.GetValue(_ValueName, null);

                if (value != null)
                {
                    ReadOnlySpan<byte> byteStr = (byte[])value;
#if DEBUG
                    // If you want to debug GeneralData, Append this to the LogWriteLine:
                    // '\r\n{Encoding.UTF8.GetString((byte[])value, 0, ((byte[])value).Length - 1)'
                    // WARNING: VERY EXPENSIVE CPU TIME WILL BE USED
                    LogWriteLine($"Loaded Genshin Settings: {_ValueName}", LogType.Debug, true);
#endif
                    JsonSerializerOptions options = new JsonSerializerOptions()
                    {
                        TypeInfoResolver = GenshinSettingsJSONContext.Default,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };
                    GeneralData data = (GeneralData?)JsonSerializer.Deserialize(byteStr.Slice(0, byteStr.Length - 1), typeof(GeneralData), options) ?? new GeneralData();
                    data.graphicsData = GraphicsData.Load(data._graphicsData);
                    data.globalPerfData = new();
                    return data;
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {_ValueName}\r\n{ex}", LogType.Error, true);
            }

            return new GeneralData();
        }

        public void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");

                _graphicsData = graphicsData.Save();
                _globalPerfData = globalPerfData.Create(graphicsData, graphicsData.volatileVersion);
                JsonSerializerOptions options = new JsonSerializerOptions()
                {
                    TypeInfoResolver = GenshinSettingsJSONContext.Default,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                string data = JsonSerializer.Serialize(this, typeof(GeneralData), options) + '\0';
                byte[] dataByte = Encoding.UTF8.GetBytes(data);

                RegistryRoot.SetValue(_ValueName, dataByte, RegistryValueKind.Binary);
#if DEBUG
                // Only tracking actually used items (besides GlobalPerfData and GraphicsData)
                LogWriteLine($"Saved Genshin Settings: {_ValueName}" +
                    $"\r\n      Text Language        : {deviceLanguageType}" +
                    $"\r\n      VO Language          : {deviceVoiceLanguageType}" +
                    $"\r\n      Audio - Master Volume: {volumeGlobal}" +
                    $"\r\n      Audio - Music Volume : {volumeMusic}" +
                    $"\r\n      Audio - SFX Volume   : {volumeSFX}" +
                    $"\r\n      Audio - Voice Volume : {volumeVoice}" +
                    $"\r\n      Audio - Dynamic Range: {audioDynamicRange}" +
                    $"\r\n      Audio - Surround     : {audioOutput}" +
                    $"\r\n      Gamma                : {gammaValue}", LogType.Debug);
                // If you want to debug GeneralData, uncomment this LogWriteLine
                // WARNING: VERY EXPENSIVE CPU TIME WILL BE USED
                // LogWriteLine($"Saved Genshin Settings: {_ValueName}\r\n{Encoding.UTF8.GetString((byte[])value, 0, ((byte[])value).Length - 1)", LogType.Debug, true);
#endif
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
            }
        }

        public bool Equals(GeneralData? comparedTo) => TypeExtensions.IsInstancePropertyEqual(this, comparedTo);
#nullable disable
        #endregion
    }
}
