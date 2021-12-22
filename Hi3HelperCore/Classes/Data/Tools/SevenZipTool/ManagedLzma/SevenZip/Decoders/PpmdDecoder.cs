using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ManagedLzma.SevenZip.Reader
{
    internal sealed class PpmdArchiveDecoder : DecoderNode
    {
        private sealed class OutputStream : ReaderNode
        {
            private PpmdArchiveDecoder mOwner;
            public OutputStream(PpmdArchiveDecoder owner) { mOwner = owner; }
            public override void Dispose() { mOwner = null; }
            public override void Skip(int count) => mOwner.Skip(count);
            public override int Read(byte[] buffer, int offset, int count) => mOwner.Read(buffer, offset, count);
        }

        private ReaderNode mInput;
        private OutputStream mOutput;
        private long mLength;

        private PPMD.CPpmd7 mState = new PPMD.CPpmd7();
        private PPMD.ISzAlloc mAlloc = new PPMD.ISzAlloc {
            Alloc = (x, sz) => Marshal.AllocCoTaskMem(checked((int)sz)),
            Free = (x, p) => Marshal.FreeCoTaskMem(p),
        };
        private PPMD.CPpmd7z_RangeDec mRangeDecoder = new PPMD.CPpmd7z_RangeDec();
        private byte mSettingOrder;
        private uint mSettingMemory;
        private bool mExceededInput;
        private enum kStatus { NeedInit, Normal, Finished, Error }
        private kStatus mDecoderStatus = kStatus.NeedInit;
        private enum HRESULT { S_OK, S_FALSE, E_FAIL }
        private long mPosition;
        private byte[] mInputBuffer = new byte[0x4000];
        private int mInputOffset;
        private int mInputEnding;

        public PpmdArchiveDecoder(ImmutableArray<byte> settings, long length)
        {
            if (settings.IsDefault)
                throw new ArgumentNullException(nameof(settings));

            if (settings.Length != 5)
                throw new InvalidDataException();

            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            mOutput = new OutputStream(this);
            mLength = length;

            mSettingOrder = settings[0];
            if (mSettingOrder < PPMD.PPMD7_MIN_ORDER || mSettingOrder > PPMD.PPMD7_MAX_ORDER)
                throw new InvalidDataException();

            mSettingMemory = (uint)settings[1] | ((uint)settings[2] << 8) | ((uint)settings[3] << 16) | ((uint)settings[4] << 24);
            if (mSettingMemory < PPMD.PPMD7_MIN_MEM_SIZE || mSettingMemory > PPMD.PPMD7_MAX_MEM_SIZE)
                throw new InvalidDataException();

            mRangeDecoder = new PPMD.CPpmd7z_RangeDec();
            PPMD.Ppmd7z_RangeDec_CreateVTable(mRangeDecoder);
            mRangeDecoder.Stream = new PPMD.IByteIn { Read = x => ReadByte() };

            PPMD.Ppmd7_Construct(mState);
            if (!PPMD.Ppmd7_Alloc(mState, mSettingMemory, mAlloc))
                throw new OutOfMemoryException();
        }

        public override void Dispose()
        {
            Utilities.NeedsReview();

            mOutput.Dispose();
            mInput?.Dispose();
            PPMD.Ppmd7_Free(mState, mAlloc);
        }

        public override void SetInputStream(int index, ReaderNode stream, long length)
        {
            if (index != 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (mInput != null)
                throw new InvalidOperationException();

            mInput = stream;
        }

        public override ReaderNode GetOutputStream(int index)
        {
            if (index != 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            return mOutput;
        }

        private byte ReadByte()
        {
            return mInputOffset < mInputEnding ? mInputBuffer[mInputOffset++] : ReadByteSlow();
        }

        private byte ReadByteSlow()
        {
            if (!mExceededInput)
            {
                mInputOffset = 0;
                mInputEnding = mInput.Read(mInputBuffer, 0, mInputBuffer.Length);

                if (mInputEnding != 0)
                    return mInputBuffer[mInputOffset++];

                mExceededInput = true;
            }

            return 0;
        }

        private void Skip(int count)
        {
            Utilities.NeedsBetterImplementation();

            var buffer = new byte[Math.Min(0x4000, count)];
            while (count > 0)
            {
                var skipped = Read(buffer, 0, Math.Min(buffer.Length, count));
                if (skipped == 0)
                    throw new InvalidOperationException(ErrorStrings.SkipBeyondEndOfStream);

                count -= skipped;
            }
        }

        private unsafe int Read(byte[] buffer, int offset, int length)
        {
            Utilities.DebugCheckStreamArguments(buffer, offset, length, StreamMode.Partial);

            if (mDecoderStatus == kStatus.Finished || mPosition >= mLength)
                return 0;

            var startPosition = mPosition;

            HRESULT res;
            fixed (byte* p = buffer)
                res = CodeSpec(p + offset, (uint)length);

            if (res != HRESULT.S_OK)
                throw new InvalidDataException();

            return checked((int)(mPosition - startPosition));
        }

        private unsafe HRESULT CodeSpec(byte* memStream, uint size)
        {
            switch (mDecoderStatus)
            {
                case kStatus.Finished:
                    return HRESULT.S_OK;

                case kStatus.Error:
                    return HRESULT.S_FALSE;

                case kStatus.NeedInit:
                    //_inStream.Init();
                    if (!PPMD.Ppmd7z_RangeDec_Init(mRangeDecoder))
                    {
                        mDecoderStatus = kStatus.Error;
                        return HRESULT.S_FALSE;
                    }
                    mDecoderStatus = kStatus.Normal;
                    PPMD.Ppmd7_Init(mState, mSettingOrder);
                    break;
            }

            /* if (mLength.HasValue) */
            {
                var rem = mLength - mPosition;
                if (size > rem)
                    size = (uint)rem;
            }

            uint i;
            int sym = 0;
            for (i = 0; i != size; i++)
            {
                sym = PPMD.Ppmd7_DecodeSymbol(mState, mRangeDecoder);
                if (mExceededInput || sym < 0)
                    break;
                memStream[i] = (byte)sym;
            }

            mPosition += i;

            if (mExceededInput)
            {
                mDecoderStatus = kStatus.Error;
                return HRESULT.E_FAIL;
            }

            if (sym < 0)
                mDecoderStatus = (sym < -1) ? kStatus.Error : kStatus.Finished;

            return HRESULT.S_OK;
        }
    }

    internal static partial class PPMD
    {
        public sealed class ISzAlloc
        {
            public Func<ISzAlloc, uint, IntPtr> Alloc;
            public Action<ISzAlloc, IntPtr> Free;
        }

        public sealed class IByteIn
        {
            public Func<object, byte> Read;
        }
    }

    unsafe partial class PPMD
    {
        public const int PPMD_INT_BITS = 7;
        public const int PPMD_PERIOD_BITS = 7;
        public const int PPMD_BIN_SCALE = (1 << (PPMD_INT_BITS + PPMD_PERIOD_BITS));

        public static int PPMD_GET_MEAN_SPEC(int summ, int shift, int round) => (((summ) + (1 << ((shift) - (round)))) >> (shift));
        public static int PPMD_GET_MEAN(int summ) => PPMD_GET_MEAN_SPEC((summ), PPMD_PERIOD_BITS, 2);
        public static void PPMD_UPDATE_PROB_0(ref ushort prob) => prob = (ushort)((prob) + (1 << PPMD_INT_BITS) - PPMD_GET_MEAN(prob));
        public static void PPMD_UPDATE_PROB_1(ref ushort prob) => prob = (ushort)((prob) - PPMD_GET_MEAN(prob));

        public const int PPMD_N1 = 4;
        public const int PPMD_N2 = 4;
        public const int PPMD_N3 = 4;
        public const int PPMD_N4 = ((128 + 3 - 1 * PPMD_N1 - 2 * PPMD_N2 - 3 * PPMD_N3) / 4);
        public const int PPMD_NUM_INDEXES = (PPMD_N1 + PPMD_N2 + PPMD_N3 + PPMD_N4);

        /* SEE-contexts for PPM-contexts with masked symbols */
        public struct CPpmd_See
        {
            public ushort Summ; /* Freq */
            public byte Shift;  /* Speed of Freq change; low Shift is for fast change */
            public byte Count;  /* Count to next change of Shift */
        }

        public static void Ppmd_See_Update(ref CPpmd_See p)
        {
            if (p.Shift < PPMD_PERIOD_BITS && --p.Count == 0)
            {
                p.Summ <<= 1;
                p.Count = (byte)(3 << p.Shift++);
            }
        }

        public struct CPpmd_State
        {
            public byte Symbol;
            public byte Freq;
            public ushort SuccessorLow;
            public ushort SuccessorHigh;
        }

        public struct CPpmd_State_Ref
        {
            public uint Value;

            public CPpmd_State_Ref(uint value)
            {
                this.Value = value;
            }

            public static implicit operator CPpmd_State_Ref(CPpmd_Void_Ref r)
            {
                return new CPpmd_State_Ref(r.Value);
            }

            public static implicit operator CPpmd_Void_Ref(CPpmd_State_Ref r)
            {
                return new CPpmd_Void_Ref(r.Value);
            }
        }

        public struct CPpmd_Void_Ref
        {
            public uint Value;

            public CPpmd_Void_Ref(uint value)
            {
                this.Value = value;
            }

            public static bool operator ==(CPpmd_Void_Ref lhs, CPpmd_Void_Ref rhs) => lhs.Value == rhs.Value;
            public static bool operator !=(CPpmd_Void_Ref lhs, CPpmd_Void_Ref rhs) => lhs.Value != rhs.Value;
        }

        public struct CPpmd_Byte_Ref
        {
            public uint Value;

            public CPpmd_Byte_Ref(uint value)
            {
                this.Value = value;
            }

            public static implicit operator CPpmd_Void_Ref(CPpmd_Byte_Ref r) => new CPpmd_Void_Ref(r.Value);
            public static implicit operator CPpmd_Byte_Ref(CPpmd_Void_Ref r) => new CPpmd_Byte_Ref(r.Value);
        }

        public static void PPMD_SetAllBitsIn256Bytes(uint* p)
        {
            for (int i = 0; i < 256 / sizeof(uint); i += 8)
            {
                p[i + 7] = p[i + 6] = p[i + 5] = p[i + 4] = p[i + 3] = p[i + 2] = p[i + 1] = p[i + 0] = ~0u;
            }
        }
    }

    unsafe partial class PPMD
    {
        public const int PPMD7_MIN_ORDER = 2;
        public const int PPMD7_MAX_ORDER = 64;

        public const int PPMD7_MIN_MEM_SIZE = (1 << 11);
        public const uint PPMD7_MAX_MEM_SIZE = (0xFFFFFFFF - 12 * 3);

        //struct CPpmd7_Context_;

        public struct CPpmd7_Context_Ref
        {
            public uint Value;

            public CPpmd7_Context_Ref(uint value)
            {
                this.Value = value;
            }

            public static implicit operator CPpmd7_Context_Ref(CPpmd_Void_Ref r)
            {
                return new CPpmd7_Context_Ref(r.Value);
            }

            public static implicit operator CPpmd_Void_Ref(CPpmd7_Context_Ref r)
            {
                return new CPpmd_Void_Ref(r.Value);
            }
        }

        public struct CPpmd7_Context
        {
            public ushort NumStats;
            public ushort SummFreq;
            public CPpmd_State_Ref Stats;
            public CPpmd7_Context_Ref Suffix;
        }

        public static CPpmd_State* Ppmd7Context_OneState(CPpmd7_Context* p) => (CPpmd_State*)&p->SummFreq;

        public sealed class CPpmd7
        {
            public CPpmd7_Context* MinContext;
            public CPpmd7_Context* MaxContext;
            public CPpmd_State* FoundState;
            public uint OrderFall;
            public uint InitEsc;
            public uint PrevSuccess;
            public uint MaxOrder;
            public uint HiBitsFlag;
            public int RunLength;
            public int InitRL; /* must be 32-bit at least */

            public uint Size;
            public uint GlueCount;
            public byte* Base;
            public byte* LoUnit;
            public byte* HiUnit;
            public byte* Text;
            public byte* UnitsStart;
            public uint AlignOffset;

            public byte[] Indx2Units = new byte[PPMD_NUM_INDEXES];
            public byte[] Units2Indx = new byte[128];
            public CPpmd_Void_Ref[] FreeList = new CPpmd_Void_Ref[PPMD_NUM_INDEXES];
            public byte[] NS2Indx = new byte[256];
            public byte[] NS2BSIndx = new byte[256];
            public byte[] HB2Flag = new byte[256];
            public CPpmd_See* DummySee;
            //public CPpmd_See[,] See = new CPpmd_See[25, 16];
            public CPpmd_See*[] See = new CPpmd_See*[25];
            //public ushort[,] BinSumm = new ushort[128, 64];
            public ushort*[] BinSumm = new ushort*[128];

            private IntPtr mBackingSee;
            private IntPtr mBackingBinSumm;

            public CPpmd7()
            {
#if NET_45
                var kSeeSize = Marshal.SizeOf(typeof(CPpmd_See));
#else
                var kSeeSize = Marshal.SizeOf<CPpmd_See>();
#endif
                mBackingSee = Marshal.AllocCoTaskMem(kSeeSize * (25 * 16 + 1));
                DummySee = (CPpmd_See*)mBackingSee;
                for (int i = 0; i < 25; i++)
                {
                    See[i] = DummySee;
                    DummySee += 16;
                }

                mBackingBinSumm = Marshal.AllocCoTaskMem(128 * 64 * 2);
                ushort* p = (ushort*)mBackingBinSumm;
                for (int i = 0; i < 128; i++)
                {
                    BinSumm[i] = p;
                    p += 64;
                }
            }

            ~CPpmd7()
            {
                Marshal.FreeCoTaskMem(mBackingSee);
                Marshal.FreeCoTaskMem(mBackingBinSumm);
            }
        }

        //void Ppmd7_Construct(CPpmd7 *p);
        //Bool Ppmd7_Alloc(CPpmd7 *p, uint size, ISzAlloc *alloc);
        //void Ppmd7_Free(CPpmd7 *p, ISzAlloc *alloc);
        //void Ppmd7_Init(CPpmd7 *p, unsigned maxOrder);
        public static bool Ppmd7_WasAllocated(CPpmd7 p) => (p.Base != null);


        /* ---------- Internal Functions ---------- */

        //extern const byte PPMD7_kExpEscape[16];

        public static void* Ppmd7_GetPtr(CPpmd7 p, CPpmd_Void_Ref offs) => ((void*)(p.Base + offs.Value));
        public static CPpmd7_Context* Ppmd7_GetContext(CPpmd7 p, CPpmd7_Context_Ref offs) => (CPpmd7_Context*)Ppmd7_GetPtr(p, offs);
        public static CPpmd_State* Ppmd7_GetStats(CPpmd7 p, CPpmd7_Context* ctx) => (CPpmd_State*)Ppmd7_GetPtr(p, ctx->Stats);

        //void Ppmd7_Update1(CPpmd7 *p);
        //void Ppmd7_Update1_0(CPpmd7 *p);
        //void Ppmd7_Update2(CPpmd7 *p);
        //void Ppmd7_UpdateBin(CPpmd7 *p);

        public static ref ushort Ppmd7_GetBinSumm(CPpmd7 p)
        {
            return ref p.BinSumm[Ppmd7Context_OneState(p.MinContext)->Freq - 1]
                     [p.PrevSuccess
                         + p.NS2BSIndx[Ppmd7_GetContext(p, p.MinContext->Suffix)->NumStats - 1]
                         + (p.HiBitsFlag = p.HB2Flag[p.FoundState->Symbol])
                         + 2 * p.HB2Flag[Ppmd7Context_OneState(p.MinContext)->Symbol]
                         + ((p.RunLength >> 26) & 0x20)];
        }

        //CPpmd_See *Ppmd7_MakeEscFreq(CPpmd7 *p, unsigned numMasked, uint *scale);


        /* ---------- Decode ---------- */

        public abstract class IPpmd7_RangeDec
        {
            public Func<object, uint, uint> GetThreshold;
            public Action<object, uint, uint> Decode;
            public Func<object, uint, uint> DecodeBit;
        }

        public sealed class CPpmd7z_RangeDec : IPpmd7_RangeDec
        {
            public uint Range;
            public uint Code;
            public IByteIn Stream;
        }

        //void Ppmd7z_RangeDec_CreateVTable(CPpmd7z_RangeDec* p);
        //Bool Ppmd7z_RangeDec_Init(CPpmd7z_RangeDec* p);
        public static bool Ppmd7z_RangeDec_IsFinishedOK(CPpmd7z_RangeDec p) => p.Code == 0;

        //int Ppmd7_DecodeSymbol(CPpmd7* p, IPpmd7_RangeDec* rc);

    }

    unsafe partial class PPMD
    {
        private static byte[] PPMD7_kExpEscape = new byte[16] { 25, 14, 9, 7, 5, 5, 4, 4, 4, 3, 3, 3, 2, 2, 2, 2 };
        private static ushort[] kInitBinEsc = { 0x3CDD, 0x1F3F, 0x59BF, 0x48F3, 0x64A1, 0x5ABC, 0x6632, 0x6051 };

        private const int MAX_FREQ = 124;
        private const int UNIT_SIZE = 12;

        private static uint U2B(uint nu) => (nu * UNIT_SIZE);
        private static byte U2I(CPpmd7 p, uint nu) => p.Units2Indx[nu - 1];
        private static byte I2U(CPpmd7 p, uint indx) => p.Indx2Units[indx];

        private static CPpmd_Void_Ref REF(CPpmd7 p, void* ptr) => new CPpmd_Void_Ref((uint)((byte*)ptr - p.Base));

        private static CPpmd_State_Ref STATS_REF(CPpmd7 p, void* ptr) => REF(p, ptr);

        private static CPpmd7_Context* CTX(CPpmd7 p, CPpmd7_Context_Ref r) => Ppmd7_GetContext(p, r);
        private static CPpmd_State* STATS(CPpmd7 p, CPpmd7_Context* ctx) => Ppmd7_GetStats(p, ctx);
        private static CPpmd_State* ONE_STATE(CPpmd7_Context* ctx) => Ppmd7Context_OneState(ctx);
        private static CPpmd7_Context* SUFFIX(CPpmd7 p, CPpmd7_Context* ctx) => CTX(p, ctx->Suffix);

        //typedef CPpmd7_Context * CTX_PTR;
        //struct CPpmd7_Node_;

        public struct CPpmd7_Node_Ref
        {
            public uint Value;

            public CPpmd7_Node_Ref(uint value)
            {
                this.Value = value;
            }

            public static bool operator ==(CPpmd7_Node_Ref lhs, CPpmd7_Node_Ref rhs) => lhs.Value == rhs.Value;
            public static bool operator !=(CPpmd7_Node_Ref lhs, CPpmd7_Node_Ref rhs) => lhs.Value != rhs.Value;
            public static implicit operator CPpmd_Void_Ref(CPpmd7_Node_Ref r) => new CPpmd_Void_Ref(r.Value);
            public static implicit operator CPpmd7_Node_Ref(CPpmd_Void_Ref r) => new CPpmd7_Node_Ref(r.Value);
        }

        struct CPpmd7_Node
        {
            public ushort Stamp; /* must be at offset 0 as CPpmd7_Context::NumStats. Stamp=0 means free */
            public ushort NU;
            public CPpmd7_Node_Ref Next; /* must be at offset >= 4 */
            public CPpmd7_Node_Ref Prev;
        }

        private static CPpmd7_Node* NODE(CPpmd7 p, CPpmd7_Node_Ref offs) => (CPpmd7_Node*)(p.Base + offs.Value);

        public static void Ppmd7_Construct(CPpmd7 p)
        {
            uint i, k, m;

            p.Base = null;

            for (i = 0, k = 0; i < PPMD_NUM_INDEXES; i++)
            {
                uint step = (i >= 12 ? 4 : (i >> 2) + 1);
                do { p.Units2Indx[k++] = (byte)i; } while (--step != 0);
                p.Indx2Units[i] = (byte)k;
            }

            p.NS2BSIndx[0] = (0 << 1);
            p.NS2BSIndx[1] = (1 << 1);

            //memset(p.NS2BSIndx + 2, (2 << 1), 9);
            for (i = 0; i < 9; i++)
                p.NS2BSIndx[i + 2] = 2 << 1;

            //memset(p.NS2BSIndx + 11, (3 << 1), 256 - 11);
            for (i = 0; i < 256 - 11; i++)
                p.NS2BSIndx[i + 11] = 3 << 1;

            for (i = 0; i < 3; i++)
                p.NS2Indx[i] = (byte)i;

            for (m = i, k = 1; i < 256; i++)
            {
                p.NS2Indx[i] = (byte)m;

                if (--k == 0)
                    k = (++m) - 2;
            }

            //memset(p.HB2Flag, 0, 0x40);
            for (i = 0; i < 0x40; i++)
                p.HB2Flag[i] = 0;

            //memset(p.HB2Flag + 0x40, 8, 0x100 - 0x40);
            for (i = 0; i < 0x100 - 0x40; i++)
                p.HB2Flag[i + 0x40] = 8;
        }

        public static void Ppmd7_Free(CPpmd7 p, ISzAlloc alloc)
        {
            alloc.Free(alloc, (IntPtr)p.Base);
            p.Size = 0;
            p.Base = null;
        }

        public static bool Ppmd7_Alloc(CPpmd7 p, uint size, ISzAlloc alloc)
        {
            if (p.Base == null || p.Size != size)
            {
                Ppmd7_Free(p, alloc);
                p.AlignOffset = 4 - (size & 3);
                if ((p.Base = (byte*)alloc.Alloc(alloc, p.AlignOffset + size + UNIT_SIZE)) == null)
                    return false;
                p.Size = size;
            }
            return true;
        }

        private static void InsertNode(CPpmd7 p, void* node, uint indx)
        {
            *((CPpmd_Void_Ref*)node) = p.FreeList[indx];
            p.FreeList[indx] = REF(p, node);
        }

        private static void* RemoveNode(CPpmd7 p, uint indx)
        {
            CPpmd_Void_Ref* node = (CPpmd_Void_Ref*)Ppmd7_GetPtr(p, p.FreeList[indx]);
            p.FreeList[indx] = *node;
            return node;
        }

        private static void SplitBlock(CPpmd7 p, void* ptr, uint oldIndx, uint newIndx)
        {
            uint i;
            uint nu = (uint)I2U(p, oldIndx) - (uint)I2U(p, newIndx);
            ptr = (byte*)ptr + U2B(I2U(p, newIndx));
            if (I2U(p, i = U2I(p, nu)) != nu)
            {
                uint k = I2U(p, --i);
                InsertNode(p, ((byte*)ptr) + U2B(k), nu - k - 1);
            }
            InsertNode(p, ptr, i);
        }

        private static void GlueFreeBlocks(CPpmd7 p)
        {
            CPpmd7_Node_Ref head = new CPpmd7_Node_Ref(p.AlignOffset + p.Size);
            CPpmd7_Node_Ref n = head;
            uint i;

            p.GlueCount = 255;

            /* create doubly-linked list of free blocks */
            for (i = 0; i < PPMD_NUM_INDEXES; i++)
            {
                ushort nu = I2U(p, i);
                CPpmd7_Node_Ref next = (CPpmd7_Node_Ref)p.FreeList[i];
                p.FreeList[i] = default(CPpmd_Void_Ref);
                while (next.Value != 0)
                {
                    CPpmd7_Node* node = NODE(p, next);
                    node->Next = n;
                    n = NODE(p, n)->Prev = next;
                    next = *(CPpmd7_Node_Ref*)node;
                    node->Stamp = 0;
                    node->NU = (ushort)nu;
                }
            }
            NODE(p, head)->Stamp = 1;
            NODE(p, head)->Next = n;
            NODE(p, n)->Prev = head;
            if (p.LoUnit != p.HiUnit)
                ((CPpmd7_Node*)p.LoUnit)->Stamp = 1;

            /* Glue free blocks */
            while (n != head)
            {
                CPpmd7_Node* node = NODE(p, n);
                uint nu = (uint)node->NU;
                for (;;)
                {
                    CPpmd7_Node* node2 = NODE(p, n) + nu;
                    nu += node2->NU;
                    if (node2->Stamp != 0 || nu >= 0x10000)
                        break;
                    NODE(p, node2->Prev)->Next = node2->Next;
                    NODE(p, node2->Next)->Prev = node2->Prev;
                    node->NU = (ushort)nu;
                }
                n = node->Next;
            }

            /* Fill lists of free blocks */
            for (n = NODE(p, head)->Next; n != head;)
            {
                CPpmd7_Node* node = NODE(p, n);
                uint nu;
                CPpmd7_Node_Ref next = node->Next;
                for (nu = node->NU; nu > 128; nu -= 128, node += 128)
                    InsertNode(p, node, PPMD_NUM_INDEXES - 1);
                if (I2U(p, i = U2I(p, nu)) != nu)
                {
                    uint k = I2U(p, --i);
                    InsertNode(p, node + k, nu - k - 1);
                }
                InsertNode(p, node, i);
                n = next;
            }
        }

        private static void* AllocUnitsRare(CPpmd7 p, uint indx)
        {
            uint i;
            void* retVal;
            if (p.GlueCount == 0)
            {
                GlueFreeBlocks(p);
                if (p.FreeList[indx].Value != 0)
                    return RemoveNode(p, indx);
            }

            i = indx;

            do
            {
                if (++i == PPMD_NUM_INDEXES)
                {
                    uint numBytes = U2B(I2U(p, indx));
                    p.GlueCount--;
                    return ((uint)(p.UnitsStart - p.Text) > numBytes) ? (p.UnitsStart -= numBytes) : null;
                }
            }
            while (p.FreeList[i].Value == 0);

            retVal = RemoveNode(p, i);
            SplitBlock(p, retVal, i, indx);
            return retVal;
        }

        private static void* AllocUnits(CPpmd7 p, uint indx)
        {
            uint numBytes;
            if (p.FreeList[indx].Value != 0)
                return RemoveNode(p, indx);
            numBytes = U2B(I2U(p, indx));
            if (numBytes <= (uint)(p.HiUnit - p.LoUnit))
            {
                void* retVal = p.LoUnit;
                p.LoUnit += numBytes;
                return retVal;
            }
            return AllocUnitsRare(p, indx);
        }

        private static void MyMem12Cpy(void* dest, void* src, uint num) => MyMem12Cpy((uint*)dest, (uint*)src, num);

        private static void MyMem12Cpy(uint* dest, uint* src, uint num)
        {
            uint* d = dest;
            uint* s = src;
            uint n = num;
            do
            {
                d[0] = s[0];
                d[1] = s[1];
                d[2] = s[2];
                s += 3;
                d += 3;
            }
            while (--n != 0);
        }

        private static void* ShrinkUnits(CPpmd7 p, void* oldPtr, uint oldNU, uint newNU)
        {
            uint i0 = U2I(p, oldNU);
            uint i1 = U2I(p, newNU);

            if (i0 == i1)
                return oldPtr;

            if (p.FreeList[i1].Value != 0)
            {
                void* ptr = RemoveNode(p, i1);
                MyMem12Cpy(ptr, oldPtr, newNU);
                InsertNode(p, oldPtr, i0);
                return ptr;
            }

            SplitBlock(p, oldPtr, i0, i1);
            return oldPtr;
        }

        private static CPpmd_Void_Ref SUCCESSOR(CPpmd_State* p) => new CPpmd_Void_Ref((uint)p->SuccessorLow | ((uint)p->SuccessorHigh << 16));

        private static void SetSuccessor(CPpmd_State* p, CPpmd_Void_Ref v)
        {
            (p)->SuccessorLow = (ushort)v.Value;
            (p)->SuccessorHigh = (ushort)(v.Value >> 16);
        }

        private static void RestartModel(CPpmd7 p)
        {
            uint i, k, m;

            //memset(p.FreeList, 0, sizeof(p.FreeList));
            for (i = 0; i < p.FreeList.Length; i++)
                p.FreeList[i] = default(CPpmd_Void_Ref);

            p.Text = p.Base + p.AlignOffset;
            p.HiUnit = p.Text + p.Size;
            p.LoUnit = p.UnitsStart = p.HiUnit - p.Size / 8 / UNIT_SIZE * 7 * UNIT_SIZE;
            p.GlueCount = 0;

            p.OrderFall = p.MaxOrder;
            p.RunLength = p.InitRL = -(Int32)((p.MaxOrder < 12) ? p.MaxOrder : 12) - 1;
            p.PrevSuccess = 0;

            p.MinContext = p.MaxContext = (CPpmd7_Context*)(p.HiUnit -= UNIT_SIZE); /* AllocContext(p); */
            p.MinContext->Suffix = default(CPpmd7_Context_Ref);
            p.MinContext->NumStats = 256;
            p.MinContext->SummFreq = 256 + 1;
            p.FoundState = (CPpmd_State*)p.LoUnit; /* AllocUnits(p, PPMD_NUM_INDEXES - 1); */
            p.LoUnit += U2B(256 / 2);
            p.MinContext->Stats = REF(p, p.FoundState);
            for (i = 0; i < 256; i++)
            {
                CPpmd_State* s = &p.FoundState[i];
                s->Symbol = (byte)i;
                s->Freq = 1;
                SetSuccessor(s, default(CPpmd_Void_Ref));
            }

            for (i = 0; i < 128; i++)
                for (k = 0; k < 8; k++)
                {
                    ushort* dest = p.BinSumm[i] + k;
                    ushort val = (ushort)(PPMD_BIN_SCALE - kInitBinEsc[k] / (i + 2));
                    for (m = 0; m < 64; m += 8)
                        dest[m] = val;
                }

            for (i = 0; i < 25; i++)
                for (k = 0; k < 16; k++)
                {
                    CPpmd_See* s = &p.See[i][k];
                    s->Summ = (ushort)((5 * i + 10) << (s->Shift = PPMD_PERIOD_BITS - 4));
                    s->Count = 4;
                }
        }

        public static void Ppmd7_Init(CPpmd7 p, uint maxOrder)
        {
            p.MaxOrder = maxOrder;
            RestartModel(p);
            p.DummySee->Shift = PPMD_PERIOD_BITS;
            p.DummySee->Summ = 0; /* unused */
            p.DummySee->Count = 64; /* unused */
        }

        private static CPpmd7_Context* CreateSuccessors(CPpmd7 p, bool skip)
        {
            CPpmd_State upState;
            CPpmd7_Context* c = p.MinContext;
            CPpmd_Byte_Ref upBranch = (CPpmd_Byte_Ref)SUCCESSOR(p.FoundState);
            CPpmd_State*[] ps = new CPpmd_State*[PPMD7_MAX_ORDER];
            uint numPs = 0;

            if (!skip)
                ps[numPs++] = p.FoundState;

            while (c->Suffix.Value != 0)
            {
                CPpmd_Void_Ref successor;
                CPpmd_State* s;
                c = SUFFIX(p, c);
                if (c->NumStats != 1)
                {
                    for (s = STATS(p, c); s->Symbol != p.FoundState->Symbol; s++) ;
                }
                else
                    s = ONE_STATE(c);

                successor = SUCCESSOR(s);

                if (successor != upBranch)
                {
                    c = CTX(p, successor);
                    if (numPs == 0)
                        return c;

                    break;
                }

                ps[numPs++] = s;
            }

            upState.Symbol = *(byte*)Ppmd7_GetPtr(p, upBranch);
            SetSuccessor(&upState, new CPpmd_Byte_Ref(upBranch.Value + 1));

            if (c->NumStats == 1)
                upState.Freq = ONE_STATE(c)->Freq;
            else
            {
                uint cf, s0;
                CPpmd_State* s;
                for (s = STATS(p, c); s->Symbol != upState.Symbol; s++) ;
                cf = s->Freq - 1u;
                s0 = (uint)c->SummFreq - (uint)c->NumStats - cf;
                upState.Freq = (byte)(1 + ((2 * cf <= s0)
                    ? (5 * cf > s0 ? 1u : 0u)
                    : ((2 * cf + 3 * s0 - 1) / (2 * s0))));
            }

            do
            {
                /* Create Child */
                CPpmd7_Context* c1; /* = AllocContext(p); */
                if (p.HiUnit != p.LoUnit)
                    c1 = (CPpmd7_Context*)(p.HiUnit -= UNIT_SIZE);
                else if (p.FreeList[0].Value != 0)
                    c1 = (CPpmd7_Context*)RemoveNode(p, 0);
                else
                {
                    c1 = (CPpmd7_Context*)AllocUnitsRare(p, 0);
                    if (c1 == null)
                        return null;
                }
                c1->NumStats = 1;
                *ONE_STATE(c1) = upState;
                c1->Suffix = REF(p, c);
                SetSuccessor(ps[--numPs], REF(p, c1));
                c = c1;
            }
            while (numPs != 0);

            return c;
        }

        private static void SwapStates(CPpmd_State* t1, CPpmd_State* t2)
        {
            CPpmd_State tmp = *t1;
            *t1 = *t2;
            *t2 = tmp;
        }

        private static void UpdateModel(CPpmd7 p)
        {
            CPpmd_Void_Ref successor, fSuccessor = SUCCESSOR(p.FoundState);
            CPpmd7_Context* c;
            uint s0, ns;

            if (p.FoundState->Freq < MAX_FREQ / 4 && p.MinContext->Suffix.Value != 0)
            {
                c = SUFFIX(p, p.MinContext);

                if (c->NumStats == 1)
                {
                    CPpmd_State* s = ONE_STATE(c);
                    if (s->Freq < 32)
                        s->Freq++;
                }
                else
                {
                    CPpmd_State* s = STATS(p, c);
                    if (s->Symbol != p.FoundState->Symbol)
                    {
                        do { s++; } while (s->Symbol != p.FoundState->Symbol);
                        if (s[0].Freq >= s[-1].Freq)
                        {
                            SwapStates(&s[0], &s[-1]);
                            s--;
                        }
                    }
                    if (s->Freq < MAX_FREQ - 9)
                    {
                        s->Freq += 2;
                        c->SummFreq += 2;
                    }
                }
            }

            if (p.OrderFall == 0)
            {
                p.MinContext = p.MaxContext = CreateSuccessors(p, true);
                if (p.MinContext == null)
                {
                    RestartModel(p);
                    return;
                }
                SetSuccessor(p.FoundState, REF(p, p.MinContext));
                return;
            }

            *p.Text++ = p.FoundState->Symbol;
            successor = REF(p, p.Text);
            if (p.Text >= p.UnitsStart)
            {
                RestartModel(p);
                return;
            }

            if (fSuccessor.Value != 0)
            {
                if (fSuccessor.Value <= successor.Value)
                {
                    CPpmd7_Context* cs = CreateSuccessors(p, false);
                    if (cs == null)
                    {
                        RestartModel(p);
                        return;
                    }
                    fSuccessor = REF(p, cs);
                }
                if (--p.OrderFall == 0)
                {
                    successor = fSuccessor;
                    p.Text -= (p.MaxContext != p.MinContext) ? 1 : 0;
                }
            }
            else
            {
                SetSuccessor(p.FoundState, successor);
                fSuccessor = REF(p, p.MinContext);
            }

            s0 = p.MinContext->SummFreq - (ns = p.MinContext->NumStats) - (p.FoundState->Freq - 1u);

            for (c = p.MaxContext; c != p.MinContext; c = SUFFIX(p, c))
            {
                uint ns1;
                uint cf, sf;
                if ((ns1 = c->NumStats) != 1)
                {
                    if ((ns1 & 1) == 0)
                    {
                        /* Expand for one UNIT */
                        uint oldNU = ns1 >> 1;
                        uint i = U2I(p, oldNU);
                        if (i != U2I(p, oldNU + 1))
                        {
                            void* ptr = AllocUnits(p, i + 1);
                            void* oldPtr;
                            if (ptr == null)
                            {
                                RestartModel(p);
                                return;
                            }
                            oldPtr = STATS(p, c);
                            MyMem12Cpy(ptr, oldPtr, oldNU);
                            InsertNode(p, oldPtr, i);
                            c->Stats = STATS_REF(p, ptr);
                        }
                    }
                    c->SummFreq = (ushort)(c->SummFreq + (2 * ns1 < ns ? 1u : 0u) + 2 * ((4 * ns1 <= ns ? 1u : 0u) & (c->SummFreq <= 8 * ns1 ? 1u : 0u)));
                }
                else
                {
                    CPpmd_State* s = (CPpmd_State*)AllocUnits(p, 0);
                    if (s == null)
                    {
                        RestartModel(p);
                        return;
                    }
                    *s = *ONE_STATE(c);
                    c->Stats = REF(p, s);
                    if (s->Freq < MAX_FREQ / 4 - 1)
                        s->Freq <<= 1;
                    else
                        s->Freq = MAX_FREQ - 4;
                    c->SummFreq = (ushort)(s->Freq + p.InitEsc + (ns > 3 ? 1u : 0u));
                }
                cf = 2 * (uint)p.FoundState->Freq * (c->SummFreq + 6u);
                sf = (uint)s0 + c->SummFreq;
                if (cf < 6 * sf)
                {
                    cf = 1 + (cf > sf ? 1u : 0u) + (cf >= 4 * sf ? 1u : 0u);
                    c->SummFreq += 3;
                }
                else
                {
                    cf = 4 + (cf >= 9 * sf ? 1u : 0u) + (cf >= 12 * sf ? 1u : 0u) + (cf >= 15 * sf ? 1u : 0u);
                    c->SummFreq = (ushort)(c->SummFreq + cf);
                }
                {
                    CPpmd_State* s = STATS(p, c) + ns1;
                    SetSuccessor(s, successor);
                    s->Symbol = p.FoundState->Symbol;
                    s->Freq = (byte)cf;
                    c->NumStats = (ushort)(ns1 + 1);
                }
            }
            p.MaxContext = p.MinContext = CTX(p, fSuccessor);
        }

        private static void Rescale(CPpmd7 p)
        {
            uint i, adder, sumFreq, escFreq;
            CPpmd_State* stats = STATS(p, p.MinContext);
            CPpmd_State* s = p.FoundState;
            {
                CPpmd_State tmp = *s;
                for (; s != stats; s--)
                    s[0] = s[-1];
                *s = tmp;
            }
            escFreq = (uint)p.MinContext->SummFreq - s->Freq;
            s->Freq += 4;
            adder = (p.OrderFall != 0) ? 1u : 0u;
            s->Freq = (byte)((s->Freq + adder) >> 1);
            sumFreq = s->Freq;

            i = (uint)p.MinContext->NumStats - 1;
            do
            {
                escFreq -= (++s)->Freq;
                s->Freq = (byte)((s->Freq + adder) >> 1);
                sumFreq += s->Freq;
                if (s[0].Freq > s[-1].Freq)
                {
                    CPpmd_State* s1 = s;
                    CPpmd_State tmp = *s1;
                    do
                        s1[0] = s1[-1];
                    while (--s1 != stats && tmp.Freq > s1[-1].Freq);
                    *s1 = tmp;
                }
            }
            while (--i != 0);

            if (s->Freq == 0)
            {
                uint numStats = p.MinContext->NumStats;
                uint n0, n1;
                do { i++; } while ((--s)->Freq == 0);
                escFreq += i;
                p.MinContext->NumStats = (ushort)(p.MinContext->NumStats - i);
                if (p.MinContext->NumStats == 1)
                {
                    CPpmd_State tmp = *stats;
                    do
                    {
                        tmp.Freq = (byte)(tmp.Freq - (tmp.Freq >> 1));
                        escFreq >>= 1;
                    }
                    while (escFreq > 1);
                    InsertNode(p, stats, U2I(p, ((numStats + 1) >> 1)));
                    *(p.FoundState = ONE_STATE(p.MinContext)) = tmp;
                    return;
                }
                n0 = (numStats + 1) >> 1;
                n1 = ((uint)p.MinContext->NumStats + 1) >> 1;
                if (n0 != n1)
                    p.MinContext->Stats = STATS_REF(p, ShrinkUnits(p, stats, n0, n1));
            }
            p.MinContext->SummFreq = (ushort)(sumFreq + escFreq - (escFreq >> 1));
            p.FoundState = STATS(p, p.MinContext);
        }

        public static CPpmd_See* Ppmd7_MakeEscFreq(CPpmd7 p, uint numMasked, uint* escFreq)
        {
            CPpmd_See* see;
            uint nonMasked = p.MinContext->NumStats - numMasked;
            if (p.MinContext->NumStats != 256)
            {
                see = p.See[p.NS2Indx[nonMasked - 1]] +
                    (nonMasked < (uint)SUFFIX(p, p.MinContext)->NumStats - p.MinContext->NumStats ? 1 : 0) +
                    2 * (p.MinContext->SummFreq < 11 * p.MinContext->NumStats ? 1 : 0) +
                    4 * (numMasked > nonMasked ? 1 : 0) +
                    p.HiBitsFlag;
                {
                    uint r = ((uint)see->Summ >> see->Shift);
                    see->Summ = (ushort)(see->Summ - r);
                    *escFreq = r + ((r == 0) ? 1u : 0u);
                }
            }
            else
            {
                see = p.DummySee;
                *escFreq = 1;
            }
            return see;
        }

        private static void NextContext(CPpmd7 p)
        {
            CPpmd7_Context* c = CTX(p, SUCCESSOR(p.FoundState));
            if (p.OrderFall == 0 && (byte*)c > p.Text)
                p.MinContext = p.MaxContext = c;
            else
                UpdateModel(p);
        }

        public static void Ppmd7_Update1(CPpmd7 p)
        {
            CPpmd_State* s = p.FoundState;
            s->Freq += 4;
            p.MinContext->SummFreq += 4;
            if (s[0].Freq > s[-1].Freq)
            {
                SwapStates(&s[0], &s[-1]);
                p.FoundState = --s;
                if (s->Freq > MAX_FREQ)
                    Rescale(p);
            }
            NextContext(p);
        }

        public static void Ppmd7_Update1_0(CPpmd7 p)
        {
            var bit = (2 * p.FoundState->Freq > p.MinContext->SummFreq) ? (byte)1 : (byte)0;
            p.PrevSuccess = bit;
            p.RunLength += bit;
            p.MinContext->SummFreq += 4;
            if ((p.FoundState->Freq += 4) > MAX_FREQ)
                Rescale(p);
            NextContext(p);
        }

        public static void Ppmd7_UpdateBin(CPpmd7 p)
        {
            p.FoundState->Freq = (byte)(p.FoundState->Freq + (p.FoundState->Freq < 128 ? 1 : 0));
            p.PrevSuccess = 1;
            p.RunLength++;
            NextContext(p);
        }

        public static void Ppmd7_Update2(CPpmd7 p)
        {
            p.MinContext->SummFreq += 4;
            if ((p.FoundState->Freq += 4) > MAX_FREQ)
                Rescale(p);
            p.RunLength = p.InitRL;
            UpdateModel(p);
        }
    }

    unsafe partial class PPMD
    {
        private const int kTopValue = (1 << 24);

        public static bool Ppmd7z_RangeDec_Init(CPpmd7z_RangeDec p)
        {
            uint i;
            p.Code = 0;
            p.Range = 0xFFFFFFFF;
            if (p.Stream.Read(p.Stream) != 0)
                return false;
            for (i = 0; i < 4; i++)
                p.Code = (p.Code << 8) | p.Stream.Read(p.Stream);
            return (p.Code < 0xFFFFFFFF);
        }

        private static uint Range_GetThreshold(object pp, uint total)
        {
            CPpmd7z_RangeDec p = (CPpmd7z_RangeDec)pp;
            return (p.Code) / (p.Range /= total);
        }

        private static void Range_Normalize(CPpmd7z_RangeDec p)
        {
            if (p.Range < kTopValue)
            {
                p.Code = (p.Code << 8) | p.Stream.Read(p.Stream);
                p.Range <<= 8;
                if (p.Range < kTopValue)
                {
                    p.Code = (p.Code << 8) | p.Stream.Read(p.Stream);
                    p.Range <<= 8;
                }
            }
        }

        private static void Range_Decode(object pp, uint start, uint size)
        {
            CPpmd7z_RangeDec p = (CPpmd7z_RangeDec)pp;
            p.Code -= start * p.Range;
            p.Range *= size;
            Range_Normalize(p);
        }

        private static uint Range_DecodeBit(object pp, uint size0)
        {
            CPpmd7z_RangeDec p = (CPpmd7z_RangeDec)pp;
            uint newBound = (p.Range >> 14) * size0;
            uint symbol;
            if (p.Code < newBound)
            {
                symbol = 0;
                p.Range = newBound;
            }
            else
            {
                symbol = 1;
                p.Code -= newBound;
                p.Range -= newBound;
            }
            Range_Normalize(p);
            return symbol;
        }

        public static void Ppmd7z_RangeDec_CreateVTable(CPpmd7z_RangeDec p)
        {
            p.GetThreshold = Range_GetThreshold;
            p.Decode = Range_Decode;
            p.DecodeBit = Range_DecodeBit;
        }

        private static sbyte MASK_GET(void* charMask, byte sym) => ((sbyte*)charMask)[sym];
        private static void MASK_SET(void* charMask, byte sym, sbyte value) => ((sbyte*)charMask)[sym] = value;

        public static int Ppmd7_DecodeSymbol(CPpmd7 p, IPpmd7_RangeDec rc)
        {
            uint* charMask = stackalloc uint[256 / sizeof(uint)];
            if (p.MinContext->NumStats != 1)
            {
                CPpmd_State* s = Ppmd7_GetStats(p, p.MinContext);
                uint i;
                uint count, hiCnt;
                if ((count = rc.GetThreshold(rc, p.MinContext->SummFreq)) < (hiCnt = s->Freq))
                {
                    byte symbol;
                    rc.Decode(rc, 0, s->Freq);
                    p.FoundState = s;
                    symbol = s->Symbol;
                    Ppmd7_Update1_0(p);
                    return symbol;
                }
                p.PrevSuccess = 0;
                i = p.MinContext->NumStats - 1u;
                do
                {
                    if ((hiCnt += (++s)->Freq) > count)
                    {
                        byte symbol;
                        rc.Decode(rc, hiCnt - s->Freq, s->Freq);
                        p.FoundState = s;
                        symbol = s->Symbol;
                        Ppmd7_Update1(p);
                        return symbol;
                    }
                }
                while (--i != 0);
                if (count >= p.MinContext->SummFreq)
                    return -2;
                p.HiBitsFlag = p.HB2Flag[p.FoundState->Symbol];
                rc.Decode(rc, hiCnt, p.MinContext->SummFreq - hiCnt);
                PPMD_SetAllBitsIn256Bytes(charMask);
                MASK_SET(charMask, s->Symbol, 0);
                i = p.MinContext->NumStats - 1u;
                do { MASK_SET(charMask, (--s)->Symbol, 0); } while (--i != 0);
            }
            else
            {
                ref ushort prob = ref Ppmd7_GetBinSumm(p);
                if (rc.DecodeBit(rc, prob) == 0)
                {
                    byte symbol;
                    PPMD_UPDATE_PROB_0(ref prob);
                    symbol = (p.FoundState = Ppmd7Context_OneState(p.MinContext))->Symbol;
                    Ppmd7_UpdateBin(p);
                    return symbol;
                }
                PPMD_UPDATE_PROB_1(ref prob);
                p.InitEsc = PPMD7_kExpEscape[prob >> 10];
                PPMD_SetAllBitsIn256Bytes(charMask);
                MASK_SET(charMask, Ppmd7Context_OneState(p.MinContext)->Symbol, 0);
                p.PrevSuccess = 0;
            }
            for (;;)
            {
                CPpmd_State** ps = stackalloc CPpmd_State*[256];
                CPpmd_State* s;
                uint freqSum, count, hiCnt;
                CPpmd_See* see;
                uint i, num, numMasked = p.MinContext->NumStats;
                do
                {
                    p.OrderFall++;
                    if (p.MinContext->Suffix.Value == 0)
                        return -1;
                    p.MinContext = Ppmd7_GetContext(p, p.MinContext->Suffix);
                }
                while (p.MinContext->NumStats == numMasked);
                hiCnt = 0;
                s = Ppmd7_GetStats(p, p.MinContext);
                i = 0;
                num = p.MinContext->NumStats - numMasked;
                do
                {
                    uint k = (uint)(int)(MASK_GET(charMask, s->Symbol));
                    hiCnt += (s->Freq & k);
                    ps[i] = s++;
                    i -= k;
                }
                while (i != num);

                see = Ppmd7_MakeEscFreq(p, numMasked, &freqSum);
                freqSum += hiCnt;
                count = rc.GetThreshold(rc, freqSum);

                if (count < hiCnt)
                {
                    byte symbol;
                    CPpmd_State** pps = ps;
                    for (hiCnt = 0; (hiCnt += (*pps)->Freq) <= count; pps++) ;
                    s = *pps;
                    rc.Decode(rc, hiCnt - s->Freq, s->Freq);
                    Ppmd_See_Update(ref *see);
                    p.FoundState = s;
                    symbol = s->Symbol;
                    Ppmd7_Update2(p);
                    return symbol;
                }
                if (count >= freqSum)
                    return -2;
                rc.Decode(rc, hiCnt, freqSum - hiCnt);
                see->Summ = (ushort)(see->Summ + freqSum);
                do { MASK_SET(charMask, ps[--i]->Symbol, 0); } while (i != 0);
            }
        }
    }
}
