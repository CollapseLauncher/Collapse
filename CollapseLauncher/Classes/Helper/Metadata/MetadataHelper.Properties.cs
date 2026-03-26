using Hi3Helper.Shared.Region;
using System;
using System.Collections.Generic;
using System.IO;

#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.Metadata;

internal static partial class MetadataHelper
{
    #region Private Constants and Static Properties

    private const string UnknownString                 = "Unknown";
    private const string MetadataVersion               = "v3";
    private const string LauncherMetadataStampFileName = "stamp.json";

    private static Stamp LauncherMetadataStampStamp => new() { MetadataPath = LauncherMetadataStampFileName };

    private static string LauncherReleaseChannel => LauncherConfig.IsPreview ? "preview" : "stable";
    private static string LauncherMetadataSuffixPathTemplate => "/metadata/{0}/{1}/{2}";

    private static bool _lockIsUpdateInstanceRunning;

    private static readonly Dictionary<string, Stamp> StampMasterKeyDict     = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Stamp> StampCommunityToolDict = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Stamp> StampGameDict          = new(StringComparer.OrdinalIgnoreCase);

    public static string        CurrentGameTitleName  = "";
    public static string        CurrentGameRegionName = "";
    public static PresetConfig? CurrentGameConfig     = null;
    #endregion

    #region Shared Static Properties
    internal static string LauncherMetadataDirectory => Path.Combine(LauncherConfig.AppGameFolder, $"_metadata{MetadataVersion}");

    internal static MasterKeyConfig? CurrentMasterKey { get; private set; }
    internal static Dictionary<string, Dictionary<string, PresetConfig>> ConfigGameDict { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal static CommunityToolsProperty CommunityToolsProperty { get; } = new();

    internal static List<string> CurrentGameTitleList => [..ConfigGameDict.Keys];
    #endregion
}
