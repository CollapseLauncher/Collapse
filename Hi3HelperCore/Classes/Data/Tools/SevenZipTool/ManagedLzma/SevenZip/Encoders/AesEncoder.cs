using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ManagedLzma.SevenZip.Metadata;

// NOTE: The provided settings match the hardcoded 7z922 settings, which make some questionable choices:
//
// - The password salt is not used in the 7z encoder. Should not be a problem unless you create a huge
//   amount of 7z archives all using the same password.
//
// - The RNG for the seed (aka IV) has some questionable choices for seeding itself, but it does
//   include timing variations over a loop of 1k iterations, so it is not completely predictable.
//
// - The seed (aka IV) uses only 8 of 16 bytes, meaning 50% are zeroes. Since we are operating in CBC
//   mode the seed is XORed with the first block of the payload; since half of it is zero this means
//   that bytes 9-16 are not "protected" by a seed. While this does not make bruteforcing the password
//   any easier it may cause problems in AES security, but I'm no expert here.
//
// - Setting the NumCyclesPower to a fixed value is ok, raising it just makes brute forcing the
//   password a bit more expensive, but doing half a million SHA1 iterations is reasonably slow.
//
// So while some choices are questionable they still don't allow any obvious attack. If you are going
// to store highly sensitive material you probably shouldn't rely on 7z encryption alone anyways.
//

namespace ManagedLzma.SevenZip.Writer
{
    public sealed class AesEncoderSeed : IDisposable
    {
        public static AesEncoderSeed CreateRandom()
        {
            var salt = new byte[0]; // 7z922 does not use a salt (?)
            var seed = new byte[8]; // 7z922 uses only 8 byte seeds (?)

            // 7z922 uses a questionable RNG hack, we will just use the standard .NET cryptography RNG
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
                rng.GetBytes(seed);
            }

            return new AesEncoderSeed(salt, seed);
        }

        internal byte[] mSalt;
        internal byte[] mSeed;
        internal byte[] mSeed16;

        private AesEncoderSeed(byte[] salt, byte[] seed)
        {
            mSalt = salt;
            mSeed = seed;
            mSeed16 = new byte[16];
            Buffer.BlockCopy(seed, 0, mSeed16, 0, seed.Length);
        }

