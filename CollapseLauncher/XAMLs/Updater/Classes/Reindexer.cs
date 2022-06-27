using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    public class Reindexer : Updater
    {
        string filePath;
        string clientVer;
        long reindexTime;
        byte downloadThread;

        public Reindexer(string filePath, string clientVer, byte downloadThread) : base(filePath, "", downloadThread)
        {
            this.filePath = NormalizePath(filePath);
            this.reindexTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            this.clientVer = clientVer;
            this.downloadThread = downloadThread;

            if (File.Exists(Path.Combine(this.filePath, "fileindex.json")))
                File.Delete(Path.Combine(this.filePath, "fileindex.json"));
        }

        public void RunReindex()
        {
            int baseLength = filePath.Length + 1;
            string nameRoot, fileCrc;
            Prop Prop = new Prop() { ver = this.clientVer, time = this.reindexTime, f = new List<fileProp>() };
            FileStream fileStream;
            foreach (string file in Directory.GetFiles(filePath, "*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) != "CollapseLauncher.Updater.exe")
                {
                    nameRoot = file.Substring(baseLength).Replace('\\', '/');
                    fileCrc = CreateMD5(fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read));
                    Prop.f.Add(new fileProp { p = nameRoot, crc = fileCrc, s = fileStream.Length });
                    LogWriteLine($"{nameRoot} -> {fileCrc}");
                }
            }

            File.WriteAllText(Path.Combine(this.filePath, "fileindex.json"), JsonConvert.SerializeObject(Prop, Formatting.Indented));
        }
    }
}
