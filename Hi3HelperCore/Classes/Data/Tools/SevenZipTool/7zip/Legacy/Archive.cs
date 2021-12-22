using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ManagedLzma.LZMA;
using ManagedLzma.LZMA.Master.SevenZip;
using BitVector = ManagedLzma.LZMA.Master.SevenZip.BitVector;
using BlockType = ManagedLzma.LZMA.Master.SevenZip.BlockType;
using CRC = ManagedLzma.CRC;

namespace master._7zip.Legacy
{
    public class CArchiveDatabaseEx
    {
        internal byte MajorVersion;
        internal byte MinorVersion;
        internal long StartPositionAfterHeader;
        internal long DataStartPosition;

        internal List<long> PackSizes = new List<long>();
        internal List<uint?> PackCRCs = new List<uint?>();
        internal List<CFolder> Folders = new List<CFolder>();
        internal List<int> NumUnpackStreamsVector;
        internal List<CFileItem> Files = new List<CFileItem>();

        internal List<long> PackStreamStartPositions = new List<long>();
        internal List<int> FolderStartFileIndex = new List<int>();
        internal List<int> FileIndexToFolderIndexMap = new List<int>();

        internal void Clear()
        {
            PackSizes.Clear();
            PackCRCs.Clear();
            Folders.Clear();
            NumUnpackStreamsVector = null;
            Files.Clear();

            PackStreamStartPositions.Clear();
            FolderStartFileIndex.Clear();
            FileIndexToFolderIndexMap.Clear();
        }

        internal bool IsEmpty()
        {
            return PackSizes.Count == 0
                && PackCRCs.Count == 0
                && Folders.Count == 0
                && NumUnpackStreamsVector.Count == 0
                && Files.Count == 0;
        }

        private void FillStartPos()
        {
            PackStreamStartPositions.Clear();

            long startPos = 0;
            for (int i = 0; i < PackSizes.Count; i++)
            {
                PackStreamStartPositions.Add(startPos);
                startPos += PackSizes[i];
            }
        }

        private void FillFolderStartFileIndex()
        {
            FolderStartFileIndex.Clear();
            FileIndexToFolderIndexMap.Clear();

            int folderIndex = 0;
            int indexInFolder = 0;
            for (int i = 0; i < Files.Count; i++)
            {
                CFileItem file = Files[i];

                bool emptyStream = !file.HasStream;

                if (emptyStream && indexInFolder == 0)
                {
                    FileIndexToFolderIndexMap.Add(-1);
                    continue;
                }

                if (indexInFolder == 0)
                {
                    // v3.13 incorrectly worked with empty folders
                    // v4.07: Loop for skipping empty folders
                    for (;;)
                    {
                        if (folderIndex >= Folders.Count)
                            throw new InvalidDataException();

                        FolderStartFileIndex.Add(i); // check it

                        if (NumUnpackStreamsVector[folderIndex] != 0)
                            break;

                        folderIndex++;
                    }
                }

                FileIndexToFolderIndexMap.Add(folderIndex);

                if (emptyStream)
                    continue;

                indexInFolder++;

                if (indexInFolder >= NumUnpackStreamsVector[folderIndex])
                {
                    folderIndex++;
                    indexInFolder = 0;
                }
            }
        }

        public void Fill()
        {
            FillStartPos();
            FillFolderStartFileIndex();
        }

        internal long GetFolderStreamPos(int folderIndex, int indexInFolder)
        {
            int index = Folders[folderIndex].FirstPackStreamId + indexInFolder;
            return DataStartPosition + PackStreamStartPositions[index];
        }

        internal long GetFolderFullPackSize(int folderIndex)
        {
            int packStreamIndex = Folders[folderIndex].FirstPackStreamId;
            CFolder folder = Folders[folderIndex];

            long size = 0;
            for (int i = 0; i < folder.PackStreams.Count; i++)
                size += PackSizes[packStreamIndex + i];

            return size;
        }

        private long GetFolderPackStreamSize(int folderIndex, int streamIndex)
        {
            return PackSizes[Folders[folderIndex].FirstPackStreamId + streamIndex];
        }

        private long GetFilePackSize(int fileIndex)
        {
            int folderIndex = FileIndexToFolderIndexMap[fileIndex];
            if (folderIndex != -1)
                if (FolderStartFileIndex[folderIndex] == fileIndex)
                    return GetFolderFullPackSize(folderIndex);
            return 0;
        }
    }

    internal class DataReader
    {
        #region Static Methods

        public static uint Get32(byte[] buffer, int offset)
        {
            return (uint)buffer[offset]
                + ((uint)buffer[offset + 1] << 8)
                + ((uint)buffer[offset + 2] << 16)
                + ((uint)buffer[offset + 3] << 24);
        }

        public static ulong Get64(byte[] buffer, int offset)
        {
            return (ulong)buffer[offset]
                + ((ulong)buffer[offset + 1] << 8)
                + ((ulong)buffer[offset + 2] << 16)
                + ((ulong)buffer[offset + 3] << 24)
                + ((ulong)buffer[offset + 4] << 32)
                + ((ulong)buffer[offset + 5] << 40)
                + ((ulong)buffer[offset + 6] << 48)
                + ((ulong)buffer[offset + 7] << 56);
        }

        #endregion

        #region Variables

        private byte[] _buffer;
        private int _origin;
        private int _offset;
        private int _ending;

        #endregion

        #region Public Methods

        public DataReader(byte[] buffer, int offset, int length)
        {
            _buffer = buffer;
            _origin = offset;
            _offset = offset;
            _ending = offset + length;
        }

        public int Offset
        {
            get { return _offset; }
        }

        public Byte ReadByte()
        {
            if (_offset >= _ending)
                throw new EndOfStreamException();

            return _buffer[_offset++];
        }

