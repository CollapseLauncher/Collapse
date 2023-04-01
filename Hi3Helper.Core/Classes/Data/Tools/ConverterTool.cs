using Force.Crc32;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Hi3Helper.Data
{
    public static class ConverterTool
    {
        private static readonly MD5 MD5Hash = MD5.Create();
        private static readonly Crc32Algorithm CRCEncoder = new Crc32Algorithm();
        private static readonly string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        public static string BytesToCRC32Simple(Stream buffer) => HexTool.BytesToHexUnsafe(CRCEncoder.ComputeHash(buffer));
        public static async Task<string> BytesToCRC32SimpleAsync(Stream buffer) => HexTool.BytesToHexUnsafe(await Task.Run(() => CRCEncoder.ComputeHash(buffer)).ConfigureAwait(false));
        public static string CreateMD5Shared(Stream fs)
        {
            MD5Hash.Initialize();
            ReadOnlySpan<byte> res = MD5Hash.ComputeHash(fs);
            return HexTool.BytesToHexUnsafe(res);
        }
        public static string CreateMD5(Stream fs)
        {
            ReadOnlySpan<byte> res = MD5.Create().ComputeHash(fs);
            return HexTool.BytesToHexUnsafe(res);
        }
        public static async Task<string> CreateMD5Async(Stream fs)
        {
            MD5Hash.Initialize();
            Memory<byte> res = await MD5Hash.ComputeHashAsync(fs).ConfigureAwait(false);
            return HexTool.BytesToHexUnsafe(res.Span);
        }

        public static double Unzeroed(double i) => i == 0 ? 1 : i;

        public static uint BytesToUInt32Big(byte[] buffer) =>
            (BitConverter.ToUInt32(buffer, 0) & 0x000000FFU) << 24 | (BitConverter.ToUInt32(buffer, 0) & 0x0000FF00U) << 8 |
            (BitConverter.ToUInt32(buffer, 0) & 0x00FF0000U) >> 8 | (BitConverter.ToUInt32(buffer, 0) & 0xFF000000U) >> 24;

        public static ushort BytesToUInt16Big(byte[] buffer) => (ushort)((BitConverter.ToUInt16(buffer, 0) & 0xFFU) << 8 | (BitConverter.ToUInt16(buffer, 0) & 0xFF00U) >> 8);

        public static double GetPercentageNumber(double cur, double max, int round = 2) => Math.Round((100 * cur) / max, round);

        private static readonly SpanAction<char, IntPtr> s_normalizePathReplaceCore = NormalizePathUnsafeCore;
        public static unsafe string NormalizePath(ReadOnlySpan<char> source)
        {
            fixed (char* ptr = source.TrimStart('/'))
            {
                return string.Create(source.Length, (IntPtr)ptr, s_normalizePathReplaceCore);
            }
        }

        // Reference: https://github.com/dotnet/aspnetcore/blob/c65dac77cf6540c81860a42fff41eb11b9804367/src/Shared/QueryStringEnumerable.cs#L169
        private static unsafe void NormalizePathUnsafeCore(Span<char> buffer, IntPtr state)
        {
            fixed (char* ptr = buffer)
            {
                var input = (ushort*)state.ToPointer();
                var output = (ushort*)ptr;

                var i = (nint)0;
                var n = (nint)(uint)buffer.Length;

                if (Sse41.IsSupported && n >= Vector128<ushort>.Count)
                {
                    var vecPlus = Vector128.Create((ushort)'/');
                    var vecSpace = Vector128.Create((ushort)'\\');

                    do
                    {
                        var vec = Sse2.LoadVector128(input + i);
                        var mask = Sse2.CompareEqual(vec, vecPlus);
                        var res = Sse41.BlendVariable(vec, vecSpace, mask);

                        Sse2.Store(output + i, res);

                        i += Vector128<ushort>.Count;

                    } while (i <= n - Vector128<ushort>.Count);
                }

                for (; i < n; ++i)
                {
                    if (input[i] != '/')
                    {
                        output[i] = input[i];
                    }
                    else
                    {
                        output[i] = '\\';
                    }
                }
            }
        }

        public static string SummarizeSizeSimple(double value, int decimalPlaces = 2)
        {
            byte mag = (byte)Math.Log(value, 1000);

            return string.Format("{0} {1}", Math.Round(value / (1L << (mag * 10)), decimalPlaces), SizeSuffixes[mag]);
        }

        public static int GetUnixTimestamp() => (int)Math.Truncate(DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);

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

        public static float ConvertRangeValue(float sMin, float sMax, float sValue, float tMin, float tMax) => ((sValue - sMin) * (tMax - tMin) / (sMax - sMin)) + tMin;

        public static string CombineURLFromString(ReadOnlySpan<char> baseURL, params string[] segments)
        {
            StringBuilder builder = new StringBuilder().Append(baseURL.TrimEnd('/'));

            foreach (ReadOnlySpan<char> a in segments)
            {
                bool isMacros = a.StartsWith("?");
                if (!isMacros)
                {
                    builder.Append('/');
                }
                builder.Append(a.Trim('/'));
            }

            return builder.ToString();
        }
    }
}
