using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.Interfaces.Class;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.LocaleSourceGen;
using Hi3Helper.Shared.Region;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using WinRT;
// ReSharper disable StringLiteralTypo

// ReSharper disable CheckNamespace
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper;

[GeneratedBindableCustomProperty]
public partial class Locale : NotifyPropertyChanged
{
    private const string LocaleCodeSeparators   = "_-,";
    public const  string FallbackLocaleCode     = "en_us";
    private const string FallbackLocaleFilename = FallbackLocaleCode + ".json";

    private static readonly Dictionary<string, string> LangFallbackDict = new()
    {
        { "en", "en-us" },
        { "zh", "zh-cn" },
        { "de", "de-de" },
        { "es", "es-419" },
        { "id", "id-id" },
        { "ja", "ja-jp" },
        { "ko", "ko-kr" },
        { "pl", "pl-pl" },
        { "pt", "pt-br" },
        { "ru", "ru-ru" },
        { "th", "th-th" },
        { "vi", "vi-vn" }
    };
    private static readonly Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> LangFallbackDictLookup =
        LangFallbackDict.GetAlternateLookup<ReadOnlySpan<char>>();

    public static Locale Current { get; } = new();

    public ObservableCollection<LangParamsBase> MetadataList
    {
        get;
        set;
    } = [];

    public LangParams? Lang
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    private int _langIndex;
    public int LangIndex
    {
        get => _langIndex;
        set
        {
            _langIndex = value;
            if (TryLoadLocaleFrom(value))
            {
                OnPropertyChanged();
            }

            if (value < 0 || value >= MetadataList.Count)
            {
                return;
            }

            LauncherConfig.SetAndSaveConfigValue("AppLanguage", MetadataList[value].LanguageID);
        }
    }