        public void ReadBytes(byte[] buffer, int offset, int length)
        {
            if (length > _ending - _offset)
                throw new EndOfStreamException();

            while (length-- > 0)
                buffer[offset++] = _buffer[_offset++];
        }

        public void SkipData(long size)
        {
            if (size > _ending - _offset)
                throw new EndOfStreamException();

            _offset += (int)size;
            Log.WriteLine("SkipData {0}", size);
        }

        public void SkipData()
        {
            SkipData(checked((long)ReadNumber()));
        }

        public ulong ReadNumber()
        {
            if (_offset >= _ending)
                throw new EndOfStreamException();

            byte firstByte = _buffer[_offset++];
            byte mask = 0x80;
            ulong value = 0;

            for (int i = 0; i < 8; i++)
            {
                if ((firstByte & mask) == 0)
                {
                    ulong highPart = firstByte & (mask - 1u);
                    value += highPart << (i * 8);
                    return value;
                }

                if (_offset >= _ending)
                    throw new EndOfStreamException();

                value |= (ulong)_buffer[_offset++] << (8 * i);
                mask >>= 1;
            }

            return value;
        }

        public int ReadNum()
        {
            ulong value = ReadNumber();
            if (value > Int32.MaxValue)
                throw new NotSupportedException();

            return (int)value;
        }

        public uint ReadUInt32()
        {
            if (_offset + 4 > _ending)
                throw new EndOfStreamException();

            uint res = Get32(_buffer, _offset);
            _offset += 4;
            return res;
        }

        public ulong ReadUInt64()
        {
            if (_offset + 8 > _ending)
                throw new EndOfStreamException();

            ulong res = Get64(_buffer, _offset);
            _offset += 8;
            return res;
        }

        public string ReadString()
        {
            int ending = _offset;

            for (;;)
            {
                if (ending + 2 > _ending)
                    throw new EndOfStreamException();

                if (_buffer[ending] == 0 && _buffer[ending + 1] == 0)
                    break;

                ending += 2;
            }

            string str = Encoding.Unicode.GetString(_buffer, _offset, ending - _offset);
            _offset = ending + 2;
            return str;
        }

        #endregion
    }

    internal struct CStreamSwitch : IDisposable
    {
        private ArchiveReader _archive;
        private bool _needRemove;
        private bool _active;

        public void Dispose()
        {
            if (_active)
            {
                _active = false;
                Log.WriteLine("[end of switch]");
            }

            if (_needRemove)
            {
                _needRemove = false;
                _archive.DeleteByteStream();
            }
        }

        public void Set(ArchiveReader archive, byte[] dataVector)
        {
            Dispose();
            _archive = archive;
            _archive.AddByteStream(dataVector, 0, dataVector.Length);
            _needRemove = true;
            _active = true;
        }

        public void Set(ArchiveReader archive, List<byte[]> dataVector)
        {
            Dispose();
            _active = true;

            byte external = archive.ReadByte();
            if (external != 0)
            {
                int dataIndex = archive.ReadNum();
                if (dataIndex < 0 || dataIndex >= dataVector.Count)
                    throw new InvalidDataException();

                Log.WriteLine("[switch to stream {0}]", dataIndex);
                _archive = archive;
                _archive.AddByteStream(dataVector[dataIndex], 0, dataVector[dataIndex].Length);
                _needRemove = true;
                _active = true;
            }
            else
            {
                Log.WriteLine("[inline data]");
            }
        }
    }

    public class ArchiveReader
    {
        internal Stream _stream;
        internal Stack<DataReader> _readerStack = new Stack<DataReader>();
        internal DataReader _currentReader;
        internal long _streamOrigin;
        internal long _streamEnding;
        internal byte[] _header;

        private Dictionary<int, Stream> _cachedStreams = new Dictionary<int, Stream>();

        internal void AddByteStream(byte[] buffer, int offset, int length)
        {
            _readerStack.Push(_currentReader);
            _currentReader = new DataReader(buffer, offset, length);
        }

        internal void DeleteByteStream()
        {
            _currentReader = _readerStack.Pop();
        }

        #region Private Methods - Data Reader

        internal Byte ReadByte()
        {
            return _currentReader.ReadByte();
        }

        private void ReadBytes(byte[] buffer, int offset, int length)
        {
            _currentReader.ReadBytes(buffer, offset, length);
        }

        private ulong ReadNumber()
        {
            return _currentReader.ReadNumber();
        }

        internal int ReadNum()
        {
            return _currentReader.ReadNum();
        }

        private uint ReadUInt32()
        {
            return _currentReader.ReadUInt32();
        }

        private ulong ReadUInt64()
        {
            return _currentReader.ReadUInt64();
        }

        private BlockType? ReadId()
        {
            ulong id = _currentReader.ReadNumber();
            if (id > 25)
                return null;

            Log.WriteLine("ReadId: {0}", (BlockType)id);
            return (BlockType)id;
        }

        private void SkipData(long size)
        {
            _currentReader.SkipData(size);
        }

        private void SkipData()
        {
            _currentReader.SkipData();
        }

        private void WaitAttribute(BlockType attribute)
        {
            for (;;)
            {
                BlockType? type = ReadId();
                if (type == attribute)
                    return;
                if (type == BlockType.End)
                    throw new InvalidDataException();
                SkipData();
            }
        }

        private void ReadArchiveProperties()
        {
            while (ReadId() != BlockType.End)
                SkipData();
        }

        #endregion

        #region Private Methods - Reader Utilities

        private BitVector ReadBitVector(int length)
        {
            var bits = new BitVector(length);

            byte data = 0;
            byte mask = 0;

            for (int i = 0; i < length; i++)
            {
                if (mask == 0)
                {
                    data = ReadByte();
                    mask = 0x80;
                }

                if ((data & mask) != 0)
                    bits.SetBit(i);

                mask >>= 1;
            }

            return bits;
        }

