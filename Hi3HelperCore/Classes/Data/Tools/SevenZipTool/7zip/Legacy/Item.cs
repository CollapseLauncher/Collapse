using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BitVector = ManagedLzma.LZMA.Master.SevenZip.BitVector;

namespace master._7zip.Legacy
{
    internal struct CMethodId
    {
        public const ulong kCopyId = 0;
        public const ulong kLzmaId = 0x030101;
        public const ulong kLzma2Id = 0x21;
        public const ulong kAESId = 0x06F10701;

        public static readonly CMethodId kCopy = new CMethodId(kCopyId);
        public static readonly CMethodId kLzma = new CMethodId(kLzmaId);
        public static readonly CMethodId kLzma2 = new CMethodId(kLzma2Id);
        public static readonly CMethodId kAES = new CMethodId(kAESId);

        public readonly ulong Id;

        public CMethodId(ulong id)
        {
            this.Id = id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is CMethodId && (CMethodId)obj == this;
        }

        public bool Equals(CMethodId other)
        {
            return Id == other.Id;
        }

        public static bool operator ==(CMethodId left, CMethodId right)
        {
            return left.Id == right.Id;
        }

        public static bool operator !=(CMethodId left, CMethodId right)
        {
            return left.Id != right.Id;
        }

        public int GetLength()
        {
            int bytes = 0;
            for (ulong value = Id; value != 0; value >>= 8)
                bytes++;
            return bytes;
        }
    }

    internal class CCoderInfo
    {
        internal CMethodId MethodId;
        internal byte[] Props;
        internal int NumInStreams;
        internal int NumOutStreams;
    }

    internal class CBindPair
    {
        internal int InIndex;
        internal int OutIndex;
    }

    internal class CFolder
    {
        internal List<CCoderInfo> Coders = new List<CCoderInfo>();
        internal List<CBindPair> BindPairs = new List<CBindPair>();
        internal List<int> PackStreams = new List<int>();
        internal int FirstPackStreamId;
        internal List<long> UnpackSizes = new List<long>();
        internal uint? UnpackCRC;
        internal bool UnpackCRCDefined { get { return UnpackCRC != null; } }

        public long GetUnpackSize()
        {
            if (UnpackSizes.Count == 0)
                return 0;

            for (int i = UnpackSizes.Count - 1; i >= 0; i--)
                if (FindBindPairForOutStream(i) < 0)
                    return UnpackSizes[i];

            throw new Exception();
        }

        public int GetNumOutStreams()
        {
            int count = 0;
            for (int i = 0; i < Coders.Count; i++)
                count += Coders[i].NumOutStreams;

            return count;
        }

        public int FindBindPairForInStream(int inStreamIndex)
        {
            for (int i = 0; i < BindPairs.Count; i++)
                if (BindPairs[i].InIndex == inStreamIndex)
                    return i;

            return -1;
        }

        public int FindBindPairForOutStream(int outStreamIndex)
        {
            for (int i = 0; i < BindPairs.Count; i++)
                if (BindPairs[i].OutIndex == outStreamIndex)
                    return i;

            return -1;
        }

        public int FindPackStreamArrayIndex(int inStreamIndex)
        {
            for (int i = 0; i < PackStreams.Count; i++)
                if (PackStreams[i] == inStreamIndex)
                    return i;

            return -1;
        }

        public bool IsEncrypted()
        {
            for (int i = Coders.Count - 1; i >= 0; i--)
                if (Coders[i].MethodId == CMethodId.kAES)
                    return true;

            return false;
        }

        public bool CheckStructure()
        {
            const int kNumCodersMax = 32; // don't change it
            const int kMaskSize = 32; // it must be >= kNumCodersMax
            const int kNumBindsMax = 32;

            if (Coders.Count > kNumCodersMax || BindPairs.Count > kNumBindsMax)
                return false;

            {
                var v = new BitVector(BindPairs.Count + PackStreams.Count);

                for (int i = 0; i < BindPairs.Count; i++)
                    if (v.GetAndSet(BindPairs[i].InIndex))
                        return false;

                for (int i = 0; i < PackStreams.Count; i++)
                    if (v.GetAndSet(PackStreams[i]))
                        return false;
            }

            {
                var v = new BitVector(UnpackSizes.Count);
                for (int i = 0; i < BindPairs.Count; i++)
                    if (v.GetAndSet(BindPairs[i].OutIndex))
                        return false;
            }

            uint[] mask = new uint[kMaskSize];

            {
                List<int> inStreamToCoder = new List<int>();
                List<int> outStreamToCoder = new List<int>();
                for (int i = 0; i < Coders.Count; i++)
                {
                    CCoderInfo coder = Coders[i];
                    for (int j = 0; j < coder.NumInStreams; j++)
                        inStreamToCoder.Add(i);
                    for (int j = 0; j < coder.NumOutStreams; j++)
                        outStreamToCoder.Add(i);
                }

                for (int i = 0; i < BindPairs.Count; i++)
                {
                    CBindPair bp = BindPairs[i];
                    mask[inStreamToCoder[bp.InIndex]] |= (1u << outStreamToCoder[bp.OutIndex]);
                }
            }

            for (int i = 0; i < kMaskSize; i++)
                for (int j = 0; j < kMaskSize; j++)
                    if (((1u << j) & mask[i]) != 0)
                        mask[i] |= mask[j];

            for (int i = 0; i < kMaskSize; i++)
                if (((1u << i) & mask[i]) != 0)
                    return false;

            return true;
        }
    }

    public class CFileItem
    {
        public long Size { get; internal set; }
        public uint? Attrib { get; internal set; }
        public uint? Crc { get; internal set; }
        public string Name { get; internal set; }

        public bool HasStream { get; internal set; }
        public bool IsDir { get; internal set; }
        public bool CrcDefined { get { return Crc != null; } }
        public bool AttribDefined { get { return Attrib != null; } }

        public void SetAttrib(uint attrib)
        {
            this.Attrib = attrib;
        }

        public DateTime? CTime { get; internal set; }
        public DateTime? ATime { get; internal set; }
        public DateTime? MTime { get; internal set; }

        public long? StartPos { get; internal set; }
        public bool IsAnti { get; internal set; }

        internal CFileItem()
        {
            HasStream = true;
        }
    }
}
