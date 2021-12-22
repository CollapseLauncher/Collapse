using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if BUILD_TESTING
namespace ManagedLzma.Testing
{
#if !BUILD_PORTABLE
    [Serializable]
#endif
    public struct PZ
    {
        public byte[] Buffer;
        public int Offset;
        public int Length;

        public PZ(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            this.Buffer = buffer;
            this.Offset = 0;
            this.Length = buffer.Length;
        }

        public PZ(byte[] buffer, int offset)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException("offset");

            this.Buffer = buffer;
            this.Offset = offset;
            this.Length = buffer.Length - offset;
        }

        public PZ(byte[] buffer, int offset, int length)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException("offset");

            if (length < 0 || length > buffer.Length - offset)
                throw new ArgumentOutOfRangeException("length");

            this.Buffer = buffer;
            this.Offset = offset;
            this.Length = length;
        }

        public byte this[int index]
        {
            get
            {
                if (index < 0 || index >= Length)
                    throw new ArgumentOutOfRangeException("index");

                return Buffer[Offset + index];
            }
            set
            {
                if (index < 0 || index >= Length)
                    throw new ArgumentOutOfRangeException("index");

                Buffer[Offset + index] = value;
            }
        }
    }

    public interface IHelper : IDisposable
    {
        void LzmaCompress(SharedSettings settings);
        void LzmaUncompress(SharedSettings settings);
    }

#if !BUILD_PORTABLE
    [Serializable]
#endif
    public class SharedSettings
    {
        public int Variant;
        public bool UseV2;

        public SharedSettings() { }
        public SharedSettings(SharedSettings other)
        {
            this.Variant = other.Variant;
            this.UseV2 = other.UseV2;

            this.Dst = other.Dst;
            this.Src = other.Src;
            this.Enc = other.Enc;
            this.UsedSize = other.UsedSize;
            this.WrittenSize = other.WrittenSize;

            this.Level = other.Level;
            this.DictSize = other.DictSize;
            this.LC = other.LC;
            this.LP = other.LP;
            this.PB = other.PB;
            this.Algo = other.Algo;
            this.FB = other.FB;
            this.BTMode = other.BTMode;
            this.NumHashBytes = other.NumHashBytes;
            this.MC = other.MC;
            this.WriteEndMark = other.WriteEndMark;
            this.NumThreads = other.NumThreads;

            this.BlockSize = other.BlockSize;
            this.NumBlockThreads = other.NumBlockThreads;
            this.NumTotalThreads = other.NumTotalThreads;
        }

        #region Data

        public PZ Dst;
        public PZ Src;
        public PZ Enc;
        public int UsedSize;
        public int WrittenSize;

        #endregion

        #region V1

        /// <summary>
        /// 0 &lt;= level &lt;= 9
        /// </summary>
        public int? Level;
        public int ActualLevel { get { return Level ?? 5; } }

        /// <summary>
        /// (1 &lt;&lt; 12) &lt;= dictSize &lt;= (1 &lt;&lt; 27) for 32-bit version
        /// (1 &lt;&lt; 12) &lt;= dictSize &lt;= (1 &lt;&lt; 30) for 64-bit version
        /// default = (1 &lt;&lt; 24)
        /// </summary>
        public uint? DictSize;
        public uint ActualDictSize { get { return DictSize ?? 0; } }

        /// <summary>
        /// 0 &lt;= lc &lt;= 8, default = 3
        /// </summary>
        public int? LC;
        public int ActualLC { get { return LC ?? -1; } }

        /// <summary>
        /// 0 &lt;= lp &lt;= 4, default = 0
        /// </summary>
        public int? LP;
        public int ActualLP { get { return LP ?? -1; } }

        /// <summary>
        /// 0 &lt;= pb &lt;= 4, default = 2
        /// </summary>
        public int? PB;
        public int ActualPB { get { return PB ?? -1; } }

        /// <summary>
        /// 0 - fast, 1 - normal, default = 1
        /// </summary>
        public int? Algo;
        public int ActualAlgo { get { return Algo ?? -1; } }

        /// <summary>
        /// 5 &lt;= fb &lt;= 273, default = 32
        /// </summary>
        public int? FB;
        public int ActualFB { get { return FB ?? -1; } }

        /// <summary>
        /// 0 - hashChain Mode, 1 - binTree mode - normal, default = 1
        /// </summary>
        public int? BTMode;
        public int ActualBTMode { get { return BTMode ?? -1; } }

        /// <summary>
        /// 2, 3 or 4, default = 4
        /// </summary>
        public int? NumHashBytes;
        public int ActualNumHashBytes { get { return NumHashBytes ?? -1; } }

        /// <summary>
        /// 1 &lt;= mc &lt;= (1 &lt;&lt; 30), default = 32
        /// </summary>
        public uint? MC;
        public uint ActualMC { get { return MC ?? 0; } }

        /// <summary>
        /// 0 - do not write EOPM, 1 - write EOPM, default = 0
        /// </summary>
        public uint? WriteEndMark;
        public uint ActualWriteEndMark { get { return WriteEndMark ?? 0; } }

        /// <summary>
        /// 1 or 2, default = 2
        /// </summary>
        public int? NumThreads;
        public int ActualNumThreads { get { return NumThreads ?? -1; } }

        #endregion

        #region V2

        public int? BlockSize;
        public int ActualBlockSize { get { return BlockSize ?? 0; } }

        public int? NumBlockThreads;
        public int ActualNumBlockThreads { get { return NumBlockThreads ?? -1; } }

        public int? NumTotalThreads;
        public int ActualNumTotalThreads { get { return NumTotalThreads ?? -1; } }

        #endregion
    }
}
#endif