        private BitVector ReadOptionalBitVector(int length)
        {
            byte allTrue = ReadByte();
            if (allTrue != 0)
                return new BitVector(length, true);

            return ReadBitVector(length);
        }

        private void ReadNumberVector(List<byte[]> dataVector, int numFiles, Action<int, long?> action)
        {
            var defined = ReadOptionalBitVector(numFiles);

            using (CStreamSwitch streamSwitch = new CStreamSwitch())
            {
                streamSwitch.Set(this, dataVector);

                for (int i = 0; i < numFiles; i++)
                {
                    if (defined[i])
                        action(i, checked((long)ReadUInt64()));
                    else
                        action(i, null);
                }
            }
        }

        private DateTime TranslateTime(long time)
        {
            // FILETIME = 100-nanosecond intervals since January 1, 1601 (UTC)
            return DateTime.FromFileTimeUtc(time);
        }

        private DateTime? TranslateTime(long? time)
        {
            if (time.HasValue)
                return TranslateTime(time.Value);
            else
                return null;
        }

        private void ReadDateTimeVector(List<byte[]> dataVector, int numFiles, Action<int, DateTime?> action)
        {
            ReadNumberVector(dataVector, numFiles, (index, value) => action(index, TranslateTime(value)));
        }

        private void ReadAttributeVector(List<byte[]> dataVector, int numFiles, Action<int, uint?> action)
        {
            BitVector boolVector = ReadOptionalBitVector(numFiles);
            using (var streamSwitch = new CStreamSwitch())
            {
                streamSwitch.Set(this, dataVector);
                for (int i = 0; i < numFiles; i++)
                {
                    if (boolVector[i])
                        action(i, ReadUInt32());
                    else
                        action(i, null);
                }
            }
        }

        #endregion

        #region Private Methods

        private void GetNextFolderItem(CFolder folder)
        {
            Log.WriteLine("-- GetNextFolderItem --");
            Log.PushIndent();
            try
            {
                int numCoders = ReadNum();
                Log.WriteLine("NumCoders: " + numCoders);

                folder.Coders = new List<CCoderInfo>(numCoders);
                int numInStreams = 0;
                int numOutStreams = 0;
                for (int i = 0; i < numCoders; i++)
                {
                    Log.WriteLine("-- Coder --");
                    Log.PushIndent();
                    try
                    {
                        CCoderInfo coder = new CCoderInfo();
                        folder.Coders.Add(coder);

                        byte mainByte = ReadByte();
                        int idSize = (mainByte & 0xF);
                        byte[] longID = new byte[idSize];
                        ReadBytes(longID, 0, idSize);
                        Log.WriteLine("MethodId: " + String.Join("", Enumerable.Range(0, idSize).Select(x => longID[x].ToString("x2"))));
                        if (idSize > 8)
                            throw new NotSupportedException();
                        ulong id = 0;
                        for (int j = 0; j < idSize; j++)
                            id |= (ulong)longID[idSize - 1 - j] << (8 * j);
                        coder.MethodId = new CMethodId(id);

                        if ((mainByte & 0x10) != 0)
                        {
                            coder.NumInStreams = ReadNum();
                            coder.NumOutStreams = ReadNum();
                            Log.WriteLine("Complex Stream (In: " + coder.NumInStreams + " - Out: " + coder.NumOutStreams + ")");
                        }
                        else
                        {
                            Log.WriteLine("Simple Stream (In: 1 - Out: 1)");
                            coder.NumInStreams = 1;
                            coder.NumOutStreams = 1;
                        }

                        if ((mainByte & 0x20) != 0)
                        {
                            int propsSize = ReadNum();
                            coder.Props = new byte[propsSize];
                            ReadBytes(coder.Props, 0, propsSize);
                            Log.WriteLine("Settings: " + String.Join("", coder.Props.Select(bt => bt.ToString("x2"))));
                        }

                        if ((mainByte & 0x80) != 0)
                            throw new NotSupportedException();

                        numInStreams += coder.NumInStreams;
                        numOutStreams += coder.NumOutStreams;
                    }
                    finally { Log.PopIndent(); }
                }

                int numBindPairs = numOutStreams - 1;
                folder.BindPairs = new List<CBindPair>(numBindPairs);
                Log.WriteLine("BindPairs: " + numBindPairs);
                Log.PushIndent();
                for (int i = 0; i < numBindPairs; i++)
                {
                    CBindPair bp = new CBindPair();
                    bp.InIndex = ReadNum();
                    bp.OutIndex = ReadNum();
                    folder.BindPairs.Add(bp);
                    Log.WriteLine("#" + i + " - In: " + bp.InIndex + " - Out: " + bp.OutIndex);
                }
                Log.PopIndent();

                if (numInStreams < numBindPairs)
                    throw new NotSupportedException();

                int numPackStreams = numInStreams - numBindPairs;
                //folder.PackStreams.Reserve(numPackStreams);
                if (numPackStreams == 1)
                {
                    for (int i = 0; i < numInStreams; i++)
                    {
                        if (folder.FindBindPairForInStream(i) < 0)
                        {
                            Log.WriteLine("Single PackStream: #" + i);
                            folder.PackStreams.Add(i);
                            break;
                        }
                    }

                    if (folder.PackStreams.Count != 1)
                        throw new NotSupportedException();
                }
                else
                {
                    Log.WriteLine("Multiple PackStreams ...");
                    Log.PushIndent();
                    for (int i = 0; i < numPackStreams; i++)
                    {
                        var num = ReadNum();
                        Log.WriteLine("#" + i + " - " + num);
                        folder.PackStreams.Add(num);
                    }
                    Log.PopIndent();
                }
            }
            finally
            {
                Log.PopIndent();
            }
        }