    public void InitLocale()
    {
        MetadataList.Clear();

        string localeDirPath = LauncherConfig.AppLangFolder;
        string localeFallbackFilePath = Path.Combine(localeDirPath, FallbackLocaleFilename);

        DownloadFallbackIfNotPresent(localeFallbackFilePath);
        LoadLocaleMetadata(MetadataList, localeDirPath);

        using FileStream fallbackFileStream =
            File.Open(localeFallbackFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        LangParams.Fallback = fallbackFileStream.Deserialize(LocaleJsonContext.Default.LangParams)
                           ?? throw new JsonException($"Fallback locale JSON file: {localeFallbackFilePath} is empty or failed to be deserialized!");
    }

    private static void LoadLocaleMetadata(ObservableCollection<LangParamsBase> localeMetas, string localeDir)
    {
        foreach (string localePath in Directory.EnumerateFiles(localeDir, "*.json", SearchOption.TopDirectoryOnly))
        {
            using FileStream fileStream = File.Open(localePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            LangParamsBase? langParam = fileStream.Deserialize(LocaleJsonContext.Default.LangParamsBase);

            ReadOnlySpan<char> fileName = Path.GetFileName(localePath);

            if (langParam != null)
            {
                langParam.LocaleSourcePath = localePath;
                localeMetas.Add(langParam);
                Logger.LogWriteLine($"Locale file: {fileName} loaded as {langParam.LanguageName} by {langParam.Author}",
                                    LogType.Scheme,
                                    true);
                continue;
            }

            Logger.LogWriteLine($"Cannot load locale base metadata info from file: {fileName}, ignoring!",
                                LogType.Error,
                                true);
        }
    }

    public bool TryLoadLocaleFrom(int localeIndex)
    {
        if (localeIndex < 0 || localeIndex >= MetadataList.Count)
        {
            Logger.LogWriteLine($"Locale index {localeIndex} is out of range. Cannot load locale.",
                                LogType.Error,
                                true);
            return false;
        }

        LangParamsBase langParams = MetadataList[localeIndex];
        return TryLoadLocaleFrom(langParams);
    }

    public bool TryLoadLocaleFrom(string? localeCodeOrFilePath)
    {
        bool         isRetry        = false;

        if (string.IsNullOrEmpty(localeCodeOrFilePath))
        {
            localeCodeOrFilePath = FallbackLocaleCode;
        }

        LangParamsBase? foundLangParam = null;
        if (Path.IsPathFullyQualified(localeCodeOrFilePath))
        {
            return TryLoadLocaleFrom(new FileInfo(localeCodeOrFilePath));
        }

    Retry:
        ReadOnlySpan<char> langIdSpan   = localeCodeOrFilePath.GetSplit(0, LocaleCodeSeparators);
        ReadOnlySpan<char> regionIdSpan = localeCodeOrFilePath.GetSplit(1, LocaleCodeSeparators);

        foreach (LangParamsBase langParam in MetadataList)
        {
            if (!(langParam.LanguageID?.StartsWith(langIdSpan, StringComparison.OrdinalIgnoreCase) ?? false) ||
                !(langParam.LanguageID?.EndsWith(regionIdSpan, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                continue;
            }

            foundLangParam = langParam;
            break;
        }

        if ((!langIdSpan.IsEmpty && !regionIdSpan.IsEmpty) || isRetry)
        {
            goto Load;
        }

        string fallbackLocaleCode = GetFallbackLocaleId(localeCodeOrFilePath);
        Logger.LogWriteLine($"Locale code '{localeCodeOrFilePath}' is invalid. Fallback locale code: {fallbackLocaleCode} will be used instead.",
                            LogType.Error,
                            true);
        localeCodeOrFilePath = fallbackLocaleCode;
        isRetry              = true;
        goto Retry;

    Load:
        return TryLoadLocaleFrom(foundLangParam);
    }

    private static string GetFallbackLocaleId(string tag)
    {
        tag = tag.ToLower();
        switch (tag)
        {
            // Traditional Chinese
            case "zh-hant":
            case "zh-hk":
            case "zh-mo":
            case "zh-tw":
                return "zh-tw";
            // Portuguese, Portugal
            case "pt-pt":
                return "pt-pt";
        }

        ReadOnlySpan<char> langIdSpan = tag.GetSplit(0, LocaleCodeSeparators);
        return LangFallbackDictLookup.TryGetValue(langIdSpan, out string? fallbackLocaleCodeFound)
            ? fallbackLocaleCodeFound
            : FallbackLocaleCode;
    }

    public bool TryLoadLocaleFrom(LangParamsBase? langParam)
    {
        if (langParam == null)
        {
            return false;
        }

        string? localeFilePath = langParam.LocaleSourcePath;
        return !string.IsNullOrEmpty(localeFilePath) && TryLoadLocaleFrom(new FileInfo(localeFilePath));
    }

    public bool TryLoadLocaleFrom(FileInfo? fileInfo)
    {
        if (fileInfo == null)
        {
            return false;
        }

        if (!fileInfo.Exists)
        {
            Logger.LogWriteLine($"Locale file: {fileInfo.FullName} doesn't exist!",
                                LogType.Error,
                                true);
            return false;
        }

        try
        {
            using FileStream fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Lang = fileStream.Deserialize(LocaleJsonContext.Default.LangParams) ?? throw new JsonException($"Locale JSON file: {fileInfo.FullName} is empty or failed to be deserialized!");
            SetLocaleSizeSuffix(Lang);

            Logger.LogWriteLine($"Locale: {Lang.LanguageName} ({Lang.LanguageID}) by {Lang.Author} has been loaded!", LogType.Info, true);

            // Update the index
            LangParamsBase? localeParam = MetadataList.FirstOrDefault(x => x.LanguageID == Lang.LanguageID);
            if (localeParam != null &&
                MetadataList.IndexOf(localeParam) is var indexOfLocale and >= 0)
            {
                _langIndex = indexOfLocale;
            }
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"Failed to load locale {fileInfo.FullName} due to an error: {ex.Message}",
                                LogType.Error,
                                true);
            return false;
        }
    }

    private static void SetLocaleSizeSuffix(LangParams localeLang)
    {
        const string             separators    = "|\\/#$!";
        const StringSplitOptions options       = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;
        const int                numOfSuffixes = 9;

        ReadOnlySpan<char> suffixes = localeLang._Misc?.SizePrefixes1000U;
        scoped Span<Range> ranges = stackalloc Range[32];
        int                splits = suffixes.SplitAny(ranges, separators, options);

        if (splits < numOfSuffixes)
        {
            return;
        }

        string[] sizeSuffixes = new string[numOfSuffixes];
        for (int i = 0; i < numOfSuffixes; i++)
        {
            sizeSuffixes[i] = suffixes[ranges[i]].ToString();
        }

        ConverterTool.SetSizeSuffixes(sizeSuffixes);
    }

    private static void DownloadFallbackIfNotPresent(string localePath)
    {
        const string localeUrl1 = "https://github.com/CollapseLauncher/Collapse/raw/refs/heads/main/Hi3Helper.Core/Lang/en_US.json";

        FileInfo localeFileInfo = new FileInfo(localePath)
                                 .EnsureCreationOfDirectory()
                                 .EnsureNoReadOnly();

        if (localeFileInfo.Exists) return;

        Logger.LogWriteLine($"Main locale file doesn't exist on this path: {localePath}. File will be redownloaded!",
                            LogType.Error,
                            true);

        HttpClient       client     = FallbackCDNUtil.GetGlobalHttpClient(true);
        using FileStream fileStream = localeFileInfo.Create();
        using Stream     httpStream = client.GetStreamAsync(localeUrl1).Result;

        httpStream.CopyTo(fileStream);
    }
}
