﻿using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.Preset;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using static Hi3Helper.Data.ConverterTool;
// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable IdentifierTypo
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Hi3Helper.Shared.ClassStruct
{
    [JsonConverter(typeof(JsonStringEnumConverter<FileType>))]
    public enum FileType : byte { Generic, Block, Audio, Video, Unused }
    public class FilePropertiesRemote : IAssetIndexSummary
    {
        public  bool   IsUsed { get; set; }

        // public long BlkS() => BlkC.Sum(x => x.BlockSize);
        public string N { get; set; }
        public string RN { get; set; }

        public string CRC
        {
            get;
            set
            {
                field     = value.ToLower();
                CRCArray = HexTool.HexToBytesUnsafe(field);
            }
        }

        public byte[]                  CRCArray          { get; private set; }
        public string                  M                 { get; set; }
        public FileType                FT                { get; set; }
        public List<XMFBlockList>      BlkC              { get; set; }
#nullable enable
        public ManifestAudioPatchInfo? AudioPatchInfo    { get; set; }
        public BlockPatchInfo?         BlockPatchInfo    { get; set; }
#nullable restore
        public long                    S                 { get; set; }
        public bool                    IsPatchApplicable { get; set; }
        public bool                    IsBlockNeedRepair { get; set; }
        public bool                    IsHasHashMark     { get; set; }

        public FilePropertiesRemote Copy() => new()
        {
            N = N,
            RN = RN,
            CRC = CRC,
            M = M,
            FT = FT,
            S = S,
            IsPatchApplicable = IsPatchApplicable,
            IsBlockNeedRepair = IsBlockNeedRepair,
            IsHasHashMark = IsHasHashMark,
        };

        public string PrintSummary() => $"File [T: {FT}]: {N}\t{SummarizeSizeSimple(S)} ({S} bytes)";
        public long GetAssetSize() => FT == FileType.Unused ? 0 : S;
        public string GetRemoteURL() => RN;
        public void SetRemoteURL(string url) => RN = url;
    }

    public class FileProperties
    {
        public string FileName { get; set; }
        public FileType DataType { get; set; }
        public string FileSource { get; set; }
        public long FileSize { get; set; }
        public string FileSizeStr => SummarizeSizeSimple(FileSize);
        // public long Offset { get; set; }
        public string ExpctCRC { get; set; }
        public string CurrCRC { get; set; }
    }
}