        private List<uint?> ReadHashDigests(int count)
        {
            Log.Write("ReadHashDigests:");

            var defined = ReadOptionalBitVector(count);
            var digests = new List<uint?>(count);
            for (int i = 0; i < count; i++)
            {
                if (defined[i])
                {
                    uint crc = ReadUInt32();
                    Log.Write("  " + crc.ToString("x8"));
                    digests.Add(crc);
                }
                else
                {
                    Log.Write("  ########");
                    digests.Add(null);
                }
            }

            Log.WriteLine();
            return digests;
        }

        private void ReadPackInfo(out long dataOffset, out List<long> packSizes, out List<uint?> packCRCs)
        {
            Log.WriteLine("-- ReadPackInfo --");
            Log.PushIndent();
            try
            {
                packCRCs = null;

                dataOffset = checked((long)ReadNumber());
                Log.WriteLine("DataOffset: " + dataOffset);

                int numPackStreams = ReadNum();
                Log.WriteLine("NumPackStreams: " + numPackStreams);

                WaitAttribute(BlockType.Size);
                packSizes = new List<long>(numPackStreams);
                Log.Write("Sizes:");
                for (int i = 0; i < numPackStreams; i++)
                {
                    var size = checked((long)ReadNumber());
                    Log.Write("  " + size);
                    packSizes.Add(size);
                }
                Log.WriteLine();

                BlockType? type;
                for (;;)
                {
                    type = ReadId();
                    if (type == BlockType.End)
                        break;
                    if (type == BlockType.CRC)
                    {
                        packCRCs = ReadHashDigests(numPackStreams);
                        continue;
                    }
                    SkipData();
                }

                if (packCRCs == null)
                {
                    packCRCs = new List<uint?>(numPackStreams);
                    for (int i = 0; i < numPackStreams; i++)
                        packCRCs.Add(null);
                }
            }
            finally { Log.PopIndent(); }
        }

        private void ReadUnpackInfo(List<byte[]> dataVector, out List<CFolder> folders)
        {
            Log.WriteLine("-- ReadUnpackInfo --");
            Log.PushIndent();
            try
            {
                WaitAttribute(BlockType.Folder);
                int numFolders = ReadNum();
                Log.WriteLine("NumFolders: {0}", numFolders);

                using (CStreamSwitch streamSwitch = new CStreamSwitch())
                {
                    streamSwitch.Set(this, dataVector);
                    //folders.Clear();
                    //folders.Reserve(numFolders);
                    folders = new List<CFolder>(numFolders);
                    int index = 0;
                    for (int i = 0; i < numFolders; i++)
                    {
                        var f = new CFolder { FirstPackStreamId = index };
                        folders.Add(f);
                        GetNextFolderItem(f);
                        index += f.PackStreams.Count;
                    }
                }

                WaitAttribute(BlockType.CodersUnpackSize);

                Log.WriteLine("UnpackSizes:");
                for (int i = 0; i < numFolders; i++)
                {
                    CFolder folder = folders[i];
                    Log.Write("  #" + i + ":");
                    int numOutStreams = folder.GetNumOutStreams();
                    for (int j = 0; j < numOutStreams; j++)
                    {
                        long size = checked((long)ReadNumber());
                        Log.Write("  " + size);
                        folder.UnpackSizes.Add(size);
                    }
                    Log.WriteLine();
                }

                for (;;)
                {
                    BlockType? type = ReadId();
                    if (type == BlockType.End)
                        return;

                    if (type == BlockType.CRC)
                    {
                        List<uint?> crcs = ReadHashDigests(numFolders);
                        for (int i = 0; i < numFolders; i++)
                            folders[i].UnpackCRC = crcs[i];
                        continue;
                    }

                    SkipData();
                }
            }
            finally { Log.PopIndent(); }
        }

        private void ReadSubStreamsInfo(List<CFolder> folders, out List<int> numUnpackStreamsInFolders, out List<long> unpackSizes, out List<uint?> digests)
        {
            Log.WriteLine("-- ReadSubStreamsInfo --");
            Log.PushIndent();
            try
            {
                numUnpackStreamsInFolders = null;

                BlockType? type;
                for (;;)
                {
                    type = ReadId();
                    if (type == BlockType.NumUnpackStream)
                    {
                        numUnpackStreamsInFolders = new List<int>(folders.Count);
                        Log.Write("NumUnpackStreams:");
                        for (int i = 0; i < folders.Count; i++)
                        {
                            var num = ReadNum();
                            Log.Write("  " + num);
                            numUnpackStreamsInFolders.Add(num);
                        }
                        Log.WriteLine();
                        continue;
                    }
                    if (type == BlockType.CRC || type == BlockType.Size)
                        break;
                    if (type == BlockType.End)
                        break;
                    SkipData();
                }

                if (numUnpackStreamsInFolders == null)
                {
                    numUnpackStreamsInFolders = new List<int>(folders.Count);
                    for (int i = 0; i < folders.Count; i++)
                        numUnpackStreamsInFolders.Add(1);
                }

                unpackSizes = new List<long>(folders.Count);
                for (int i = 0; i < numUnpackStreamsInFolders.Count; i++)
                {
                    // v3.13 incorrectly worked with empty folders
                    // v4.07: we check that folder is empty
                    int numSubstreams = numUnpackStreamsInFolders[i];
                    if (numSubstreams == 0)
                        continue;

                    Log.Write("#{0} StreamSizes:", i);
                    long sum = 0;
                    for (int j = 1; j < numSubstreams; j++)
                    {
                        if (type == BlockType.Size)
                        {
                            long size = checked((long)ReadNumber());
                            Log.Write("  " + size);
                            unpackSizes.Add(size);
                            sum += size;
                        }
                    }
                    unpackSizes.Add(folders[i].GetUnpackSize() - sum);
                    if (unpackSizes.Last() <= 0) throw new InvalidDataException();
                    Log.WriteLine("  -  rest: " + unpackSizes.Last());
                }
                if (type == BlockType.Size)
                    type = ReadId();

                int numDigests = 0;
                int numDigestsTotal = 0;
                for (int i = 0; i < folders.Count; i++)
                {
                    int numSubstreams = numUnpackStreamsInFolders[i];
                    if (numSubstreams != 1 || !folders[i].UnpackCRCDefined)
                        numDigests += numSubstreams;
                    numDigestsTotal += numSubstreams;
                }

                digests = null;

                for (;;)
                {
                    if (type == BlockType.CRC)
                    {
                        digests = new List<uint?>(numDigestsTotal);

                        List<uint?> digests2 = ReadHashDigests(numDigests);

                        int digestIndex = 0;
                        for (int i = 0; i < folders.Count; i++)
                        {
                            int numSubstreams = numUnpackStreamsInFolders[i];
                            CFolder folder = folders[i];
                            if (numSubstreams == 1 && folder.UnpackCRCDefined)
                            {
                                digests.Add(folder.UnpackCRC.Value);
                            }
                            else
                            {
                                for (int j = 0; j < numSubstreams; j++, digestIndex++)
                                    digests.Add(digests2[digestIndex]);
                            }
                        }

                        if (digestIndex != numDigests || numDigestsTotal != digests.Count)
                            System.Diagnostics.Debugger.Break();
                    }
                    else if (type == BlockType.End)
                    {
                        if (digests == null)
                        {
                            digests = new List<uint?>(numDigestsTotal);
                            for (int i = 0; i < numDigestsTotal; i++)
                                digests.Add(null);
                        }
                        return;
                    }
                    else
                    {
                        SkipData();
                    }

                    type = ReadId();
                }
            }
            finally { Log.PopIndent(); }
        }

