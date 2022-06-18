using Hi3Helper.Preset;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    public class PostInstallCheck
    {
        List<PkgVersionProperties> Manifest;
        List<PkgVersionProperties> BrokenManifest;
        class ThreadProperty
        {
            public int FileStart { get; set; }
            public int FileEnd { get; set; }
            public int Count => (FileEnd - FileStart) + 1;
        }

        CancellationToken ThreadToken;

        List<ThreadProperty> ThreadChildren = new List<ThreadProperty>();

        string GameBasePath;

        int Thread = 0;
        int ReadCount = 0;

        long TotalManifestSize = 0,
             Read = 0;

        Stopwatch sW;

        public PostInstallCheck(string GameBasePath, in List<PkgVersionProperties> Manifest, int Thread, CancellationToken ThreadToken)
        {
            this.Manifest = Manifest;
            this.GameBasePath = GameBasePath;
            this.Thread = Thread;
            this.ThreadToken = ThreadToken;
            this.TotalManifestSize = this.Manifest.Sum(x => x.fileSize);
        }

        public async Task<List<PkgVersionProperties>> StartCheck()
        {
            BrokenManifest = new List<PkgVersionProperties>();
            sW = Stopwatch.StartNew();
            Read = 0;
            GetThreadChildren();
            await StartThreadChildren();

            return BrokenManifest;
        }

        async Task StartThreadChildren()
        {
            List<Task> threads = new List<Task>();

            foreach (ThreadProperty p in ThreadChildren)
                threads.Add(Task.Run(() => ThreadChild(p)));

            await Task.WhenAll(threads);
            threads.Clear();
        }

        void ThreadChild(ThreadProperty ThreadProp)
        {
            string FilePath,
                   FileRemoteHash,
                   FileLocalHash;

            FileInfo FileProp;
            Stream FilePropStream;

            for (int i = ThreadProp.FileStart; i <= ThreadProp.FileEnd; i++)
            {
                ReadCount++;
                this.ThreadToken.ThrowIfCancellationRequested();
                FilePath = Path.Combine(this.GameBasePath, NormalizePath(this.Manifest[i].remoteName));
                FileRemoteHash = this.Manifest[i].md5.ToLower();

                FileProp = new FileInfo(FilePath);

                if (FileProp.Exists)
                {
                    using (FilePropStream = FileProp.OpenRead())
                        FileLocalHash = CreateMD5(FilePropStream).ToLower();

                    if (FileRemoteHash != FileLocalHash)
                    {
                        LogWriteLine($"File: {this.Manifest[i].remoteName} is corrupted!", Hi3Helper.LogType.Warning);
                        BrokenManifest.Add(this.Manifest[i]);
                    }
                }
                else
                {
                    LogWriteLine($"File: {this.Manifest[i].remoteName} is missing!", Hi3Helper.LogType.Warning);
                    BrokenManifest.Add(this.Manifest[i]);
                }

                Read += this.Manifest[i].fileSize;
                OnProgressChanged(new PostInstallCheckProp(ReadCount, 0, Read, TotalManifestSize, sW.Elapsed.TotalSeconds));
            }
        }

        void GetThreadChildren()
        {
            int start = 0,
                end = 0;
            long aCur = 0,
                 partitionSize = (long)Math.Ceiling((double)this.TotalManifestSize / this.Thread);

            int i;
            for (i = 0; i < this.Manifest.Count; i++)
            {
                aCur += this.Manifest[i].fileSize;
                if (aCur > partitionSize)
                {
                    end = i;
                    aCur = 0;
                    ThreadChildren.Add(new ThreadProperty { FileStart = start, FileEnd = end });
                    start = end + 1;
                }
            }
            ThreadChildren.Add(new ThreadProperty { FileStart = start, FileEnd = this.Manifest.Count - 1 });
        }


        public event EventHandler<PostInstallCheckProp> PostInstallCheckChanged;
        protected virtual void OnProgressChanged(PostInstallCheckProp e) => PostInstallCheckChanged?.Invoke(this, e);

    }
    public class PostInstallCheckProp : EventArgs
    {
        public PostInstallCheckProp(int TotalReadCount, int TotalCount, long TotalReadSize, long TotalCheckSize, double TotalSecond)
        {
            this.TotalReadCount = TotalReadCount;
            this.TotalCount = TotalCount;
            this.TotalReadSize = TotalReadSize;
            this.TotalCheckSize = TotalCheckSize;
            this.CurrentSpeed = (long)(TotalReadSize / TotalSecond);
        }
        public long TotalCount { get; private set; }
        public long TotalReadCount { get; private set; }
        public long TotalReadSize { get; private set; }
        public long TotalCheckSize { get; private set; }
        public float ProgressPercentage => ((float)TotalReadSize / (float)TotalCheckSize) * 100;
        public long CurrentSpeed { get; private set; }
        public TimeSpan TimeLeft => TimeSpan.FromSeconds((TotalReadSize - TotalCheckSize) / CurrentSpeed);
    }
}
