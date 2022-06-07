using Hi3Helper.Shared.ClassStruct;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.InvokeProp;

namespace Hi3Helper.Data
{
    public class HPatchUtil
    {
        uint bufSize = 0x10000;
        long WriteSize = 0;
        CancellationToken Token = new CancellationToken();

        public void HPatchFile(string inputFile, string diffFile, string outputFile) =>
                    GetEnumStatus(HPatch(
                        inputFile, diffFile, outputFile,
                        false, new UIntPtr(bufSize), 0,
                        new FileInfo(diffFile).Length));

        public void HPatchDir(string inputPath, string diffFile, string outputPath)
        {
            string[] args = new string[] { "_", "-f", "-s", inputPath, diffFile, outputPath };

            GetEnumStatus(HPatchCommand(args.Length, args));
        }

        public async Task HPatchDir(string inputPath, string inputManifestURL, string diffFile, string outputPath, CancellationToken Token = new CancellationToken())
        {
            FileSystemWatcher fsWatcher;
            this.Token = Token;
            string LastOutputPath = outputPath;

            List<FileProperties> RecipeList = await BuildManifest(inputManifestURL);

            outputPath = ConverterTool.NormalizePath(outputPath) + "_Ingredients";

            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, true);

            Directory.CreateDirectory(outputPath);

            await CopyRecipe(inputPath, outputPath, RecipeList);
            await RepairRecipe(outputPath, inputManifestURL, await VerifyRecipe(outputPath, inputManifestURL, RecipeList));

            inputPath = outputPath;
            outputPath = LastOutputPath;

            fsWatcher = new FileSystemWatcher()
            {
                Path = outputPath,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                             | NotifyFilters.Size,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            fsWatcher.Created += FsWatcher_ObjectCreated;

            string[] args = new string[] { "_", "-f", "-s", inputPath, diffFile, outputPath };

            GetEnumStatus(HPatchCommand(args.Length, args));

            WriteSize += new FileInfo(lastName).Length;

            Directory.Delete(inputPath, true);

            Console.WriteLine($"{ConverterTool.SummarizeSizeSimple(WriteSize)}");

            fsWatcher.Created -= FsWatcher_ObjectCreated;
        }

