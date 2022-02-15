using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

using SharpCompress.Archives;
using SharpCompress.Readers;

using master._7zip.Legacy;

namespace Hi3Helper.Data
{
    public class SevenZipTool : IDisposable
    {
        class ExtractThreadProp
        {
            public int start { get; set; }
            public int end { get; set; }
            public CArchiveDatabaseEx db { get; set; }
            public ArchiveReader x { get; set; }
            public FileStream fs { get; set; }
        }

        string inputFilePath;
        FileStream inputFileStream;

        CArchiveDatabaseEx archiveDB;
        ArchiveReader archiveReader;
        List<ExtractThreadProp> extractProp = new List<ExtractThreadProp>();
        int thread,
            extractedCount;
        byte[] buffer = new byte[0x1000];

        // Public entity
        public List<CFileItem> archiveEntries;
        public List<CFileItem> failedEntries = new List<CFileItem>();
        public int entriesCount,
                   filesCount,
                   foldersCount;
        public long totalUncompressedSize,
                    totalCompressedSize,
                    totalExtractedSize = 0;

        public void Load(string inputFile)
        {
            inputFilePath = inputFile;
            inputFileStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read);

            archiveDB = new CArchiveDatabaseEx();
            archiveReader = new ArchiveReader();

            archiveReader.Open(inputFileStream);
            archiveReader.ReadDatabase(archiveDB, null);
            archiveDB.Fill();

            archiveEntries = archiveReader.GetFiles(archiveDB).ToList();
            entriesCount = archiveEntries.Count();
            filesCount = archiveEntries.Sum(x => x.IsDir ? 0 : 1);
            foldersCount = entriesCount - filesCount;

            totalUncompressedSize = archiveEntries.Sum(f => f.Size);
            totalCompressedSize = inputFileStream.Length;
        }

        public void Dispose()
        {
            inputFileStream.Dispose();
            archiveDB.Clear();
            archiveReader.Close();
            archiveEntries?.Clear();
            failedEntries?.Clear();
        }

        public void ExtractSingleFile(string outputFile, int fileIndex)
        {
            this.thread = 1;

            if (!archiveEntries[fileIndex].IsDir)
                archiveReader.OpenStream(archiveDB, fileIndex, null).CopyTo(new FileStream(outputFile, FileMode.Create, FileAccess.Write));
        }

