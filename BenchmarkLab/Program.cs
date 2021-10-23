using System;
using System.Text;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Order;
using System.IO;
using SharpHash.Base;
using SharpHash.Interfaces;
using SharpHash.Checksum;
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

        [Benchmark]
        public void CreateMD5() => Console.WriteLine(Convert.ToHexString(CreateMD5(new FileStream(@"C:\Program Files\Honkai Impact 3rd glb\Games\BH3_Data\StreamingAssets\Asb\pc\hash\0c66a644c0dac3fbf59d0f0a4e784047.wmv", FileMode.Open, FileAccess.Read))));

        [Benchmark]
        public void CreateMD5Buffer() => Console.WriteLine(Convert.ToHexString(CreateMD5(new FileStream(@"C:\Program Files\Honkai Impact 3rd glb\Games\BH3_Data\StreamingAssets\Asb\pc\hash\0c66a644c0dac3fbf59d0f0a4e784047.wmv", FileMode.Open, FileAccess.Read, FileShare.Read, 4096))));


        public byte[] CreateMD5(Stream fs) => MD5.Create().ComputeHash(fs);

        public double GetPercentageNumber(double cur, double max, int round = 2) => Math.Round((100 * cur) / max, round);
    }
}
