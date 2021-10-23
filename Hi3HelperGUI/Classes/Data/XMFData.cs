using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hi3HelperGUI.Preset;

using static Hi3HelperGUI.Data.ConverterTool;
using static Hi3HelperGUI.Logger;

namespace Hi3HelperGUI.Data
{
    public class XMFPatchUtils : XMFDictionaryClasses
    {
        // XMF Section
        internal protected Stream fs;
        internal protected long readoffset;
        internal protected string xmfpath;
        public ushort offset = 0x0;
        public bool debug = false;
        public short deviceID = 0;

        // Buffer Section
        internal protected byte[] buffer;

        public PatchFilesList XMFBook;

        public XMFPatchUtils(Stream i)
        {
            fs = i;
            XMFBook = new PatchFilesList();
            using (fs)
            {
                fs.Position = offset;
                fs.Read(buffer = new byte[4], 0, 4);
                XMFBook.PatchCount = BytesToUInt32Big(buffer);
#if DEBUG
                LogWriteLine($"{XMFBook.PatchCount} patches are available.");
#endif
                readoffset = fs.Position;
            }
        }

        public void Read()
        {
            // fs = new FileStream(xmfpath, FileMode.Open);
            fs.Position = readoffset;

            using (fs)
                for (uint i = XMFBook.PatchCount; i > 0; i--)
                    XMFBook.PatchContent.Add(ReadPatch());
        }

        ushort _L;
        string ReadString()
        {
            fs.Read(buffer = new byte[2], 0, 2);
            _L = BytesToUInt16Big(buffer);
            fs.Read(buffer = new byte[_L], 0, _L);

            return Path.GetFileNameWithoutExtension(Encoding.ASCII.GetString(buffer));
        }

        uint ReadSize()
        {
            fs.Read(buffer = new byte[4], 0, 4);

            return BytesToUInt32Big(buffer);
        }
        internal virtual PatchFileProperty ReadPatch() => new PatchFileProperty
        {
            SourceFileName = ReadString(),
            TargetFileName = ReadString(),
            PatchFileName = ReadString(),
            PatchDir = ReadString(),
            PatchFileSize = ReadSize(),
        };

        public void GetPatchFile()
        {
            if (XMFBook == null)
                throw new NullReferenceException("XMFBook is null. Please .Read() it first!");

            throw new NotImplementedException($"GetPatchFile() is not implemented yet");
        }
    }

    // FileFormat Enum
    public enum XMFFileFormat
    {
        Dictionary,
        XMF
    }

    public class XMFUtils : XMFDictionaryClasses
    {

        readonly XMFFileFormat format;

        // XMF Section
        internal protected Stream fs;
        internal protected long readoffset;
        internal protected string xmfpath;
        public ushort offset = 0x21;
        public bool debug = false;
        protected uint blockcount;
        public string dictmagicword = "Dict";

        // Buffer Section
        internal protected byte[] buffer;

        XMFBlockList _blocklist;

        public List<XMFBlockList> XMFBook;

        public XMFUtils(MemoryStream i, XMFFileFormat j)
        {
            fs = i;
            switch (j)
            {
                case XMFFileFormat.XMF:
                    format = XMFFileFormat.XMF;
                    InitXMF();
                    break;
                case XMFFileFormat.Dictionary:
                    format = XMFFileFormat.Dictionary;
                    InitDict();
                    break;
            }
        }

        void InitDict()
        {
            fs.Read(buffer = new byte[4], 0, 4);
            if (Encoding.ASCII.GetString(buffer) != dictmagicword)
                throw new FormatException("Read file is not a dictionary file. Magic string in header is not expected!"
                    + $"\r\nExpecting \"{dictmagicword}\" but got \"{Encoding.ASCII.GetString(buffer)}\" instead.");
            fs.Read(buffer = new byte[1], 0, 1);
            blockcount = buffer[0];
#if DEBUG
            LogWriteLine($"{blockcount} blocks are available.");
#endif
            readoffset = fs.Position;
        }

