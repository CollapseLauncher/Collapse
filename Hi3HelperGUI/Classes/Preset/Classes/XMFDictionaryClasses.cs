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

        public class _XMFBlockList
        {
            // XMF Block Section
            public virtual string BlockHash { get; set; }
            public virtual long BlockSize { get; set; }
            public List<_XMFFileProperty> BlockContent = new List<_XMFFileProperty>();
        }

        public class _XMFFileProperty
        {
            // Files Section
            public virtual string _filename { get; set; }
            public virtual uint _filesize { get; set; }
            public virtual uint _startoffset { get; set; }
            public virtual string _filecrc32 { get; set; }
        }

        public class _PatchFilesList
        {
            // Patch book section
            public virtual uint PatchCount { get; set; }
            public List<_PatchFileProperty> PatchContent = new List<_PatchFileProperty>();
        }

        public class _PatchFileProperty
        {
            // Files Section
            public virtual string _sourcefilename { get; set; }
            public virtual string _targetfilename { get; set; }
            public virtual string _patchfilename { get; set; }
            public virtual string _patchdir { get; set; }
            public virtual uint _patchfilesize { get; set; }
        }
    }
}
