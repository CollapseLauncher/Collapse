using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using Hi3Helper;
using Hi3Helper.Data;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher
{
    internal partial class FileMigrationProcess
    {
        private async Task MoveWriteFile(FileMigrationProcessUIRef uiRef, FileInfo inputFile, FileInfo outputFile, CancellationToken token)
        {
            int bufferSize = 1 << 18; // 256 kB Buffer

            if (inputFile.Length < bufferSize)
                bufferSize = (int)inputFile.Length;

            bool isUseArrayPool = Environment.ProcessorCount * bufferSize > 2 << 20;
            byte[] buffer = isUseArrayPool ? ArrayPool<byte>.Shared.Rent(bufferSize) : new byte[bufferSize];

            try
            {
                await using (FileStream inputStream = inputFile.OpenRead())
                await using (FileStream outputStream = outputFile.Exists && outputFile.Length <= inputFile.Length ? outputFile.Open(FileMode.Open) : outputFile.Create())
                {
                    // Just in-case if the previous move is incomplete, then update and seek to the last position.
                    if (outputFile.Length <= inputStream.Length && outputFile.Length >= bufferSize)
                    {
                        // Do check by comparing the first and last 128K data of the file
                        Memory<byte> firstCompareInputBytes = new byte[bufferSize];
                        Memory<byte> firstCompareOutputBytes = new byte[bufferSize];
                        Memory<byte> lastCompareInputBytes = new byte[bufferSize];
                        Memory<byte> lastCompareOutputBytes = new byte[bufferSize];

                        // Seek to the first data
                        inputStream.Position = 0;
                        await inputStream.ReadExactlyAsync(firstCompareInputBytes);
                        outputStream.Position = 0;
                        await outputStream.ReadExactlyAsync(firstCompareOutputBytes);

                        // Seek to the last data
                        long lastPos = outputStream.Length - bufferSize;
                        inputStream.Position = lastPos;
                        await inputStream.ReadExactlyAsync(lastCompareInputBytes);
                        outputStream.Position = lastPos;
                        await outputStream.ReadExactlyAsync(lastCompareOutputBytes);

                        bool isMatch = firstCompareInputBytes.Span.SequenceEqual(firstCompareOutputBytes.Span)
                            && lastCompareInputBytes.Span.SequenceEqual(lastCompareOutputBytes.Span);

                        // If the buffers don't match, then start the copy from the beginning
                        if (!isMatch)
                        {
                            inputStream.Position = 0;
                            outputStream.Position = 0;
                        }
                        else
                        {
                            UpdateSizeProcessed(uiRef, outputStream.Length);
                        }
                    }

                    await MoveWriteFileInner(uiRef, inputStream, outputStream, buffer, token);
                }

                inputFile.IsReadOnly = false;
                inputFile.Delete();
            }
            catch { throw; } // Re-throw to 
            finally
            {
                if (isUseArrayPool) ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private async Task MoveWriteFileInner(FileMigrationProcessUIRef uiRef, FileStream inputStream, FileStream outputStream, byte[] buffer, CancellationToken token)
        {
            int read;
            while ((read = await inputStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
            {
                await outputStream.WriteAsync(buffer, 0, read, token);
                UpdateSizeProcessed(uiRef, read);
            }
        }

        private async ValueTask<bool> IsOutputPathSpaceSufficient(string inputPath, string outputPath)
        {
            DriveInfo inputDriveInfo = new DriveInfo(Path.GetPathRoot(inputPath));
            DriveInfo outputDriveInfo = new DriveInfo(Path.GetPathRoot(outputPath));

            this.TotalFileSize = await Task.Run(() =>
            {
                if (this.isFileTransfer)
                {
                    FileInfo fileInfo = new FileInfo(inputPath);
                    return fileInfo.Length;
                }

                DirectoryInfo directoryInfo = new DirectoryInfo(inputPath);
                long returnSize = directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(x =>
                {
                    this.TotalFileCount++;
                    return x.Length;
                });
                return returnSize;
            });

            if (IsSameOutputDrive = inputDriveInfo.Name == outputDriveInfo.Name)
                return true;

            bool isSpaceSufficient = outputDriveInfo.TotalFreeSpace < this.TotalFileSize;
            if (!isSpaceSufficient)
            {
                string errStr = $"Free Space on {outputDriveInfo.Name} is not sufficient! (Free space: {outputDriveInfo.TotalFreeSpace}, Req. Space: {this.TotalFileSize}, Drive: {outputDriveInfo.Name})";
                Logger.LogWriteLine(errStr, LogType.Error, true);
                await SimpleDialogs.SpawnDialog(
                        string.Format(Locale.Lang._Dialogs.OperationErrorDiskSpaceInsufficientTitle, outputDriveInfo.Name),
                        string.Format(Locale.Lang._Dialogs.OperationErrorDiskSpaceInsufficientMsg,
                                      ConverterTool.SummarizeSizeSimple(outputDriveInfo.TotalFreeSpace),
                                      ConverterTool.SummarizeSizeSimple(this.TotalFileSize),
                                      outputDriveInfo.Name),
                        parentUI,
                        null,
                        Locale.Lang._Misc.Okay,
                        null,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Error
                );
            }

            return isSpaceSufficient;
        }
    }
}
