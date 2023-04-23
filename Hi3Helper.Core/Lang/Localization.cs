using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace Hi3Helper
{
    public class LangMetadata
    {
        public LangMetadata(string filePath, int index)
        {
            this.LangFilePath = filePath;
            this.LangIndex = index;
            this.LangIsLoaded = false;

            ReadOnlySpan<char> langRelativePath = filePath.AsSpan().Slice(AppLangFolder.Length + 1);

            try
            {
                _ = LoadLang();
                LogWriteLine($"Locale file: {langRelativePath} loaded as {this.LangName} by {this.LangAuthor}", LogType.Scheme, true);
            }
            catch (Exception e)
            {
                LogWriteLine($"Failed while parsing locale file: {langRelativePath}. Ignoring!\r\n{e}", LogType.Warning, true);
            }
        }

        public LocalizationParams LoadLang()
        {
            using (Stream s = new FileStream(this.LangFilePath, FileMode.Open, FileAccess.Read))
            {
                LocalizationParams _langData = (LocalizationParams)JsonSerializer.Deserialize(s, typeof(LocalizationParams), LocalizationParamsContext.Default);
                this.LangAuthor = _langData.Author;
                this.LangID = _langData.LanguageID.ToLower();
                this.LangName = _langData.LanguageName;
                this.LangIsLoaded = true;

                return _langData;
            }
        }

        public int LangIndex { get; private set; }
        public string LangFilePath { get; private set; }
        public string LangAuthor { get; private set; }
        public string LangID { get; private set; }
        public string LangName { get; private set; }
        public bool LangIsLoaded { get; private set; }
    }

    public partial class Locale
    {
        public const string FallbackLangID = "en-us";
        public static void InitializeLocale()
        {
            TrySafeRenameOldEnFallback();

            int i = 0;
            foreach (string langPath in Directory.EnumerateFiles(AppLangFolder, "*.json", SearchOption.AllDirectories))
            {
                LangMetadata Metadata = new LangMetadata(langPath, i);
                if (Metadata.LangIsLoaded)
                {
                    LanguageNames.Add(Metadata.LangID.ToLower(), Metadata);
                    LanguageIDIndex.Add(Metadata.LangID);
                    i++;
                }
            }

            if (!LanguageNames.ContainsKey(FallbackLangID))
            {
                throw new LocalizationNotFoundException($"Fallback locale file with ID: \"{FallbackLangID}\" doesn't exist!");
            }
        }

        private static void TrySafeRenameOldEnFallback()
        {
            string possibleOldPath = Path.Combine(AppLangFolder, "en.json");
            string possibleNewPath = Path.Combine(AppLangFolder, "en-us.json");

            if (File.Exists(possibleOldPath) && File.Exists(possibleNewPath)) File.Delete(possibleOldPath);
            if (File.Exists(possibleOldPath) && !File.Exists(possibleNewPath)) File.Move(possibleOldPath, possibleNewPath);
        }

        public static void LoadLocale(string langID)
        {
            try
            {
                LangFallback = LanguageNames[FallbackLangID].LoadLang();
                langID = langID.ToLower();
                if (!LanguageNames.ContainsKey(langID))
                {
                    Lang = LanguageNames[FallbackLangID].LoadLang();
                    LogWriteLine($"Locale file with ID: {langID} doesn't exist! Fallback locale will be loaded instead.", LogType.Warning, true);
                    return;
                }

                Lang = LanguageNames[langID].LoadLang();
            }
            catch (Exception e)
            {
                throw new LocalizationInnerException($"Failed while loading locale with ID {langID}!", e);
            }
            finally
            {
                LangFallback = null;
            }
        }

        public static Dictionary<string, LangMetadata> LanguageNames = new Dictionary<string, LangMetadata>();
        public static List<string> LanguageIDIndex = new List<string>();
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

    public class LocalizationNotFoundException : Exception
    {
        public LocalizationNotFoundException(string message)
            : base(message) { }
    }

    public class LocalizationInnerException : Exception
    {
        public LocalizationInnerException(string message, Exception ex)
            : base(message, ex) { }
    }
}
