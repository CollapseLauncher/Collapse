using CollapseLauncher.Dialogs;
using Hi3Helper.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher
{
    internal partial class FileMigrationProcess
    {
        internal static async Task<FileMigrationProcess> CreateJob(UIElement parentUI, string dialogTitle, string inputPath, string outputPath = null, CancellationTokenSource token = default, bool showWarningMessage = true)
        {
            // Check whether the input is a file or not.
            bool isFileTransfer = File.Exists(inputPath) && !inputPath.StartsWith('\\');
            outputPath = await InitializeAndCheckOutputPath(parentUI, dialogTitle, outputPath, isFileTransfer);
            if (outputPath == null) return null;

            if (showWarningMessage)
                if (await ShowNotCancellableProcedureMessage(parentUI) == ContentDialogResult.None)
                    return null;

            return new FileMigrationProcess(parentUI, dialogTitle, inputPath, outputPath, isFileTransfer, token);
        }

        private static async ValueTask<string> InitializeAndCheckOutputPath(UIElement parentUI, string dialogTitle, string outputPath, bool isFileTransfer)
        {
            if (!string.IsNullOrEmpty(outputPath) && ConverterTool.IsUserHasPermission(outputPath))
                return outputPath;

            return await BuildCheckOutputPathUI(parentUI, dialogTitle, outputPath, isFileTransfer);
        }

        private static async Task<ContentDialogResult> ShowNotCancellableProcedureMessage(UIElement parentUI) => await SimpleDialogs.Dialog_WarningOperationNotCancellable(parentUI);

    }
}