        private void ReadStreamsInfo(
            List<byte[]> dataVector,
            out long dataOffset,
            out List<long> packSizes,
            out List<uint?> packCRCs,
            out List<CFolder> folders,
            out List<int> numUnpackStreamsInFolders,
            out List<long> unpackSizes,
            out List<uint?> digests)
        {
            Log.WriteLine("-- ReadStreamsInfo --");
            Log.PushIndent();
            try
            {
                dataOffset = long.MinValue;
                packSizes = null;
                packCRCs = null;
                folders = null;
                numUnpackStreamsInFolders = null;
                unpackSizes = null;
                digests = null;

                for (;;)
                {
                    switch (ReadId())
                    {
                        case BlockType.End:
                            return;
                        case BlockType.PackInfo:
                            ReadPackInfo(out dataOffset, out packSizes, out packCRCs);
                            break;
                        case BlockType.UnpackInfo:
                            ReadUnpackInfo(dataVector, out folders);
                            break;
                        case BlockType.SubStreamsInfo:
                            ReadSubStreamsInfo(folders, out numUnpackStreamsInFolders, out unpackSizes, out digests);
                            break;
                        default:
                            throw new InvalidDataException();
                    }
                }
            }
            finally { Log.PopIndent(); }
        }

        private List<byte[]> ReadAndDecodePackedStreams(long baseOffset, IPasswordProvider pass)
        {
            Log.WriteLine("-- ReadAndDecodePackedStreams --");
            Log.PushIndent();
            try
            {
                long dataStartPos;
                List<long> packSizes;
                List<uint?> packCRCs;
                List<CFolder> folders;
                List<int> numUnpackStreamsInFolders;
                List<long> unpackSizes;
                List<uint?> digests;

                ReadStreamsInfo(null,
                  out dataStartPos,
                  out packSizes,
                  out packCRCs,
                  out folders,
                  out numUnpackStreamsInFolders,
                  out unpackSizes,
                  out digests);

                dataStartPos += baseOffset;

                var dataVector = new List<byte[]>(folders.Count);
                int packIndex = 0;
                foreach (var folder in folders)
                {
                    long oldDataStartPos = dataStartPos;
                    long[] myPackSizes = new long[folder.PackStreams.Count];
                    for (int i = 0; i < myPackSizes.Length; i++)
                    {
                        long packSize = packSizes[packIndex + i];
                        myPackSizes[i] = packSize;
                        dataStartPos += packSize;
                    }

                    var outStream = DecoderStreamHelper.CreateDecoderStream(_stream, oldDataStartPos, myPackSizes, folder, pass);

                    int unpackSize = checked((int)folder.GetUnpackSize());
                    byte[] data = new byte[unpackSize];
                    outStream.ReadExact(data, 0, data.Length);
                    if (outStream.ReadByte() >= 0)
                        throw new InvalidDataException("Decoded stream is longer than expected.");
                    dataVector.Add(data);

                    if (folder.UnpackCRCDefined)
                        if (CRC.Finish(CRC.Update(CRC.kInitCRC, data, 0, unpackSize)) != folder.UnpackCRC)
                            throw new InvalidDataException("Decoded stream does not match expected CRC.");
                }
                return dataVector;
            }
            finally { Log.PopIndent(); }
        }