        Stopwatch stopWatch;
        public void ExtractToDirectory(string outputDirectory, int thread = 1, CancellationToken token = new CancellationToken())
        {
            stopWatch = Stopwatch.StartNew();
            List<Task> tasks = new List<Task>();

            try
            {
                List<Task> fallbackTasks = new List<Task>();

                totalExtractedSize = 0;
                extractedCount = 0;
                this.thread = thread;

                Console.Write(string.Format(@"Input File: {0}
Output Directory: {1}
Processing Thread: {2}
Files Count: {3}
Folders Count: {4}
Compressed Size: {5}
Uncompressed Size: {6}
Comp. > Uncomp. Ratio: {7}

Initializing...", inputFilePath, outputDirectory, this.thread,
                       filesCount, foldersCount, totalCompressedSize,
                       totalUncompressedSize, $"{(float)((float)totalCompressedSize / totalUncompressedSize) * 100}%"));

                GetThreadPartition();

                InitializeFolder(outputDirectory);

                foreach (ExtractThreadProp j in extractProp)
                {
                    j.db = new CArchiveDatabaseEx();
                    j.x = new ArchiveReader();
                    j.fs = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                    j.x.Open(j.fs);
                    j.x.ReadDatabase(j.db, null);
                    j.db.Fill();
                }

                int threadID = 0;
                foreach (ExtractThreadProp j in extractProp)
                {
                    fallbackTasks.Add(Task.Run(() => ThreadChild(inputFilePath, outputDirectory, j, threadID, token), token));

                    // Give a 1/8 seconds to delay for each thread because of the thread bug
                    Thread.Sleep(125);
                    threadID++;
                }

                Console.WriteLine($"\rExtracting...");
                Task.WhenAll(fallbackTasks).GetAwaiter().GetResult();
                fallbackTasks.Clear();

                if (failedEntries.Count != 0)
                {
                    // Console.WriteLine($"There are {failedEntries.Count} files that failed to be extracted. Including:");
                    foreach (CFileItem entry in failedEntries)
                    {
                        Console.WriteLine(entry.Name);
                    }
                    // Console.WriteLine($"This file will be extracted using fallback method, but it will takes so much memory and long time to process.");
                    GetFallbackThreadPartition(failedEntries);

                    int fallbackThreadID = 0;
                    foreach (FallbackExtractThreadProp j in fallbackExtractProp)
                    {
                        tasks.Add(Task.Run(() => FallbackThreadChild(inputFilePath, failedEntries, j, outputDirectory, threadID, token), token));

                        // Give a 1/8 seconds to delay for each thread because of the thread bug
                        Thread.Sleep(125);
                        fallbackThreadID++;
                    }

                    Task.WhenAll(tasks).GetAwaiter().GetResult();
                    tasks.Clear();
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Extraction cancelled!");
            }

            stopWatch.Stop();
            // Get the elapsed time as a TimeSpan value.
            TimeSpan ts = stopWatch.Elapsed;

            // Format and display the TimeSpan value.
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
            Console.WriteLine(elapsedTime, "RunTime");
            Console.WriteLine($"Extracted: {extractedCount} | Listed Entry: {filesCount}");
        }

        private void InitializeFolder(in string outputDirectory)
        {
            foreach (CFileItem file in archiveEntries)
            {
                string path = Path.Combine(outputDirectory, file.Name.Replace('/', '\\'));
                if (file.IsDir)
                {
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                }
            }
        }

        public class FallbackExtractThreadProp
        {
            public int start { get; set; }
            public int end { get; set; }
            public IArchive reader { get; set; }
        }

        List<FallbackExtractThreadProp> fallbackExtractProp = new List<FallbackExtractThreadProp>();

        long fallbackSingleUncompressedSize = 0;
        private void FallbackThreadChild(in string inputFilePath, List<CFileItem> inputEntry, FallbackExtractThreadProp j, in string outputDirectory, int threadID, CancellationToken token)
        {
            FileStream fileStream = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read);
            using (j.reader = ArchiveFactory.Open(fileStream, new ReaderOptions { LookForHeader = true }))
            {
                for (int i = j.start; i <= j.end; i++)
                {
                    string path = Path.Combine(outputDirectory, inputEntry[i].Name.Replace('/', '\\'));
                    IArchiveEntry entry1 = j.reader.Entries.FirstOrDefault(x => x.Key.Contains(inputEntry[i].Name));
                    // Console.WriteLine($"{entry1.Key} on threadID: {threadID}");
                    fallbackSingleUncompressedSize = entry1.Size;
                    WriteStream(entry1.OpenEntryStream(), new FileStream(path, FileMode.Create, FileAccess.Write), true, token);
                }
            }
            fileStream.Dispose();
        }

        private void GetFallbackThreadPartition(in List<CFileItem> inputEntry)
        {
            int start = 0;
            int end = 0;
            long totalSize = inputEntry.Sum(x => x.Size),
                 partitionSize = (long)Math.Ceiling((double)inputEntry.Count / this.thread),
                 aCur = 0;

            for (int i = 0; i < inputEntry.Count; i++)
            {
                aCur++;
                if (aCur > partitionSize)
                {
                    end = i;
                    aCur = 0;
                    fallbackExtractProp.Add(new FallbackExtractThreadProp { start = start, end = end });
                    start = end + 1;
                }
            }
            fallbackExtractProp.Add(new FallbackExtractThreadProp { start = start, end = inputEntry.Count - 1 });
        }

        private void GetThreadPartition()
        {
            int start = 0;
            int end = 0;
            long aCur = 0,
                 partitionSize = (long)Math.Ceiling((double)totalUncompressedSize / this.thread);

            for (int i = 0; i < entriesCount; i++)
            {
                aCur += archiveEntries[i].Size;
                if (aCur > partitionSize)
                {
                    end = i;
                    aCur = 0;
                    extractProp.Add(new ExtractThreadProp { start = start, end = end });
                    start = end + 1;
                }
            }
            extractProp.Add(new ExtractThreadProp { start = start, end = entriesCount - 1 });
        }

