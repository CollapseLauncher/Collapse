using Hi3Helper.Data;
using Hi3Helper.UABT.Binary;
using System;
using System.IO;

namespace Hi3Helper.EncTool
{
    public class XMFAsset
    {
        private ReadOnlyMemory<byte> _parentHash { get; set; }
        private string _parentHashString { get => HexTool.BytesToHexUnsafe(_parentHash.Span); }
        private string _parentHashFilePath { get => Path.Combine(XMFParser._folderPath, _parentHashString + ".wmv"); }
        private long _parentHashFileSize { get => new FileInfo(_parentHashFilePath).Length; }
        private bool _isParentFileExist { get => File.Exists(_parentHashFilePath); }

        /// <summary>
        /// This class contains some information about the asset inside of the block file,
        /// including the size, name and offsets.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="end"></param>
        /// <param name="start"></param>
        /// <param name="parentBlockIndex"></param>
        internal XMFAsset(string name, uint end, uint start, ReadOnlyMemory<byte> parentBlockIndex)
        {
            _parentHash = parentBlockIndex;
            Name = name;
            OffsetEnd = end;
            OffsetStart = start;
        }

        /// <summary>
        /// Gets a <c>ChunkStream</c> of the asset file with Read-only mode.<br/>
        /// This will give you a direct access of Read operation within offsets of the asset file inside
        /// of its block file.
        /// </summary>
        /// <param name="skipHeaderCheck">Skip UnityFS header check (default: false)</param>
        /// <returns>Stream of the asset file with Read-only mode.</returns>
        public ChunkStream GetAssetStreamOpenRead(bool skipHeaderCheck = false)
        {
            CheckParentFileAndThrow();

            if (!skipHeaderCheck) IsUnityFSHeaderValid();

            Stream fs = new FileStream(_parentHashFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return new ChunkStream(fs, OffsetStart, OffsetEnd);
        }

        /// <summary>
        /// Gets a <c>ChunkStream</c> of the asset file with Write-only mode.<br/>
        /// This will give you a direct access of Write operation within offsets of the asset file inside
        /// of its block file.<br/><br/>
        /// However, you can't write data more than the given size.
        /// </summary>
        /// <param name="skipHeaderCheck">Skip UnityFS header check (default: false)</param>
        /// <returns>Stream of the asset file with Write-only mode.</returns>
        public ChunkStream GetAssetStreamOpenWrite(bool skipHeaderCheck = false)
        {
            CheckParentFileAndThrow();

            if (!skipHeaderCheck) IsUnityFSHeaderValid();

            Stream fs = new FileStream(_parentHashFilePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            return new ChunkStream(fs, OffsetStart, OffsetEnd);
        }

        /// <summary>
        /// Gets a <c>ChunkStream</c> of the asset file with Read/Write mode.<br/>
        /// This will give you a direct access of Read operation within offsets of the asset file inside
        /// of its block file.<br/><br/>
        /// However, you can't write data more than the given size.
        /// </summary>
        /// <param name="skipHeaderCheck">Skip UnityFS header check (default: false)</param>
        /// <returns>Stream of the asset file with Read/Write mode.</returns>
        public ChunkStream GetAssetStreamOpenReadWrite(bool skipHeaderCheck = false)
        {
            CheckParentFileAndThrow();

            if (!skipHeaderCheck) IsUnityFSHeaderValid();

            Stream fs = new FileStream(_parentHashFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            return new ChunkStream(fs, OffsetStart, OffsetEnd);
        }

        internal void SetOffsetEnd(long offsetEnd) => OffsetEnd = offsetEnd;

        private void CheckParentFileAndThrow()
        {
            if (!_isParentFileExist)
            {
                throw new FileNotFoundException($"Cannot create a stream because parent block file with hash: {_parentHashString} doesn't exist!");
            }
            if (OffsetStart > _parentHashFileSize || _parentHashFileSize < OffsetEnd)
            {
                throw new FileLoadException($"Cannot create a stream because parent block file with hash: {_parentHashString} doesn't have a correct size or maybe corrupted!");
            }
        }

        private void IsUnityFSHeaderValid(long exSig = 0x53467974696E55)
        {
            using (Stream fs = new FileStream(_parentHashFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            using (Stream cs = new ChunkStream(fs, OffsetStart, OffsetEnd))
            using (EndianBinaryReader s = new EndianBinaryReader(cs, UABT.EndianType.LittleEndian))
            {
                long sig = s.ReadInt64();
                if (sig != exSig)
                {
                    throw new FormatException($"Asset file doesn't seem to be a valid UnityFS file! (Expecting: {exSig} but get: {sig} instead)");
                }
            }
        }

        /// <summary>
        /// Gets a name of the asset file
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets an End Offset/Location of the asset file inside of the block file.
        /// </summary>
        public long OffsetEnd { get; private set; }

        /// <summary>
        /// Gets an Start Offset/Location of the asset file inside of the block file.
        /// </summary>
        public long OffsetStart { get; private set; }

        /// <summary>
        /// Gets the size of the asset file inside of the block file.
        /// </summary>
        public long Size { get => OffsetEnd - OffsetStart; }
    }
}