        private void ReadHeader(CArchiveDatabaseEx db, IPasswordProvider getTextPassword)
        {
            Log.WriteLine("-- ReadHeader --");
            Log.PushIndent();
            try
            {
                BlockType? type = ReadId();

                if (type == BlockType.ArchiveProperties)
                {
                    ReadArchiveProperties();
                    type = ReadId();
                }

                List<byte[]> dataVector = null;
                if (type == BlockType.AdditionalStreamsInfo)
                {
                    dataVector = ReadAndDecodePackedStreams(db.StartPositionAfterHeader, getTextPassword);
                    type = ReadId();
                }

                List<long> unpackSizes;
                List<uint?> digests;

                if (type == BlockType.MainStreamsInfo)
                {
                    ReadStreamsInfo(dataVector,
                        out db.DataStartPosition,
                        out db.PackSizes,
                        out db.PackCRCs,
                        out db.Folders,
                        out db.NumUnpackStreamsVector,
                        out unpackSizes,
                        out digests);

                    db.DataStartPosition += db.StartPositionAfterHeader;
                    type = ReadId();
                }
                else
                {
                    unpackSizes = new List<long>(db.Folders.Count);
                    digests = new List<uint?>(db.Folders.Count);
                    db.NumUnpackStreamsVector = new List<int>(db.Folders.Count);
                    for (int i = 0; i < db.Folders.Count; i++)
                    {
                        var folder = db.Folders[i];
                        unpackSizes.Add(folder.GetUnpackSize());
                        digests.Add(folder.UnpackCRC);
                        db.NumUnpackStreamsVector.Add(1);
                    }
                }

                db.Files.Clear();

                if (type == BlockType.End)
                    return;

                if (type != BlockType.FilesInfo)
                    throw new InvalidDataException();

                int numFiles = ReadNum();
                Log.WriteLine("NumFiles: " + numFiles);
                db.Files = new List<CFileItem>(numFiles);
                for (int i = 0; i < numFiles; i++)
                    db.Files.Add(new CFileItem());

                BitVector emptyStreamVector = new BitVector(numFiles);
                BitVector emptyFileVector = null;
                BitVector antiFileVector = null;
                int numEmptyStreams = 0;

                for (;;)
                {
                    type = ReadId();
                    if (type == BlockType.End)
                        break;

                    long size = checked((long)ReadNumber()); // TODO: throw invalid data on negative
                    int oldPos = _currentReader.Offset;
                    switch (type)
                    {
                        case BlockType.Name:
                            using (var streamSwitch = new CStreamSwitch())
                            {
                                streamSwitch.Set(this, dataVector);
                                Log.Write("FileNames:");
                                for (int i = 0; i < db.Files.Count; i++)
                                {
                                    db.Files[i].Name = _currentReader.ReadString();
                                    Log.Write("  " + db.Files[i].Name);
                                }
                                Log.WriteLine();
                            }
                            break;
                        case BlockType.WinAttributes:
                            Log.Write("WinAttributes:");
                            ReadAttributeVector(dataVector, numFiles, delegate (int i, uint? attr)
                            {
                                db.Files[i].Attrib = attr;
                                Log.Write("  " + (attr.HasValue ? attr.Value.ToString("x8") : "n/a"));
                            });
                            Log.WriteLine();
                            break;
                        case BlockType.EmptyStream:
                            emptyStreamVector = ReadBitVector(numFiles);

                            Log.Write("EmptyStream: ");
                            for (int i = 0; i < emptyStreamVector.Length; i++)
                            {
                                if (emptyStreamVector[i])
                                {
                                    Log.Write("x");
                                    numEmptyStreams++;
                                }
                                else
                                {
                                    Log.Write(".");
                                }
                            }
                            Log.WriteLine();

                            emptyFileVector = new BitVector(numEmptyStreams);
                            antiFileVector = new BitVector(numEmptyStreams);
                            break;
                        case BlockType.EmptyFile:
                            emptyFileVector = ReadBitVector(numEmptyStreams);
                            Log.Write("EmptyFile: ");
                            for (int i = 0; i < numEmptyStreams; i++)
                                Log.Write(emptyFileVector[i] ? "x" : ".");
                            Log.WriteLine();
                            break;
                        case BlockType.Anti:
                            antiFileVector = ReadBitVector(numEmptyStreams);
                            Log.Write("Anti: ");
                            for (int i = 0; i < numEmptyStreams; i++)
                                Log.Write(antiFileVector[i] ? "x" : ".");
                            Log.WriteLine();
                            break;
                        case BlockType.StartPos:
                            Log.Write("StartPos:");
                            ReadNumberVector(dataVector, numFiles, delegate (int i, long? startPos)
                            {
                                db.Files[i].StartPos = startPos;
                                Log.Write("  " + (startPos.HasValue ? startPos.Value.ToString() : "n/a"));
                            });
                            Log.WriteLine();
                            break;
                        case BlockType.CTime:
                            Log.Write("CTime:");
                            ReadDateTimeVector(dataVector, numFiles, delegate (int i, DateTime? time)
                            {
                                db.Files[i].CTime = time;
                                Log.Write("  " + (time.HasValue ? time.Value.ToString() : "n/a"));
                            });
                            Log.WriteLine();
                            break;
                        case BlockType.ATime:
                            Log.Write("ATime:");
                            ReadDateTimeVector(dataVector, numFiles, delegate (int i, DateTime? time)
                            {
                                db.Files[i].ATime = time;
                                Log.Write("  " + (time.HasValue ? time.Value.ToString() : "n/a"));
                            });
                            Log.WriteLine();
                            break;
                        case BlockType.MTime:
                            Log.Write("MTime:");
                            ReadDateTimeVector(dataVector, numFiles, delegate (int i, DateTime? time)
                            {
                                db.Files[i].MTime = time;
                                Log.Write("  " + (time.HasValue ? time.Value.ToString() : "n/a"));
                            });
                            Log.WriteLine();
                            break;
                        case BlockType.Dummy:
                            Log.Write("Dummy: " + size);
                            for (long j = 0; j < size; j++)
                                if (ReadByte() != 0)
                                    throw new InvalidDataException();
                            break;
                        default:
                            SkipData(size);
                            break;
                    }

                    // since 0.3 record sizes must be correct
                    bool checkRecordsSize = (db.MajorVersion > 0 || db.MinorVersion > 2);
                    if (checkRecordsSize && _currentReader.Offset - oldPos != size)
                        throw new InvalidDataException();
                }

                int emptyFileIndex = 0;
                int sizeIndex = 0;
                for (int i = 0; i < numFiles; i++)
                {
                    CFileItem file = db.Files[i];
                    file.HasStream = !emptyStreamVector[i];
                    if (file.HasStream)
                    {
                        file.IsDir = false;
                        file.IsAnti = false;
                        file.Size = unpackSizes[sizeIndex];
                        file.Crc = digests[sizeIndex];
                        sizeIndex++;
                    }
                    else
                    {
                        file.IsDir = !emptyFileVector[emptyFileIndex];
                        file.IsAnti = antiFileVector[emptyFileIndex];
                        emptyFileIndex++;
                        file.Size = 0;
                        file.Crc = null;
                    }
                }
            }
            finally { Log.PopIndent(); }
        }

