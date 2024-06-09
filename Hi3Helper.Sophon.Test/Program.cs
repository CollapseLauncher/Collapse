using Hi3Helper.Sophon.Helper;
using Hi3Helper.Sophon.Infos;
using Hi3Helper.Sophon.Structs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
#if !DEMODIFF
            Console.WriteLine($"{executableName} [Sophon Branch Url] [Matching field name (usually, you can set \"game\" as the value)] [Download Output Path] [OPTIONAL: Amount of threads to be used (Default: {Environment.ProcessorCount})] [OPTIONAL: Amount of max. connection used for Http Client (Default: 128)]");
#else
            Console.WriteLine($"{executableName} [Preload/Update] [Sophon Branch Url From] [Sophon Branch Url To] [Matching field name (usually, you can set \"game\" as the value)] [Old Directory Path] [New Directory Path] [OPTIONAL: Amount of threads to be used (Default: {Environment.ProcessorCount})] [OPTIONAL: Amount of max. connection used for Http Client (Default: 128)]");
#endif
            return 1;
        }

            public static async Task<int> Main(params string[] args)
        {
            int threads = Environment.ProcessorCount;
            int maxHttpHandle = 128;

#if !DEMODIFF
            if (args.Length < 3)
                return UsageHelp();

            if (args.Length > 3 && int.TryParse(args[3], out threads))
                Console.WriteLine($"Thread count has been set to: {threads} for downloading!");

            if (args.Length > 4 && int.TryParse(args[4], out maxHttpHandle))
                Console.WriteLine($"HTTP Client maximum connection has been set to: {maxHttpHandle} handles!");

            string outputDir = args[2];
#else
            bool isPreloadMode = false;

            if (args.Length < 6)
                return UsageHelp();

            if (!((isPreloadMode = args[0].Equals("Preload", StringComparison.OrdinalIgnoreCase)) || args[0].Equals("Update", StringComparison.OrdinalIgnoreCase)))
                return UsageHelp();

            if (args.Length > 6 && int.TryParse(args[6], out threads))
                Console.WriteLine($"Thread count has been set to: {threads} for downloading!");

            if (args.Length > 7 && int.TryParse(args[7], out maxHttpHandle))
                Console.WriteLine($"HTTP Client maximum connection has been set to: {maxHttpHandle} handles!");

            string outputDir = args[4];
#endif

        // Logger.LogHandler += Logger_LogHandler;

        StartDownload:
            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                CancelMessage = "[\"C\"] Stop or [\"R\"] Restart";
                using (HttpClientHandler httpHandler = new HttpClientHandler
                {
                    MaxConnectionsPerServer = maxHttpHandle
                })
                using (HttpClient httpClient = new HttpClient(httpHandler)
                {
#if NET6_0_OR_GREATER
                    DefaultRequestVersion = HttpVersion.Version30,
                    DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
#endif
                })
                {
#if !DEMODIFF
                    SophonChunkManifestInfoPair manifestPair = await SophonManifest.CreateSophonChunkManifestInfoPair(httpClient, args[0], args[1], tokenSource.Token);

                    SophonChunksInfo sophonChunksInfo = manifestPair.ChunksInfo;
                    SophonManifestInfo sophonManifestInfo = manifestPair.ManifestInfo;
#else
                    string outputNewDir = args[5];

                    SophonChunkManifestInfoPair manifestPairFrom = await SophonManifest.CreateSophonChunkManifestInfoPair(httpClient, args[1], args[3], tokenSource.Token);

                    SophonChunksInfo sophonChunksInfoFrom = manifestPairFrom.ChunksInfo;
                    SophonManifestInfo sophonManifestInfoFrom = manifestPairFrom.ManifestInfo;

                    SophonChunkManifestInfoPair manifestPairTo = await SophonManifest.CreateSophonChunkManifestInfoPair(httpClient, args[2], args[3], tokenSource.Token);

                    SophonChunksInfo sophonChunksInfoTo = manifestPairTo.ChunksInfo;
                    SophonManifestInfo sophonManifestInfoTo = manifestPairTo.ManifestInfo;
#endif

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
                        string chunkOutPath = Path.Combine(outputDir, "chunk_collapse");
                        if (!Directory.Exists(chunkOutPath))
                            Directory.CreateDirectory(chunkOutPath);

                        if (!Directory.Exists(outputNewDir))
                            Directory.CreateDirectory(outputNewDir);
#if !DEMODIFF
                        string totalSizeUnit = SummarizeSizeSimple(sophonChunksInfo.TotalSize);
                        string totalSizeDiffUnit = "0 B";
#else
                        long totalSizeDiff = await SophonUpdate.GetCalculatedDiffSizeAsync(httpClient, manifestPairFrom, manifestPairTo, false, tokenSource.Token);
                        string totalSizeDiffUnit = SummarizeSizeSimple(totalSizeDiff);
                        string totalSizeUnit = SummarizeSizeSimple(manifestPairTo.ChunksInfo.TotalSize);
#endif

                        foreach (string fileTemp in Directory.EnumerateFiles(outputDir, "*_tempUpdate", SearchOption.AllDirectories))
                        {
                            File.Delete(fileTemp);
                        }

                        IsRetry = false;
#if NET6_0_OR_GREATER
                        await Parallel.ForEachAsync(
#if !DEMODIFF
                            SophonManifest.EnumerateAsync(
                            httpClient,
                            sophonManifestInfo,
                            sophonChunksInfo,
                            tokenSource.Token)
#else
                            SophonUpdate.EnumerateUpdateAsync(httpClient, manifestPairFrom, manifestPairTo)
#endif
                            ,
                            parallelOptions,
                            async (asset, token) =>
                            {
                                if (asset.IsDirectory)
                                    return;

                                string outputAssetPath = Path.Combine(outputDir, asset.AssetName!);
                                string outputAssetDir = Path.GetDirectoryName(outputAssetPath);

                                if (!string.IsNullOrEmpty(outputAssetDir) && !Directory.Exists(outputAssetDir))
                                    Directory.CreateDirectory(outputAssetDir);

#if !DEMODIFF
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
                                        Console.Write($"{CancelMessage} | {sizeUnit}/{totalSizeUnit} ({totalSizeDiffUnit} diff) -> {currentRead} ({speedUnit}/s)    \r");
                                    },
                                    (asset) =>
                                    {
                                        Console.WriteLine($"Downloaded: {asset.AssetName}");
                                    }
                                    );
#else
                                if (isPreloadMode)
                                {
                                    await asset.DownloadDiffChunksAsync(
                                        httpClient,
                                        chunkOutPath,
                                        parallelOptions,
                                        (read) =>
                                        {
                                            Interlocked.Add(ref currentRead, read);
                                            string sizeUnit = SummarizeSizeSimple(currentRead);
                                            string speedUnit = SummarizeSizeSimple(currentRead / stopwatch.Elapsed.TotalSeconds);
                                            Console.Write($"{CancelMessage} | {sizeUnit}/{totalSizeUnit} ({totalSizeDiffUnit} diff) -> {currentRead} ({speedUnit}/s)    \r");
                                        }
                                        );
                                }
                                else
                                {
                                    await asset.WriteUpdateAsync(
                                        httpClient,
                                        outputDir,
                                        outputNewDir,
                                        chunkOutPath,
                                        parallelOptions,
                                        (read) =>
                                        {
                                            Interlocked.Add(ref currentRead, read);
                                            string sizeUnit = SummarizeSizeSimple(currentRead);
                                            string speedUnit = SummarizeSizeSimple(currentRead / stopwatch.Elapsed.TotalSeconds);
                                            Console.Write($"{CancelMessage} | {sizeUnit}/{totalSizeUnit} ({totalSizeDiffUnit} diff) -> {currentRead} ({speedUnit}/s)    \r");
                                        },
                                        (asset) =>
                                        {
                                            Console.WriteLine($"Downloaded: {asset.AssetName}");
                                        }
                                        );
                                }
#endif
                            });
