using Hi3Helper.Data;
using Microsoft.UI.Xaml;
using System;
using System.Collections.ObjectModel;
using System.Runtime.ConstrainedExecution;
using System.Threading.Tasks;

namespace CollapseLauncher
{
    internal enum RepairAssetType
    {
        General,
        Block,
        Audio,
        Video,
        Chunk,
        Unused
    }

    internal enum RepairAssetAction
    {
        Repair,
        Delete,
        Create
    }

    internal interface IRepair : IDisposable
    {
        public ObservableCollection<RepairAssetProperty> RepairAssetEntry { get; set; }
        public event EventHandler<RepairProgress> ProgressChanged;
        public event EventHandler<RepairStatus> StatusChanged;
        public Task<bool> StartCheckRoutine();
        public Task StartRepairRoutine(bool showInteractivePrompt = false);
        public void CancelRoutine();
    }

    internal struct RepairProgress
    {
        public double ProgressPerFilePercentage;
        public double ProgressTotalPercentage;
        public double ProgressTotalEntryCount;
        public double ProgressTotalSpeed;
    }

    internal struct RepairStatus
    {
        public string RepairActivityStatus;

        public string RepairActivityTotal;
        public bool IsProgressTotalIndetermined;

        public string RepairActivityPerFile;
        public bool IsProgressPerFileIndetermined;

        public bool IsAssetEntryPanelShow;

        public bool IsCompleted;
        public bool IsCanceled;
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
                throw new ArgumentException($"Version form should be in \"x.x.x\" format! (current value: \"{version}\")");
            }

            if (!int.TryParse(ver[0], out Major)) throw new ArgumentException($"Major version is not a number! (current value: {ver[0]}");
            if (!int.TryParse(ver[1], out Minor)) throw new ArgumentException($"Minor version is not a number! (current value: {ver[1]}");
            if (!int.TryParse(ver[2], out Revision)) throw new ArgumentException($"Revision version is not a number! (current value: {ver[2]}");
        }

        public string VersionString { get => string.Join('.', VersionArray); }
        public int[] VersionArrayXMF { get => new int[4] { 0, Major, Minor, Revision }; }
        public int[] VersionArray { get => new int[3] { Major, Minor, Revision }; }
        public readonly int Major;
        public readonly int Minor;
        public readonly int Revision;
    }

    internal readonly struct RepairAssetProperty
    {
        internal RepairAssetProperty(
            string name, RepairAssetType assetType, string source,
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
        public RepairAssetType AssetType { get; private init; }
        public string Source { get; private init; }
        public long Size { get; private init; }
        public string SizeStr { get => ConverterTool.SummarizeSizeSimple(Size); }
        public string LocalCRC { get => LocalCRCByte == null ? "-" : HexTool.BytesToHexUnsafe(LocalCRCByte); }
        public byte[] LocalCRCByte { get; private init; }
        public string RemoteCRC { get => RemoteCRCByte == null ? "-" : HexTool.BytesToHexUnsafe(RemoteCRCByte); }
        public byte[] RemoteCRCByte { get; private init; }
    }
}
