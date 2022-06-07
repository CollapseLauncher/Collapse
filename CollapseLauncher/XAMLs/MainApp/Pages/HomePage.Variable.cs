using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml.Controls;

namespace CollapseLauncher.Pages
{
    public sealed partial class HomePage : Page
    {
        string GameDirPath;

        string GameZipUrl;
        string GameZipPath;
        string GameZipRemoteHash;
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
