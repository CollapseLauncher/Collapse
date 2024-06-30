using CollapseLauncher.Helper.Metadata;
using Hi3Helper.Data;
using Hi3Helper.Http;
using System;
using System.IO;

namespace CollapseLauncher
{
    internal class TotalPerfileProgress
    {
        private double _progressPerFilePercentage;
        private double _progressTotalPercentage;
        private double _progressTotalEntryCount;
        private double _progressTotalSpeed;

        public double ProgressPerFilePercentage { get => _progressPerFilePercentage; set => _progressPerFilePercentage = value.UnNaNInfinity(); }
        public double ProgressTotalPercentage { get => _progressTotalPercentage; set => _progressTotalPercentage = value.UnNaNInfinity(); }
        public double ProgressTotalEntryCount { get => _progressTotalEntryCount; set => _progressTotalEntryCount = value.UnNaNInfinity(); }
        public double ProgressTotalSpeed { get => _progressTotalSpeed; set => _progressTotalSpeed = value.UnNaNInfinity(); }

        // Extension for IGameInstallManager
        public long ProgressPerFileDownload { get; set; }
        public long ProgressPerFileSizeToDownload { get; set; }
        public long ProgressTotalDownload { get; set; }
        public long ProgressTotalSizeToDownload { get; set; }
        public TimeSpan ProgressTotalTimeLeft;
        public DownloadEvent DownloadEvent;
    }

    internal class TotalPerfileStatus
    {
        public string ActivityStatus;

        public string ActivityTotal;
        public bool IsProgressTotalIndetermined;

        public string ActivityPerFile;
        public bool IsProgressPerFileIndetermined;

        public bool IsAssetEntryPanelShow;

        public bool IsCompleted;
        public bool IsCanceled;
        public bool IsRunning;

        public bool IsIncludePerFileIndicator;
    }

    internal struct GameVendorProp
    {
#nullable enable
        public GameVendorProp(string gamePath, string execName, GameVendorType fallbackVendorType)
        {
            // Set the fallback value of the Vendor Type first (in case the game isn't yet installed)
            VendorType = fallbackVendorType;

            // Concat the vendor app info file and return if it doesn't exist
            string infoVendorPath = Path.Combine(gamePath, $"{execName}_Data\\app.info");
            if (!File.Exists(infoVendorPath)) return;

            // If does, then process the file
            string[] infoEntries = File.ReadAllLines(infoVendorPath);
            if (infoEntries.Length < 2) return;

            // Try parse the first line. If parsing fail, then return
            if (!Enum.TryParse(infoEntries[0], out GameVendorType _VendorType)) return;

            // Assign the values
            VendorType = _VendorType;
            GameName = infoEntries[1];
        }

        public GameVendorType? VendorType { get; set; }
        public string? GameName { get; set; }
#nullable disable
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

    public struct GameVersion
    {
        public GameVersion(int major, int minor, int build, int revision = 0)
        {
            Major = major;
            Minor = minor;
            Build = build;
            Revision = revision;
        }

        public GameVersion(ReadOnlySpan<int> ver)
        {
            if (!(ver.Length == 3 || ver.Length == 4))
            {
                throw new ArgumentException($"Version array entered should have length of 3 or 4!");
            }

            Major = ver[0];
            Minor = ver[1];
            Build = ver[2];
            if (ver.Length == 4)
            {
                Revision = ver[3];
            }
        }

        public GameVersion(Version version)
        {
            Major = version.Major;
            Minor = version.Minor;
            Build = version.Build;
        }

        public GameVersion(string version)
        {
            string[] ver = version.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (!(ver.Length == 3 || ver.Length == 4))
            {
                throw new ArgumentException($"Version in the config.ini should be in \"x.x.x\" or \"x.x.x.x\" format! (current value: \"{version}\")");
            }

            if (!int.TryParse(ver[0], out Major)) throw new ArgumentException($"Major version is not a number! (current value: {ver[0]}");
            if (!int.TryParse(ver[1], out Minor)) throw new ArgumentException($"Minor version is not a number! (current value: {ver[1]}");
            if (!int.TryParse(ver[2], out Build)) throw new ArgumentException($"Build version is not a number! (current value: {ver[2]}");
            if (ver.Length == 4)
            {
                if (!int.TryParse(ver[3], out Revision)) throw new ArgumentException($"Revision version is not a number! (current value: {ver[3]}");
            }
        }

        public static bool TryParse(string version, out GameVersion? result)
        {
            result = null;
            Span<Range> ranges = stackalloc Range[8];
            ReadOnlySpan<char> versionSpan = version.AsSpan();
            int splitRanges = versionSpan.Split(ranges, '.', StringSplitOptions.TrimEntries);

            if (!(splitRanges == 3 || splitRanges == 4)) return false;

            Span<int> versionSplits = stackalloc int[4];
            for (int i = 0; i < splitRanges; i++)
            {
                if (!int.TryParse(versionSpan[ranges[i]], null, out int versionParsed))
                    return false;

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
            if (versionToCompare == null) return false;
            return Major == versionToCompare?.Major && Minor == versionToCompare?.Minor && Build == versionToCompare?.Build && Revision == versionToCompare?.Revision;
        }

        public GameVersion GetIncrementedVersion()
        {
            int NextMajor = Major;
            int NextMinor = Minor;

            NextMinor++;
            if (NextMinor >= 10)
            {
                NextMinor = 0;
                NextMajor++;
            }

            return new GameVersion(new int[] { NextMajor, NextMinor, Build, Revision });
        }

        public Version ToVersion() => new Version(Major, Minor, Build, Revision);
        public override string ToString() => $"{Major}.{Minor}.{Build}";

        public string VersionStringManifest { get => string.Join('.', VersionArrayManifest); }
        public string VersionString { get => string.Join('.', VersionArray); }
        public int[] VersionArrayManifest { get => new int[4] { Major, Minor, Build, Revision }; }
        public int[] VersionArray { get => new int[3] { Major, Minor, Build }; }
        public readonly int Major;
        public readonly int Minor;
        public readonly int Build;
        public readonly int Revision;
    }

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

    public class AssetProperty<T> : IAssetProperty
    {
        public AssetProperty(
            string name, T assetType, string source,
            long size, byte[] localCRCByte, byte[] remoteCRCByte)
        {
            Name = name;
            AssetType = assetType;
            Source = '\\' + source;
            Size = size;
            LocalCRCByte = localCRCByte;
            RemoteCRCByte = remoteCRCByte;
        }

        public string AssetTypeString { get => Enum.GetName(typeof(T), AssetType); }
        public string Name { get; private init; }
        public T AssetType { get; private init; }
        public string Source { get; private init; }
        public long Size { get; private init; }
        public string SizeStr { get => ConverterTool.SummarizeSizeSimple(Size); }
        public string LocalCRC { get => LocalCRCByte == null ? "-" : HexTool.BytesToHexUnsafe(LocalCRCByte); }
        public byte[] LocalCRCByte { get; private init; }
        public string RemoteCRC { get => RemoteCRCByte == null ? "-" : HexTool.BytesToHexUnsafe(RemoteCRCByte); }
        public byte[] RemoteCRCByte { get; private init; }

        public IAssetProperty ToIAssetProperty() => this;
    }
}
