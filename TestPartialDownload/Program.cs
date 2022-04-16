using System;
using System.IO;
using System.Text;
using System.Threading;
using Hi3Helper.Data;


namespace Test
{
    public class bruh
    {
        public static void Main(string[] args)
        {
            string URL = @"https://prophost.ironmaid.xyz/test.webm";
            string Out = @"C:\myGit\test.webm";
            HttpClientHelper Tool = new HttpClientHelper();
            Tool.DownloadFile(URL, Out, new CancellationToken(), 0, 4095);
            Tool.DownloadProgress += Tool_DownloadProgress;
        }

        private static void Tool_DownloadProgress(object? sender, HttpClientHelper._DownloadProgress e)
        {
            Console.Write($"\r\n{e.ProgressPercentage}%");
        }
    }
}