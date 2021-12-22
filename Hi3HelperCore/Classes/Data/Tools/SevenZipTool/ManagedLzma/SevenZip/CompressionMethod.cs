using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagedLzma.SevenZip.Metadata
{
    /// <summary>
    /// Describes a compression method as defined in the 7z file format.
    /// </summary>
    /// <remarks>
    /// These methods are defined by the 7z file format and not by the application.
    /// You cannot add new compression methods here, nobody will understand them.
    /// </remarks>
    public struct CompressionMethod : IEquatable<CompressionMethod>
    {
        private static ImmutableArray<byte> SignatureCopy => ImmutableArray.Create<byte>(0x00);
        private static ImmutableArray<byte> SignatureDelta => ImmutableArray.Create<byte>(0x03);
        private static ImmutableArray<byte> SignatureLZMA2 => ImmutableArray.Create<byte>(0x21);
        private static ImmutableArray<byte> SignatureLZMA => ImmutableArray.Create<byte>(0x03, 0x01, 0x01);
        private static ImmutableArray<byte> SignaturePPMD => ImmutableArray.Create<byte>(0x03, 0x04, 0x01);
        private static ImmutableArray<byte> SignatureBCJ => ImmutableArray.Create<byte>(0x03, 0x03, 0x01, 0x03);
        private static ImmutableArray<byte> SignatureBCJ2 => ImmutableArray.Create<byte>(0x03, 0x03, 0x01, 0x1B);
        private static ImmutableArray<byte> SignatureDeflate => ImmutableArray.Create<byte>(0x04, 0x01, 0x08);
        private static ImmutableArray<byte> SignatureBZip2 => ImmutableArray.Create<byte>(0x04, 0x02, 0x02);
        private static ImmutableArray<byte> SignatureAES => ImmutableArray.Create<byte>(0x06, 0xF1, 0x07, 0x01);

        private const int kCopy = 0x00;
        private const int kDelta = 0x03;
        private const int kLZMA2 = 0x21;
        private const int kLZMA = 0x030101;
        private const int kPPMD = 0x030401;
        private const int kBCJ = 0x03030103;
        private const int kBCJ2 = 0x0303011B;
        private const int kDeflate = 0x040108;
        private const int kBZip2 = 0x040202;
        private const int kAES = 0x06F10701;

        public static CompressionMethod Undefined => default(CompressionMethod);
        public static CompressionMethod Copy => new CompressionMethod(kCopy);
        public static CompressionMethod Delta => new CompressionMethod(kDelta);
        public static CompressionMethod LZMA2 => new CompressionMethod(kLZMA2);
        public static CompressionMethod LZMA => new CompressionMethod(kLZMA);
        public static CompressionMethod PPMD => new CompressionMethod(kPPMD);
        public static CompressionMethod BCJ => new CompressionMethod(kBCJ);
        public static CompressionMethod BCJ2 => new CompressionMethod(kBCJ2);
        public static CompressionMethod Deflate => new CompressionMethod(kDeflate);
        public static CompressionMethod BZip2 => new CompressionMethod(kBZip2);
        public static CompressionMethod AES => new CompressionMethod(kAES);

        #region Internal Methods

        internal static CompressionMethod TryDecode(int value)
        {
            switch (value)
            {
                case kCopy:
                case kDelta:
                case kLZMA2:
                case kLZMA:
                case kPPMD:
                case kBCJ:
                case kBCJ2:
                case kDeflate:
                case kBZip2:
                case kAES:
                    return new CompressionMethod(value);

                default:
                    return Undefined;
            }
        }

        internal ImmutableArray<byte> Encode()
        {
            switch (~mSignature)
            {
                case kCopy: return SignatureCopy;
                case kDelta: return SignatureDelta;
                case kLZMA2: return SignatureLZMA2;
                case kLZMA: return SignatureLZMA;
                case kPPMD: return SignaturePPMD;
                case kBCJ: return SignatureBCJ;
                case kBCJ2: return SignatureBCJ2;
                case kDeflate: return SignatureDeflate;
                case kBZip2: return SignatureBZip2;
                case kAES: return SignatureAES;
                default: throw new InvalidOperationException();
            }
        }

        internal void CheckInputOutputCount(int inputCount, int outputCount)
        {
            switch (~mSignature)
            {
                case kCopy:
                case kDeflate:
                case kLZMA:
                case kLZMA2:
                case kAES:
                case kBCJ:
                case kPPMD:
                    if (inputCount != 1)
                        throw new InvalidDataException();

                    if (outputCount != 1)
                        throw new InvalidDataException();

                    break;

                case kBCJ2:
                    if (inputCount != 4)
                        throw new InvalidDataException();

                    if (outputCount != 1)
                        throw new InvalidDataException();

                    break;

                case kDelta:
                case kBZip2:
                    throw new NotImplementedException();

                default:
                    throw new InvalidDataException();
            }
        }

        internal int GetInputCount()
        {
            switch (~mSignature)
            {
                case kCopy:
                case kDeflate:
                case kLZMA:
                case kLZMA2:
                case kAES:
                case kBCJ:
                case kPPMD:
                    return 1;

                case kBCJ2:
                    return 4;

                case kDelta:
                case kBZip2:
                    throw new NotImplementedException();

                default:
                    throw new InternalFailureException();
            }
        }

        internal int GetOutputCount()
        {
            switch (~mSignature)
            {
                case kCopy:
                case kDeflate:
                case kLZMA:
                case kLZMA2:
                case kAES:
                case kBCJ:
                case kBCJ2:
                case kPPMD:
                    return 1;

                case kDelta:
                case kBZip2:
                    throw new NotImplementedException();

                default:
                    throw new InvalidOperationException();
            }
        }

        internal Reader.DecoderNode CreateDecoder(ImmutableArray<byte> settings, ImmutableArray<Metadata.DecoderOutputMetadata> output, PasswordStorage password)
        {
            switch (~mSignature)
            {
                case kCopy: return new Reader.CopyArchiveDecoder(settings, output.Single().Length);
                case kLZMA: return new Reader.LzmaArchiveDecoder(settings, output.Single().Length);
                case kLZMA2: return new Reader.Lzma2ArchiveDecoder(settings, output.Single().Length);
                case kAES: return new Reader.AesArchiveDecoder(settings, password, output.Single().Length);
                case kBCJ: return new Reader.BcjArchiveDecoder(settings, output.Single().Length);
                case kBCJ2: return new Reader.Bcj2ArchiveDecoder(settings, output.Single().Length);
                case kPPMD: return new Reader.PpmdArchiveDecoder(settings, output.Single().Length);

                case kDeflate:
                case kDelta:
                case kBZip2:
                    throw new NotImplementedException();

                default:
                    throw new InvalidDataException();
            }
        }

        #endregion

        private int mSignature;

        private CompressionMethod(int signature)
        {
            mSignature = ~signature;
        }

        public bool IsUndefined => mSignature == 0;

        public override string ToString()
        {
            switch (~mSignature)
            {
                case kCopy: return nameof(Copy);
                case kDelta: return nameof(Delta);
                case kLZMA2: return nameof(LZMA2);
                case kLZMA: return nameof(LZMA);
                case kPPMD: return nameof(PPMD);
                case kBCJ: return nameof(BCJ);
                case kBCJ2: return nameof(BCJ2);
                case kDeflate: return nameof(Deflate);
                case kBZip2: return nameof(BZip2);
                case kAES: return nameof(AES);
                default: return nameof(Undefined);
            }
        }

        public override int GetHashCode()
        {
            return mSignature;
        }

        public override bool Equals(object obj)
        {
            return obj is CompressionMethod && ((CompressionMethod)obj).mSignature == mSignature;
        }

        public bool Equals(CompressionMethod other)
        {
            return mSignature == other.mSignature;
        }

        public static bool operator ==(CompressionMethod left, CompressionMethod right)
        {
            return left.mSignature == right.mSignature;
        }

        public static bool operator !=(CompressionMethod left, CompressionMethod right)
        {
            return left.mSignature != right.mSignature;
        }
    }
}
