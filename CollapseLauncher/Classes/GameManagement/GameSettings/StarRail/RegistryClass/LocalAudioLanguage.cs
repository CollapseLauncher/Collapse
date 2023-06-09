using CollapseLauncher.Interfaces;
using Hi3Helper;
using Microsoft.Win32;
using System;
using System.Text;
using static CollapseLauncher.GameSettings.Statics;
using static Hi3Helper.Logger;


namespace CollapseLauncher.GameSettings.StarRail
{
    internal class LocalAudioLanguage : IGameSettingsValue<LocalAudioLanguage>
    {
        #region Fields
        private const string _ValueName = "LanguageSettings_LocalAudioLanguage_h882585060";
        #endregion

        #region Properties
        /// <summary>
        /// This defines "<c>Audio Language</c>" in-game setting
        /// Range: en. // jp.
        /// Default: en.
        /// </summary>
        public string LocalAudioLang { get; set; } = "en";

        /// <summary>
        /// This defines "<c>Voice-over</c>" radiobox In-game settings -> Audio.<br/><br/>
        /// Values:<br/>
        ///     - 1 = jp.<br/>
        ///     - 0 = en.<br/><br/>
        /// Default: 0 (en.)
        /// </summary>
        public int LocalAudioLangInt
        {
            get => LocalAudioLang switch
            {
                "jp" => 1,
                _ => 0,
            };
            set => LocalAudioLang = value switch
            {
                1 => "jp",
                _ => "en",
            };
        }
        #endregion

        #region Methods
#nullable enable
        public static LocalAudioLanguage Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = RegistryRoot.GetValue(_ValueName, null);
                if (value != null)
                {
                    // This probably not an efficient code but damnit my brain hurt
                    string __LocalAudioLang = Encoding.UTF8.GetString((byte[])value);
                    string _LocalAudioLang = __LocalAudioLang.Remove(__LocalAudioLang.Length - 1);
                    return new LocalAudioLanguage { LocalAudioLang = _LocalAudioLang };
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {_ValueName}\r\n{ex}", LogType.Error, true);
            }
            return new LocalAudioLanguage();
        }

        public void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");
                string data = LocalAudioLang + '\0';
                byte[] dataByte = Encoding.UTF8.GetBytes(data);
                RegistryRoot?.SetValue(_ValueName, dataByte, RegistryValueKind.Binary);;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
            }

        }

       public bool Equals(LocalAudioLanguage? comparedTo)
       {
            if (ReferenceEquals(this, comparedTo)) return true;
            if (comparedTo == null) return false;

            return comparedTo.LocalAudioLang == this.LocalAudioLang;
        }
#nullable disable
        #endregion
    }
}
