using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.FileDialogCOM;
using Hi3Helper.Data;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Locale;

namespace CollapseLauncher
{
    internal partial class FileMigrationProcess
    {
        internal static async Task<FileMigrationProcess> CreateJob(string dialogTitle, string inputPath, string outputPath = null, CancellationTokenSource token = default, bool showWarningMessage = true)
        {
            // Normalize Path (also normalize path from '/' separator)
            inputPath = ConverterTool.NormalizePath(inputPath);

            // Check whether the input is a file or not.
            bool isFileTransfer = File.Exists(inputPath) && !inputPath.StartsWith('\\');
            outputPath = await InitializeAndCheckOutputPath(dialogTitle, inputPath, outputPath, isFileTransfer);
            if (outputPath == null) return null;

            if (FileDialogHelper.IsRootPath(outputPath))
            {
                await SimpleDialogs.SpawnDialog(Lang._HomePage.InstallFolderRootTitle,
                                  Lang._HomePage.InstallFolderRootSubtitle,
                                  null,
                                  Lang._Misc.Close,
                                  null, null, ContentDialogButton.Close, ContentDialogTheme.Error);
                return null;
            }

            if (!showWarningMessage)
            {
                return new FileMigrationProcess(dialogTitle, inputPath, outputPath, isFileTransfer, token);
            }

            return await ShowNotCancellableProcedureMessage() == ContentDialogResult.None ? null : new FileMigrationProcess(dialogTitle, inputPath, outputPath, isFileTransfer, token);
        }

        private static async ValueTask<string> InitializeAndCheckOutputPath(string dialogTitle, string inputPath, string outputPath, bool isFileTransfer)
        {
            if (!string.IsNullOrEmpty(outputPath)
                && ConverterTool.IsUserHasPermission(outputPath)
                && !IsOutputPathSameAsInput(inputPath, outputPath, isFileTransfer))
                return outputPath;

            return await BuildCheckOutputPathUI(dialogTitle, inputPath, outputPath, isFileTransfer);
        }

        private static bool IsOutputPathSameAsInput(string inputPath, string outputPath, bool isFilePath)
        {
            bool isStringEmpty = string.IsNullOrEmpty(outputPath);

            if (!isFilePath) inputPath = Path.GetDirectoryName(inputPath);
            bool isPathEqual = inputPath.AsSpan().TrimEnd("\\/").SequenceEqual(outputPath.AsSpan().TrimEnd("\\/"));

            return isStringEmpty || isPathEqual;
        }

        private static async Task<ContentDialogResult> ShowNotCancellableProcedureMessage() => await SimpleDialogs.Dialog_WarningOperationNotCancellable();

    }
}
