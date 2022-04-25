using Microsoft.UI.Xaml.Controls;

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
        long GameZipSize;
        long GameZipRequiredSize;

        string GameZipVoiceUrl;
        string GameZipVoicePath;
        string GameZipVoiceRemoteHash;
        long GameZipVoiceSize;
        long GameZipVoiceRequiredSize;

        RegionResourceVersion VoicePackFile = new RegionResourceVersion();
        InstallManagement InstallTool;

        bool IsGameHasVoicePack;
    }
}
