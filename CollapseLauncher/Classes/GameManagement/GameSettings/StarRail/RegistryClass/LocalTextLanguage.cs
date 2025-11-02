using CollapseLauncher.Extension;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.SentryHelper;
using Microsoft.Win32;
using System;
using System.Text;
using System.Text.Json.Serialization;
using static Hi3Helper.Logger;
// ReSharper disable RedundantDefaultMemberInitializer
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo

#pragma warning disable CS0659
namespace CollapseLauncher.GameSettings.StarRail
{
    internal class LocalTextLanguage : IGameSettingsValue<LocalTextLanguage>
    {
        #region Fields
        private const string ValueName = "LanguageSettings_LocalTextLanguage_h2764291023";

        [JsonIgnore]
        public IGameSettings ParentGameSettings { get; }

        private LocalTextLanguage(IGameSettings gameSettings)
        {
            ParentGameSettings = gameSettings;
        }
        #endregion

        #region Properties
        /// <summary>
        /// This defines "<c>Text Language</c>" in-game setting
        /// Range: en. // jp.
        /// Default: en.
        /// </summary>
        public string LocalTextLang { get; set; } = "en";

        /// <summary>
        /// This defines "<c>Text</c>" language In-game settings -> Text.<br/><br/>
        /// Values:<br/>
        ///     - 2 = cn.
        ///     - 1 = jp.<br/>
        ///     - 0 = en.<br/><br/>
        /// Default: 0 (en.)
        /// </summary>
        public int LocalTextLangInt
        {
            get => LocalTextLang switch
            {
                "pt" => 12, //portuguese
                "de" => 11, //german
                "fr" => 10, //french
                "id" => 9, //indonesian
                "vi" => 8, //vietnamese
                "th" => 7, //thai
                "ru" => 6, //russian
                "es" => 5, //spanish
                "kr" => 4, //korean
                "cht" => 3, //chinese traditional
                "cn" => 2, //chinese simplified
                "jp" => 1, //japanese
                _ => 0 //english
            };
            set => LocalTextLang = value switch
            {
                12 => "pt",
                11 => "de",
                10 => "fr",
                9 => "id",
                8 => "vi",
                7 => "th",
                6 => "ru",
                5 => "es",
                4 => "kr",
                3 => "cht",
                2 => "cn",
                1 => "jp",
                _ => "en"
            };
        }
        #endregion

        #region Methods
#nullable enable
        public static LocalTextLanguage Load(IGameSettings gameSettings)
        {
            try
            {
                if (gameSettings.RegistryRoot == null) throw new NullReferenceException($"Cannot load {ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = gameSettings.RegistryRoot.TryGetValue(ValueName, null, gameSettings.RefreshRegistryRoot);
                if (value != null)
                {
                    string localTextLang = Encoding.UTF8.GetString((byte[])value, 0, ((byte[])value).Length - 1);
#if DEBUG
                    LogWriteLine($"Loaded StarRail Settings: {ValueName} : {Encoding.UTF8.GetString((byte[])value, 0, ((byte[])value).Length - 1)}", LogType.Debug, true);
#endif
                    return new LocalTextLanguage(gameSettings) { LocalTextLang = localTextLang };
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {ValueName}\r\n{ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(new Exception($"Failed to read {ValueName}!", ex), SentryHelper.ExceptionType.UnhandledOther);
            }
            return new LocalTextLanguage(gameSettings);
        }

        public void Save()
        {
            try
            {
                if (ParentGameSettings.RegistryRoot == null) throw new NullReferenceException($"Cannot save {ValueName} since RegistryKey is unexpectedly not initialized!");
                string data = LocalTextLang + '\0';
                byte[] dataByte = Encoding.UTF8.GetBytes(data);
                ParentGameSettings.RegistryRoot.SetValue(ValueName, dataByte, RegistryValueKind.Binary);
#if DEBUG
                LogWriteLine($"Saved StarRail Settings: {ValueName} : {data}", LogType.Debug, true);
#endif
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {ValueName}" +
                             $"\r\n  Please open the game and change any Graphics Settings, then close normally. After that you can use this feature." +
                             $"\r\n  If the issue persist, please report it on GitHub" +
                             $"\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(new Exception(
                    $"Failed when reading game settings {ValueName}\r\n" +
                    $"Please open the game and change any graphics settings, then safely close the game. If the problem persist, report the issue on our GitHub\r\n" +
                    $"{ex}", ex));
            }

        }

        public override bool Equals(object? comparedTo) => comparedTo is LocalTextLanguage toThis && TypeExtensions.IsInstancePropertyEqual(this, toThis);
        #endregion
    }
}
