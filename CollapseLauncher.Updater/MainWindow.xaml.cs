using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

using Newtonsoft.Json;

using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Logger;

namespace CollapseLauncher.Updater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    enum UpdaterStatus { Reindex, Update }
    public partial class MainWindow : Window
    {
        static UpdaterStatus status;
        static string[] argument;

        [STAThread]
        [DebuggerNonUserCode]
        public static void Main(string[] args)
        {
            argument = args;

            if (argument.Length > 0)
            {
                if (argument[0].ToLower() == "reindex" && argument.Length > 2)
                {
                    new Reindexer(argument[1], argument[2]).RunReindex();
                    return;
                }

                if (argument[0].ToLower() == "update")
                    new Application() { StartupUri = new Uri("MainWindow.xaml", UriKind.Relative) }.Run();
            }
        }

        public MainWindow()
        {
            InitializeComponent();
        }
    }

    public class Reindexer
    {
        string filePath;
        string clientVer;
        long reindexTime;

        class Prop
        {
            public string ver { get; set; }
            public long time { get; set; }
            public List<fileProp> f { get; set; }
        }

        class fileProp
        {
            public string p { get; set; }
            public string crc { get; set; }
            public long s { get; set; }
        }

        public Reindexer(string filePath, string clientVer)
        {
            Hi3Helper.InvokeProp.InitializeConsole();
            this.filePath = NormalizePath(filePath);
            this.reindexTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            this.clientVer = clientVer;

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
                if (Path.GetFileName(file).ToLower() != "CollapseLauncher.Updater.exe")
                {
                    nameRoot = file.Substring(baseLength).Replace('\\', '/');
                    fileCrc = BytesToCRC32Simple(fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read));
                    Prop.f.Add(new fileProp { p = nameRoot, crc = fileCrc, s = fileStream.Length });
                    LogWriteLine($"{nameRoot} -> {fileCrc}");
                }
            }

            File.WriteAllText(Path.Combine(this.filePath, "fileindex.json"), JsonConvert.SerializeObject(Prop));
        }
    }
}
