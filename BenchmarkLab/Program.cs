using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Order;

namespace BenchmarkLab
{
    public class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            BenchmarkRunner.Run<Tool>();
        }
    }

    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class Tool
    {
        internal protected static readonly string[] SizeSuffixesOpt =
           { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

        [Benchmark]
        public void StartSummarizeOriginal()
        {
            for (short i = short.MaxValue; i > 0; i--)
                SummarizeSize((long)i);
        }

        [Benchmark]
        public void StartSummarizeSimplifiedNew()
        {
            for (short i = short.MaxValue; i > 0; i--)
                SummarizeSizeSimplified((long)i);
        }

        static readonly string[] SizeSuffixes =
                   { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

        public string SummarizeSize(Int64 value, int decimalPlaces = 2)
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

        public string SummarizeSizeSimplified(long value, byte decimalPlaces = 2)
        {
            if (value == 0 || value < 0)
                return "0 bytes";

            //if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
            //if (value < 0) { return "-" + SummarizeSize(-value, decimalPlaces); }
            // if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }
            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            int mag = (int)Math.Log(value, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            // make adjustment when the value is large enough that
            // it would round up to 1000 or more
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag++;
                adjustedSize /= 1024;
            }

            /*return string.Format("{0:n" + decimalPlaces + "} {1}",
                adjustedSize,
                SizeSuffixes[mag]);*/

            return $"{Math.Round(adjustedSize, decimalPlaces)} {SizeSuffixesOpt[mag]}";
        }
    }
}
