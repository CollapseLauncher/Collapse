﻿using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Data;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMemberInSuper.Global

namespace CollapseLauncher
{
    internal class TotalPerFileProgress
    {
        public double ProgressPerFilePercentage;
        public double ProgressPerFileSpeed;
        public double ProgressAllPercentage;
        public double ProgressAllSpeed;

        public long ProgressPerFileEntryCountCurrent;
        public long ProgressPerFileEntryCountTotal;
        public long ProgressAllEntryCountCurrent;
        public long ProgressAllEntryCountTotal;

        // Extension for IGameInstallManager
        public long ProgressPerFileSizeCurrent;
        public long ProgressPerFileSizeTotal;
        public long ProgressAllSizeCurrent;
        public long ProgressAllSizeTotal;
        public TimeSpan ProgressAllTimeLeft;
    }

    internal class TotalPerFileStatus
    {
        public string ActivityStatus         { get; set; }
        public bool   ActivityStatusInternet { get; set; }

        public string ActivityAll { get; set; }
        public bool IsProgressAllIndetermined { get; set; }

        public string ActivityPerFile { get; set; }
        public bool IsProgressPerFileIndetermined { get; set; }

        public bool IsAssetEntryPanelShow { get; set; }

        public bool IsCompleted { get; set; }
        public bool IsCanceled { get; set; }
        public bool IsRunning { get; set; }

        public bool IsIncludePerFileIndicator { get; set; }
    }

#nullable enable
    internal struct GameVendorProp
    {
        public GameVendorProp(string gamePath, string execName, GameVendorType fallbackVendorType)
        {
            // Set the fallback value of the Vendor Type first (in case the game isn't yet installed)
            VendorType = fallbackVendorType;

            // Concat the vendor app info file and return if it doesn't exist
            string infoVendorPath = Path.Combine(gamePath, $"{execName}_Data\\app.info");
            if (!File.Exists(infoVendorPath))
            {
                Logger.LogWriteLine("app.info file is not found!", LogType.Error);
                return;
            }

            // If it does, then process the file
            string[] infoEntries = File.ReadAllLines(infoVendorPath);
            if (infoEntries.Length < 2)
            {
                Logger.LogWriteLine("app.info file is malformed!", LogType.Error);
                return;
            }

            // Try parse the first line. If parsing fail, then return
            if (!Enum.TryParse(infoEntries[0], out GameVendorType _VendorType))
            {
                Logger.LogWriteLine($"Failed when parsing app.info\r\n\t{infoEntries[0]}\r\n\t{infoEntries[1]}");
                return;
            }

            // Assign the values
            VendorType = _VendorType;
            GameName = infoEntries[1];
        }

        public GameVendorType? VendorType { get; set; }
        public string? GameName { get; set; }
    }

    public static class GameVersionExt
    {
        public static bool Compare(this GameVersion? fromVersion, GameVersion? toVersion)
        {
            if (!fromVersion.HasValue || !toVersion.HasValue) return false;
            return fromVersion.Value.ToVersion() < toVersion.Value.ToVersion();
        }

        public static bool Equals(this GameVersion? fromVersion, GameVersion? toVersion)
        {
            if (!fromVersion.HasValue || !toVersion.HasValue) return false;
            return fromVersion.Value.ToVersion() == toVersion.Value.ToVersion();
        }
    }

