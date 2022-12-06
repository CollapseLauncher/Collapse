using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace Hi3Helper
{
    public partial class Locale
    {
        public static void LoadLocalization(string appLang)
        {
            try
            {
                Lang = LanguageNames[appLang].LangData;
                LogWriteLine($"Using language: {Lang.LanguageName} by {Lang.Author}");
            }
            catch (Exception ex)
            {
                Lang = LangFallback;
                LogWriteLine($"Failed to load LanguageID: {appLang}. Fallback to: {Lang.LanguageName} by {Lang.Author}\r\n{ex}", LogType.Warning, true);
            }
        }

        private static void LoadFallbackLanguage()
        {
            try
            {
                string langPath = Path.Combine(AppLangFolder, "en.json");
                if (!File.Exists(langPath))
                    throw new FileNotFoundException($"Fallback/Default language file \"en.json\" doesn't exist!");

                using (Stream s = new FileStream(langPath, FileMode.Open, FileAccess.Read))
                {
                    LocalizationParams lang = (LocalizationParams)JsonSerializer.Deserialize(s, typeof(LocalizationParams), LocalizationParamsContext.Default);
                    LogWriteLine($"Loaded lang. resource: {lang.LanguageName} ({lang.LanguageID}) by {lang.Author}", LogType.Scheme);
                    LanguageNames.Add(lang.LanguageID, new LangMetadata
                    {
                        LangData = lang,
                        LangFilePath = langPath
                    });

                    LangFallback = lang;
                }
            }
            catch (JsonException ex)
            {
                LogWriteLine($"Error while parsing fallback/default language: \"en.json\"\r\n{ex}", LogType.Error, true);
                throw new LocalizationException($"Error occured while parsing fallback/default language file: \"en.json\"", ex);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error while parsing fallback/default language: \"en.json\"\r\n{ex}", LogType.Error, true);
                throw new LocalizationException($"Error occured while parsing fallback/default language file: \"en.json\"", ex);
            }
        }

        public static void TryParseLocalizations()
        {
            LanguageNames.Clear();
            LoadFallbackLanguage();
            foreach (string Entry in Directory.EnumerateFiles(AppLangFolder, "*-*.json", SearchOption.AllDirectories))
            {
                LocalizationParams lang = new LocalizationParams();
                try
                {
                    using (Stream s = new FileStream(Entry, FileMode.Open, FileAccess.Read))
                    {
                        lang = (LocalizationParams)JsonSerializer.Deserialize(s, typeof(LocalizationParams), LocalizationParamsContext.Default);

                        if (!LanguageNames.ContainsKey(lang.LanguageID))
                        {
                            LogWriteLine($"Loaded lang. resource: {lang.LanguageName} ({lang.LanguageID}) by {lang.Author}", LogType.Scheme);
                            LanguageNames.Add(lang.LanguageID, new LangMetadata
                            {
                                LangData = lang,
                                LangFilePath = Entry
                            });
                        }
                    }
                }
                catch (JsonException ex)
                {
                    LogWriteLine($"Error while parsing lang. resource: \"{Path.GetFileName(Entry)}\"\r\n{ex}", LogType.Error, true);
                    throw new LocalizationException($"Error occured while parsing translation file: \"{Path.GetFileName(Entry)}\"", ex);
                }
                catch (Exception ex)
                {
                    LogWriteLine($"Error while parsing lang. resource: \"{Path.GetFileName(Entry)}\"\r\n{ex}", LogType.Error, true);
                    throw new LocalizationException($"Error occured while parsing translation file: \"{Path.GetFileName(Entry)}\"", ex);
                }
            }
        }

        public struct LangMetadata
        {
            public string LangFilePath;
            public LocalizationParams LangData;
        }

        public static Dictionary<string, LangMetadata> LanguageNames = new Dictionary<string, LangMetadata>();
        public static LocalizationParams Lang;
#nullable enable
        public static LocalizationParams? LangFallback;
        public partial class LocalizationParams
        {
            public string LanguageName { get; set; } = "";
            public string LanguageID { get; set; } = "";
            public string Author { get; set; } = "Unknown";
        }
    }

    [Serializable]
    public class LocalizationException : Exception
    {
        public LocalizationException() { }

        public LocalizationException(string message)
            : base(message) { }

        public LocalizationException(string message, Exception inner)
            : base(message, inner) { }
    }
}
