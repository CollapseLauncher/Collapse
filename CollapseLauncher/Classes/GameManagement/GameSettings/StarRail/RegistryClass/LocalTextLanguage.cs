using CollapseLauncher.Interfaces;
using Hi3Helper;
using Microsoft.Win32;
using System;
using System.Text;
using static CollapseLauncher.GameSettings.Statics;
using static Hi3Helper.Logger;


namespace CollapseLauncher.GameSettings.StarRail
{
    internal class LocalTextLanguage : IGameSettingsValue<LocalTextLanguage>
    {
        #region Fields
        private const string _ValueName = "LanguageSettings_LocalTextLanguage_h2764291023";
        #endregion

        #region Properties
        /// <summary>
        /// This defines "<c>Text Language</c>" in-game setting
        /// Range: en. // jp.
        /// Default: en.
        /// </summary>
        public string LocalTextLang { get; set; } = "en";

        /// <summary>
        /// This defines "<c>Voice-over</c>" radiobox In-game settings -> Text.<br/><br/>
        /// Values:<br/>
        ///     - 1 = jp.<br/>
        ///     - 0 = en.<br/><br/>
        /// Default: 0 (en.)
        /// </summary>
        public int LocalTextLangInt
        {
            get => LocalTextLang switch
            {
                "jp" => 1,
                _ => 0,
            };
            set => LocalTextLang = value switch
            {
                1 => "jp",
                _ => "en",
            };
        }
        #endregion

        #region Methods
#nullable enable
        public static LocalTextLanguage Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = RegistryRoot.GetValue(_ValueName, null);
                if (value != null)
                {
                    // This probably not an efficient code but damnit my brain hurt
                    string __LocalTextLang = Encoding.UTF8.GetString((byte[])value);
                    string _LocalTextLang = __LocalTextLang.Remove(__LocalTextLang.Length - 1);
                    return new LocalTextLanguage { LocalTextLang = _LocalTextLang };
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {_ValueName}\r\n{ex}", LogType.Error, true);
            }
            return new LocalTextLanguage();
        }

        public void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");
                string data = LocalTextLang + '\0';
                byte[] dataByte = Encoding.UTF8.GetBytes(data);
                RegistryRoot?.SetValue(_ValueName, dataByte, RegistryValueKind.Binary);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
            }

        }

       public bool Equals(LocalTextLanguage? comparedTo)
       {
            if (ReferenceEquals(this, comparedTo)) return true;
            if (comparedTo == null) return false;

            return comparedTo.LocalTextLang == this.LocalTextLang;
        }
#nullable disable
        #endregion
    }
}
