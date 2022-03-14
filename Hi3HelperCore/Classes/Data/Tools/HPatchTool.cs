using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Hi3Helper.Shared.ClassStruct;

using static Hi3Helper.InvokeProp;

namespace Hi3Helper.Data
{
    public class HPatchUtil
    {
        uint bufSize = 0x10000;

        public void HPatchFile(string inputFile, string diffFile, string outputFile) =>
                    GetEnumStatus(hpatch(
                        inputFile, diffFile, outputFile,
                        false, new UIntPtr(bufSize), 0,
                        new FileInfo(diffFile).Length));

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
