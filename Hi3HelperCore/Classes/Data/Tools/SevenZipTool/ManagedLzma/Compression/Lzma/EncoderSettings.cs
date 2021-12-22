using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagedLzma.LZMA
{
    public sealed class EncoderSettings
    {
        private long mReduceSize;
        private int mDictionarySize;
        private int mLC;
        private int mLP;
        private int mPB;
        private int mFB;
        private int mHashBytes;
        private int mMC;
        private bool mFastMode;
        private bool mBinaryTreeMode;
        private bool mMultiThreaded;
        private bool mWriteEndMark;

        public EncoderSettings()
        {
            mReduceSize = Int64.MaxValue;
            mWriteEndMark = false;
            SetLevel(5);
        }

        public void SetLevel(int level)
        {
            if (level < 0 || level > 9)
                throw new ArgumentOutOfRangeException(nameof(level));

            if (level <= 5)
                mDictionarySize = (1 << (level * 2 + 14));
            else if (level == 6)
                mDictionarySize = (1 << 25);
            else
                mDictionarySize = (1 << 26);

            mLC = 3;
            mLP = 0;
            mPB = 2;
            mFastMode = (level <= 4);
            mFB = (level <= 6) ? 32 : 64;
            mBinaryTreeMode = !mFastMode;
            mHashBytes = 4;

            if (level <= 4)
                mMC = 16;
            else if (level <= 6)
                mMC = 32;
            else
                mMC = 48;

            mMultiThreaded = mBinaryTreeMode && !mFastMode;
        }

        public long ReduceSize
        {
            get { return mReduceSize; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));

                mReduceSize = value;
            }
        }

        public int DictionarySize
        {
            get { return mDictionarySize; }
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value));

                mDictionarySize = value;
            }
        }

        public int LC
        {
            get { return mLC; }
            set
            {
                if (value < 0 || value > 8)
                    throw new ArgumentOutOfRangeException(nameof(value));

                mLC = value;
            }
        }

        public int LP
        {
            get { return mLP; }
            set
            {
                if (value < 0 || value > 4)
                    throw new ArgumentOutOfRangeException(nameof(value));

                mLP = value;
            }
        }

        public int PB
        {
            get { return mPB; }
            set
            {
                if (value < 0 || value > 4)
                    throw new ArgumentOutOfRangeException(nameof(value));

                mPB = value;
            }
        }

        public int FB
        {
            get { return mFB; }
            set
            {
                if (value < 5 || value > 273)
                    throw new ArgumentOutOfRangeException(nameof(value));

                mFB = value;
            }
        }

        public int MC
        {
            get { return mMC; }
            set
            {
                if (value < 1 || value > (1 << 30))
                    throw new ArgumentOutOfRangeException(nameof(value));

                mMC = value;
            }
        }

        public int HashBytes
        {
            get { return mHashBytes; }
            set
            {
                if (value < 2 || value > 4)
                    throw new ArgumentOutOfRangeException(nameof(value));

                mHashBytes = value;
            }
        }

        public bool FastMode
        {
            get { return mFastMode; }
            set { mFastMode = value; }
        }

        public bool BinaryTreeMode
        {
            get { return mBinaryTreeMode; }
            set { mBinaryTreeMode = value; }
        }

        public bool MultiThreaded
        {
            get { return mMultiThreaded; }
            set { mMultiThreaded = value; }
        }

        public bool WriteEndMark
        {
            get { return mWriteEndMark; }
            set { mWriteEndMark = value; ; }
        }

        public DecoderSettings GetDecoderSettings()
        {
            return new DecoderSettings((uint)mDictionarySize, (byte)mLC, (byte)mPB, (byte)mLP);
        }

        internal Master.LZMA.CLzmaEncProps GetInternalSettings()
        {
            var settings = Master.LZMA.CLzmaEncProps.LzmaEncProps_Init();
            settings.mDictSize = (uint)mDictionarySize;
            settings.mReduceSize = mReduceSize > UInt32.MaxValue ? UInt32.MaxValue : (uint)mReduceSize;
            settings.mLC = mLC;
            settings.mLP = mLP;
            settings.mPB = mPB;
            settings.mAlgo = mFastMode ? 0 : 1;
            settings.mFB = mFB;
            settings.mBtMode = mBinaryTreeMode ? 1 : 0;
            settings.mNumHashBytes = mHashBytes;
            settings.mMC = (uint)mMC;
            settings.mWriteEndMark = mWriteEndMark ? 1u : 0u;
            settings.mNumThreads = mMultiThreaded ? 2 : 1;
            return settings;
        }

        private static void LzmaEnc_WriteProperties(Master.LZMA.CLzmaEncProps settings, ImmutableArray<byte>.Builder props)
        {
            settings.LzmaEncProps_Normalize();

            uint dictSize = settings.mDictSize;
            props.Add((byte)((settings.mPB * 5 + settings.mLP) * 9 + settings.mLC));

            for (int i = 11; i <= 30; i++)
            {
                if (dictSize <= (2u << i))
                {
                    dictSize = (2u << i);
                    break;
                }
                if (dictSize <= (3u << i))
                {
                    dictSize = (3u << i);
                    break;
                }
            }

            for (int i = 0; i < 4; i++)
                props.Add((byte)(dictSize >> (8 * i)));
        }

        internal ImmutableArray<byte> GetSerializedSettings()
        {
            var props = ImmutableArray.CreateBuilder<byte>(5);
            LzmaEnc_WriteProperties(GetInternalSettings(), props);
            return props.MoveToImmutable();
        }
    }
}