    public readonly record struct GameVersion
    {
        public GameVersion(params ReadOnlySpan<int> ver)
        {
            if (ver.Length == 0)
            {
                throw new ArgumentException("Version array entered should have length at least 1 or max. 4!");
            }

            Major    = ver[0];
            Minor    = ver.Length >= 2 ? ver[1] : 0;
            Build    = ver.Length >= 3 ? ver[2] : 0;
            Revision = ver.Length >= 4 ? ver[3] : 0;
        }

        public GameVersion(Version version)
        {
            Major    = version.Major;
            Minor    = version.Minor;
            Build    = version.Build;
            Revision = version.Revision;
        }

        public GameVersion(string? version)
        {
            if (!TryParse(version, out GameVersion? versionOut) || !versionOut.HasValue)
            {
                throw new ArgumentException($"Version in the config.ini should be either in \"x\", \"x.x\", \"x.x.x\" or \"x.x.x.x\" format or all the values aren't numbers! (current value: \"{version}\")");
            }

            Major    = versionOut.Value.Major;
            Minor    = versionOut.Value.Minor;
            Build    = versionOut.Value.Build;
            Revision = versionOut.Value.Revision;
        }

        public static bool TryParse(string? version, [NotNullWhen(true)] out GameVersion? result)
        {
            const string Separators = ",.;|";

            result = null;
            if (string.IsNullOrEmpty(version))
            {
                return false;
            }

            Span<Range>        ranges      = stackalloc Range[8];
            ReadOnlySpan<char> versionSpan = version.AsSpan();
            int                splitRanges = versionSpan.SplitAny(ranges, Separators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (splitRanges == 0)
            {
                if (!int.TryParse(versionSpan, null, out int majorOnly))
                {
                    return false;
                }

                result = new GameVersion(majorOnly);
                return true;
            }

            Span<int> versionSplits = stackalloc int[4];
            for (int i = 0; i < splitRanges; i++)
            {
                if (!int.TryParse(versionSpan[ranges[i]], null, out int versionParsed))
                {
                    return false;
                }

                versionSplits[i] = versionParsed;
            }

            result = new GameVersion(versionSplits);
            return true;
        }

        public bool IsMatch(string versionToCompare)
        {
            GameVersion parsed = new GameVersion(versionToCompare);
            return IsMatch(parsed);
        }

        public bool IsMatch(GameVersion? versionToCompare)
        {
            if (!versionToCompare.HasValue) return false;
            return Major == versionToCompare.Value.Major &&
                   Minor == versionToCompare.Value.Minor &&
                   Build == versionToCompare.Value.Build &&
                   Revision == versionToCompare.Value.Revision;
        }

        public GameVersion GetIncrementedVersion()
        {
            int nextMajor = Major;
            int nextMinor = Minor;

            nextMinor++;
            if (nextMinor < 10)
            {
                return new GameVersion(nextMajor, nextMinor, Build, Revision);
            }

            nextMinor = 0;
            nextMajor++;

            return new GameVersion(nextMajor, nextMinor, Build, Revision);
        }

        public          Version ToVersion() => new(Major, Minor, Build, Revision);
        public override string  ToString()  => $"{Major}.{Minor}.{Build}";

        public          string VersionStringManifest { get => string.Join('.', VersionArrayManifest); }
        public          string VersionString         { get => string.Join('.', VersionArray); }
        public          int[]  VersionArrayManifest  { get => [Major, Minor, Build, Revision]; }
        public          int[]  VersionArray          { get => [Major, Minor, Build]; }
        public readonly int    Major;
        public readonly int    Minor;
        public readonly int    Build;
        public readonly int    Revision;
    }
#nullable restore

    public interface IAssetProperty
    {
        public string Name { get; }
        public string AssetTypeString { get; }
        public string Source { get; }
        public long Size { get; }
        public string SizeStr { get; }
        public string LocalCRC { get; }
        public byte[] LocalCRCByte { get; }
        public string RemoteCRC { get; }
        public byte[] RemoteCRCByte { get; }
    }

#nullable enable
    public class AssetProperty<T>(
        string  name,
        T       assetType,
        string  source,
        long    size,
        byte[]? localCrcByte,
        byte[]? remoteCrcByte)
        : IAssetProperty
        where T : struct, Enum
    {
        public string  AssetTypeString { get => Enum.GetName(AssetType) ?? "Unknown"; }
        public string  Name { get; } = name;
        public T       AssetType { get; } = assetType;
        public string  Source { get; } = '\\' + source;
        public long    Size { get; } = size;
        public string  SizeStr { get => ConverterTool.SummarizeSizeSimple(Size); }
        public string  LocalCRC { get => LocalCRCByte?.Length == 0 ? "-" : HexTool.BytesToHexUnsafe(LocalCRCByte) ?? "-"; }
        public byte[]? LocalCRCByte { get; } = localCrcByte;
        public string  RemoteCRC { get => RemoteCRCByte?.Length == 0 ? "-" : HexTool.BytesToHexUnsafe(RemoteCRCByte) ?? "-"; }
        public byte[]? RemoteCRCByte { get; } = remoteCrcByte;

        public IAssetProperty ToIAssetProperty() => this;
    }
}
