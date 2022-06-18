using System;
using System.Threading;
using System.IO;
using System.Diagnostics;

namespace ApplyUpdate
{
    internal class Program
    {
        public static string execPath = Process.GetCurrentProcess().MainModule.FileName;
        public static string workingDir = Path.GetDirectoryName(execPath);
        public static string sourcePath = Path.Combine(workingDir, Path.GetFileName(execPath));
        public static string applyName = Path.Combine(workingDir, "ApplyUpdate.exe");
        public static string launcherPath = Path.Combine(workingDir, "CollapseLauncher.exe");

        static void Main(string[] args)
        {
            string BasePath, TargetPath, TempFolder;
            if (args.Length == 0)
                TempFolder = Path.Combine(workingDir, "_Temp");
            else
                TempFolder = Path.Combine(args[0], "_Temp");
            Console.WriteLine("Put 5 seconds delay before applying update...");
            Thread.Sleep(5000);
            if (Directory.Exists(TempFolder))
            {
                foreach (string file in Directory.EnumerateFiles(TempFolder, "*", SearchOption.AllDirectories))
                {
                    BasePath = file.Substring(TempFolder.Length + 1);
                    TargetPath = Path.Combine(workingDir, BasePath);

                    if (!Directory.Exists(Path.GetDirectoryName(TargetPath)))
                        Directory.CreateDirectory(Path.GetDirectoryName(TargetPath));

                    if (Path.GetFileNameWithoutExtension(file) != "ApplyUpdate")
                        File.Copy(file, TargetPath, true);
                }

                new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        Verb = "runas",
                        FileName = launcherPath,
                        WorkingDirectory = workingDir
                    }
                }.Start();
            }
        }
    }
}
