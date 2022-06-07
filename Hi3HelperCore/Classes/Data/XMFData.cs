using Hi3Helper.Preset;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Logger;

namespace Hi3Helper.Data
{
    // FileFormat Enum
    public enum XMFFileFormat
    {
        Dictionary,
        XMF
    }

    public class XMFUtils : XMFDictionaryClasses
    {
        // XMF Section
        internal protected Stream stream;
        internal protected long readoffset;
        internal protected string xmfpath;
        internal protected uint blockcount;
        internal protected readonly int dictmagicword;
        internal protected readonly ushort offset;
        internal protected readonly XMFFileFormat format;

        // Buffer Section
        internal protected byte[] buffer;

        // Reading Section
        internal protected string blockhash;
        internal protected uint blocksize;
        internal protected uint contentcount;

        internal protected string filename;
        internal protected ushort filenamelength;
        internal protected string filehash;
        internal protected int filehasharray;
        internal protected uint fileoffset;
        internal protected uint Ffileoffset;
        internal protected uint filesize;
        internal protected uint curFileRead = 1;
        internal protected Func<XMFFileProperty> ReadContentMethod;

        internal protected XMFBlockList _blocklist;

        public List<XMFBlockList> XMFBook;

        public XMFUtils(Stream stream, XMFFileFormat format = XMFFileFormat.Dictionary, ushort offset = 0x21, int dictmagicword = 1952672068)
        {
            this.stream = stream;
            this.format = format;
            this.dictmagicword = dictmagicword;
            this.offset = offset;
            switch (format)
            {
                case XMFFileFormat.XMF:
                    InitXMF();
                    break;
                case XMFFileFormat.Dictionary:
                    InitDict();
                    break;
            }
        }

        void InitDict()
        {
            stream.Read(buffer = new byte[4], 0, 4);
            if (BitConverter.ToInt32(buffer, 0) != dictmagicword)
                throw new FormatException("Read file is not a dictionary file. Magic string in header is not expected!"
                    + $"\r\nExpecting \"{dictmagicword}\" but got \"{Encoding.ASCII.GetString(buffer)}\" instead.");
            stream.Read(buffer = new byte[1], 0, 1);
            blockcount = buffer[0];
#if DEBUG
            LogWriteLine($"{blockcount} blocks are available.");
#endif
            readoffset = stream.Position;
        }

        void InitXMF()
        {
            stream.Position += offset;
            stream.Read(buffer = new byte[4], 0, 4);
            blockcount = BytesToUInt32Big(buffer);
#if DEBUG
            LogWriteLine($"{blockcount} blocks are available.");
#endif
            readoffset = stream.Position;
        }

        public void Read()
        {
            XMFBook = new List<XMFBlockList>();
            stream.Position = readoffset;

            using (stream)
                for (uint i = blockcount; i > 0; i--)
                    InitRead();
        }

        void InitRead()
        {

            switch (format)
            {
                case XMFFileFormat.XMF:
                    ReadContentMethod = ReadXMFContentInfo;
                    GetMetadataInfo(4, 0xC, true);
                    break;
                case XMFFileFormat.Dictionary:
                default:
                    ReadContentMethod = ReadDictContentInfo;
                    GetMetadataInfo(2, 0, false);
                    break;
            }

            curFileRead = 1;

            for (uint i = contentcount; i > 0; i--)
                _blocklist.BlockContent.Add(ReadContentMethod());

            XMFBook.Add(_blocklist);
        }

        void GetMetadataInfo(int contentbytelength, int jumper, bool isBigEndian)
        {
            _blocklist = new XMFBlockList();

            stream.Read(buffer = new byte[16], 0, 16);
            blockhash = BytesToHex(buffer);
            stream.Position += jumper;
            stream.Read(buffer = new byte[4], 0, 4);
            blocksize = isBigEndian ? BytesToUInt32Big(buffer) : BitConverter.ToUInt32(buffer, 0);

            // Reserve 4 bytes but will read as it requested by contentbytelength.
            stream.Read(buffer = new byte[4], 0, contentbytelength);
            contentcount = isBigEndian ? BytesToUInt32Big(buffer) : BitConverter.ToUInt32(buffer, 0);

            _blocklist.BlockHash = blockhash;
            _blocklist.BlockSize = blocksize;

#if (DEBUG)
            Console.WriteLine($"    > {blockhash} -> {SummarizeSizeSimple(blocksize)} ({contentcount} chunks)");
#endif
        }

        XMFFileProperty ReadXMFContentInfo()
        {
            stream.Read(buffer = new byte[2], 0, 2);
            filenamelength = BytesToUInt16Big(buffer);
            stream.Read(buffer = new byte[filenamelength], 0, filenamelength);
            filename = Encoding.UTF8.GetString(buffer);
            stream.Read(buffer = new byte[4], 0, 4);
            fileoffset = BytesToUInt32Big(buffer);

            readoffset = stream.Position;

            filesize = GetFileSize();

#if DEBUG   
            LogWriteLine($"[C:{curFileRead}] {filename} | Offset: {fileoffset} | Size: {filesize}", LogType.NoTag);
#endif
            curFileRead++;

            return new XMFFileProperty()
            {
                FileName = filename,
                FileSize = filesize,
                StartOffset = fileoffset
            };
        }

        XMFFileProperty ReadDictContentInfo()
        {
            stream.Read(buffer = new byte[1], 0, 1);
            filenamelength = buffer[0];
            stream.Read(buffer = new byte[filenamelength], 0, filenamelength);
            filename = Encoding.UTF8.GetString(buffer);
            stream.Read(buffer = new byte[4], 0, 4);
            fileoffset = BitConverter.ToUInt32(buffer, 0);
            stream.Read(buffer = new byte[4], 0, 4);
            filesize = BitConverter.ToUInt32(buffer, 0);

            stream.Read(buffer = new byte[4], 0, 4);
            filehasharray = BitConverter.ToInt32(buffer, 0);

#if DEBUG   
            LogWriteLine($"[C:{curFileRead}] {filename} | Offset: {fileoffset} | Size: {filesize}", LogType.NoTag);
#endif
            curFileRead++;

            return new XMFFileProperty()
            {
                FileName = filename,
                FileSize = filesize,
                StartOffset = fileoffset,
                FileHashArray = filehasharray
            };
        }

        uint GetFileSize()
        {
            stream.Read(buffer = new byte[2], 0, 2);
            stream.Position += BytesToUInt16Big(buffer);
            stream.Read(buffer = new byte[4], 0, 4);
            Ffileoffset = BytesToUInt32Big(buffer);

            stream.Position = readoffset;

            return (curFileRead == contentcount ? blocksize : Ffileoffset) - fileoffset;
        }
    }
}
