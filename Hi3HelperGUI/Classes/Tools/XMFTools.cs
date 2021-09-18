/* Grabbed from Hi3MirrorServer Code
 * There are bunch of things to get done for making this to work.
 * 
 * 
using System;
using System.Collections.Generic;
using System.IO;
// using System.Linq;
using System.Text;
using Hi3Helper.DownloadtoolsV2;
using Hi3Helper.Downloadtools;
using static Hi3Helper.ConverterTools;
using static Hi3Helper.Log;
using static Hi3Helper.DataClasses;

namespace Hi3HelperGUI.Data
{
    public class XMFPatchUtils
    {
        // XMF Section
        internal protected FileStream fs;
        internal protected long readoffset;
        internal protected string xmfpath;
        public ushort offset = 0x0;
        public bool debug = false;
        public short deviceID = 0;

        // Buffer Section
        internal protected byte[] buffer;

        public _PatchFilesList XMFBook;

        public XMFPatchUtils(string path)
        {
            fs = new FileStream(path, FileMode.Open);
            XMFBook = new _PatchFilesList();
            using (fs)
            {
                fs.Position = offset;
                fs.Read(buffer = new byte[4], 0, 4);
                XMFBook.PatchCount = BytesToUInt32Big(buffer);
                if (debug) Console.WriteLine($"{XMFBook.PatchCount} patches are available.");
                readoffset = fs.Position;
                xmfpath = path;
            }
        }

        public void Read()
        {
            fs = new FileStream(xmfpath, FileMode.Open);
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

        string _sourcename;
        string _targetname;
        string _patchname;
        string _patchdir;
        uint _Patchsize;
        internal virtual _PatchFileProperty ReadPatch()
        {
            _sourcename = ReadString();
            _targetname = ReadString();
            _patchname = ReadString();
            _patchdir = ReadString();
            _Patchsize = ReadSize();

            return new _PatchFileProperty
            {
                _sourcefilename = _sourcename,
                _targetfilename = _targetname,
                _patchfilename = _patchname,
                _patchdir = _patchdir,
                _patchfilesize = _Patchsize,
            };
        }

        public void GetPatchFile()
        {
            if (XMFBook == null)
                throw new NullReferenceException("XMFBook is null. Please .Read() it first!");

            Dictionary<string, string> Host = DownloadTools.ServerSwitch > 0 ?
                DownloadTools.HostPropertiesGLB["Asb"] :
                DownloadTools.HostProperties["Asb"];

            string url = Host["Hostname"] + "/"
                + Host["StreamingPath"] + "/"
                + DownloadTools.GetVersionString() + "/"
                + Host[deviceID == 0 ? "AndroidPatchPath" : deviceID == 1 ? "PCPatchPath" : "iOSPatchPath"] + "/";

            string inp,
                outp;

            ushort c = 1;
            foreach (_PatchFileProperty i in XMFBook.PatchContent)
            {
                inp = $"{url}{i._patchdir}/{i._patchfilename}.wmv";
                outp = Path.Combine(Path.GetDirectoryName(xmfpath), i._patchdir);
                if (!Directory.Exists(outp))
                    Directory.CreateDirectory(outp);

                if (!File.Exists(Path.Combine(outp, $"{i._patchfilename}.wmv")) || new FileInfo(Path.Combine(outp, $"{i._patchfilename}.wmv")).Length != i._patchfilesize)
                    while (!DownloadToolsV2.DownloadFileV2(inp, Path.Combine(outp, $"{i._patchfilename}.wmv"), false, $"\rDownloading ({c}/{XMFBook.PatchCount})"))
                        LoggerOverride($"\rRetrying to download \u001b[32;1m{i._patchfilename}\u001b[0m...");
                c++;
            }
        }
    }

    public class XMFUtils : DataClasses
    {
        LogWriter log = new LogWriter();

        // FileFormat Enum
        internal protected enum FileFormat
        {
            Dictionary,
            XMF
        }
        FileFormat format;

        // XMF Section
        internal protected FileStream fs;
        internal protected long readoffset;
        internal protected string xmfpath;
        public ushort offset = 0x21;
        public bool debug = false;
        protected uint blockcount;
        public string dictmagicword = "Dict";

        // Buffer Section
        internal protected byte[] buffer;

        _XMFBlockList _blocklist;

        public List<_XMFBlockList> XMFBook;

        public XMFUtils(string path)
        {
            xmfpath = path;
            fs = new FileStream(path, FileMode.Open);
            using (fs)
            {
                switch (Path.GetExtension(xmfpath).ToLower())
                {
                    case ".xmf":
                        format = FileFormat.XMF;
                        InitXMF();
                        break;
                    case ".dict":
                        format = FileFormat.Dictionary;
                        InitDict();
                        break;
                }
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
            readoffset = fs.Position;
        }

        void InitXMF()
        {
            fs.Position += offset;
            fs.Read(buffer = new byte[4], 0, 4);
            blockcount = BytesToUInt32Big(buffer);
            if (debug) Console.WriteLine($"{blockcount} blocks are available.");
            readoffset = fs.Position;
        }

        public void Read()
        {
            XMFBook = new List<_XMFBlockList>();
            fs = new FileStream(xmfpath, FileMode.Open);
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
        protected uint fileoffset;
        protected uint Ffileoffset;
        protected uint filesize;
        protected uint curFileRead = 1;
        protected Func<_XMFFileProperty> ReadContentMethod;

        void InitRead()
        {

            switch (format)
            {
                case FileFormat.XMF:
                    ReadContentMethod = ReadXMFContentInfo;
                    GetMetadataInfo(4, 0xC, true);
                    break;
                case FileFormat.Dictionary:
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
            _blocklist = new _XMFBlockList();

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

            if (debug) Console.WriteLine($"    > {blockhash} -> {SummarizeSize(blocksize)} ({contentcount} files)");
        }

        _XMFFileProperty ReadXMFContentInfo()
        {
            fs.Read(buffer = new byte[2], 0, 2);
            filenamelength = BytesToUInt16Big(buffer);
            fs.Read(buffer = new byte[filenamelength], 0, filenamelength);
            filename = Encoding.UTF8.GetString(buffer);
            fs.Read(buffer = new byte[4], 0, 4);
            fileoffset = BytesToUInt32Big(buffer);

            readoffset = fs.Position;

            filesize = GetFileSize();

            if (debug) Console.WriteLine($"        > [C:{curFileRead}] {filename} | Start Offset: {fileoffset} | Size: {filesize}");
            curFileRead++;

            return new _XMFFileProperty() { _filename = filename, _filesize = filesize, _startoffset = fileoffset };
        }

        _XMFFileProperty ReadDictContentInfo()
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
            filehash = BytesToHex(buffer);

            if (debug) Console.WriteLine($"        > [C:{curFileRead}] {filename} | CRC32: {filehash} | Start Offset: {fileoffset} | Size: {filesize}");
            curFileRead++;

            return new _XMFFileProperty() { _filename = filename, _filesize = filesize, _startoffset = fileoffset, _filecrc32 = filehash };
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

        string GetBlockURL() 
        {
            Dictionary<string, string> Host = DownloadTools.ServerSwitch > 0 ?
                DownloadTools.HostPropertiesGLB["Asb"] :
                DownloadTools.HostProperties["Asb"];

            Dictionary<string, string> patchfiles = new Dictionary<string, string>();
            string versionstring = DownloadTools.GetVersionString(),
                   asbpath = Host[deviceID == 0 ? "AndroidAsbPath" : deviceID == 1 ? "PCAsbPath" : "iOSAsbPath"],
                   bundleurl = $"{Host["Hostname"]}/{Host["StreamingPath"]}/{versionstring}/" + asbpath;
            return bundleurl;
        }

        public void CheckDownloadedBlock()
        {
            if (XMFBook == null)
                throw new NullReferenceException("XMFBook is null. Please .Read() it first!");

            string blockurl = GetBlockURL();
            string blockurlpath;
            string blockpath;
            string outputdir = Path.GetDirectoryName(xmfpath);

            short c = 1;
            foreach (_XMFBlockList i in XMFBook)
            {
                blockpath = Path.Combine(outputdir, $"{i.BlockHash}.wmv");
                blockurlpath = $"{blockurl}/{i.BlockHash}.wmv";
                LoggerOverride($"\rChecking ({c}/{XMFBook.Count}) \u001b[32;1m{i.BlockHash}\u001b[0m availability...");
                if (!File.Exists(blockpath))
                    while (!DownloadToolsV2.DownloadFileV2(blockurlpath, blockpath, false, $"\rDownloading ({c}/{XMFBook.Count})"))
                        LoggerOverride($"\rRetrying to download \u001b[32;1m{i.BlockHash}\u001b[0m...");
                c++;
            }
        }

        public void GenerateChunks(bool overwrite = false)
        {
            if (XMFBook == null)
                throw new NullReferenceException("XMFBook is null. Please .Read() it first!");

            string outputdir = Path.GetDirectoryName(xmfpath);
            string blockpath;
            string chunkpath;

            if (File.Exists(Path.Combine(outputdir, "index.dict")))
            {
                LoggerLine($"Index file in {outputdir} is already exist! Use GenerateChunks(true) to overwrite it or just delete the Index file.", LogType.Warning);
                if (!overwrite)
                    return;
            }

            FileStream DictBuffer = new FileStream(Path.Combine(outputdir, "index.dict"), FileMode.Create);

            // Write magic header
            DictBuffer.Write(Encoding.ASCII.GetBytes("Dict"), 0, 4);

            // Write block counts (1 byte/byte)(since block counts are < max.value of byte).
            DictBuffer.Write(new byte[] { (byte)XMFBook.Count }, 0, 1);

            short c = 1,
                  f;
            bool w;
            foreach (_XMFBlockList i in XMFBook)
            {
                // Write MD5 Hash of block as bytes (16 bytes).
                DictBuffer.Write(ToBytes(i.BlockHash), 0, 16);

                // Write size of block (4 bytes/uint).
                DictBuffer.Write(BitConverter.GetBytes((uint)i.BlockSize), 0, 4);

                // Write file counts inside of block file (2 bytes/ushort).
                DictBuffer.Write(BitConverter.GetBytes((ushort)i.BlockContent.Count), 0, 2);

                blockpath = Path.Combine(outputdir, i.BlockHash);
                chunkpath = blockpath + ".c";
                if (!Directory.Exists(chunkpath))
                    Directory.CreateDirectory(chunkpath);

                try
                {
                    using (fs = new FileStream(blockpath + ".wmv", FileMode.Open, FileAccess.Read))
                    {
                        if (c > 1)
                            ClearConsoleLines(1);
                        f = 1;
                        LoggerOverride($"\rGenerating chunk from {i.BlockHash} [{c}/{XMFBook.Count}] ({SummarizeSize(i.BlockSize)}) ({i.BlockContent.Count} files)", true);
                        foreach (_XMFFileProperty j in i.BlockContent)
                        {
                            w = false;
                            if (!File.Exists(Path.Combine(chunkpath, j._filename)))
                                w = true;

                            fs.Read(buffer = new byte[j._filesize], 0, (int)j._filesize);
                            filehash = BytesToCRC32(buffer);

                            LoggerOverride($"\r    > [CRC32: {filehash} {(w ? "W" : "S")}: {f}] Writing {j._filename} ({SummarizeSize(j._filesize)})...");

                            // Write filename length (1 byte/byte)(255 char max).
                            DictBuffer.Write(new byte[] { (byte)j._filename.Length }, 0, 1);

                            // Write filename (size depends on filename length).
                            DictBuffer.Write(Encoding.ASCII.GetBytes(j._filename), 0, j._filename.Length);

                            // Write start offset (4 bytes/uint).
                            DictBuffer.Write(BitConverter.GetBytes((uint)j._startoffset), 0, 4);

                            // Write filesize (4 bytes/uint).
                            DictBuffer.Write(BitConverter.GetBytes((uint)j._filesize), 0, 4);

                            // Write filehash CRC32 as bytes (4 bytes/int).
                            DictBuffer.Write(ToBytes(filehash), 0, 4);

                            if (w) File.WriteAllBytes(Path.Combine(chunkpath, j._filename), buffer);

                            // Console.WriteLine($"    > {j._filename}\tCRC32: {filehash}");
                            f++;
                        }
                        c++;
                    }
                }
                catch (FileNotFoundException e)
                {
                    Logger($"Block {i.BlockHash} doesn't exist!. Have you tried to do CheckDownloadedBlock() first?\r\nTraceback: {e}", LogType.Error);
                    return;
                }
                catch (Exception e)
                {
                    Logger($"An error occured while generating chunk for {i.BlockHash}\r\nTraceback: {e}", LogType.Error);
                    return;
                }
                
            }
            DictBuffer.Close();
        }
    }
}
*/
