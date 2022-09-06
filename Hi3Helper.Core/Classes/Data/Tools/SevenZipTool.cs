using SevenZipExtractor;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hi3Helper.Data
{
    public enum ArchiveType { SevenZip, Zip, AutoLoad7Zip }
    public class SevenZipTool : IDisposable
    {
        class ExtractThreadProp
        {
            public int start { get; set; }
            public int end { get; set; }
            public ZipArchive xZip { get; set; }
            public FileStream fs { get; set; }
        }

        string inputFilePath;
        ArchiveType inputFileType;
        FileStream inputFileStream;
        ArchiveFile archiveReader7Zip;

        ZipArchive archiveReaderZip;

        List<ExtractThreadProp> extractProp = new List<ExtractThreadProp>();
        int thread,
            extractedCount;

        // Public entity
        public IList<Entry> archiveEntries7Zip;
        public ReadOnlyCollection<ZipArchiveEntry> archiveEntriesZip;
        public int entriesCount,
                   filesCount,
                   foldersCount;
        public long totalUncompressedSize,
                    totalCompressedSize,
                    totalExtractedSize = 0;

        public void AutoLoad(string inputFile)
        {
            switch (Path.GetExtension(inputFile).ToLower())
            {
                case ".7z":
                case ".zip":
                    LoadAuto7Zip(inputFile);
                    break;
                // case ".zip":
                //     LoadZip(inputFile);
                //     break;
                default:
                    throw new FormatException($"Format {Path.GetExtension(inputFile)} is unsupported!");
            }
        }

        public void LoadAuto7Zip(string InputFile)
        {
            inputFilePath = InputFile;
            inputFileStream = new FileStream(InputFile, FileMode.Open, FileAccess.Read);
            archiveReader7Zip = new ArchiveFile(inputFileStream, null, @"Lib\7z.dll");
            archiveEntries7Zip = archiveReader7Zip.Entries;
            entriesCount = archiveEntries7Zip.Count();
            filesCount = archiveEntries7Zip.Sum(x => x.IsFolder ? 0 : 1);
            foldersCount = entriesCount - filesCount;

            totalUncompressedSize = archiveEntries7Zip.Sum(f => (long)f.Size);
            totalCompressedSize = archiveEntries7Zip.Sum(f => (long)f.PackedSize);

            inputFileType = ArchiveType.SevenZip;
        }

        public void LoadZip(string inputFile)
        {
            inputFilePath = inputFile;
            inputFileStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read);

            archiveReaderZip = ZipFile.OpenRead(inputFile);
            archiveEntriesZip = archiveReaderZip.Entries;

            entriesCount = archiveEntriesZip.Count;
            filesCount = entriesCount;

            totalUncompressedSize = archiveReaderZip.Entries.Sum(x => x.Length);
            totalCompressedSize = archiveReaderZip.Entries.Sum(x => x.CompressedLength);

            inputFileType = ArchiveType.Zip;
        }

        public long GetUncompressedSize(string inputFile, ArchiveType type = ArchiveType.SevenZip)
        {
            inputFilePath = inputFile;
            inputFileStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read);

            switch (Path.GetExtension(inputFile).ToLower())
            {
                case ".7z":
                case ".zip":
                    LoadAuto7Zip(inputFile);
                    return totalUncompressedSize;
                // case ".zip":
                //    LoadZip(inputFile);
                //    return totalUncompressedSize;
                default:
                    throw new FormatException($"Format {Path.GetExtension(inputFile)} is unsupported!");
            }
        }

        public void Dispose()
        {
            inputFileStream.Dispose();
            archiveReaderZip?.Dispose();
        }

        Stopwatch stopWatch;
        public void ExtractToDirectory(string outputDirectory, int thread = 1, CancellationToken token = new CancellationToken())
        {
            stopWatch = Stopwatch.StartNew();

            try
            {
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

Initializing...", inputFilePath, outputDirectory,
                       inputFileType == ArchiveType.AutoLoad7Zip ? $"Auto ({Environment.ProcessorCount})" : $"{this.thread}",
                       filesCount, foldersCount, totalCompressedSize,
                       totalUncompressedSize, $"{(float)((float)totalCompressedSize / totalUncompressedSize) * 100}%"));

                switch (inputFileType)
                {
                    case ArchiveType.SevenZip:
                        StartAuto7ZipThread(outputDirectory, token);
                        break;
                    case ArchiveType.Zip:
                        StartZipThreads(outputDirectory, thread, token);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Extraction cancelled!");
                throw new OperationCanceledException($"Extraction cancelled!");
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

        private void StartAuto7ZipThread(string OutputDirectory, CancellationToken Token)
        {
            stopWatch = Stopwatch.StartNew();
            string _outPath;
            archiveReader7Zip.ExtractProgress += ArchiveReaderAuto7ZipAdapter;
            using (archiveReader7Zip)
            {
                archiveReader7Zip.Extract(entry =>
                {
                    _outPath = Path.Combine(OutputDirectory, entry.FileName);
                    return _outPath;
                }, Token);
            }
            archiveReader7Zip.ExtractProgress -= ArchiveReaderAuto7ZipAdapter;
        }

        private void ArchiveReaderAuto7ZipAdapter(object sender, ArchiveFile.ExtractProgressProp e)
        {
            OnProgressChanged(new ExtractProgress(extractedCount, filesCount, (long)e.TotalRead, (long)e.TotalSize, stopWatch.Elapsed.TotalSeconds));
        }

        private void StartZipThreads(string outputDirectory, int thread, CancellationToken token)
        {
            List<Task> tasks = new List<Task>();
            List<Task> fallbackTasks = new List<Task>();

            GetThreadPartitionZip();

            InitializeFolderZip(outputDirectory);

            foreach (ExtractThreadProp j in extractProp)
            {
                j.fs = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                j.xZip = new ZipArchive(j.fs, ZipArchiveMode.Read);
            }

            int threadID = 0;

            foreach (ExtractThreadProp j in extractProp)
            {
                fallbackTasks.Add(Task.Run(() => ThreadChild(inputFilePath, outputDirectory, j, threadID, token), token));

                // Give a 1/8 seconds to delay for each thread because of the thread bug
                Thread.Sleep(125);
                threadID++;
            }

            Task.WhenAll(fallbackTasks).GetAwaiter().GetResult();
            fallbackTasks.Clear();
        }

        private void InitializeFolderZip(in string outputDirectory)
        {
            foreach (ZipArchiveEntry file in archiveEntriesZip)
            {
                string path = Path.Combine(outputDirectory, Path.GetDirectoryName(file.FullName.Replace('/', '\\')));

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
            }
        }

        private void GetThreadPartitionZip()
        {
            int start = 0;
            int end = 0;
            long aCur = 0,
                 partitionSize = (long)Math.Ceiling((double)totalUncompressedSize / this.thread);

            for (int i = 0; i < entriesCount; i++)
            {
                aCur += archiveEntriesZip[i].Length;
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

        private void StartZipThreadChild(string input, string output, ExtractThreadProp j, int threadID, CancellationToken token)
        {
            for (int i = j.start; i <= j.end; i++)
            {
                string path = Path.Combine(output, archiveEntriesZip[i].FullName.Replace('/', '\\'));
                using (FileStream localStream = new FileStream(path, FileMode.Create,
                    FileAccess.Write, FileShare.None))
                {
                    try
                    {
                        Console.WriteLine(j.xZip.Entries[i].Name);
                        using (Stream stream = j.xZip.Entries[i].Open())
                            WriteStream(stream, localStream, j.xZip.Entries[i].Length, token);
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
                    catch (Exception ex)
                    {
                        throw new OperationCanceledException($"Unhandled Exception on threadID: {threadID}!", ex);
                    }
                }
            }
            j.xZip.Dispose();
        }

        private void ThreadChild(string input, string output, ExtractThreadProp j, int threadID, CancellationToken token)
        {
            switch (inputFileType)
            {
                case ArchiveType.Zip:
                    StartZipThreadChild(input, output, j, threadID, token);
                    break;
            }
        }

        private void WriteStream(Stream input, Stream output, long fileSize, CancellationToken token)
        {
            using (input) using (output)
            {
                // token.ThrowIfCancellationRequested();
                input.CopyTo(output);

                totalExtractedSize += fileSize;
                OnProgressChanged(new ExtractProgress(extractedCount, filesCount, totalExtractedSize, totalUncompressedSize, stopWatch.Elapsed.TotalSeconds));

                input.Dispose();
                output.Dispose();
            }
            extractedCount++;
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
