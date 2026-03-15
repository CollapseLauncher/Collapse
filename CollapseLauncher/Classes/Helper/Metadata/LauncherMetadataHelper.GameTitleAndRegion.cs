using Hi3Helper.Shared.Region;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
// ReSharper disable CheckNamespace
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.Metadata;

internal static partial class LauncherMetadataHelper
{
    #region Translation Helper

    public static string? GetGameTitleTranslation(string? gameTitle) =>
        GetDictTranslationValueOrDefault(gameTitle, Locale.Current.Lang?._GameClientTitles);

    public static string? GetGameRegionTranslation(string? gameRegion) =>
        GetDictTranslationValueOrDefault(gameRegion, Locale.Current.Lang?._GameClientRegions);

    #endregion

    #region Collection Helper

    public static List<string> GetGameTitleList() => LauncherMetadataConfig.Keys.ToList();

    public static List<PresetConfig> GetGameRegionList(string? gameTitle)
    {
        if (string.IsNullOrEmpty(gameTitle) ||
            !LauncherMetadataConfig.TryGetValue(gameTitle, out Dictionary<string, PresetConfig>? value))
        {
            return [];
        }

        return value.Values.ToList();
    }

    public static int GetGameRegionLastSavedIndexOrDefault(string? gameTitle)
    {
        ThrowIfDictionaryEmpty(LauncherMetadataConfig);
        if (string.IsNullOrEmpty(gameTitle) ||
            !LauncherMetadataConfig.TryGetValue(gameTitle, out Dictionary<string, PresetConfig>? regionDict))
        {
            return 0;
        }

        ThrowIfDictionaryEmpty(regionDict);

        string configLastRegionKey = $"LastRegion_{GetNonSpaceGameTitle(gameTitle)}";
        string? lastRegion = LauncherConfig.GetAppConfigValue(configLastRegionKey);
        return string.IsNullOrEmpty(lastRegion)
            ? 0
            : GetValueIndexFromKey(regionDict, lastRegion);
    }

    public static void SaveGameRegionIndex(string? gameTitle, string? gameRegion, bool isSave = true)
    {
        string iniKeyName = $"LastRegion_{GetNonSpaceGameTitle(gameTitle)}";

        if (isSave)
        {
            LauncherConfig.SetAndSaveConfigValue(iniKeyName, gameRegion);
        }
        else
        {
            LauncherConfig.SetAppConfigValue(iniKeyName, gameRegion);
        }
    }

    #endregion

    #region Private Methods

    private static string? GetDictTranslationValueOrDefault(string? lookup, Dictionary<string, string>? dict)
    {
        if (string.IsNullOrEmpty(lookup) || dict == null) return lookup;
        return dict.GetValueOrDefault(lookup, lookup);
    }

    private static int GetValueIndexFromKey<TKey, TValue>(Dictionary<TKey, TValue> dict, TKey key)
        where TKey : notnull
    {
        // Get value reference
        ref TValue selectedRef = ref CollectionsMarshal.GetValueRefOrNullRef(dict, key);
        if (Unsafe.IsNullRef(ref selectedRef))
        {
            return 0;
        }

        // Get first value reference
        TKey? firstKey = dict.Keys.FirstOrDefault();
        if (firstKey == null)
        {
            throw new InvalidOperationException();
        }
        ref TValue firstRef = ref CollectionsMarshal.GetValueRefOrNullRef(dict, firstKey);
        if (Unsafe.IsNullRef(ref firstRef))
        {
            throw new InvalidOperationException();
        }

        int sizeOfValue   = RuntimeHelpers.IsReferenceOrContainsReferences<TValue>() ? 24 : Marshal.SizeOf<TValue>();
        int refByteOffset = (int)Unsafe.ByteOffset(ref firstRef, ref selectedRef);
        int offset        = refByteOffset / sizeOfValue;
        return offset;
    }

    [return: NotNullIfNotNull(nameof(gameTitle))]
    internal static unsafe string? GetNonSpaceGameTitle(string? gameTitle)
    {
        if (string.IsNullOrEmpty(gameTitle))
        {
            return gameTitle;
        }

        int        j      = 0;
        Span<char> buffer = stackalloc char[gameTitle.Length];

        // UNSAFELY iterate as writeable reference.
        ref char start = ref Unsafe.AsRef<char>(Unsafe.AsPointer(in gameTitle.AsSpan().GetPinnableReference()));
        ref char end   = ref Unsafe.Add(ref Unsafe.AsRef<char>(Unsafe.AsPointer(in start)), gameTitle.Length);

        while (Unsafe.IsAddressLessThan(in start, in end))
        {
            if (char.IsAsciiLetter(start) ||
                char.IsAsciiDigit(start))
            {
                buffer[j++] = start;
            }

            start = ref Unsafe.Add(ref start, 1);
        }

        return new string(buffer[..j]);
    }

    #endregion

    #region Throw Helpers

    private static void ThrowIfDictionaryEmpty<TKey, TValue>(Dictionary<TKey, TValue> dict, [CallerMemberName] string? memberName = null)
        where TKey : notnull
    {
        if (dict.Count == 0)
        {
            throw new InvalidOperationException($"{memberName} was empty!");
        }
    }

    #endregion
}
