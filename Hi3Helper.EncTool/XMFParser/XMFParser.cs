using Hi3Helper.Data;
using Hi3Helper.UABT.Binary;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Hi3Helper.EncTool
{
    public sealed partial class XMFParser
    {
        private const byte _versioningLength = 4;
        private const byte _allowedMinVersion = 0;
        private const byte _allowedMaxVersion = 64;
        private string _xmfPath { get; set; }
        internal static string _folderPath;
        private EndianBinaryReader _bigEndBinReader { get; set; }

        /// <summary>
        /// Represent the class of the XMFParser.<br/>
        /// During initialization, the XMF file or directory path that contains a XMF file defined in "<c>path</c>" argument will be read.
        /// </summary>
        /// <param name="path">The path of a directory that contains a XMF file or a path of the XMF file itself.</param>
        public XMFParser(string path)
        {
            _xmfPath = path;

            TryCheckXMFPath();
            ParseMetadata();

#if DEBUG
            Console.WriteLine($"XMF File Loaded   : {_xmfPath}");
            Console.WriteLine($"Folder Path       : {_folderPath}");
            Console.WriteLine($"Version Signature : {HexTool.BytesToHexUnsafe(VersionSignature)}");
            Console.WriteLine($"Manifest Version  : {string.Join('.', Version)}");
            Console.WriteLine($"Block Count       : {BlockCount}");
            Console.WriteLine($"Block Total Size  : {BlockTotalSize} bytes");
            Console.WriteLine($"Asset Entries     : {BlockEntry.Sum(x => x.AssetCount)}");
#endif
        }

        /// <summary>
        /// A Dictionary that includes key of the block hash with an index as its value.
        /// </summary>
        public Dictionary<string, uint> BlockIndexCatalog { get; private set; }

        /// <summary>
        /// The signature/hash of the game version. This section is at the first 16 bytes inside the XMF file.
        /// </summary>
        public byte[] VersionSignature { get; private set; }

        /// <summary>
        /// The version of the game defined as Int32 array.
        /// </summary>
        public int[] Version { get; private set; }

        /// <summary>
        /// Entries of the Block file defined as an array of <c>XMFBlock</c> class.
        /// </summary>
        public XMFBlock[] BlockEntry { get; private set; }

        /// <summary>
        /// Gets the number of Blocks defined inside the XMF file.
        /// </summary>
        public uint BlockCount
        {
            get
            {
                if (BlockEntry == null) return 0;
                return (uint)BlockEntry.Length;
            }
        }

        /// <summary>
        /// Gets a total size of the block files.
        /// </summary>
        public long BlockTotalSize
        {
            get
            {
                if (BlockEntry == null) return 0;
                return BlockEntry.Sum(x => x.Size);
            }
        }
    }
}