#else
                        await foreach (SophonAsset asset in
#if !DEMODIFF
                        await SophonManifest.GetAssetListAsync(
                            httpClient,
                            sophonManifestInfo,
                            sophonChunksInfo,
                            tokenSource.Token)
#else
                            SophonUpdate.EnumerateUpdateAsync(httpClient, manifestPairFrom, manifestPairTo).WithCancellation(tokenSource.Token)
#endif
                            )
                        {
                            if (asset.IsDirectory)
                                continue;

                            string outputAssetPath = Path.Combine(outputDir, asset.AssetName);
                            string outputAssetDir = Path.GetDirectoryName(outputAssetPath);

                            if (!string.IsNullOrEmpty(outputAssetDir) && !Directory.Exists(outputAssetDir))
                                Directory.CreateDirectory(outputAssetDir);

                            // using FileStream fileStream = new FileStream(outputAssetPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

#if !DEMODIFF
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
                                    Console.Write($"{CancelMessage} | {sizeUnit}/{totalSizeUnit} ({totalSizeDiffUnit} diff) -> {currentRead} ({speedUnit}/s)    \r");
                                },
                                (assetDone) =>
                                {
                                    Console.WriteLine($"Downloaded: {asset.AssetName}");
                                }
                                );
#else
                            if (isPreloadMode)
                            {
                                await asset.DownloadDiffChunksAsync(
                                    httpClient,
                                    chunkOutPath,
                                    parallelOptions,
                                    (read) =>
                                    {
                                        Interlocked.Add(ref currentRead, read);
                                        string sizeUnit = SummarizeSizeSimple(currentRead);
                                        string speedUnit = SummarizeSizeSimple(currentRead / stopwatch.Elapsed.TotalSeconds);
                                        Console.Write($"{CancelMessage} | {sizeUnit}/{totalSizeUnit} ({totalSizeDiffUnit} diff) -> {currentRead} ({speedUnit}/s)    \r");
                                    }
                                    );
                            }
                            else
                            {
                                await asset.WriteUpdateAsync(
                                    httpClient,
                                    outputDir,
                                    outputNewDir,
                                    chunkOutPath,
                                    parallelOptions,
                                    (read) =>
                                    {
                                        Interlocked.Add(ref currentRead, read);
                                        string sizeUnit = SummarizeSizeSimple(currentRead);
                                        string speedUnit = SummarizeSizeSimple(currentRead / stopwatch.Elapsed.TotalSeconds);
                                        Console.Write($"{CancelMessage} | {sizeUnit}/{totalSizeUnit} ({totalSizeDiffUnit} diff) -> {currentRead} ({speedUnit}/s)    \r");
                                    },
                                    (asset) =>
                                    {
                                        Console.WriteLine($"Downloaded: {asset.AssetName}");
                                    }
                                    );
                            }
#endif
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