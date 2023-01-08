using Force.Crc32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Hi3Helper.Data
{
    unsafe public class ConverterToolUnsafe
    {
        private static readonly uint[] _lookup32Unsafe = new uint[]
        {
            0x300030, 0x310030, 0x320030, 0x330030, 0x340030, 0x350030, 0x360030, 0x370030, 0x380030, 0x390030, 0x610030, 0x620030,
            0x630030, 0x640030, 0x650030, 0x660030, 0x300031, 0x310031, 0x320031, 0x330031, 0x340031, 0x350031, 0x360031, 0x370031,
            0x380031, 0x390031, 0x610031, 0x620031, 0x630031, 0x640031, 0x650031, 0x660031, 0x300032, 0x310032, 0x320032, 0x330032,
            0x340032, 0x350032, 0x360032, 0x370032, 0x380032, 0x390032, 0x610032, 0x620032, 0x630032, 0x640032, 0x650032, 0x660032,
            0x300033, 0x310033, 0x320033, 0x330033, 0x340033, 0x350033, 0x360033, 0x370033, 0x380033, 0x390033, 0x610033, 0x620033,
            0x630033, 0x640033, 0x650033, 0x660033, 0x300034, 0x310034, 0x320034, 0x330034, 0x340034, 0x350034, 0x360034, 0x370034,
            0x380034, 0x390034, 0x610034, 0x620034, 0x630034, 0x640034, 0x650034, 0x660034, 0x300035, 0x310035, 0x320035, 0x330035,
            0x340035, 0x350035, 0x360035, 0x370035, 0x380035, 0x390035, 0x610035, 0x620035, 0x630035, 0x640035, 0x650035, 0x660035,
            0x300036, 0x310036, 0x320036, 0x330036, 0x340036, 0x350036, 0x360036, 0x370036, 0x380036, 0x390036, 0x610036, 0x620036,
            0x630036, 0x640036, 0x650036, 0x660036, 0x300037, 0x310037, 0x320037, 0x330037, 0x340037, 0x350037, 0x360037, 0x370037,
            0x380037, 0x390037, 0x610037, 0x620037, 0x630037, 0x640037, 0x650037, 0x660037, 0x300038, 0x310038, 0x320038, 0x330038,
            0x340038, 0x350038, 0x360038, 0x370038, 0x380038, 0x390038, 0x610038, 0x620038, 0x630038, 0x640038, 0x650038, 0x660038,
            0x300039, 0x310039, 0x320039, 0x330039, 0x340039, 0x350039, 0x360039, 0x370039, 0x380039, 0x390039, 0x610039, 0x620039,
            0x630039, 0x640039, 0x650039, 0x660039, 0x300061, 0x310061, 0x320061, 0x330061, 0x340061, 0x350061, 0x360061, 0x370061,
            0x380061, 0x390061, 0x610061, 0x620061, 0x630061, 0x640061, 0x650061, 0x660061, 0x300062, 0x310062, 0x320062, 0x330062,
            0x340062, 0x350062, 0x360062, 0x370062, 0x380062, 0x390062, 0x610062, 0x620062, 0x630062, 0x640062, 0x650062, 0x660062,
            0x300063, 0x310063, 0x320063, 0x330063, 0x340063, 0x350063, 0x360063, 0x370063, 0x380063, 0x390063, 0x610063, 0x620063,
            0x630063, 0x640063, 0x650063, 0x660063, 0x300064, 0x310064, 0x320064, 0x330064, 0x340064, 0x350064, 0x360064, 0x370064,
            0x380064, 0x390064, 0x610064, 0x620064, 0x630064, 0x640064, 0x650064, 0x660064, 0x300065, 0x310065, 0x320065, 0x330065,
            0x340065, 0x350065, 0x360065, 0x370065, 0x380065, 0x390065, 0x610065, 0x620065, 0x630065, 0x640065, 0x650065, 0x660065,
            0x300066, 0x310066, 0x320066, 0x330066, 0x340066, 0x350066, 0x360066, 0x370066, 0x380066, 0x390066, 0x610066, 0x620066,
            0x630066, 0x640066, 0x650066, 0x660066
        };

        private static readonly uint* _lookup32UnsafeP = (uint*)GCHandle.Alloc(_lookup32Unsafe, GCHandleType.Pinned).AddrOfPinnedObject();

        public static string ByteArrayToHexViaLookup32Unsafe(ReadOnlySpan<byte> bytes)
        {
            var lookupP = _lookup32UnsafeP;
            var result = new char[bytes.Length * 2];
            fixed (byte* bytesP = bytes)
            fixed (char* resultP = result)
            {
                uint* resultP2 = (uint*)resultP;
                for (int i = 0; i < bytes.Length; i++)
                {
                    resultP2[i] = lookupP[bytesP[i]];
                }
            }
            return new string(result);
        }
    }

    public class ConverterTool
    {
        private static readonly MD5 MD5Hash = MD5.Create();
        private static readonly Crc32Algorithm CRCEncoder = new Crc32Algorithm();
        private static readonly string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        public static string BytesToCRC32Simple(Stream buffer) => ConverterToolUnsafe.ByteArrayToHexViaLookup32Unsafe(CRCEncoder.ComputeHash(buffer));
        public static async Task<string> BytesToCRC32SimpleAsync(Stream buffer) => ConverterToolUnsafe.ByteArrayToHexViaLookup32Unsafe(await Task.Run(() => CRCEncoder.ComputeHash(buffer)).ConfigureAwait(false));
        public static string CreateMD5(Stream fs)
        {
            MD5Hash.Initialize();
            ReadOnlySpan<byte> res = MD5Hash.ComputeHash(fs);
            return ConverterToolUnsafe.ByteArrayToHexViaLookup32Unsafe(res);
        }
        public static async Task<string> CreateMD5Async(Stream fs)
        {
            MD5Hash.Initialize();
            Memory<byte> res = await MD5Hash.ComputeHashAsync(fs).ConfigureAwait(false);
            return ConverterToolUnsafe.ByteArrayToHexViaLookup32Unsafe(res.Span);
        }
        public static double Unzeroed(double i) => i == 0 ? 1 : i;
        public static double UnInfinity(double i) => double.IsInfinity(i) ? 0.0001f : i;

        public static uint BytesToUInt32Big(byte[] buffer) =>
            (BitConverter.ToUInt32(buffer, 0) & 0x000000FFU) << 24 | (BitConverter.ToUInt32(buffer, 0) & 0x0000FF00U) << 8 |
            (BitConverter.ToUInt32(buffer, 0) & 0x00FF0000U) >> 8 | (BitConverter.ToUInt32(buffer, 0) & 0xFF000000U) >> 24;

        public static ushort BytesToUInt16Big(byte[] buffer) => (ushort)((BitConverter.ToUInt16(buffer, 0) & 0xFFU) << 8 | (BitConverter.ToUInt16(buffer, 0) & 0xFF00U) >> 8);

        public static uint ToUInt32Big(uint value) =>
            (value & 0x000000FFU) << 24 | (value & 0x0000FF00U) << 8 |
            (value & 0x00FF0000U) >> 8 | (value & 0xFF000000U) >> 24;

        public static double GetPercentageNumber(double cur, double max, int round = 2) => Math.Round((100 * cur) / max, round);

        public static ushort ToUInt16Big(ushort value) => (ushort)((value & 0xFFU) << 8 | (value & 0xFF00U) >> 8);

        public static string NormalizePath(string i) =>
            Path.Combine(Path.GetDirectoryName(i), Path.GetFileName(i));

        public static uint ConcatUint(uint a, uint b)
        {
            uint c0 = 10, c1 = 100, c2 = 1000, c3 = 10000, c4 = 100000,
                    c5 = 1000000, c6 = 10000000, c7 = 100000000, c8 = 1000000000;
            a *= b < c0 ? c0 : b < c1 ? c1 : b < c2 ? c2 : b < c3 ? c3 :
                 b < c4 ? c4 : b < c5 ? c5 : b < c6 ? c6 : b < c7 ? c7 : c8;
            return a + b;
        }

        public static uint SumBinaryUint(uint a, uint b)
        {
            uint mask = uint.MaxValue;

            while ((mask & b) != 0)
            {
                mask <<= 1;
                a <<= 1;
            }

            return a | b;
        }

        public static string SummarizeSizeSimple(double value, int decimalPlaces = 2)
        {
            byte mag = (byte)Math.Log(value, 1000);

            return $"{Math.Round(value / (1L << (mag * 10)), decimalPlaces)} {SizeSuffixes[mag]}";
        }

        // Reference:
        // https://stackoverflow.com/questions/249760/how-can-i-convert-a-unix-timestamp-to-datetime-and-vice-versa
        public static DateTime UnixTimeStampToDateTime(long unixTimeStamp) => new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTimeStamp).ToLocalTime();

        public static int UnixTimestamp() => (int)Math.Truncate(DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);

        // Reference:
        // https://social.msdn.microsoft.com/Forums/vstudio/en-US/7f5765cc-3edc-44b4-92c6-7b9680e778ed/getting-md5sha-as-number-instead-of-string?forum=csharpgeneral
        public static BigInteger HexToNumber(HashAlgorithm algorithm, byte[] data) => new BigInteger(algorithm.ComputeHash(data));

        // Reference:
        // https://makolyte.com/csharp-hex-string-to-byte-array
        internal readonly static Dictionary<char, byte> hexmap = new Dictionary<char, byte>()
        {
            { 'a', 0xA },{ 'b', 0xB },{ 'c', 0xC },{ 'd', 0xD },
            { 'e', 0xE },{ 'f', 0xF },{ 'A', 0xA },{ 'B', 0xB },
            { 'C', 0xC },{ 'D', 0xD },{ 'E', 0xE },{ 'F', 0xF },
            { '0', 0x0 },{ '1', 0x1 },{ '2', 0x2 },{ '3', 0x3 },
            { '4', 0x4 },{ '5', 0x5 },{ '6', 0x6 },{ '7', 0x7 },
            { '8', 0x8 },{ '9', 0x9 }
        };

        public static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                throw new ArgumentException("Hex cannot be null/empty/whitespace");

            if (hex.Length % 2 != 0)
                throw new FormatException("Hex must have an even number of characters");

            bool startsWithHexStart = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase);

            if (startsWithHexStart && hex.Length == 2)
                throw new ArgumentException("There are no characters in the hex string");


            int startIndex = startsWithHexStart ? 2 : 0;

            byte[] bytesArr = new byte[(hex.Length - startIndex) / 2];

            char left;
            char right;

            try
            {
                int x = 0;
                for (int i = startIndex; i < hex.Length; i += 2, x++)
                {
                    left = hex[i];
                    right = hex[i + 1];
                    bytesArr[x] = (byte)((hexmap[left] << 4) | hexmap[right]);
                }
                return bytesArr;
            }
            catch (KeyNotFoundException)
            {
                throw new FormatException("Hex string has non-hex character");
            }
        }

        /// <summary>
        /// Convert an integer to a string of hexidecimal numbers.
        /// </summary>
        /// <param name="n">The int to convert to Hex representation</param>
        /// <param name="len">number of digits in the hex string. Pads with leading zeros.</param>
        /// <returns></returns>
        public static string NumberToHexString(long n, int len = 8) => new string(StringToChars(n, len));

        private static char[] StringToChars(long n, int len)
        {
            char[] ch = new char[len--];
            for (int i = len; i >= 0; i--) ch[len - i] = ByteToHexChar((byte)((ulong)(n >> 4 * i) & 15));

            return ch;
        }

        /// <summary>
        /// Convert a byte to a hexidecimal char
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        private static char ByteToHexChar(byte b)
        {
            if (b < 0 || b > 15)
                throw new Exception("IntToHexChar: input out of range for Hex value");
            return b < 10 ? (char)(b + 48) : (char)(b + 55);
        }

        public static bool IsUserHasPermission(string input)
        {
            try
            {
                if (!Directory.Exists(input))
                    Directory.CreateDirectory(input);

                File.Create(Path.Combine(input, "write_test"), 1, FileOptions.DeleteOnClose).Close();
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            return true;
        }
    }
}
