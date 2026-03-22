using Hi3Helper.Data;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Extension;

internal static class FontCollections
{
    internal static FontFamily FontAwesomeSolid   { get; }
    internal static FontFamily FontAwesomeRegular { get; }
    internal static FontFamily FontAwesomeBrand   { get; }

    private static readonly Dictionary<string, FontFamily> LookupByResourceKey = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, FontFamily>.AlternateLookup<ReadOnlySpan<char>> AltLookupByResourceKey =
        LookupByResourceKey.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly Dictionary<string, FontFamily> LookupBySourcePath = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, FontFamily>.AlternateLookup<ReadOnlySpan<char>> AltLookupBySourcePath =
        LookupBySourcePath.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly Dictionary<string, FontFamily> LookupByFontNameClass = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, FontFamily>.AlternateLookup<ReadOnlySpan<char>> AltLookupByFontNameClass =
        LookupByFontNameClass.GetAlternateLookup<ReadOnlySpan<char>>();

    private const string TrimChars = "\\/ ";

    public static bool TryGetFontFamily(
        ReadOnlySpan<char> key,
        [NotNullWhen(true)]
        out FontFamily? fontFamily)
    {
        Unsafe.SkipInit(out fontFamily);
        if (key.IsEmpty)
            return false;

        key = key.Trim(TrimChars);
        if (AltLookupByResourceKey.TryGetValue(key, out fontFamily))
            return true;

        if (AltLookupByFontNameClass.TryGetValue(key, out fontFamily))
            return true;

        if (AltLookupBySourcePath.TryGetValue(key, out fontFamily))
            return true;

        if (key.IndexOf('#') is var fontNameClassKey and >= 0 && (
                TryGetFontFamily(key[..fontNameClassKey].Trim(TrimChars),       out fontFamily) || // Lookup by path
                TryGetFontFamily(key[(fontNameClassKey + 1)..].Trim(TrimChars), out fontFamily)    // Lookup by class name
                ))
            return true;

        string fullyQualifiedPath = key.ToString().GetFullyQualifiedPath();
        return AltLookupBySourcePath.TryGetValue(fullyQualifiedPath, out fontFamily);
    }

    static FontCollections()
    {
        foreach (KeyValuePair<object, object> resource in UIElementExtensions.CurrentResourceDictionary)
        {
            if (resource.Value is not FontFamily family ||
                resource.Key == null!)
                continue;

            LookupByResourceKey.TryAdd(resource.Key.ToString() ?? "", family);

            string  normalizedPath = family.Source;
            string? fontNameKey    = null;

            if (normalizedPath.IndexOf('#') is var indexOfFontName and >= 0)
            {
                ReadOnlySpan<char> keySpan = normalizedPath;
                normalizedPath = keySpan[..indexOfFontName].Trim(TrimChars).ToString().GetFullyQualifiedPath();
                fontNameKey    = keySpan[(indexOfFontName + 1)..].Trim(TrimChars).ToString();
            }
            else
            {
                normalizedPath = normalizedPath.GetFullyQualifiedPath();
            }

            LookupBySourcePath.TryAdd(normalizedPath, family);
            if (!string.IsNullOrEmpty(fontNameKey))
            {
                LookupByFontNameClass.TryAdd(fontNameKey, family);
            }
        }

        LookupByResourceKey.TryGetValue("FontAwesome",      out FontFamily? fontAwesome);
        LookupByResourceKey.TryGetValue("FontAwesomeSolid", out FontFamily? fontAwesomeSolid);
        LookupByResourceKey.TryGetValue("FontAwesomeBrand", out FontFamily? fontAwesomeBrand);

        FontAwesomeRegular = fontAwesome!;
        FontAwesomeSolid   = fontAwesomeSolid!;
        FontAwesomeBrand   = fontAwesomeBrand!;
    }
}
