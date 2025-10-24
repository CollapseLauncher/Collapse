using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Data;
using System;
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
        public string ActivityStatus { get; set; }
        public bool ActivityStatusInternet { get; set; }

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
            VendorType = Enum.GetName(fallbackVendorType);

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
            VendorType = Enum.GetName(_VendorType);
            GameName = infoEntries[1];
        }

        public string? VendorType { get; set; }
        public string? GameName { get; set; }
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
        string? source,
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