        private async Task RepairRecipe(string OutputDir, string inputManifestURL, List<FileProperties> Entries)
        {
            string OutputPath;
            string InputURL;
            HttpClientHelper http = new HttpClientHelper();
            if (Entries.Count == 0) return;

            foreach (FileProperties Entry in Entries)
            {
                Token.ThrowIfCancellationRequested();
                OutputPath = Path.Combine(OutputDir, Entry.FileName);
                InputURL = inputManifestURL + Entry.FileName;

                if (!Directory.Exists(Path.GetDirectoryName(OutputPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

                if (File.Exists(OutputPath))
                    File.Delete(OutputPath);

                if (Entry.FileSize >= 20 << 20)
                    await http.DownloadFileAsync(InputURL, OutputPath, 8, Token);
                else
                    await http.DownloadFileAsync(InputURL, new FileStream(OutputPath, FileMode.Create, FileAccess.Write), Token, -1, -1, true);
            }
        }

        private async Task<List<FileProperties>> VerifyRecipe(string OutputDir, string inputManifestURL, List<FileProperties> Entries)
        {
            string LocalHash;
            string OutputPath;
            bool IsFileExist;
            List<FileProperties> BrokenOut = new List<FileProperties>();

            foreach (FileProperties Entry in Entries)
            {
                OutputPath = Path.Combine(OutputDir, Entry.FileName);

                IsFileExist = File.Exists(OutputPath);

                if (IsFileExist)
                {
                    using (FileStream fs = new FileStream(OutputPath, FileMode.Open, FileAccess.Read))
                    {
                        Console.Write($"Verifying Recipe: {Entry.FileName}");
                        await Task.Run(() =>
                        {
                            LocalHash = Entry.DataType != FileType.Blocks ?
                                ConverterTool.BytesToCRC32Simple(fs) :
                                ConverterTool.CreateMD5(fs);

                            if (LocalHash.ToLower() != Entry.CurrCRC)
                            {
                                Console.WriteLine($" BROKEN!");
                                BrokenOut.Add(Entry);
                            }
                            else
                                Console.WriteLine($" Success!");
                        });
                    }
                }
                else
                {
                    Console.WriteLine($"Verifying Recipe: {Entry.FileName} MISSING!");
                    BrokenOut.Add(Entry);
                }
            }

            return BrokenOut;
        }

        private async Task CopyRecipe(string InputDir, string OutputDir, List<FileProperties> Entries)
        {
            string InputPath;
            string OutputPath;
            foreach (FileProperties Entry in Entries)
            {
                Token.ThrowIfCancellationRequested();
                InputPath = Path.Combine(InputDir, Entry.FileName);
                OutputPath = Path.Combine(OutputDir, Entry.FileName);

                if (!Directory.Exists(Path.GetDirectoryName(OutputPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

                if (File.Exists(InputPath))
                {
                    Console.Write($"Copying Recipe: {Entry.FileName}");
                    await Copy(InputPath, OutputPath);
                    Console.WriteLine($" Done!");
                }
            }
        }

        private async Task Copy(string InputPath, string OutputPath, bool UseCopyTo = true)
        {
            byte[] buffer = new byte[4 << 10];
            using (FileStream source = new FileStream(InputPath, FileMode.Open, FileAccess.Read))
            {
                using (FileStream dest = new FileStream(OutputPath, FileMode.Create, FileAccess.Write))
                {
                    if (UseCopyTo)
                        await source.CopyToAsync(dest, Token);
                    else
                    {
                        int read = 0;
                        while ((read = source.Read(buffer)) > 0)
                        {
                            Token.ThrowIfCancellationRequested();
                            dest.Write(buffer, 0, read);
                        }
                    }
                }
            }
        }

        private async Task<List<FileProperties>> BuildManifest(string manifestURL)
        {
            List<FileProperties> _out = new List<FileProperties>();
            List<FilePropertiesRemote> remoteProp = new List<FilePropertiesRemote>();

            HttpClientHelper http = new HttpClientHelper();
            using (MemoryStream _Response = new MemoryStream())
            {
                await http.DownloadFileAsync(manifestURL + "index.json", _Response, this.Token, -1, -1, false);
                remoteProp = JsonConvert.DeserializeObject<List<FilePropertiesRemote>>(Encoding.UTF8.GetString(_Response.ToArray()));
            }

            foreach (FilePropertiesRemote Entry in remoteProp)
            {
                switch (Entry.FT)
                {
                    case FileType.Generic:
                        {
                            _out.Add(new FileProperties
                            {
                                FileName = Entry.N,
                                FileSize = Entry.S,
                                CurrCRC = Entry.CRC,
                                DataType = FileType.Generic
                            });
                        }
                        break;
                    case FileType.Blocks:
                        {
                            _out.AddRange(BuildBlockManifest(Entry.BlkC, Entry.N));
                        }
                        break;
                }
            }

            return _out;
        }

        private List<FileProperties> BuildBlockManifest(List<Preset.XMFDictionaryClasses.XMFBlockList> BlockC, string BaseName)
        {
            string Name;
            List<FileProperties> _out = new List<FileProperties>();

            foreach (Preset.XMFDictionaryClasses.XMFBlockList Block in BlockC)
            {
                Name = BaseName + "/" + Block.BlockHash + ".wmv";
                _out.Add(new FileProperties
                {
                    FileName = Name,
                    FileSize = Block.BlockSize,
                    CurrCRC = Block.BlockHash
                });
            }

            return _out;
        }

        string lastName = null;
        private void FsWatcher_ObjectCreated(object sender, FileSystemEventArgs e)
        {
            if (!Directory.Exists(e.FullPath))
            {
                Console.WriteLine($"Created: {e.Name} {WriteSize}");
                if (lastName != null)
                    WriteSize += new FileInfo(lastName).Length;
                lastName = e.FullPath;
            }
        }

        void GetEnumStatus(int i)
        {
            switch ((HPatchUtilStat)i)
            {
                case HPatchUtilStat.HPATCH_SUCCESS: return;
                case HPatchUtilStat.HPATCH_MEM_ERROR:
                    throw new OutOfMemoryException($"Out Of Memory. ERRMSG: {(HPatchUtilStat)i}");
                default:
                    throw new Exception($"Unhandled Error. ERRMSG: {(HPatchUtilStat)i}");
            }
        }
    }
}
