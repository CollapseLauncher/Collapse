using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hi3Helper;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace Hi3Helper
{
    public partial class Locale
    {
        public static void LoadLocalization(string appLang)
        {
            Console.WriteLine(LanguageNames.Count);
            Console.WriteLine(LanguageNames.Keys);

            LangFallback = (LocalizationParams)JsonSerializer.Deserialize(File
                .ReadAllText(LanguageNames
                    .Where(x => x.Value.LangID.ToLower() == "en" || x.Value.LangID.ToLower() == "en-us")
                    .First().Value.LangFilePath), typeof(LocalizationParams), LocalizationParamsContext.Default);
            try
            {
                Lang = (LocalizationParams)JsonSerializer.Deserialize(
                    File.ReadAllText(LanguageNames
                        .Where(x => x.Value.LangID.ToLower() == appLang.ToLower())
                        .First().Value.LangFilePath), typeof(LocalizationParams), LocalizationParamsContext.Default);

                LogWriteLine($"Using language: {Lang.LanguageName} by {Lang.Author}");
            }
            catch (Exception ex)
            {
                Lang = LangFallback;
                LogWriteLine($"Failed to load LanguageID: {appLang}. Fallback to: {Lang.LanguageName} by {Lang.Author}\r\n{ex}", LogType.Warning, true);
            }
        }

        public static void TryParseLocalizations()
        {
            foreach (string Entry in Directory.EnumerateFiles(AppLangFolder, "*.json", SearchOption.AllDirectories))
            {
                LocalizationParams lang = new LocalizationParams();
                try
                {
                    lang = (LocalizationParams)JsonSerializer.Deserialize(File.ReadAllText(Entry), typeof(LocalizationParams), LocalizationParamsContext.Default);

                    Console.WriteLine(lang.LanguageName);
                    if (!LanguageNames.ContainsKey(lang.LanguageName))
                        LanguageNames.Add(lang.LanguageName, new LangMetadata { LangID = lang.LanguageID, Author = lang.Author, LangFilePath = Entry });
                }
                catch (JsonException ex)
                {
                    LogWriteLine($"Error occured while parsing translation file: \"{Path.GetFileName(Entry)}\"\r\n{ex}", LogType.Error, true);
                    throw new LocalizationException($"Error occured while parsing translation file: \"{Path.GetFileName(Entry)}\"", ex);
                }
                catch (Exception ex)
                {
                    LogWriteLine($"Error occured while parsing translation file: \"{Path.GetFileName(Entry)}\"\r\n{ex}", LogType.Error, true);
                    throw new LocalizationException($"Error occured while parsing translation file: \"{Path.GetFileName(Entry)}\"", ex);
                }
            }
        }

        public struct LangMetadata
        {
            public string LangID;
            public string Author;
            public string LangFilePath;
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
