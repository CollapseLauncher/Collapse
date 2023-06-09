using Hi3Helper.Shared.ClassStruct;
using System;
using System.IO;
#if HDIFFEXPERIMENTAL
using Hi3Helper.SharpHDiffPatch;
#endif
using static Hi3Helper.InvokeProp;

namespace Hi3Helper.Data
{
    public class HPatchUtil
    {
#if HDIFFEXPERIMENTAL
        private HDiffPatch _patcher = new HDiffPatch();
#endif
        uint bufSize = 0x10000;

        public void HPatchFile(string inputFile, string diffFile, string outputFile) =>
                    GetEnumStatus(HPatch(
                        inputFile, diffFile, outputFile,
                        false, new UIntPtr(bufSize), 0,
                        new FileInfo(diffFile).Length));

        public void HPatchDir(string inputPath, string diffFile, string outputPath)
        {
#if HDIFFEXPERIMENTAL
            bool isInputAfile = File.Exists(inputPath) && !Directory.Exists(inputPath);

            if (isInputAfile)
            {
                _patcher.Initialize(diffFile);
                _patcher.Patch(inputPath, outputPath, false);
                return;
            }
#endif
            string[] args = new string[] { "_", "-f", "-s", inputPath, diffFile, outputPath };

            GetEnumStatus(HPatchCommand(args.Length, args));
        }

        void GetEnumStatus(int i)
        {
            switch ((HPatchUtilStat)i)
            {
                case HPatchUtilStat.HPATCH_SUCCESS: return;
                case HPatchUtilStat.HPATCH_MEM_ERROR:
                    throw new OutOfMemoryException($"Out Of Memory. ERRMSG: {(HPatchUtilStat)i}");
                default:
                    throw new Exception($"Unhandled Error. ERRMSG: {(HPatchUtilStat)i}");
            }
        }
    }
}
