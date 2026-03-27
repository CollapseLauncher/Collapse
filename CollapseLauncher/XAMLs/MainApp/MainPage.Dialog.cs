using CollapseLauncher.Dialogs;
using CollapseLauncher.GameManagement.ImageBackground;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo

#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher;

public partial class MainPage
{
    private static async Task ShowFFmpegInstallationDialog()
    {
        const string doNotAskInstallFFmpegKey = "DoNotAskInstallFFmpeg";

        if (!ImageBackgroundManager.Shared.GlobalIsUseFFmpeg ||
            LauncherConfig.GetAppConfigValue(doNotAskInstallFFmpegKey))
        {
            return;
        }

        // If FFmpeg already installed, skip.
        if (ImageBackgroundManager.Shared.GlobalIsFFmpegAvailable)
        {
            return;
        }

        ContentDialogResult dialogResult =
            await SimpleDialogs.Dialog_SpawnStartUpFFmpegInstallDialog();

        if (dialogResult == ContentDialogResult.Secondary)
        {
            ImageBackgroundManager.Shared.GlobalIsUseFFmpeg = false;
            return;
        }

        await SimpleDialogs.Dialog_SpawnFfmpegInstallDialog();
    }
}
