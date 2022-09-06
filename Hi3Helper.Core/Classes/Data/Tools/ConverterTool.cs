using Force.Crc32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Hi3Helper.Data
{
    public class ConverterTool
    {
        static readonly Crc32Algorithm CRCEncoder = new Crc32Algorithm();
        public static string BytesToCRC32Simple(in byte[] buffer) => BytesToHex(CRCEncoder.ComputeHash(new MemoryStream(buffer, false)));
        public static string BytesToCRC32Simple(Stream buffer) => BytesToHex(CRCEncoder.ComputeHash(buffer));
        public static async Task<string> BytesToCRC32SimpleAsync(Stream buffer) => BytesToHex(await Task.Run(() => CRCEncoder.ComputeHash(buffer)));
        public static string CreateMD5(Stream fs) => BytesToHex(MD5.Create().ComputeHash(fs));
        public static async Task<string> CreateMD5Async(Stream fs) => BytesToHex(await Task.Run(() => MD5.Create().ComputeHash(fs)));
        public static int BytesToCRC32Int(Stream buffer) => BitConverter.ToInt32(CRCEncoder.ComputeHash(buffer), 0);
        public static string BytesToHex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty);
        public static string CreateMD5(Stream fs, CancellationToken token) => BytesToHex(MD5.Create().ComputeHash(fs));

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

        public static string BytesToMD5(byte[] stream) => BytesToHex(MD5.Create().ComputeHash(stream));

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

        internal readonly static string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

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