        #endregion

        #region Public Methods

        public void Open(Stream stream)
        {
            Close();

            _streamOrigin = stream.Position;
            _streamEnding = stream.Length;

            // TODO: Check Signature!
            _header = new byte[0x20];
            for (int offset = 0; offset < 0x20;)
            {
                int delta = stream.Read(_header, offset, 0x20 - offset);
                if (delta == 0)
                    throw new EndOfStreamException();
                offset += delta;
            }

            _stream = stream;
        }

        public void Close()
        {
            if (_stream != null)
                _stream.Dispose();

            foreach (var stream in _cachedStreams.Values)
                stream.Dispose();

            _cachedStreams.Clear();
        }

        public void ReadDatabase(CArchiveDatabaseEx db, IPasswordProvider pass)
        {
            db.Clear();

            db.MajorVersion = _header[6];
            db.MinorVersion = _header[7];

            if (db.MajorVersion != 0)
                throw new InvalidDataException();

            uint crcFromArchive = DataReader.Get32(_header, 8);
            long nextHeaderOffset = (long)DataReader.Get64(_header, 0xC);
            long nextHeaderSize = (long)DataReader.Get64(_header, 0x14);
            uint nextHeaderCrc = DataReader.Get32(_header, 0x1C);

            uint crc = CRC.kInitCRC;
            crc = CRC.Update(crc, nextHeaderOffset);
            crc = CRC.Update(crc, nextHeaderSize);
            crc = CRC.Update(crc, nextHeaderCrc);
            crc = CRC.Finish(crc);

            if (crc != crcFromArchive)
                throw new InvalidDataException();

            db.StartPositionAfterHeader = _streamOrigin + 0x20;

            // empty header is ok
            if (nextHeaderSize == 0)
                return;

            if (nextHeaderOffset < 0 || nextHeaderSize < 0 || nextHeaderSize > Int32.MaxValue)
                throw new InvalidDataException();

            if (nextHeaderOffset > _streamEnding - db.StartPositionAfterHeader)
                throw new InvalidDataException();

            _stream.Seek(nextHeaderOffset, SeekOrigin.Current);

            byte[] header = new byte[nextHeaderSize];
            _stream.ReadExact(header, 0, header.Length);

            if (CRC.Finish(CRC.Update(CRC.kInitCRC, header, 0, header.Length)) != nextHeaderCrc)
                throw new InvalidDataException();

            using (CStreamSwitch streamSwitch = new CStreamSwitch())
            {
                streamSwitch.Set(this, header);

                BlockType? type = ReadId();
                if (type != BlockType.Header)
                {
                    if (type != BlockType.EncodedHeader)
                        throw new InvalidDataException();

                    var dataVector = ReadAndDecodePackedStreams(db.StartPositionAfterHeader, pass);

                    // compressed header without content is odd but ok
                    if (dataVector.Count == 0)
                        return;

                    if (dataVector.Count != 1)
                        throw new InvalidDataException();

                    streamSwitch.Set(this, dataVector[0]);

                    if (ReadId() != BlockType.Header)
                        throw new InvalidDataException();
                }

                ReadHeader(db, pass);
            }
        }

        internal class CExtractFolderInfo
        {
            internal int FileIndex;
            internal int FolderIndex;
            internal List<bool> ExtractStatuses = new List<bool>();
            internal CExtractFolderInfo(int fileIndex, int folderIndex)
            {
                FileIndex = fileIndex;
                FolderIndex = folderIndex;
                if (fileIndex != -1)
                    ExtractStatuses.Add(true);
            }
        }

        private class FolderUnpackStream : Stream
        {
            private CArchiveDatabaseEx _db;
            private int _otherIndex;
            private int _startIndex;
            private List<bool> _extractStatuses;

            public FolderUnpackStream(CArchiveDatabaseEx db, int p, int startIndex, List<bool> list)
            {
                this._db = db;
                this._otherIndex = p;
                this._startIndex = startIndex;
                this._extractStatuses = list;
            }

            #region Stream

            public override bool CanRead
            {
                get { throw new NotImplementedException(); }
            }

            public override bool CanSeek
            {
                get { throw new NotImplementedException(); }
            }

            public override bool CanWrite
            {
                get { throw new NotImplementedException(); }
            }

            public override void Flush()
            {
                throw new NotImplementedException();
            }

            public override long Length
            {
                get { throw new NotImplementedException(); }
            }

            public override long Position
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            private Stream _stream;
            private long _rem;
            private int _currentIndex;
            private void ProcessEmptyFiles()
            {
                while (_currentIndex < _extractStatuses.Count && _db.Files[_startIndex + _currentIndex].Size == 0)
                {
                    OpenFile();
                    _stream.Close();
                    _stream = null;
                    _currentIndex++;
                }
            }
            private void OpenFile()
            {
                bool skip = !_extractStatuses[_currentIndex];
                int index = _startIndex + _currentIndex;
                int realIndex = _otherIndex + index;
                //string filename = @"D:\_testdump\" + _db.Files[index].Name;
                //Directory.CreateDirectory(Path.GetDirectoryName(filename));
                //_stream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.Delete);
                Log.WriteLine(_db.Files[index].Name);
                if (_db.Files[index].CrcDefined)
                    _stream = new CrcCheckStream(_db.Files[index].Crc.Value);
                else
                    _stream = new MemoryStream();
                _rem = _db.Files[index].Size;
            }
            public override void Write(byte[] buffer, int offset, int count)
            {
                while (count != 0)
                {
                    if (_stream != null)
                    {
                        int write = count;
                        if (write > _rem)
                            write = (int)_rem;
                        _stream.Write(buffer, offset, write);
                        count -= write;
                        _rem -= write;
                        offset += write;
                        if (_rem == 0)
                        {
                            _stream.Close();
                            _stream = null;
                            _currentIndex++;
                            ProcessEmptyFiles();
                        }
                    }
                    else
                    {
                        ProcessEmptyFiles();
                        if (_currentIndex == _extractStatuses.Count)
                        {
                            // we support partial extracting
                            System.Diagnostics.Debugger.Break();
                            throw new NotImplementedException();
                        }
                        OpenFile();
                    }
                }
            }

