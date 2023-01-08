using Hi3Helper.Data;
using Hi3Helper.UABT.Binary;
using System;
using System.Collections.Generic;
using System.IO;

namespace Hi3Helper.EncTool
{
    public class XMFBlock
    {
        private const byte _uniqueIDLength = 3;
        private const byte _hashLength = 16;

        /// <summary>
        /// This initialize the read process of the block information
        /// inside of the XMF file.
        /// </summary>
        /// <param name="reader">The Endianess-aware BinaryReader of the XMF file.</param>
        internal XMFBlock(EndianBinaryReader reader)
        {
            LoadBlockInfoHeader(reader);
            LoadBlockInfoMetadata(reader);
        }

        private void LoadBlockInfoHeader(EndianBinaryReader reader)
        {
            // Read Hash and UniqueID of the block file in XMF metadata.
            Hash = reader.ReadBytes(_hashLength);
            UniqueID = new int[_uniqueIDLength];

            for (int i = 0; i < _uniqueIDLength; i++)
            {
                UniqueID[i] = reader.ReadInt32();
            }

            // Read size of the block file and allocate the AssetEntry based on the
            // count in XMF metadata.
            Size = reader.ReadInt32();
            AssetEntry = new XMFAsset[reader.ReadUInt32()];
        }

        private void LoadBlockInfoMetadata(EndianBinaryReader reader)
        {
            // Read the block information inside of the metadata section.
            for (uint i = 0; i < AssetCount; i++)
            {
                short namelength = reader.ReadInt16();
                ReadOnlySpan<char> name = reader.ReadChars(namelength);
                uint offset = reader.ReadUInt32();

                // Initialize the AssetEntry array.
                AssetEntry[i] = new XMFAsset(name.ToString(), 0, offset, Hash);
            }

            // Get the size information for the block assets.
            // This unfortunately cannot be done directly by reading the XMF file
            // directly since the size information is not included inside of the
            // XMF file. To solve this, we need to subtract the numbers of End
            // and Start Offset.
            LoadBlockInfoAssetSize(AssetCount);

            // Then, create the Asset Index catalog for lookup.
            CreateAssetIndexCatalog(AssetCount);
        }

        private void LoadBlockInfoAssetSize(uint count)
        {
            // Do loop and subtract the numbers of End and Start Offset.
            // If the loop is at the last entry, use the number for the
            // Size of block file instead of the End Offset number.
            for (uint i = 1; i <= count; i++)
            {
                AssetEntry[i - 1].SetOffsetEnd(i == count ? Size : AssetEntry[i].OffsetStart - 1);
            }
        }

        private void CreateAssetIndexCatalog(uint count)
        {
            // Initialize asset lookup catalog and start adding asset name as key
            // and asset index as value.
            AssetIndexCatalog = new Dictionary<string, uint>();
            for (uint i = 0; i < count; i++)
            {
                AssetIndexCatalog.Add(AssetEntry[i].Name, i);
            }
        }

        /// <summary>
        /// Get an asset by using the name string of the asset file.<br/>
        /// If you can't figure out the name you want to get, Use <c>EnumerateAssetNames()</c> to get the asset file names.
        /// </summary>
        /// <param name="name">Given name of the asset file.</param>
        /// <returns>An instance of <c>XMFAsset</c> that contains an information about the asset file.</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        public XMFAsset GetAssetByName(string name)
        {
            if (!AssetIndexCatalog.ContainsKey(name))
            {
                throw new KeyNotFoundException($"Asset \"{name}\" in block: {HashString} doesn't exist!");
            }

            return AssetEntry[AssetIndexCatalog[name]];
        }

        /// <summary>
        /// Get the enumeration of the asset file name in a string form.
        /// </summary>
        /// <returns>Ennumeration of the asset file names.</returns>
        public IEnumerable<string> EnumerateAssetNames()
        {
            uint count = AssetCount;
            for (uint i = 0; i < count; i++)
            {
                yield return AssetEntry[i].Name;
            }
        }

        /// <summary>
        /// Gets a <c>FileStream</c> of the block file.<br/><br/>
        /// If the block file doesn't exist, it will throw <c>FileNotFoundException</c>.
        /// </summary>
        /// <param name="fileAccess">The file access mode for the given Block stream.</param>
        /// <param name="isCreateFile">The operation will create/overwrite the file or not (default: <c>false</c>).</param>
        /// <returns><c>FileStream</c> of the block file.</returns>
        /// <exception cref="FileNotFoundException"></exception>
        public FileStream GetBlockStream(FileAccess fileAccess, bool isCreateFile = false)
        {
            if (IsExist && !isCreateFile)
            {
                throw new FileNotFoundException($"The block file doesn't exist!");
            }

            return new FileStream(FilePath, isCreateFile ? FileMode.Create : FileMode.Open, fileAccess, FileShare.ReadWrite);
        }

        /// <summary>
        /// A Dictionary that includes key of the asset file name with an index as its value.
        /// </summary>
        public Dictionary<string, uint> AssetIndexCatalog { get; private set; }

        /// <summary>
        /// The Unique ID of the block file. Still unsure what's the purpose of this.
        /// </summary>
        public int[] UniqueID { get; private set; }

        /// <summary>
        /// The MD5 Hash of the block file in a byte array form.
        /// </summary>
        public byte[] Hash { get; private set; }

        /// <summary>
        /// The MD5 Hash of the block file in a string form.
        /// </summary>
        public string HashString { get => Hash == null ? null : HexTool.BytesToHexUnsafe(Hash); }

        /// <summary>
        /// The absolute file path of the block file.
        /// </summary>
        public string FilePath { get => Path.Combine(XMFParser._folderPath, HashString + ".wmv"); }

        /// <summary>
        /// Gets a status whether the file exist or not.
        /// </summary>
        public bool IsExist { get => File.Exists(FilePath); }

        /// <summary>
        /// Entries of the Asset file defined as an array of <c>XMFAsset</c> class.
        /// </summary>
        public XMFAsset[] AssetEntry { get; private set; }

        /// <summary>
        /// Gets the number of Asset files defined inside the XMF file.
        /// </summary>
        public uint AssetCount { get => AssetEntry == null ? 0 : (uint)AssetEntry.Length; }

        /// <summary>
        /// Gets a size of current block file.
        /// </summary>
        public long Size { get; private set; }
    }
}
