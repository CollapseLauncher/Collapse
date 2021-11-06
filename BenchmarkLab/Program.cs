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
            Uh uh = new Uh();
            uh.bruh();

            // BenchmarkRunner.Run<bruh>();
        }
    }

    public class Uh
    {
        IEnumerable<string> fileList = Directory.GetFiles(@"C:\Users\neon-nyan\Documents\git\myApp\Hi3Helper\build\betaoutput\", "*.unity3d", SearchOption.AllDirectories);
        byte[] buf1 = new byte[0x5];
        byte[] buf2 = new byte[0xD];
        public void bruh()
        {
            foreach (string file in fileList)
            {
                using (FileStream stream = new FileStream(file, FileMode.Open))
                {
                    stream.Position = 0xC;
                    stream.Read(buf1);
                    stream.Position++;
                    stream.Read(buf2);
                    if (Encoding.UTF8.GetString(buf2) != "2017.4.18f1.2" ||
                        Encoding.UTF8.GetString(buf1) != "5.x.x")
                        Console.WriteLine($"{Encoding.UTF8.GetString(buf1)} {Encoding.UTF8.GetString(buf2)}");
                }
            }
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
