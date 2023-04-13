using Hi3Helper.Data;
using System.Collections.Generic;

namespace Hi3Helper.Preset
{
    public class XMFBlockList
    {
        // XMF Block Section
        public string BlockHash { get; set; }
        public long BlockSize { get; set; }
        public long BlockExistingSize { get; set; }
        public bool BlockMissing { get; set; }
        public bool BlockUnused { get; set; }
        public List<XMFFileProperty> BlockContent { get; set; }
    }

    public class XMFFileProperty
    {
        // Files Section
        public virtual string FileName { get; set; }
        public virtual uint FileSize { get; set; }
        public virtual uint StartOffset { get; set; }
        public virtual int FileHashArray { get; set; }
        public virtual int FileActualHashArray { get; set; }
        public long _filesize { get; set; }
        public long _startoffset { get; set; }
        public string _filecrc32 { get; set; }
        public byte[] _filecrc32array { get => HexTool.HexToBytesUnsafe(_filecrc32); }
    }
}
