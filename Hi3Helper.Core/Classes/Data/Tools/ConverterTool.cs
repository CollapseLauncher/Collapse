using Force.Crc32;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Hi3Helper.Data
{
    public class ConverterTool
    {
        private static readonly MD5 MD5Hash = MD5.Create();
        private static readonly Crc32Algorithm CRCEncoder = new Crc32Algorithm();
        private static readonly string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        public static string BytesToCRC32Simple(Stream buffer) => HexTool.BytesToHexUnsafe(CRCEncoder.ComputeHash(buffer));
        public static async Task<string> BytesToCRC32SimpleAsync(Stream buffer) => HexTool.BytesToHexUnsafe(await Task.Run(() => CRCEncoder.ComputeHash(buffer)).ConfigureAwait(false));
        public static string CreateMD5(Stream fs)
        {
            MD5 md5Instance = MD5.Create();
            ReadOnlySpan<byte> res = md5Instance.ComputeHash(fs);
            return HexTool.BytesToHexUnsafe(res);
        }
        public static async Task<string> CreateMD5Async(Stream fs)
        {
            MD5Hash.Initialize();
            Memory<byte> res = await MD5Hash.ComputeHashAsync(fs).ConfigureAwait(false);
            return HexTool.BytesToHexUnsafe(res.Span);
        }
        public static double Unzeroed(double i) => i == 0 ? 1 : i;
        public static double UnInfinity(double i) => double.IsInfinity(i) ? 0.0001f : i;

        public static uint BytesToUInt32Big(byte[] buffer) =>
            (BitConverter.ToUInt32(buffer, 0) & 0x000000FFU) << 24 | (BitConverter.ToUInt32(buffer, 0) & 0x0000FF00U) << 8 |
            (BitConverter.ToUInt32(buffer, 0) & 0x00FF0000U) >> 8 | (BitConverter.ToUInt32(buffer, 0) & 0xFF000000U) >> 24;

        public static ushort BytesToUInt16Big(byte[] buffer) => (ushort)((BitConverter.ToUInt16(buffer, 0) & 0xFFU) << 8 | (BitConverter.ToUInt16(buffer, 0) & 0xFF00U) >> 8);

        public static double GetPercentageNumber(double cur, double max, int round = 2) => Math.Round((100 * cur) / max, round);

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

        public static string SummarizeSizeSimple(double value, int decimalPlaces = 2)
        {
            byte mag = (byte)Math.Log(value, 1000);

            return string.Format("{0} {1}", Math.Round(value / (1L << (mag * 10)), decimalPlaces), SizeSuffixes[mag]);
        }

        // Reference:
        // https://stackoverflow.com/questions/249760/how-can-i-convert-a-unix-timestamp-to-datetime-and-vice-versa
        public static DateTime UnixTimeStampToDateTime(long unixTimeStamp) => new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTimeStamp).ToLocalTime();

        public static int UnixTimestamp() => (int)Math.Truncate(DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);

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
