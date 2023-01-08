using Hi3Helper.UABT.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Hi3Helper.EncTool
{
    public sealed partial class XMFParser
    {
        private void TryCheckXMFPath()
        {
            // If the path does exist as a file, then set _folderPath.
            if (File.Exists(_xmfPath))
            {
                _folderPath = Path.GetDirectoryName(_xmfPath);
                return;
            }

            // Try find XMF file by enumerating the content of the given path as a directory.
            if (!Directory.Exists(_xmfPath))
            {
                throw new DirectoryNotFoundException($"You're trying to load XMF from a directory in this path: \"{_xmfPath}\" and it doesn't exist.");
            }

            // Try enumerate XMF file from the given path.
            string assumedXMFPath = Directory.EnumerateFiles(_xmfPath, "Blocks*.xmf", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (assumedXMFPath != null)
            {
                _xmfPath = assumedXMFPath;
                _folderPath = Path.GetDirectoryName(assumedXMFPath);
                return;
            }

            // If it doesn't, then...
            throw new FileNotFoundException($"XMF file in this path: \"{_xmfPath}\" doesn't exist or the directory with the path given has no XMF file inside it.");
        }

        private void ParseMetadata()
        {
            using (Stream stream = new FileStream(_xmfPath, FileMode.Open, FileAccess.Read))
            {
                // Read XMF with Endianess-aware BinaryReader
                using (_bigEndBinReader = new EndianBinaryReader(stream))
                {
                    // Start read the header of the XMF file.
                    ReadHeader();
                    // Start read the metadata including block info and asset indexes.
                    ReadMetadata();
                    // Finalize by creating catalog for block lookup as hash name and index.
                    // This will make searching process for the block easier.
                    CreateBlockIndexCatalog(BlockCount);
                }
            }
        }

        private void CreateBlockIndexCatalog(uint count)
        {
            // Initialize block lookup catalog and start adding hash as key and index as value.
            BlockIndexCatalog = new Dictionary<string, uint>();
            for (uint i = 0; i < count; i++)
            {
                BlockIndexCatalog.Add(BlockEntry[i].HashString, i);
            }
        }

        private void ReadHeader()
        {
            // Switch to Little-endian to read header.
            _bigEndBinReader.endian = UABT.EndianType.LittleEndian;
            VersionSignature = _bigEndBinReader.ReadBytes(0x10);

            // Read block version.
            Version = new int[_versioningLength];
            for (int i = 0; i < _versioningLength; i++)
            {
                Version[i] = _bigEndBinReader.ReadInt32();
                if (Version[i] < _allowedMinVersion || Version[i] > _allowedMaxVersion)
                {
                    throw new InvalidDataException($"Header version on array: {i} is invalid with value: {Version[i]}. The allowed range is: ({_allowedMinVersion} - {_allowedMaxVersion})");
                }
            }

            // Try read endian mode.
            byte readMode = _bigEndBinReader.ReadByte();
            if (readMode > 1)
            {
                throw new InvalidDataException("Read mode is invalid! The value should be 0 for Big-endian and 1 for Little-endian");
            }

            // If readMode == 0, then switch to Big-endian.
            if (readMode == 0) _bigEndBinReader.endian = UABT.EndianType.BigEndian;

            // Allocate the size of Block array.
            BlockEntry = new XMFBlock[_bigEndBinReader.ReadUInt32()];
        }

        private void ReadMetadata()
        {
            // Initialize the XMFBlock instance to the BlockEntry array.
            // At the same time, the XMFBlock will read the metadata section of the block.
            for (int i = 0; i < BlockEntry.Length; i++)
            {
                BlockEntry[i] = new XMFBlock(_bigEndBinReader);
            }
        }

        /// <summary>
        /// Get a block by using the hash string of the block.<br/>
        /// If you can't figure out the hash you want to get, Use <c>EnumerateBlockHashString()</c> to get the block hashes.
        /// </summary>
        /// <param name="hash">Given hash string of the block.</param>
        /// <returns>An instance of <c>XMFBlock</c> that contains an information about the block.</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        public XMFBlock GetBlockByHashString(string hash)
        {
            // Check if the block catalog contains the key name.
            // If not, then throw.
            if (!BlockIndexCatalog.ContainsKey(hash))
            {
                throw new KeyNotFoundException($"Block: {hash} doesn't exist!");
            }

            // If found, then return the block entry.
            return BlockEntry[BlockIndexCatalog[hash]];
        }

        /// <summary>
        /// Get the enumeration of the block hashes in a string form.
        /// </summary>
        /// <returns>Ennumeration of the block hashes.</returns>
        public IEnumerable<string> EnumerateBlockHashString()
        {
            uint count = BlockCount;
            for (uint i = 0; i < count; i++)
            {
                yield return BlockEntry[i].HashString;
            }
        }

        /// <summary>
        /// Get the enumeration of the block hashes in a byte array.
        /// </summary>
        /// <returns>Ennumeration of the block hashes in byte array form.</returns>
        public IEnumerable<byte[]> EnumerateBlockHash()
        {
            uint count = BlockCount;
            for (uint i = 0; i < count; i++)
            {
                yield return BlockEntry[i].Hash;
            }
        }
    }
}
