using CollapseLauncher.GameSettings.Zenless.Context;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Statics;
using Hi3Helper;
using System;
using System.IO;
using static Hi3Helper.Logger;

namespace CollapseLauncher.GameSettings.Zenless;

internal class GeneralData
{
    #region Fields
#nullable enable
    private static GamePresetProperty? _gamePresetProperty;
    private static GamePresetProperty ZenlessGameProperty
    {
        get
        {
            if (_gamePresetProperty != null) return _gamePresetProperty;
            _gamePresetProperty = GamePropertyVault.GetCurrentGameProperty();
            if (ZenlessGameProperty._GamePreset.GameType == GameNameType.Zenless)
                throw new InvalidDataException("[ZenlessSettings] GameProperty value is not Zenless!");
            return _gamePresetProperty;
        }
    }

    private static string gameFolder    = ZenlessGameProperty._GameVersion.GameDirPath;
    private static string gameExec      = ZenlessGameProperty._GamePreset.GameExecutableName!;
    private static string configFileLoc = $@"{gameExec}_Data\Persistent\LocalStorage\GENERAL_DATA.bin";

    private static string configFile = Path.Join(gameFolder, configFileLoc);
    #endregion

    #region Methods

    public static GeneralData Load(byte[] magic)
    {
        try
        {
            if (!File.Exists(configFile)) throw new FileNotFoundException("Zenless settings not found!");

            string raw = Decode.DecodeToString(configFile, magic);

        #if DEBUG
            LogWriteLine($"RAW Zenless Settings: {configFile}\r\n" +
                         $"{raw}", LogType.Debug, true);
        #endif
            GeneralData data = raw.Deserialize<GeneralData>(ZenlessSettingsJSONContext.Default) ?? new GeneralData();
            return data;
        }
        catch (Exception ex)
        {
            LogWriteLine($"Failed to parse Zenless settings\r\n{ex}", LogType.Error, true);
            return new GeneralData();
        }
    }

    
    #endregion
}