        public void Dispose()
        {
            Utilities.ClearBuffer(ref mSalt);
            Utilities.ClearBuffer(ref mSeed);
            Utilities.ClearBuffer(ref mSeed16);
        }
    }

    public sealed class AesEncoderSettings : EncoderSettings
    {
        private readonly PasswordStorage mPassword;
        private readonly AesEncoderSeed mSeed;
        private readonly int mSlowdown;

        /// <summary>
        /// Creates encoder settings with a random seed.
        /// </summary>
        /// <remarks>
        /// This overload does not dispose the encoder seed. This is no security flaw because the
        /// seed is public anyways. If you still want to ensure there are no traces in memory when
        /// you are done, use the other overload which gives you control over disposing the seed.
        /// </remarks>
        public AesEncoderSettings(PasswordStorage password)
            : this(password, AesEncoderSeed.CreateRandom())
        {
        }

        public AesEncoderSettings(PasswordStorage password, AesEncoderSeed seed)
        {
            mPassword = password;
            mSeed = seed;
            mSlowdown = 19; // 7z922 has this parameter fixed
        }

        internal override CompressionMethod GetDecoderType()
        {
            return CompressionMethod.AES;
        }

        internal override ImmutableArray<byte> SerializeSettings()
        {
            var salt = mSeed.mSalt;
            var saltSize = salt.Length;
            while (saltSize > 0 && salt[saltSize - 1] == 0)
                saltSize -= 1;

            var seed = mSeed.mSeed;
            var seedSize = seed.Length;
            while (seedSize > 0 && seed[seedSize - 1] == 0)
                seedSize -= 1;

            if (saltSize == 0 && seedSize == 0)
                return ImmutableArray.Create((byte)mSlowdown);

            var buffer = ImmutableArray.CreateBuilder<byte>(2 + saltSize + seedSize);

            var flags = (byte)mSlowdown;
            if (saltSize > 0) flags |= 0x80;
            if (seedSize > 0) flags |= 0x40;
            buffer.Add(flags);

            var encodedSaltSize = saltSize == 0 ? 0 : saltSize - 1;
            var encodedSeedSize = seedSize == 0 ? 0 : seedSize - 1;
            buffer.Add((byte)((encodedSaltSize << 4) | encodedSeedSize));
            buffer.AddRange(salt, saltSize);
            buffer.AddRange(seed, seedSize);

            return buffer.MoveToImmutable();
        }

        internal override EncoderNode CreateEncoder()
        {
            var passwordAccess = default(PasswordAccessor);
            var encryptionKey = default(byte[]);
            var passwordBytes = default(byte[]);
            try
            {
                passwordAccess = mPassword.GetPassword();
                passwordBytes = Encoding.Unicode.GetBytes(passwordAccess);
                encryptionKey = Reader.AesArchiveDecoder.InitKey(mSlowdown, mSeed.mSalt, passwordBytes);

                using (var aes = System.Security.Cryptography.Aes.Create())
                {
                    aes.Mode = System.Security.Cryptography.CipherMode.CBC;
                    aes.Padding = System.Security.Cryptography.PaddingMode.None;
                    return new AesEncoderNode(aes.CreateEncryptor(encryptionKey, mSeed.mSeed16));
                }
            }
            finally
            {
                passwordAccess.Dispose();
                Utilities.ClearBuffer(ref passwordBytes);
                Utilities.ClearBuffer(ref encryptionKey);
            }
        }
    }

    internal sealed class AesEncoderNode : EncoderNode
    {
        private sealed class EncryptionStream : IStreamWriter
        {
            private AesEncoderNode mNode;
            private IStreamWriter mOutput;
            private System.Security.Cryptography.ICryptoTransform mEncoder;
            private byte[] mBuffer1;
            private byte[] mBuffer2;
            private int mOffset;

            public EncryptionStream(AesEncoderNode node, System.Security.Cryptography.ICryptoTransform encoder)
            {
                mNode = node;
                mEncoder = encoder;
                mBuffer1 = new byte[16];
                mBuffer2 = new byte[16];
            }

            public void SetOutputSink(IStreamWriter output)
            {
                System.Diagnostics.Debug.Assert(mOutput == null && output != null);
                mOutput = output;
            }

            public async Task<int> WriteAsync(byte[] buffer, int offset, int length, StreamMode mode)
            {
                Utilities.DebugCheckStreamArguments(buffer, offset, length, mode);

                int result = 0;
                for (;;)
                {
                    if (mOffset == 16)
                    {
                        mEncoder.TransformBlock(mBuffer1, 0, 16, mBuffer2, 0);
                        var written = await mOutput.WriteAsync(mBuffer2, 0, 16, StreamMode.Complete).ConfigureAwait(false);
                        System.Diagnostics.Debug.Assert(written == 16);
                        mOffset = 0;
                    }

                    int copy = Math.Min(16 - mOffset, length);
                    System.Diagnostics.Debug.Assert(copy > 0);
                    Buffer.BlockCopy(buffer, offset, mBuffer1, mOffset, copy);
                    mOffset += copy;
                    result += copy;
                    offset += copy;
                    length -= copy;

                    if (length == 0 || mode == StreamMode.Partial)
                        return result;
                }
            }

            public async Task CompleteAsync()
            {
                if (mOffset != 0)
                {
                    while (mOffset < 16)
                        mBuffer1[mOffset++] = 0;

                    System.Diagnostics.Debug.Assert(mOffset == 16);
                    mBuffer2 = mEncoder.TransformFinalBlock(mBuffer1, 0, 16);
                    var written = await mOutput.WriteAsync(mBuffer2, 0, 16, StreamMode.Complete).ConfigureAwait(false);
                    System.Diagnostics.Debug.Assert(written == 16);
                    mOffset = 0;
                }

                System.Diagnostics.Debug.Assert(mOffset == 0);
                await mOutput.CompleteAsync().ConfigureAwait(false);
            }

            public void DisposeInternal()
            {
                mEncoder.Dispose();
            }
        }

        private EncryptionStream mStream;

        public AesEncoderNode(System.Security.Cryptography.ICryptoTransform encoder)
        {
            mStream = new EncryptionStream(this, encoder);
        }

        public override IStreamWriter GetInputSink(int index)
        {
            if (index != 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            return mStream;
        }

        public override void SetInputSource(int index, IStreamReader stream)
        {
            throw new InternalFailureException();
        }

        public override IStreamReader GetOutputSource(int index)
        {
            if (index != 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            return null;
        }

        public override void SetOutputSink(int index, IStreamWriter stream)
        {
            if (index != 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            mStream.SetOutputSink(stream);
        }

        public override void Start()
        {
        }

        public override void Dispose()
        {
            mStream.DisposeInternal();
        }
    }
}
