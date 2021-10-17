using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hi3HelperGUI.Preset
{
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
                BlockMissing ? BlockSize :
                BlockSize < BlockExistingSize ? BlockSize :
                (BlockSize - BlockExistingSize);

            // XMF Block Section
            public string BlockHash { get; set; }
            public long BlockSize { get; set; }
            public long BlockExistingSize { get; set; }
            public bool BlockMissing { get; set; }
            public List<XMFFileProperty> BlockContent = new();
        }

        public class XMFFileProperty
        {
            // Files Section
            public virtual string FileName { get; set; }
            public virtual uint FileSize { get; set; }
            public virtual uint StartOffset { get; set; }
            public virtual string FileHash { get; set; }
            public virtual string FileActualHash { get; set; }
        }

        public class PatchFilesList
        {
            // Patch book section
            public virtual uint PatchCount { get; set; }
            public List<PatchFileProperty> PatchContent = new();
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
