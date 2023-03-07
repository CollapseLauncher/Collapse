using Hi3Helper.Data;
using Hi3Helper.Preset;
using System;
using System.IO;

namespace CollapseLauncher
{
    internal struct TotalPerfileProgress
    {
        public double ProgressPerFilePercentage;
        public double ProgressTotalPercentage;
        public double ProgressTotalEntryCount;
        public double ProgressTotalSpeed;
    }

    internal struct TotalPerfileStatus
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

    public struct GameVersion
    {
        public GameVersion(int major, int minor, int rev)
        {
            Major = major;
            Minor = minor;
            Revision = rev;
        }

        public GameVersion(int[] version)
        {
            if (version.Length != 3)
            {
                throw new ArgumentException($"Version array entered should have length of 3!");
            }

            Major = version[0];
            Minor = version[1];
            Revision = version[2];
        }

        public GameVersion(string version)
        {
            string[] ver = version.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (ver.Length != 3)
            {
                throw new ArgumentException($"Version in the config.ini should be in \"x.x.x\" format! (current value: \"{version}\")");
            }

            if (!int.TryParse(ver[0], out Major)) throw new ArgumentException($"Major version is not a number! (current value: {ver[0]}");
            if (!int.TryParse(ver[1], out Minor)) throw new ArgumentException($"Minor version is not a number! (current value: {ver[1]}");
            if (!int.TryParse(ver[2], out Revision)) throw new ArgumentException($"Revision version is not a number! (current value: {ver[2]}");
        }

        public bool IsMatch(GameVersion versionToCompare) => Major == versionToCompare.Major && Minor == versionToCompare.Minor && Revision == versionToCompare.Revision;

        public string VersionString { get => string.Join('.', VersionArray); }
        public int[] VersionArrayXMF { get => new int[4] { 0, Major, Minor, Revision }; }
        public int[] VersionArrayAudioManifest { get => new int[4] { Major, Minor, Revision, 0 }; }
        public int[] VersionArray { get => new int[3] { Major, Minor, Revision }; }
        public readonly int Major;
        public readonly int Minor;
        public readonly int Revision;
    }

    internal readonly struct AssetProperty<T>
        where T : Enum
    {
        internal AssetProperty(
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

        public string Name { get; private init; }
        public T AssetType { get; private init; }
        public string Source { get; private init; }
        public long Size { get; private init; }
        public string SizeStr { get => ConverterTool.SummarizeSizeSimple(Size); }
        public string LocalCRC { get => LocalCRCByte == null ? "-" : HexTool.BytesToHexUnsafe(LocalCRCByte); }
        public byte[] LocalCRCByte { get; private init; }
        public string RemoteCRC { get => RemoteCRCByte == null ? "-" : HexTool.BytesToHexUnsafe(RemoteCRCByte); }
        public byte[] RemoteCRCByte { get; private init; }
    }
}