        void InitXMF()
        {
            fs.Position += offset;
            fs.Read(buffer = new byte[4], 0, 4);
            blockcount = BytesToUInt32Big(buffer);
#if DEBUG
            LogWriteLine($"{blockcount} blocks are available.");
#endif
            readoffset = fs.Position;
        }

        public void Read()
        {
            XMFBook = new List<XMFBlockList>();
            fs.Position = readoffset;

            using (fs)
                for (uint i = blockcount; i > 0; i--)
                    InitRead();
        }

        protected string blockhash;
        protected uint blocksize;
        protected uint contentcount;

        protected string filename;
        protected ushort filenamelength;
        protected string filehash;
        protected int filehasharray;
        protected uint fileoffset;
        protected uint Ffileoffset;
        protected uint filesize;
        protected uint curFileRead = 1;
        protected Func<XMFFileProperty> ReadContentMethod;

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

            fs.Read(buffer = new byte[16], 0, 16);
            blockhash = BytesToHex(buffer);
            fs.Position += jumper;
            fs.Read(buffer = new byte[4], 0, 4);
            blocksize = isBigEndian ? BytesToUInt32Big(buffer) : BitConverter.ToUInt32(buffer, 0);

            // Reserve 4 bytes but will read as it requested by contentbytelength.
            fs.Read(buffer = new byte[4], 0, contentbytelength);
            contentcount = isBigEndian ? BytesToUInt32Big(buffer) : BitConverter.ToUInt32(buffer, 0);

            _blocklist.BlockHash = blockhash;
            _blocklist.BlockSize = blocksize;

#if (DEBUG)
            // Console.WriteLine($"    > {blockhash} -> {SummarizeSizeSimple(blocksize)} ({contentcount} files)");
#endif
        }

        XMFFileProperty ReadXMFContentInfo()
        {
            fs.Read(buffer = new byte[2], 0, 2);
            filenamelength = BytesToUInt16Big(buffer);
            fs.Read(buffer = new byte[filenamelength], 0, filenamelength);
            filename = Encoding.UTF8.GetString(buffer);
            fs.Read(buffer = new byte[4], 0, 4);
            fileoffset = BytesToUInt32Big(buffer);

            readoffset = fs.Position;

            filesize = GetFileSize();

#if DEBUG   
            // LogWriteLine($"[C:{curFileRead}] {filename} | Start Offset: {fileoffset} | Size: {filesize}", LogType.NoTag);
#endif
            curFileRead++;

            return new XMFFileProperty() { FileName = filename, FileSize = filesize, StartOffset = fileoffset };
        }

        XMFFileProperty ReadDictContentInfo()
        {
            fs.Read(buffer = new byte[1], 0, 1);
            filenamelength = buffer[0];
            fs.Read(buffer = new byte[filenamelength], 0, filenamelength);
            filename = Encoding.UTF8.GetString(buffer);
            fs.Read(buffer = new byte[4], 0, 4);
            fileoffset = BitConverter.ToUInt32(buffer, 0);
            fs.Read(buffer = new byte[4], 0, 4);
            filesize = BitConverter.ToUInt32(buffer, 0);

            fs.Read(buffer = new byte[4], 0, 4);
            filehasharray = BitConverter.ToInt32(buffer);

#if DEBUG   
            // LogWriteLine($"[C:{curFileRead}] {filename} | Start Offset: {fileoffset} | Size: {filesize}", LogType.NoTag);
#endif
            curFileRead++;

            return new XMFFileProperty() {
                FileName = filename,
                FileSize = filesize,
                StartOffset = fileoffset,
                FileHashArray = filehasharray
            };
        }

        uint GetFileSize()
        {
            fs.Read(buffer = new byte[2], 0, 2);
            fs.Position += BytesToUInt16Big(buffer);
            fs.Read(buffer = new byte[4], 0, 4);
            Ffileoffset = BytesToUInt32Big(buffer);

            fs.Position = readoffset;

            return (curFileRead == contentcount ? blocksize : Ffileoffset) - fileoffset;
        }

        public short deviceID = 0;
    }
}
