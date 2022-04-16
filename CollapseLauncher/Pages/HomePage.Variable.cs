using System;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Logger;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;

namespace CollapseLauncher.Pages
{
    public sealed partial class HomePage : Page
    {
        public struct AdditionalFile
        {
            public string path;
            public long size;
            public string md5;
        }

        string ProgressStatusTitleName;

        long TotalPackageDownloadSize = 0;
        long DownloadedSize = 0;

        string GameDirPath;

        string GameZipUrl;
        string GameZipPath;
        string GameZipRemoteHash;
        string GameZipLocalHash;

        string GameZipVoiceUrl;
        string GameZipVoicePath;
        string GameZipVoiceRemoteHash;
        long GameZipVoiceSize;

        RegionResourceVersion VoicePackFile = new RegionResourceVersion();
        InstallManagement InstallTool;

        bool IsGameHasVoicePack;
    }
}
