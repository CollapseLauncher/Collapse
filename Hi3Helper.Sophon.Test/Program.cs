using Hi3Helper.Sophon.Helper;
using Hi3Helper.Sophon.Infos;
using Hi3Helper.Sophon.Structs;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Hi3Helper.Sophon.Test
{
    public class MainApp
    {
        static string CancelMessage = "";
        static bool IsRetry = false;
        static string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

        private static int UsageHelp()
        {
            string executableName = Process.GetCurrentProcess().ProcessName;
            Console.WriteLine($"{executableName} [Sophon Branch Url] [Matching field name (usually, you can set \"game\" as the value)] [Download Output Path] [OPTIONAL: Amount of threads to be used (Default: {Environment.ProcessorCount})] [OPTIONAL: Amount of max. connection used for Http Client (Default: 128)]");
            return 1;
        }

        public static async Task<int> Main(params string[] args)
        {
            int threads = Environment.ProcessorCount;
            int maxHttpHandle = 128;

            if (args.Length < 3)
                return UsageHelp();

            if (args.Length > 3 && int.TryParse(args[3], out threads))
                Console.WriteLine($"Thread count has been set to: {threads} for downloading!");

            if (args.Length > 4 && int.TryParse(args[4], out maxHttpHandle))
                Console.WriteLine($"HTTP Client maximum connection has been set to: {maxHttpHandle} handles!");

            string outputDir = args[2];

            Logger.LogHandler += Logger_LogHandler;

        StartDownload:
            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                CancelMessage = "Press \"C\" key to stop or \"R\" key to restart the download";
                using (HttpClientHandler httpHandler = new HttpClientHandler
                {
                    MaxConnectionsPerServer = maxHttpHandle
                })
                using (HttpClient httpClient = new HttpClient(httpHandler)
                {
                    // DefaultRequestVersion = HttpVersion.Version30,
                    // DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
                })
                {
                    SophonChunkManifestInfoPair manifestPair = await SophonManifest.CreateSophonChunkManifestInfoPair(httpClient, args[0], args[1], tokenSource.Token);

                    SophonChunksInfo sophonChunksInfo = manifestPair.ChunksInfo;
                    SophonManifestInfo sophonManifestInfo = manifestPair.ManifestInfo;

                    long currentRead = 0;
                    Task.Run(() => AppExitKeyTrigger(tokenSource));

                    ParallelOptions parallelOptions =
                        new ParallelOptions
                        {
                            CancellationToken = tokenSource.Token,
                            MaxDegreeOfParallelism = threads
                        };

                    Stopwatch stopwatch = Stopwatch.StartNew();

                    try
                    {
                        string totalSizeUnit = SummarizeSizeSimple(sophonChunksInfo.TotalSize);

                        IsRetry = false;
#if NET6_0_OR_GREATER
                        await Parallel.ForEachAsync(SophonManifest.EnumerateAsync(
                            httpClient,
                            sophonManifestInfo,
                            sophonChunksInfo,
                            tokenSource.Token),
                            parallelOptions,
                            async (asset, token) =>
                            {
                                if (asset.IsDirectory)
                                    return;

                                string outputAssetPath = Path.Combine(outputDir, asset.AssetName!);
                                string outputAssetDir = Path.GetDirectoryName(outputAssetPath);

                                if (!string.IsNullOrEmpty(outputAssetDir) && !Directory.Exists(outputAssetDir))
                                    Directory.CreateDirectory(outputAssetDir);

                                // using FileStream fileStream = new FileStream(outputAssetPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

                                await asset.WriteToStreamAsync(
                                    httpClient,
                                    // fileStream,
                                    () => new FileStream(outputAssetPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite),
                                    parallelOptions,
                                    (read) =>
                                    {
                                        Interlocked.Add(ref currentRead, read);
                                        string sizeUnit = SummarizeSizeSimple(currentRead);
                                        string speedUnit = SummarizeSizeSimple(currentRead / stopwatch.Elapsed.TotalSeconds);
                                        Console.Write($"{CancelMessage} | {sizeUnit}/{totalSizeUnit} -> {currentRead} ({speedUnit}/s)    \r");
                                    },
                                    (asset) =>
                                    {
                                        Console.WriteLine($"Downloaded: {asset.AssetName}");
                                    }
                                    );
                            });
#else
                        foreach (SophonAsset asset in await SophonManifest.GetAssetListAsync(
                            httpClient,
                            sophonManifestInfo,
                            sophonChunksInfo,
                            tokenSource.Token))
                        {
                            if (asset.IsDirectory)
                                continue;

                            string outputAssetPath = Path.Combine(outputDir, asset.AssetName);
                            string outputAssetDir = Path.GetDirectoryName(outputAssetPath);

                            if (!string.IsNullOrEmpty(outputAssetDir) && !Directory.Exists(outputAssetDir))
                                Directory.CreateDirectory(outputAssetDir);

                            // using FileStream fileStream = new FileStream(outputAssetPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

                            await asset.WriteToStreamAsync(
                                httpClient,
                                // fileStream,
                                () => new FileStream(outputAssetPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite),
                                parallelOptions,
                                (read) =>
                                {
                                    Interlocked.Add(ref currentRead, read);
                                    string sizeUnit = SummarizeSizeSimple(currentRead);
                                    string speedUnit = SummarizeSizeSimple(currentRead / stopwatch.Elapsed.TotalSeconds);
                                    Console.Write($"{CancelMessage} | {sizeUnit}/{totalSizeUnit} -> {currentRead} ({speedUnit}/s)    \r");
                                },
                                (assetDone) =>
                                {
                                    Console.WriteLine($"Downloaded: {assetDone.AssetName}");
                                }
                                );
                        }
#endif
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine("Download has been cancelled!");
                    }
                    finally
                    {
                        stopwatch.Stop();
                    }
                }
            }

            if (IsRetry)
                goto StartDownload;

            return 0;
        }

        private static string SummarizeSizeSimple(double value, int decimalPlaces = 2)
        {
            byte mag = (byte)Math.Log(value, 1000);

            return string.Format("{0} {1}", Math.Round(value / (1L << (mag * 10)), decimalPlaces), SizeSuffixes[mag]);
        }

        private static void AppExitKeyTrigger(CancellationTokenSource tokenSource)
        {
            while (true)
            {
                ConsoleKeyInfo keyInfo = Console.ReadKey();
                switch (keyInfo.Key)
                {
                    case ConsoleKey.C:
                        CancelMessage = "Cancelling download...";
                        tokenSource.Cancel();
                        return;
                    case ConsoleKey.R:
                        IsRetry = true;
                        CancelMessage = "Retrying download...";
                        tokenSource.Cancel();
                        return;
                }
            }
        }

        private static void Logger_LogHandler(object sender, LogStruct e)
        {
#if !DEBUG
            // if (e.LogLevel == LogLevel.Debug) return;
#endif

            Console.WriteLine($"[{e.LogLevel}] {e.Message}");
        }
    }
}