        private void ThreadChild(string input, string output, ExtractThreadProp j, int threadID, CancellationToken token)
        {
            for (int i = j.start; i <= j.end; i++)
            {
                string path = Path.Combine(output, archiveEntries[i].Name.Replace('/', '\\'));

                if (archiveEntries[i].IsDir)
                {
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                }
                else
                {
                    try
                    {
                        WriteStream(j.x.OpenStream(j.db, i, null), new FileStream(path, FileMode.Create, FileAccess.Write), false, token);
                    }
                    catch (ArgumentOutOfRangeException ex)
                    {
                        if (archiveEntries[i].Size == 0)
                        {
                            Console.WriteLine($"This file {archiveEntries[i].Name} on threadID {threadID} has 0 byte in size.");
                            new FileInfo(path).Create().Dispose();
                        }
                        else
                        {
                            Console.WriteLine($"This file {archiveEntries[i].Name} on threadID {threadID} is failed to be extracted.\r\nWill try with fallback method. Traceback: {ex}");
                            failedEntries.Add(archiveEntries[i]);
                        }
                    }
                    catch (AggregateException ex)
                    {
                        Console.WriteLine($"Operation cancelled on threadID: {threadID}!");
                        throw new OperationCanceledException($"Operation cancelled on threadID: {threadID}!", ex);
                    }
                    catch (OperationCanceledException ex)
                    {
                        Console.WriteLine($"Operation cancelled on threadID: {threadID}!");
                        throw new OperationCanceledException($"Operation cancelled on threadID: {threadID}!", ex);
                    }
                    catch (NotImplementedException)
                    {
                        Console.WriteLine($"{archiveEntries[i].Name} on threadID {threadID} will be extracted using fallback method (but slower).");
                        failedEntries.Add(archiveEntries[i]);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"This file {archiveEntries[i].Name} on threadID {threadID} is failed to be extracted.\r\nWill try with fallback method. Traceback: {ex}");
                        failedEntries.Add(archiveEntries[i]);
                    }
                }
            }
            j.fs.Dispose();
        }

        private void WriteStream(Stream input, Stream output, bool fallback, CancellationToken token)
        {
            // int read = 0;
            using (input) using (output)
            {

                /*
                if (fallback)
                {
                    token.ThrowIfCancellationRequested();
                    input.CopyTo(output, buffer.Length);

                    totalExtractedSize += fallbackSingleUncompressedSize;
                    OnProgressChanged(new ExtractProgress(extractedCount, filesCount, totalExtractedSize, totalUncompressedSize, stopWatch.Elapsed.TotalSeconds));
                }
                else
                {
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        output.Write(buffer, 0, read);

                        totalExtractedSize += read;
                        OnProgressChanged(new ExtractProgress(extractedCount, filesCount, totalExtractedSize, totalUncompressedSize, stopWatch.Elapsed.TotalSeconds));
                    }
                }
                */

                token.ThrowIfCancellationRequested();
                input.CopyTo(output);

                totalExtractedSize += fallback ? fallbackSingleUncompressedSize : input.Length;
                OnProgressChanged(new ExtractProgress(extractedCount, filesCount, totalExtractedSize, totalUncompressedSize, stopWatch.Elapsed.TotalSeconds));
            }
            extractedCount++;
            // OnProgressChanged(new ExtractProgress(extractedCount, filesCount, totalExtractedSize, totalUncompressedSize, stopWatch.Elapsed.TotalSeconds));
        }

        public event EventHandler<ExtractProgress> ExtractProgressChanged;
        protected virtual void OnProgressChanged(ExtractProgress e) => ExtractProgressChanged?.Invoke(this, e);
    }

    public class ExtractProgress : EventArgs
    {
        public ExtractProgress(int _totalExtractedCount, int _totalCount, long totalExtracted, long totalUncompressed, double totalSecond)
        {
            totalExtractedCount = _totalExtractedCount;
            totalCount = _totalCount;
            totalExtractedSize = totalExtracted;
            totalUncompressedSize = totalUncompressed;
            CurrentSpeed = (long)(totalExtracted / totalSecond);
        }
        public long totalExtractedCount { get; private set; }
        public long totalCount { get; private set; }
        public long totalExtractedSize { get; private set; }
        public long totalUncompressedSize { get; private set; }
        public float ProgressPercentage => ((float)totalExtractedSize / (float)totalUncompressedSize) * 100;
        public long CurrentSpeed { get; private set; }
        public TimeSpan TimeLeft => TimeSpan.FromSeconds((totalUncompressedSize - totalExtractedSize) / CurrentSpeed);
    }
}
