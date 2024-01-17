﻿using CollapseLauncher.Dialogs;
using Hi3Helper.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher
{
    internal partial class FileMigrationProcess
    {
        internal static async Task<FileMigrationProcess> CreateJob(UIElement parentUI, string dialogTitle, string inputPath, string outputPath = null, CancellationTokenSource token = default, bool showWarningMessage = true)
        {
            // Normalize Path (also normalize path from '/' separator)
            inputPath = ConverterTool.NormalizePath(inputPath);

            // Check whether the input is a file or not.
            bool isFileTransfer = File.Exists(inputPath) && !inputPath.StartsWith('\\');
            outputPath = await InitializeAndCheckOutputPath(parentUI, dialogTitle, inputPath, outputPath, isFileTransfer);
            if (outputPath == null) return null;

            if (showWarningMessage)
                if (await ShowNotCancellableProcedureMessage(parentUI) == ContentDialogResult.None)
                    return null;

            return new FileMigrationProcess(parentUI, dialogTitle, inputPath, outputPath, isFileTransfer, token);
        }

        private static async ValueTask<string> InitializeAndCheckOutputPath(UIElement parentUI, string dialogTitle, string inputPath, string outputPath, bool isFileTransfer)
        {
            if (!string.IsNullOrEmpty(outputPath)
                && ConverterTool.IsUserHasPermission(outputPath)
                && !IsOutputPathSameAsInput(inputPath, outputPath, isFileTransfer))
                return outputPath;

            return await BuildCheckOutputPathUI(parentUI, dialogTitle, inputPath, outputPath, isFileTransfer);
        }

        private static bool IsOutputPathSameAsInput(string inputPath, string outputPath, bool isFilePath)
        {
            bool isStringEmpty = string.IsNullOrEmpty(outputPath);

            if (!isFilePath) inputPath = Path.GetDirectoryName(inputPath);
            bool isPathEqual = inputPath.AsSpan().TrimEnd('\\').SequenceEqual(outputPath.AsSpan().TrimEnd('\\'));

            if (isStringEmpty) return true;
            return isPathEqual;
        }

        private static async Task<ContentDialogResult> ShowNotCancellableProcedureMessage(UIElement parentUI) => await SimpleDialogs.Dialog_WarningOperationNotCancellable(parentUI);

    }
}
