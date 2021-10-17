using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Numerics;
using Force.Crc32;

namespace Hi3HelperGUI.Data
{
    public class ConverterTool
    {
        static readonly Crc32Algorithm CRCEncoder = new Crc32Algorithm();
        public static string BytesToCRC32Simple(in byte[] buffer) => BytesToHex(CRCEncoder.ComputeHash(new MemoryStream(buffer, false)));
        public static string BytesToCRC32Simple(MemoryStream buffer) => BytesToHex(CRCEncoder.ComputeHash(buffer));
#if (NETCOREAPP)
        public static string BytesToHex(in ReadOnlySpan<byte> bytes) => Convert.ToHexString(bytes);
#else
        public static string BytesToHex(in byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty);
#endif

        public static uint BytesToUInt32Big(byte[] buffer) =>
            (BitConverter.ToUInt32(buffer, 0) & 0x000000FFU) << 24 | (BitConverter.ToUInt32(buffer, 0) & 0x0000FF00U) << 8 |
            (BitConverter.ToUInt32(buffer, 0) & 0x00FF0000U) >> 8  | (BitConverter.ToUInt32(buffer, 0) & 0xFF000000U) >> 24;

        public static ushort BytesToUInt16Big(byte[] buffer) => (ushort)((BitConverter.ToUInt16(buffer, 0) & 0xFFU) << 8 | (BitConverter.ToUInt16(buffer, 0) & 0xFF00U) >> 8);

        public static uint ToUInt32Big(uint value) =>
            (value & 0x000000FFU) << 24 | (value & 0x0000FF00U) << 8 |
            (value & 0x00FF0000U) >> 8  | (value & 0xFF000000U) >> 24;

        public static double GetPercentageNumber(double cur, double max, int round = 2) => Math.Round((100 * cur) / max, round);

        public static ushort ToUInt16Big(ushort value) => (ushort)((value & 0xFFU) << 8 | (value & 0xFF00U) >> 8);

        public static string BytesToMD5(byte[] stream) => BytesToHex(MD5.Create().ComputeHash(stream));

        internal readonly static string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

        public static string NormalizePath(string i) => Path.Combine(Path.GetDirectoryName(i), Path.GetFileName(i));

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

        /// <summary>
        /// Convert a hexidecimal string to an base 10 integer
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private static int HexStringToInt(string str)
        {
            int value = 0;
            for (int i = 0; i < str.Length; i++)
            {
                value += HexCharToInt(str[i]) << ((str.Length - 1 - i) * 4);
            }
            return value;
        }

        /// <summary>
        /// Convert a hex char to it an integer.
        /// </summary>
        /// <param name="ch"></param>
        /// <returns></returns>
        private static int HexCharToInt(char ch) => (ch < 58) ? ch - 48 : ch - 55;
    }
}