            #endregion
        }

        private Stream GetCachedDecoderStream(CArchiveDatabaseEx _db, int folderIndex, IPasswordProvider pw)
        {
            Stream s;
            if (!_cachedStreams.TryGetValue(folderIndex, out s))
            {
                CFolder folderInfo = _db.Folders[folderIndex];

                int packStreamIndex = _db.Folders[folderIndex].FirstPackStreamId;
                long folderStartPackPos = _db.GetFolderStreamPos(folderIndex, 0);
                List<long> packSizes = new List<long>();
                for (int j = 0; j < folderInfo.PackStreams.Count; j++)
                    packSizes.Add(_db.PackSizes[packStreamIndex + j]);

                s = DecoderStreamHelper.CreateDecoderStream(_stream, folderStartPackPos, packSizes.ToArray(), folderInfo, pw);
                if (!s.CanSeek)
                    s = new ManagedLzma._7zip.Decoder.FileBufferedDecoderStream(s);

                _cachedStreams.Add(folderIndex, s);
            }

            return s;
        }

        public Stream OpenStream(CArchiveDatabaseEx _db, int fileIndex, IPasswordProvider pw)
        {
            int folderIndex = _db.FileIndexToFolderIndexMap[fileIndex];
            int numFilesInFolder = _db.NumUnpackStreamsVector[folderIndex];
            int firstFileIndex = _db.FolderStartFileIndex[folderIndex];
            if (firstFileIndex > fileIndex || fileIndex - firstFileIndex >= numFilesInFolder)
                throw new InvalidOperationException();

            int skipCount = fileIndex - firstFileIndex;
            long skipSize = 0;
            for (int i = 0; i < skipCount; i++)
                skipSize += _db.Files[firstFileIndex + i].Size;

            Stream s = GetCachedDecoderStream(_db, folderIndex, pw);
            s.Position = skipSize;
            return new master._7zip.Utilities.UnpackSubStream(s, _db.Files[fileIndex].Size);
        }

        public void Extract(CArchiveDatabaseEx _db, int[] indices, IPasswordProvider pw)
        {
            int numItems;
            bool allFilesMode = (indices == null);
            if (allFilesMode)
                numItems = _db.Files.Count;
            else
                numItems = indices.Length;

            if (numItems == 0)
                return;

            List<CExtractFolderInfo> extractFolderInfoVector = new List<CExtractFolderInfo>();
            for (int i = 0; i < numItems; i++)
            {
                int fileIndex = allFilesMode ? i : indices[i];

                int folderIndex = _db.FileIndexToFolderIndexMap[fileIndex];
                if (folderIndex == -1)
                {
                    extractFolderInfoVector.Add(new CExtractFolderInfo(fileIndex, -1));
                    continue;
                }

                if (extractFolderInfoVector.Count == 0 || folderIndex != extractFolderInfoVector.Last().FolderIndex)
                    extractFolderInfoVector.Add(new CExtractFolderInfo(-1, folderIndex));

                CExtractFolderInfo efi = extractFolderInfoVector.Last();

                int startIndex = _db.FolderStartFileIndex[folderIndex];
                for (int index = efi.ExtractStatuses.Count; index <= fileIndex - startIndex; index++)
                    efi.ExtractStatuses.Add(index == fileIndex - startIndex);
            }

            foreach (CExtractFolderInfo efi in extractFolderInfoVector)
            {
                int startIndex;
                if (efi.FileIndex != -1)
                    startIndex = efi.FileIndex;
                else
                    startIndex = _db.FolderStartFileIndex[efi.FolderIndex];

                var outStream = new FolderUnpackStream(_db, 0, startIndex, efi.ExtractStatuses);

                if (efi.FileIndex != -1)
                    continue;

                int folderIndex = efi.FolderIndex;
                CFolder folderInfo = _db.Folders[folderIndex];

                int packStreamIndex = _db.Folders[folderIndex].FirstPackStreamId;
                long folderStartPackPos = _db.GetFolderStreamPos(folderIndex, 0);

                List<long> packSizes = new List<long>();
                for (int j = 0; j < folderInfo.PackStreams.Count; j++)
                    packSizes.Add(_db.PackSizes[packStreamIndex + j]);

                // TODO: If the decoding fails the last file may be extracted incompletely. Delete it?

                Stream s = DecoderStreamHelper.CreateDecoderStream(_stream, folderStartPackPos, packSizes.ToArray(), folderInfo, pw);
                byte[] buffer = new byte[4 << 10];
                for (;;)
                {
                    int processed = s.Read(buffer, 0, buffer.Length);
                    if (processed == 0) break;
                    outStream.Write(buffer, 0, processed);
                }
            }
        }

        public IEnumerable<CFileItem> GetFiles(CArchiveDatabaseEx db)
        {
            return db.Files;
        }

        public int GetFileIndex(CArchiveDatabaseEx db, CFileItem item)
        {
            return db.Files.IndexOf(item);
        }

        #endregion
    }
}
