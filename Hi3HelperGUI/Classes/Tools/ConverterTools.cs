using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Numerics;
using Force.Crc32;

namespace Hi3HelperGUI
{
    public class ConverterTools
    {
        public static string BytesToCRC32(byte[] buffer)
        {
            Crc32Algorithm crc = new Crc32Algorithm();
            String hash = String.Empty;
            using (MemoryStream stream = new MemoryStream(buffer))
                foreach (byte a in crc.ComputeHash(stream)) hash += a.ToString("x2").ToLower();
            return hash;
        }

        public static uint BytesToUInt32Big(byte[] buffer)
        {
            return (BitConverter.ToUInt32(buffer, 0) & 0x000000FFU) << 24 | (BitConverter.ToUInt32(buffer, 0) & 0x0000FF00U) << 8 |
                   (BitConverter.ToUInt32(buffer, 0) & 0x00FF0000U) >> 8  | (BitConverter.ToUInt32(buffer, 0) & 0xFF000000U) >> 24;
        }

        public static ushort BytesToUInt16Big(byte[] buffer)
        {
            return (ushort)((BitConverter.ToUInt16(buffer, 0) & 0xFFU) << 8 | (BitConverter.ToUInt16(buffer, 0) & 0xFF00U) >> 8);
        }

        public static uint ToUInt32Big(uint value)
        {
            return (value & 0x000000FFU) << 24 | (value & 0x0000FF00U) << 8 |
                   (value & 0x00FF0000U) >> 8  | (value & 0xFF000000U) >> 24;
        }

        public static ushort ToUInt16Big(ushort value)
        {
            return (ushort)((value & 0xFFU) << 8 | (value & 0xFF00U) >> 8);
        }

        public static string BytesToHex(byte[] bytes, bool upperCase = false)
        {
            return string.Concat(bytes.Select(x => x.ToString(upperCase ? "X2" : "x2")));
        }

        public static string BytesToMD5(byte[] stream)
        {
            return BitConverter.ToString(MD5.Create().ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        static readonly string[] SizeSuffixes =
                   { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

        public static string SummarizeSize(Int64 value, int decimalPlaces = 2)
        {
            if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
            if (value < 0) { return "-" + SummarizeSize(-value, decimalPlaces); }
            if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            int mag = (int)Math.Log(value, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            // make adjustment when the value is large enough that
            // it would round up to 1000 or more
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            return string.Format("{0:n" + decimalPlaces + "} {1}",
                adjustedSize,
                SizeSuffixes[mag]);
        }

        // Reference:
        // https://stackoverflow.com/questions/249760/how-can-i-convert-a-unix-timestamp-to-datetime-and-vice-versa
        public static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTimeStamp).ToLocalTime();
        }

        public static int UnixTimestamp()
        {
            return (int)Math.Truncate((DateTime.Now.Subtract(new DateTime(1970, 1, 1))).TotalSeconds);
        }

        // Reference:
        // https://social.msdn.microsoft.com/Forums/vstudio/en-US/7f5765cc-3edc-44b4-92c6-7b9680e778ed/getting-md5sha-as-number-instead-of-string?forum=csharpgeneral
        public static BigInteger HexToNumber(HashAlgorithm algorithm, byte[] data)
        {
            return new BigInteger(algorithm.ComputeHash(data));
        }

        // Reference:
        // https://makolyte.com/csharp-hex-string-to-byte-array
        private readonly static Dictionary<char, byte> hexmap = new Dictionary<char, byte>()
        {
            { 'a', 0xA },{ 'b', 0xB },{ 'c', 0xC },{ 'd', 0xD },
            { 'e', 0xE },{ 'f', 0xF },{ 'A', 0xA },{ 'B', 0xB },
            { 'C', 0xC },{ 'D', 0xD },{ 'E', 0xE },{ 'F', 0xF },
            { '0', 0x0 },{ '1', 0x1 },{ '2', 0x2 },{ '3', 0x3 },
            { '4', 0x4 },{ '5', 0x5 },{ '6', 0x6 },{ '7', 0x7 },
            { '8', 0x8 },{ '9', 0x9 }
        };
        public static byte[] ToBytes(string hex)
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
    }
}
