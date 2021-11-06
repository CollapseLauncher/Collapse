using System;
using System.Text;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Order;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Force.Crc32;

namespace BenchmarkLab
{
    public class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<bruh>();
        }
    }

    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class bruh
    {
        byte[] buffer = new byte[512];
        MD5 md5;

        public byte[] CreateMD5(Stream fs) => MD5.Create().ComputeHash(fs);

        public double GetPercentageNumber(double cur, double max, int round = 2) => Math.Round((100 * cur) / max, round);
    }
}
