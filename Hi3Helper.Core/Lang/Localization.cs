using Hi3Helper.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using WinRT;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
#if !APPLYUPDATE
using static Hi3Helper.Shared.Region.LauncherConfig;
#else
using ApplyUpdate;
using Avalonia.Platform;
using System.Linq;
using System.Reflection;
using static ApplyUpdate.Statics;
#endif
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace Hi3Helper
{
    public struct LangMetadata
    {
        public LangMetadata(string filePath, int index)
        {
            LangFilePath = filePath;
            LangIndex = index;
            LangIsLoaded = false;

            ReadOnlySpan<char> langRelativePath = filePath.AsSpan()[(AppLangFolder!.Length + 1)..];

            try
            {
                _ = LoadLangBase(filePath);
                LogWriteLine($"Locale file: {langRelativePath} loaded as {LangName} by {LangAuthor}", LogType.Scheme, true);
            }
            catch (Exception ex)
            {
#if !APPLYUPDATE
                SentryHelper.SentryHelper.ExceptionHandler(ex, SentryHelper.SentryHelper.ExceptionType.UnhandledOther);
#endif
                LogWriteLine($"Failed while parsing locale file: {langRelativePath}. Ignoring!\r\n{ex}", LogType.Warning, true);
            }
        }

#if APPLYUPDATE
        public LangMetadata(Uri fileUri, int index)
        {
            this.LangFilePath = fileUri.AbsoluteUri;
            this.LangIndex = index;
            this.LangIsLoaded = false;

            try
            {
#if !APPLYUPDATE
                _ = LoadLang(fileUri);
#else
                _ = LoadLangBase(fileUri);
#endif
                LogWriteLine($"Locale file: {fileUri.AbsoluteUri} loaded as {this.LangName} by {this.LangAuthor}", LogType.Scheme, true);
            }
            catch (Exception e)
            {
#if !APPLYUPDATE
                SentryHelper.SentryHelper.ExceptionHandler(ex, SentryHelper.SentryHelper.ExceptionType.UnhandledOther);
#endif
                LogWriteLine($"Failed while parsing locale file: {fileUri.AbsoluteUri}. Ignoring!\r\n{e}", LogType.Warning, true);
            }
        }
#endif

        public LocalizationParams LoadLang()
        {
#if APPLYUPDATE
            using Stream s = AssetLoader.Open(new Uri(LangFilePath));
            return LoadLang(s);
#else
            using Stream s = new FileStream(LangFilePath!, FileMode.Open, FileAccess.Read);
            return LoadLang(s);
#endif
        }

        public LocalizationParamsBase LoadLangBase(string langPath)
        {
            using Stream s = new FileStream(langPath!, FileMode.Open, FileAccess.Read);
            return LoadLangBase(s);
        }

#if APPLYUPDATE
        public LocalizationParamsBase LoadLangBase(Uri langPath)
        {
            using Stream s = AssetLoader.Open(langPath);
            return LoadLang(s);
        }
#endif

        public LocalizationParams LoadLang(Stream langStream)
        {
            LocalizationParams _langData = JsonSerializer.Deserialize(langStream!, CoreLibraryFieldsJsonContext.Default.LocalizationParams);
            LangAuthor = _langData!.Author;
            LangID = _langData.LanguageID.ToLower();
            LangName = _langData.LanguageName;
            LangIsLoaded = true;

            return _langData;
        }

        public LocalizationParamsBase LoadLangBase(Stream langStream)
        {
            LocalizationParamsBase _langData = JsonSerializer.Deserialize(langStream!, CoreLibraryFieldsJsonContext.Default.LocalizationParamsBase);
            LangAuthor = _langData!.Author;
            LangID = _langData.LanguageID.ToLower();
            LangName = _langData.LanguageName;
            LangIsLoaded = true;

            return _langData;
        }

        public int LangIndex;
        public string LangFilePath;
        public string LangAuthor;
        public string LangID;
        public string LangName;
        public bool LangIsLoaded;
    }

    [GeneratedBindableCustomProperty]
    public sealed partial class Locale
    {
        public const string FallbackLangID = "en-us";

#if APPLYUPDATE
        public static IEnumerable<Uri> GetLocaleUri()
        {
            Assembly thisAssembly = Assembly.GetExecutingAssembly();
            AssetLoader.SetDefaultAssembly(thisAssembly);
            foreach (Uri asset in AssetLoader
                .GetAssets(new Uri($"avares://ApplyUpdate-Core/Assets/Locale"), null)
                .Where(x => x.AbsoluteUri
                    .EndsWith(".json", StringComparison.InvariantCultureIgnoreCase)))
                yield return asset;
        }
#endif

        public static void InitializeLocale()
        {
            int i = 0;
#if APPLYUPDATE
            foreach (Uri langPath in GetLocaleUri())
            {
                LangMetadata Metadata = new LangMetadata(langPath, i);
#else
            TrySafeRenameOldEnFallback();
            foreach (string langPath in Directory.EnumerateFiles(AppLangFolder!, "*.json", SearchOption.AllDirectories))
            {
                LangMetadata Metadata = new LangMetadata(langPath, i);
#endif
                if (!Metadata.LangIsLoaded)
                {
                    continue;
                }

                LanguageNames!.Add(Metadata.LangID.ToLower(), Metadata);
                LanguageIDIndex!.Add(Metadata.LangID);
                i++;
            }

            if (!LanguageNames!.ContainsKey(FallbackLangID))
            {
                throw new LocalizationNotFoundException($"Fallback locale file with ID: \"{FallbackLangID}\" doesn't exist!");
            }
        }

#if !APPLYUPDATE
        private static void TrySafeRenameOldEnFallback()
        {
            string possibleOldPath = Path.Combine(AppLangFolder!, "en.json");
            string possibleNewPath = Path.Combine(AppLangFolder!, "en-us.json");

            if (File.Exists(possibleOldPath) && File.Exists(possibleNewPath)) File.Delete(possibleOldPath);
            if (File.Exists(possibleOldPath) && !File.Exists(possibleNewPath)) File.Move(possibleOldPath, possibleNewPath);
        }
#endif

        public static void LoadLocale(string langID)
        {
            try
            {
                LangFallback = LanguageNames![FallbackLangID].LoadLang();
                langID = langID!.ToLower();
                if (!LanguageNames.TryGetValue(langID, out LangMetadata langMetadata))
                {
                    Lang = LanguageNames[FallbackLangID].LoadLang();
                    LogWriteLine($"Locale file with ID: {langID} doesn't exist! Fallback locale will be loaded instead.", LogType.Warning, true);
                    return;
                }

                Lang = langMetadata.LoadLang();
                TryLoadSizePrefix(Lang);
            }
            catch (Exception ex)
            {
#if !APPLYUPDATE
                SentryHelper.SentryHelper.ExceptionHandler(ex, SentryHelper.SentryHelper.ExceptionType.UnhandledOther);
#endif
                throw new LocalizationInnerException($"Failed while loading locale with ID {langID}!", ex);
            }
            finally
            {
                LangFallback = null;
            }
        }

        private static void TryLoadSizePrefix(LocalizationParams langData)
        {
            if (string.IsNullOrEmpty(langData._Misc.SizePrefixes1000U))
            {
                LogWriteLine($"Locale file with ID: {langData.LanguageID} doesn't have size prefix value! The size prefix will not be changed!.", LogType.Warning, true);
                return;
            }

            ReadOnlySpan<char> speedPrefixSpan = langData._Misc.SizePrefixes1000U.AsSpan();
            Span<Range> spanRange = stackalloc Range[32];
            int rangesCount = speedPrefixSpan.Split(spanRange, '|', StringSplitOptions.RemoveEmptyEntries);

            if (rangesCount != 9)
            {
                LogWriteLine($"Locale file with ID: {langData.LanguageID} doesn't have a correct size prefix values! Make sure that the prefix value consists 9 values with '|' as its delimiter!", LogType.Warning, true);
                return;
            }

            string[] sizeSurfixes = new string[rangesCount];

            try
            {
                for (int i = 0; i < rangesCount; i++)
                    sizeSurfixes[i] = speedPrefixSpan[spanRange[i]].ToString();
            }
            catch (Exception ex)
            {
#if !APPLYUPDATE
                SentryHelper.SentryHelper.ExceptionHandler(ex);
#endif
                LogWriteLine($"An error has occurred while parsing size prefix value for locale file with ID: {langData.LanguageID}!\r\n{ex}", LogType.Warning, true);
                return;
            }

#if !APPLYUPDATE
            ConverterTool.SizeSuffixes = sizeSurfixes;
#else
            UpdateTask.SizeSuffixes = sizeSurfixes;
#endif
        }

        public static Dictionary<string, LangMetadata> LanguageNames   = new();
        public static List<string>                     LanguageIDIndex = [];
        public static LocalizationParams               Lang;
#nullable enable
        internal static LocalizationParams? LangFallback;

        [GeneratedBindableCustomProperty]
        public partial class LocalizationParamsBase
        {
            public string LanguageName { get; set; } = "";
            public string LanguageID { get; set; } = "";
            public string Author { get; set; } = "Unknown";
        }

        [GeneratedBindableCustomProperty]
        public sealed partial class LocalizationParams : LocalizationParamsBase;
    }

    public class LocalizationNotFoundException(string message) : Exception(message);

    public class LocalizationInnerException(string message, Exception ex) : Exception(message, ex);
}
