using System.Collections.Generic;
using System.Linq;

namespace Hi3Helper.Preset
{
    public class ChunkProperties
    {
        public string ChunkOffset { get; set; }
        public string ChunkSize { get; set; }
        public string ChunkName { get; set; }
    }

    public class BlockName
    {
        public string BlockHash { get; set; }
        public string BlockStatus { get; set; }
        public List<ChunkProperties> ChunkItems { get; set; }
    }

    public class GameZoneName
    {
        public string ZoneName { get; set; }
        public string ZoneStatus { get; set; }
        public List<BlockName> BlockItems { get; set; }
    }

    public class XMFDictionaryClasses
    {
        public class VersionFile
        {
            public string N { get; set; }
            public string CRC { get; set; }
            public string CS { get; set; }
        }

        public class XMFBlockList
        {
            public long SumDownloadableContent() =>
                BlockContent.Count != 0 ? BlockContent.Sum(i => i.FileSize) :
                BlockMissing || BlockUnused ? BlockSize :
                BlockSize < BlockExistingSize ? BlockSize :
                (BlockSize - BlockExistingSize);

            // XMF Block Section
            public string BlockHash { get; set; }
            public long BlockSize { get; set; }
            public long BlockExistingSize { get; set; }
            public bool BlockMissing { get; set; }
            public bool BlockUnused { get; set; }
            public List<XMFFileProperty> BlockContent = new List<XMFFileProperty>();
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
        }

        public class PatchFilesList
        {
            // Patch book section
            public virtual uint PatchCount { get; set; }
            public List<PatchFileProperty> PatchContent = new List<PatchFileProperty>();
        }

        public class PatchFileProperty
        {
            // Files Section
            public virtual string SourceFileName { get; set; }
            public virtual string TargetFileName { get; set; }
            public virtual string PatchFileName { get; set; }
            public virtual string PatchDir { get; set; }
            public virtual uint PatchFileSize { get; set; }
        }
    }
}
