using System;
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
            Console.WriteLine("Old SummarizeSize Result: " + new Tool().SummarizeSize((long)(uint.MaxValue * 0.987654321)) + " " + new Tool().SummarizeSize((long)(int.MaxValue * 0.987654321)));
            Console.WriteLine("SummarizeSizeSimple Result: " + new Tool().SummarizeSizeSimple(uint.MaxValue * 0.987654321) + " " + new Tool().SummarizeSizeSimple((int.MaxValue * 0.987654321)));
            Console.WriteLine("SummarizeSizeSimpleWithDecimal Result: " + new Tool().SummarizeSizeSimple(uint.MaxValue * 0.987654321, 9) + " " + new Tool().SummarizeSizeSimple(int.MaxValue * 0.987654321, 9));

            BenchmarkRunner.Run<Tool>();
        }
    }

    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class Tool
    {
        private readonly string[] SizeSuffixes =
           { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

        [Benchmark]
        public void SummarizeSize()
        {
            for (ushort i = ushort.MaxValue; i > 0; i--)
                SummarizeSize((long)(i * 0.987654321));
        }

        [Benchmark]
        public void SummarizeSizeSimpleWithDecimal()
        {
            for (ushort i = ushort.MaxValue; i > 0; i--)
                SummarizeSizeSimple(i * 0.987654321, 9);
        }

        [Benchmark]
        public void SummarizeSizeSimple()
        {
            for (ushort i = ushort.MaxValue; i > 0; i--)
                SummarizeSizeSimple(i * 0.987654321);
        }

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

        public string SummarizeSizeSimple(double value, int decimalPlaces = 2)
        {
            byte mag = (byte)Math.Log(value, 1000);

            return $"{Math.Round(value / (1L << (mag * 10)), decimalPlaces)} {SizeSuffixes[mag]}";
        }
    }
}
