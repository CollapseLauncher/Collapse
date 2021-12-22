using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ManagedLzma.LZMA.Master
{
    partial class LZMA
    {
        internal class CMatchFinder
        {
            #region Constants

            internal const int kEmptyHashValue = 0;
            internal const uint kMaxValForNormalize = 0xFFFFFFFF;
            internal const uint kNormalizeStepMin = 1u << 10; // it must be power of 2
            internal const uint kNormalizeMask = ~(kNormalizeStepMin - 1u);
            internal const uint kMaxHistorySize = (3u << 30);

            internal const int kStartMaxLen = 3;

            #endregion

            #region Variables

            public P<byte> mBuffer;
            public uint mPos;
            public uint mPosLimit;
            public uint mStreamPos;
            public uint mLenLimit;

            public uint mCyclicBufferPos;
            public uint mCyclicBufferSize; // it must be = (historySize + 1)

            public uint mMatchMaxLen;
            public uint[] mHash;
            public P<uint> mSon;
            public uint mHashMask;
            public uint mCutValue;

            public P<byte> mBufferBase;
            public ISeqInStream mStream;
            public bool mStreamEndWasReached;

            public uint mBlockSize;
            public uint mKeepSizeBefore;
            public uint mKeepSizeAfter;

            public uint mNumHashBytes;
            public bool mDirectInput;
            public long mDirectInputRem;
            public bool mBtMode;
            public bool mBigHash;
            public uint mHistorySize;
            public uint mFixedHashSize;
            public uint mHashSizeSum;
            public uint mNumSons;
            public SRes mResult;

            #endregion

            internal CMatchFinder()
            {
                TR("MatchFinder_Construct", 0);

                mBufferBase = null;
                mDirectInput = false;
                mHash = null;
                mCutValue = 32;
                mBtMode = true;
                mNumHashBytes = 4;
                mBigHash = false;
            }

            internal bool MatchFinder_NeedMove()
            {
                if (mDirectInput)
                    return false;

                // if (p.streamEndWasReached) return false;
                return mBufferBase + mBlockSize - mBuffer <= mKeepSizeAfter;
            }

            internal P<byte> MatchFinder_GetPointerToCurrentPos()
            {
                return mBuffer;
            }

            internal void MatchFinder_MoveBlock()
            {
                // Note: source and destination memory regions may overlap!
                CUtils.memmove(mBufferBase, mBuffer - mKeepSizeBefore, mStreamPos - mPos + mKeepSizeBefore);
                mBuffer = mBufferBase + mKeepSizeBefore;
            }

            internal void MatchFinder_ReadIfRequired()
            {
                if (!mStreamEndWasReached && mKeepSizeAfter >= mStreamPos - mPos)
                    MatchFinder_ReadBlock();
            }

            private void LzInWindow_Free(ISzAlloc alloc)
            {
                if (!mDirectInput)
                {
                    alloc.FreeBytes(alloc, mBufferBase.mBuffer);
                    mBufferBase = null;
                }
            }

            // keepSizeBefore + keepSizeAfter + keepSizeReserv must be < 4G)
            private bool LzInWindow_Create(uint keepSizeReserv, ISzAlloc alloc)
            {
                uint blockSize = mKeepSizeBefore + mKeepSizeAfter + keepSizeReserv;

                if (mDirectInput)
                {
                    mBlockSize = blockSize;
                    return true;
                }

                if (mBufferBase == null || mBlockSize != blockSize)
                {
                    LzInWindow_Free(alloc);
                    mBlockSize = blockSize;
                    mBufferBase = alloc.AllocBytes(alloc, (long)blockSize);
                }

                return mBufferBase != null;
            }

            internal byte MatchFinder_GetIndexByte(int index)
            {
                return mBuffer[index];
            }

            internal uint MatchFinder_GetNumAvailableBytes()
            {
                return mStreamPos - mPos;
            }

            internal void MatchFinder_ReduceOffsets(uint subValue)
            {
                mPosLimit -= subValue;
                mPos -= subValue;
                mStreamPos -= subValue;
            }

            private void MatchFinder_ReadBlock()
            {
                if (mStreamEndWasReached || mResult != SZ_OK)
                    return;

                if (mDirectInput)
                {
                    uint curSize = 0xFFFFFFFF - mStreamPos;
                    if (curSize > mDirectInputRem)
                        curSize = (uint)mDirectInputRem;

                    mDirectInputRem -= curSize;
                    mStreamPos += curSize;
                    if (mDirectInputRem == 0)
                        mStreamEndWasReached = true;

                    return;
                }

                for (;;)
                {
                    P<byte> dest = mBuffer + (mStreamPos - mPos);
                    long size = mBufferBase + mBlockSize - dest;
                    if (size == 0)
                        return;

                    mResult = mStream.Read(dest, ref size);
                    if (mResult != SZ_OK)
                        return;

                    if (size == 0)
                    {
                        mStreamEndWasReached = true;
                        return;
                    }

                    mStreamPos += (uint)size;
                    if (mStreamPos - mPos > mKeepSizeAfter)
                        return;
                }
            }

            private void MatchFinder_CheckAndMoveAndRead()
            {
                if (MatchFinder_NeedMove())
                    MatchFinder_MoveBlock();

                MatchFinder_ReadBlock();
            }

            private void MatchFinder_FreeThisClassMemory(ISzAlloc alloc)
            {
                alloc.FreeUInt32(alloc, mHash);
                mHash = null;
            }

            internal void MatchFinder_Free(ISzAlloc alloc)
            {
                MatchFinder_FreeThisClassMemory(alloc);
                LzInWindow_Free(alloc);
            }

            private static uint[] AllocRefs(uint num, ISzAlloc alloc)
            {
                long sizeInBytes = (long)num * sizeof(uint);
                if (sizeInBytes / sizeof(uint) != num)
                    return null;

                return alloc.AllocUInt32(alloc, num);
            }

            // Conditions:
            //     historySize <= 3 GB
            //     keepAddBufferBefore + matchMaxLen + keepAddBufferAfter < 511MB
            internal bool MatchFinder_Create(uint historySize, uint keepAddBufferBefore, uint matchMaxLen, uint keepAddBufferAfter, ISzAlloc alloc)
            {
                if (historySize > kMaxHistorySize)
                {
                    MatchFinder_Free(alloc);
                    return false;
                }

                uint sizeReserv = historySize >> 1;
                if (historySize > ((uint)2 << 30))
                    sizeReserv = historySize >> 2;
                sizeReserv += (keepAddBufferBefore + matchMaxLen + keepAddBufferAfter) / 2 + (1 << 19);

                mKeepSizeBefore = historySize + keepAddBufferBefore + 1;
                mKeepSizeAfter = matchMaxLen + keepAddBufferAfter;

                // we need one additional byte, since we use MoveBlock after pos++ and before dictionary using
                if (LzInWindow_Create(sizeReserv, alloc))
                {
                    uint newCyclicBufferSize = historySize + 1;
                    uint hs;

                    mMatchMaxLen = matchMaxLen;

                    {
                        mFixedHashSize = 0;

                        if (mNumHashBytes == 2)
                        {
                            hs = (1 << 16) - 1;
                        }
                        else
                        {
                            hs = historySize - 1;
                            hs |= (hs >> 1);
                            hs |= (hs >> 2);
                            hs |= (hs >> 4);
                            hs |= (hs >> 8);
                            hs >>= 1;
                            hs |= 0xFFFF; // don't change it! It's required for Deflate
                            if (hs > (1 << 24))
                            {
                                if (mNumHashBytes == 3)
                                    hs = (1 << 24) - 1;
                                else
                                    hs >>= 1;
                            }
                        }

                        mHashMask = hs;
                        hs++;

                        if (mNumHashBytes > 2)
                            mFixedHashSize += kHash2Size;
                        if (mNumHashBytes > 3)
                            mFixedHashSize += kHash3Size;
                        if (mNumHashBytes > 4)
                            mFixedHashSize += kHash4Size;

                        hs += mFixedHashSize;
                    }

                    {
                        uint prevSize = mHashSizeSum + mNumSons;

                        mHistorySize = historySize;
                        mHashSizeSum = hs;
                        mCyclicBufferSize = newCyclicBufferSize;
                        mNumSons = (mBtMode ? newCyclicBufferSize * 2 : newCyclicBufferSize);

                        uint newSize = mHashSizeSum + mNumSons;
                        if (mHash != null && prevSize == newSize)
                            return true;

                        MatchFinder_FreeThisClassMemory(alloc);

                        mHash = AllocRefs(newSize, alloc);
                        if (mHash != null)
                        {
                            mSon = P.From(mHash, mHashSizeSum);
                            return true;
                        }
                    }
                }

                MatchFinder_Free(alloc);
                return false;
            }

            private void MatchFinder_SetLimits()
            {
                uint limit = kMaxValForNormalize - mPos;
                uint limit2 = mCyclicBufferSize - mCyclicBufferPos;
                if (limit2 < limit)
                    limit = limit2;
                limit2 = mStreamPos - mPos;
                if (limit2 <= mKeepSizeAfter)
                {
                    if (limit2 > 0)
                        limit2 = 1;
                }
                else
                {
                    limit2 -= mKeepSizeAfter;
                }
                if (limit2 < limit)
                    limit = limit2;
                uint lenLimit = mStreamPos - mPos;
                if (lenLimit > mMatchMaxLen)
                    lenLimit = mMatchMaxLen;
                mLenLimit = lenLimit;
                mPosLimit = mPos + limit;
            }

            internal void MatchFinder_Init()
            {
                //for(uint i = 0; i < mHashSizeSum; i++)
                //    mHash[i] = kEmptyHashValue;
                Array.Clear(mHash, 0, (int)mHashSizeSum);

                mCyclicBufferPos = 0;
                mBuffer = mBufferBase;
                mPos = mCyclicBufferSize;
                mStreamPos = mCyclicBufferSize;
                mResult = SZ_OK;
                mStreamEndWasReached = false;
                MatchFinder_ReadBlock();
                MatchFinder_SetLimits();
            }

            private uint MatchFinder_GetSubValue()
            {
                return (mPos - mHistorySize - 1) & kNormalizeMask;
            }

            internal static void MatchFinder_Normalize3(uint subValue, P<uint> items, uint numItems)
            {
                for (uint i = 0; i < numItems; i++)
                {
                    uint value = items[i];
                    if (value <= subValue)
                        value = kEmptyHashValue;
                    else
                        value -= subValue;
                    items[i] = value;
                }
            }

            private void MatchFinder_Normalize()
            {
                uint subValue = MatchFinder_GetSubValue();
                MatchFinder_Normalize3(subValue, mHash, mHashSizeSum + mNumSons);
                MatchFinder_ReduceOffsets(subValue);
            }

            private void MatchFinder_CheckLimits()
            {
                if (mPos == kMaxValForNormalize)
                    MatchFinder_Normalize();

                if (!mStreamEndWasReached && mKeepSizeAfter == mStreamPos - mPos)
                    MatchFinder_CheckAndMoveAndRead();

                if (mCyclicBufferPos == mCyclicBufferSize)
                    mCyclicBufferPos = 0;

                MatchFinder_SetLimits();
            }

            private static P<uint> Hc_GetMatchesSpec(uint lenLimit, uint curMatch, uint pos, P<byte> cur, P<uint> son, uint _cyclicBufferPos, uint _cyclicBufferSize, uint cutValue, P<uint> distances, uint maxLen)
            {
                son[_cyclicBufferPos] = curMatch;

                for (;;)
                {
                    uint delta = pos - curMatch;
                    if (cutValue-- == 0 || delta >= _cyclicBufferSize)
                        return distances;

                    P<byte> pb = cur - delta;
                    curMatch = son[_cyclicBufferPos - delta + ((delta > _cyclicBufferPos) ? _cyclicBufferSize : 0)];
                    if (pb[maxLen] == cur[maxLen] && pb[0] == cur[0])
                    {
                        uint len = 0;
                        while (++len != lenLimit)
                            if (pb[len] != cur[len])
                                break;

                        if (maxLen < len)
                        {
                            distances[0] = maxLen = len;
                            distances++;
                            distances[0] = delta - 1;
                            distances++;
                            if (len == lenLimit)
                                return distances;
                        }
                    }
                }
            }

            internal static P<uint> GetMatchesSpec1(uint lenLimit, uint curMatch, uint pos, P<byte> cur, P<uint> son, uint _cyclicBufferPos, uint _cyclicBufferSize, uint cutValue, P<uint> distances, uint maxLen)
            {
                P<uint> ptr0 = son + (_cyclicBufferPos << 1) + 1;
                P<uint> ptr1 = son + (_cyclicBufferPos << 1);
                uint len0 = 0;
                uint len1 = 0;

                for (;;)
                {
                    uint delta = pos - curMatch;
                    if (cutValue-- == 0 || delta >= _cyclicBufferSize)
                    {
                        ptr0[0] = ptr1[0] = CMatchFinder.kEmptyHashValue;
                        return distances;
                    }

                    P<uint> pair = son + ((_cyclicBufferPos - delta + ((delta > _cyclicBufferPos) ? _cyclicBufferSize : 0)) << 1);
                    P<byte> pb = cur - delta;
                    uint len = (len0 < len1 ? len0 : len1);

                    if (pb[len] == cur[len])
                    {
                        if (++len != lenLimit && pb[len] == cur[len])
                            while (++len != lenLimit)
                                if (pb[len] != cur[len])
                                    break;

                        if (maxLen < len)
                        {
                            distances[0] = maxLen = len;
                            distances++;
                            distances[0] = delta - 1;
                            distances++;

                            if (len == lenLimit)
                            {
                                ptr1[0] = pair[0];
                                ptr0[0] = pair[1];
                                return distances;
                            }
                        }
                    }

                    if (pb[len] < cur[len])
                    {
                        ptr1[0] = curMatch;
                        ptr1 = pair + 1;
                        curMatch = ptr1[0];
                        len1 = len;
                    }
                    else
                    {
                        ptr0[0] = curMatch;
                        ptr0 = pair;
                        curMatch = ptr0[0];
                        len0 = len;
                    }
                }
            }

            private static void SkipMatchesSpec(uint lenLimit, uint curMatch, uint pos, P<byte> cur, P<uint> son, uint _cyclicBufferPos, uint _cyclicBufferSize, uint cutValue)
            {
                P<uint> ptr0 = son + (_cyclicBufferPos << 1) + 1;
                P<uint> ptr1 = son + (_cyclicBufferPos << 1);
                uint len0 = 0;
                uint len1 = 0;

                for (;;)
                {
                    uint delta = pos - curMatch;
                    if (cutValue-- == 0 || delta >= _cyclicBufferSize)
                    {
                        ptr0[0] = ptr1[0] = CMatchFinder.kEmptyHashValue;
                        return;
                    }

                    P<uint> pair = son + ((_cyclicBufferPos - delta + ((delta > _cyclicBufferPos) ? _cyclicBufferSize : 0)) << 1);
                    P<byte> pb = cur - delta;
                    uint len = (len0 < len1 ? len0 : len1);

                    if (pb[len] == cur[len])
                    {
                        while (++len != lenLimit)
                            if (pb[len] != cur[len])
                                break;

                        if (len == lenLimit)
                        {
                            ptr1[0] = pair[0];
                            ptr0[0] = pair[1];
                            return;
                        }
                    }

                    if (pb[len] < cur[len])
                    {
                        ptr1[0] = curMatch;
                        ptr1 = pair + 1;
                        curMatch = ptr1[0];
                        len1 = len;
                    }
                    else
                    {
                        ptr0[0] = curMatch;
                        ptr0 = pair;
                        curMatch = ptr0[0];
                        len0 = len;
                    }
                }
            }

            private void MatchFinder_MovePos()
            {
                mCyclicBufferPos++;
                mBuffer++;
                mPos++;

                if (mPos == mPosLimit)
                    MatchFinder_CheckLimits();
            }

            internal uint Bt2_MatchFinder_GetMatches(P<uint> distances)
            {
                uint lenLimit = mLenLimit;
                if (lenLimit < 2)
                {
                    MatchFinder_MovePos();
                    return 0;
                }

                P<byte> cur = mBuffer;
                uint hashValue = cur[0] | ((uint)cur[1] << 8);
                uint curMatch = mHash[hashValue];
                mHash[hashValue] = mPos;

                uint offset = 0;
                offset = (uint)(GetMatchesSpec1(lenLimit, curMatch, mPos, mBuffer, mSon, mCyclicBufferPos, mCyclicBufferSize, mCutValue, distances + offset, 1) - distances);

                mCyclicBufferPos++;
                mBuffer++;

                if (++mPos == mPosLimit)
                    MatchFinder_CheckLimits();

                return offset;
            }

            internal uint Bt3_MatchFinder_GetMatches(P<uint> distances)
            {
                uint lenLimit = mLenLimit;
                if (lenLimit < 3)
                {
                    MatchFinder_MovePos();
                    return 0;
                }

                P<byte> cur = mBuffer;

                uint temp = cur[0].CRC() ^ cur[1];
                uint hash2Value = temp & (kHash2Size - 1);
                uint hashValue = (temp ^ ((uint)cur[2] << 8)) & mHashMask;

                uint delta2 = mPos - mHash[hash2Value];
                uint curMatch = mHash[kFix3HashSize + hashValue];

                mHash[hash2Value] = mPos;
                mHash[kFix3HashSize + hashValue] = mPos;

                uint maxLen = 2;
                uint offset = 0;

                if (delta2 < mCyclicBufferSize && cur[-delta2] == cur[0])
                {
                    while (maxLen != lenLimit)
                    {
                        if (cur[maxLen - delta2] != cur[maxLen])
                            break;

                        maxLen++;
                    }

                    distances[0] = maxLen;
                    distances[1] = delta2 - 1;
                    offset = 2;

                    if (maxLen == lenLimit)
                    {
                        SkipMatchesSpec(lenLimit, curMatch, mPos, mBuffer, mSon, mCyclicBufferPos, mCyclicBufferSize, mCutValue);

                        mCyclicBufferPos++;
                        mBuffer++;

                        if (++mPos == mPosLimit)
                            MatchFinder_CheckLimits();

                        return offset;
                    }
                }

                offset = (uint)(GetMatchesSpec1(lenLimit, curMatch, mPos, mBuffer, mSon, mCyclicBufferPos, mCyclicBufferSize, mCutValue, distances + offset, maxLen) - distances);

                mCyclicBufferPos++;
                mBuffer++;

                if (++mPos == mPosLimit)
                    MatchFinder_CheckLimits();

                return offset;
            }

            internal uint Bt4_MatchFinder_GetMatches(P<uint> distances)
            {
                uint lenLimit = mLenLimit;
                if (lenLimit < 4)
                {
                    MatchFinder_MovePos();
                    return 0;
                }

                P<byte> cur = mBuffer;

                uint temp = cur[0].CRC() ^ cur[1];
                uint hash2Value = temp & (kHash2Size - 1);
                uint hash3Value = (temp ^ ((uint)cur[2] << 8)) & (kHash3Size - 1);
                uint hashValue = (temp ^ ((uint)cur[2] << 8) ^ (cur[3].CRC() << 5)) & mHashMask;

                uint delta2 = mPos - mHash[hash2Value];
                uint delta3 = mPos - mHash[kFix3HashSize + hash3Value];
                uint curMatch = mHash[kFix4HashSize + hashValue];

                mHash[hash2Value] = mPos;
                mHash[kFix3HashSize + hash3Value] = mPos;
                mHash[kFix4HashSize + hashValue] = mPos;

                uint maxLen = 1;
                uint offset = 0;

                if (delta2 < mCyclicBufferSize && cur[-delta2] == cur[0])
                {
                    distances[0] = maxLen = 2;
                    distances[1] = delta2 - 1;
                    offset = 2;
                }

                if (delta2 != delta3 && delta3 < mCyclicBufferSize && cur[-delta3] == cur[0])
                {
                    maxLen = 3;
                    distances[offset + 1] = delta3 - 1;
                    offset += 2;
                    delta2 = delta3;
                }

                if (offset != 0)
                {
                    while (maxLen != lenLimit && cur[maxLen - delta2] == cur[maxLen])
                        maxLen++;

                    distances[offset - 2] = maxLen;

                    if (maxLen == lenLimit)
                    {
                        SkipMatchesSpec(lenLimit, curMatch, mPos, mBuffer, mSon, mCyclicBufferPos, mCyclicBufferSize, mCutValue);
                        mCyclicBufferPos++;
                        mBuffer++;

                        if (++mPos == mPosLimit)
                            MatchFinder_CheckLimits();

                        return offset;
                    }
                }

                if (maxLen < 3)
                    maxLen = 3;

                offset = (uint)(GetMatchesSpec1(lenLimit, curMatch, mPos, mBuffer, mSon, mCyclicBufferPos, mCyclicBufferSize, mCutValue, distances + offset, maxLen) - distances);

                mCyclicBufferPos++;
                mBuffer++;

                if (++mPos == mPosLimit)
                    MatchFinder_CheckLimits();

                return offset;
            }

            internal uint Hc4_MatchFinder_GetMatches(P<uint> distances)
            {
                uint lenLimit = mLenLimit;
                if (lenLimit < 4)
                {
                    MatchFinder_MovePos();
                    return 0;
                }

                P<byte> cur = mBuffer;

                uint temp = cur[0].CRC() ^ cur[1];
                uint hash2Value = temp & (kHash2Size - 1);
                uint hash3Value = (temp ^ ((uint)cur[2] << 8)) & (kHash3Size - 1);
                uint hashValue = (temp ^ ((uint)cur[2] << 8) ^ (cur[3].CRC() << 5)) & mHashMask;

                uint delta2 = mPos - mHash[hash2Value];
                uint delta3 = mPos - mHash[kFix3HashSize + hash3Value];
                uint curMatch = mHash[kFix4HashSize + hashValue];

                mHash[hash2Value] = mPos;
                mHash[kFix3HashSize + hash3Value] = mPos;
                mHash[kFix4HashSize + hashValue] = mPos;

                uint maxLen = 1;
                uint offset = 0;

                if (delta2 < mCyclicBufferSize && cur[-delta2] == cur[0])
                {
                    TR("Hc4_MatchFinder_GetMatches:a1", maxLen);
                    TR("Hc4_MatchFinder_GetMatches:a2", delta2);
                    distances[0] = maxLen = 2;
                    distances[1] = delta2 - 1;
                    offset = 2;
                }

                if (delta2 != delta3 && delta3 < mCyclicBufferSize && cur[-delta3] == cur[0])
                {
                    TR("Hc4_MatchFinder_GetMatches:b1", offset);
                    TR("Hc4_MatchFinder_GetMatches:b2", delta3);
                    maxLen = 3;
                    distances[offset + 1] = delta3 - 1;
                    offset += 2;
                    delta2 = delta3;
                }

                if (offset != 0)
                {
                    while (maxLen != lenLimit && cur[maxLen - delta2] == cur[maxLen])
                        maxLen++;

                    TR("Hc4_MatchFinder_GetMatches:c1", offset);
                    TR("Hc4_MatchFinder_GetMatches:c2", maxLen);

                    distances[offset - 2] = maxLen;

                    if (maxLen == lenLimit)
                    {
                        TR("Hc4_MatchFinder_GetMatches:d", curMatch);
                        mSon[mCyclicBufferPos] = curMatch;
                        mCyclicBufferPos++;
                        mBuffer++;
                        mPos++;
                        if (mPos == mPosLimit)
                            MatchFinder_CheckLimits();
                        return offset;
                    }
                }

                if (maxLen < 3)
                    maxLen = 3;

                offset = (uint)(Hc_GetMatchesSpec(lenLimit, curMatch, mPos, mBuffer, mSon, mCyclicBufferPos, mCyclicBufferSize, mCutValue, distances + offset, maxLen) - distances);

                mCyclicBufferPos++;
                mBuffer++;
                mPos++;

                if (mPos == mPosLimit)
                    MatchFinder_CheckLimits();

                return offset;
            }

            internal void Bt2_MatchFinder_Skip(uint num)
            {
                do
                {
                    uint lenLimit = mLenLimit;
                    if (lenLimit < 2)
                    {
                        MatchFinder_MovePos();
                        continue;
                    }

                    P<byte> cur = mBuffer;
                    uint hashValue = cur[0] | ((uint)cur[1] << 8);

                    uint curMatch = mHash[hashValue];

                    mHash[hashValue] = mPos;

                    SkipMatchesSpec(lenLimit, curMatch, mPos, mBuffer, mSon, mCyclicBufferPos, mCyclicBufferSize, mCutValue);

                    mCyclicBufferPos++;
                    mBuffer++;

                    if (++mPos == mPosLimit)
                        MatchFinder_CheckLimits();
                }
                while (--num != 0);
            }

            internal void Bt3_MatchFinder_Skip(uint num)
            {
                do
                {
                    uint lenLimit = mLenLimit;
                    if (lenLimit < 3)
                    {
                        MatchFinder_MovePos();
                        continue;
                    }

                    P<byte> cur = mBuffer;
                    uint temp = cur[0].CRC() ^ cur[1];
                    uint hash2Value = temp & (kHash2Size - 1);
                    uint hashValue = (temp ^ ((uint)cur[2] << 8)) & mHashMask;

                    uint curMatch = mHash[kFix3HashSize + hashValue];

                    mHash[hash2Value] = mPos;
                    mHash[kFix3HashSize + hashValue] = mPos;

                    SkipMatchesSpec(lenLimit, curMatch, mPos, mBuffer, mSon, mCyclicBufferPos, mCyclicBufferSize, mCutValue);

                    mCyclicBufferPos++;
                    mBuffer++;

                    if (++mPos == mPosLimit)
                        MatchFinder_CheckLimits();
                }
                while (--num != 0);
            }

            internal void Bt4_MatchFinder_Skip(uint num)
            {
                do
                {
                    uint lenLimit = mLenLimit;
                    if (lenLimit < 4)
                    {
                        MatchFinder_MovePos();
                        continue;
                    }

                    P<byte> cur = mBuffer;
                    uint temp = cur[0].CRC() ^ cur[1];
                    uint hash2Value = temp & (kHash2Size - 1);
                    uint hash3Value = (temp ^ ((uint)cur[2] << 8)) & (kHash3Size - 1);
                    uint hashValue = (temp ^ ((uint)cur[2] << 8) ^ (cur[3].CRC() << 5)) & mHashMask;

                    uint curMatch = mHash[kFix4HashSize + hashValue];

                    mHash[hash2Value] = mPos;
                    mHash[kFix3HashSize + hash3Value] = mPos;
                    mHash[kFix4HashSize + hashValue] = mPos;

                    SkipMatchesSpec(lenLimit, curMatch, mPos, mBuffer, mSon, mCyclicBufferPos, mCyclicBufferSize, mCutValue);

                    mCyclicBufferPos++;
                    mBuffer++;

                    if (++mPos == mPosLimit)
                        MatchFinder_CheckLimits();
                }
                while (--num != 0);
            }

            internal void Hc4_MatchFinder_Skip(uint num)
            {
                do
                {
                    uint lenLimit = mLenLimit;
                    if (lenLimit < 4)
                    {
                        MatchFinder_MovePos();
                        continue;
                    }

                    P<byte> cur = mBuffer;
                    uint temp = cur[0].CRC() ^ cur[1];
                    uint hash2Value = temp & (kHash2Size - 1);
                    uint hash3Value = (temp ^ ((uint)cur[2] << 8)) & (kHash3Size - 1);
                    uint hashValue = (temp ^ ((uint)cur[2] << 8) ^ (cur[3].CRC() << 5)) & mHashMask;

                    uint curMatch = mHash[kFix4HashSize + hashValue];

                    mHash[hash2Value] = mPos;
                    mHash[kFix3HashSize + hash3Value] = mPos;
                    mHash[kFix4HashSize + hashValue] = mPos;

                    mSon[mCyclicBufferPos] = curMatch;

                    mCyclicBufferPos++;
                    mBuffer++;

                    if (++mPos == mPosLimit)
                        MatchFinder_CheckLimits();
                }
                while (--num != 0);
            }
        }

        // Conditions:
        //    GetNumAvailableBytes must be called before each GetMatchLen.
        //    GetPointerToCurrentPos result must be used only before any other function

        internal interface IMatchFinder
        {
            void Init(object p);
            byte GetIndexByte(object p, Int32 index);
            uint GetNumAvailableBytes(object p);
            P<byte> GetPointerToCurrentPos(object p);
            uint GetMatches(object p, P<uint> distances);
            void Skip(object p, uint num);
        }

        internal static void MatchFinder_CreateVTable(CMatchFinder p, out IMatchFinder vTable)
        {
            TR("MatchFinder_CreateVTable", p.mNumHashBytes);
            if (!p.mBtMode)
                vTable = new MatchFinderHc4();
            else if (p.mNumHashBytes == 2)
                vTable = new MatchFinderBt2();
            else if (p.mNumHashBytes == 3)
                vTable = new MatchFinderBt3();
            else
                vTable = new MatchFinderBt4();
        }

        private abstract class MatchFinderBase : IMatchFinder
        {
            public void Init(object p)
            {
                ((CMatchFinder)p).MatchFinder_Init();
            }

            public byte GetIndexByte(object p, int index)
            {
                return ((CMatchFinder)p).MatchFinder_GetIndexByte(index);
            }

            public uint GetNumAvailableBytes(object p)
            {
                return ((CMatchFinder)p).MatchFinder_GetNumAvailableBytes();
            }

            public P<byte> GetPointerToCurrentPos(object p)
            {
                return ((CMatchFinder)p).MatchFinder_GetPointerToCurrentPos();
            }

            public abstract uint GetMatches(object p, P<uint> distances);
            public abstract void Skip(object p, uint num);
        }

        private sealed class MatchFinderHc4 : MatchFinderBase
        {
            public override uint GetMatches(object p, P<uint> distances)
            {
                return ((CMatchFinder)p).Hc4_MatchFinder_GetMatches(distances);
            }

            public override void Skip(object p, uint num)
            {
                ((CMatchFinder)p).Hc4_MatchFinder_Skip(num);
            }
        }

        private sealed class MatchFinderBt2 : MatchFinderBase
        {
            public override uint GetMatches(object p, P<uint> distances)
            {
                return ((CMatchFinder)p).Bt2_MatchFinder_GetMatches(distances);
            }

            public override void Skip(object p, uint num)
            {
                ((CMatchFinder)p).Bt2_MatchFinder_Skip(num);
            }
        }

        private sealed class MatchFinderBt3 : MatchFinderBase
        {
            public override uint GetMatches(object p, P<uint> distances)
            {
                return ((CMatchFinder)p).Bt3_MatchFinder_GetMatches(distances);
            }

            public override void Skip(object p, uint num)
            {
                ((CMatchFinder)p).Bt3_MatchFinder_Skip(num);
            }
        }

        private sealed class MatchFinderBt4 : MatchFinderBase
        {
            public override uint GetMatches(object p, P<uint> distances)
            {
                return ((CMatchFinder)p).Bt4_MatchFinder_GetMatches(distances);
            }

            public override void Skip(object p, uint num)
            {
                ((CMatchFinder)p).Bt4_MatchFinder_Skip(num);
            }
        }
    }
}
