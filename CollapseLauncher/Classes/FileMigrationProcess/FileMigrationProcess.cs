using CollapseLauncher.FileDialogCOM;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper;
using Hi3Helper.Shared.Region;
using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Native.ManagedTools;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher
{
    internal partial class FileMigrationProcess
    {
        private const int RefreshInterval = 100; // 100ms UI refresh interval

        private string                  DialogTitle       { get; }
        private string                  InputPath         { get; }
        private string                  OutputPath        { get; }
        private bool                    IsFileTransfer    { get; }
        private CancellationTokenSource TokenSource       { get; }
        private bool                    IsSameOutputDrive { get; set; }

#nullable enable
        private DispatcherQueue? DispatcherQueue => field ??= WindowUtility.CurrentDispatcherQueue;
#nullable restore

        private long _currentSizeMoved;
        private long _currentFileCountMoved;
        private long _totalFileSize;
        private long _totalFileCount;
        private Stopwatch _processStopwatch;
        private Stopwatch _eventsStopwatch;

        private FileMigrationProcess(string dialogTitle, string inputPath, string outputPath, bool isFileTransfer, CancellationTokenSource tokenSource)
        {
            DialogTitle       = dialogTitle;
            InputPath         = inputPath;
            OutputPath        = outputPath;
            IsFileTransfer    = isFileTransfer;
            TokenSource       = tokenSource;
        }

        internal async Task<string> StartRoutine()
        {
            bool isSuccess = false;
            _currentSizeMoved = 0;
            _currentFileCountMoved = 0;
            _totalFileSize = 0;
            _totalFileCount = 0;
            FileMigrationProcessUIRef uiRef = null;

            try
            {
                if (!await IsOutputPathSpaceSufficient(InputPath, OutputPath))
                    throw new OperationCanceledException("Disk space is not sufficient. Cancelling!");

                if (FileDialogHelper.IsRootPath(OutputPath))
                {
                    throw new NotSupportedException("Cannot move game to the root of the drive!");
                }

                uiRef = BuildMainMigrationUI();
                string outputPath = await StartRoutineInner(uiRef);
                uiRef.MainDialogWindow!.Hide();
                isSuccess = true;

                return outputPath;
            }
            catch when (!isSuccess) // Throw if the isSuccess is not set to true
            {
                if (uiRef?.MainDialogWindow == null)
                {
                    throw;
                }

                uiRef.MainDialogWindow.Hide();
                await Task.Delay(500); // Give artificial delay to give main dialog window thread to close first
                throw;
            }
            finally
            {
                _processStopwatch?.Stop();
                _eventsStopwatch?.Stop();
            }
        }

        private async Task<string> StartRoutineInner(FileMigrationProcessUIRef uiRef)
        {
            _processStopwatch = Stopwatch.StartNew();
            _eventsStopwatch = Stopwatch.StartNew();
            return IsFileTransfer ? await MoveFile(uiRef) : await MoveDirectory(uiRef);
        }

        private async Task<string> MoveFile(FileMigrationProcessUIRef uiRef)
        {
            FileInfo inputPathInfo = new FileInfo(InputPath);
            FileInfo outputPathInfo = new FileInfo(OutputPath);

            var inputPathDir = FileDialogHelper.IsRootPath(InputPath)
                ? Path.GetPathRoot(InputPath)
                : Path.GetDirectoryName(inputPathInfo.FullName);

            if (string.IsNullOrEmpty(inputPathDir))
                throw new InvalidOperationException(string.Format(Locale.Lang._Dialogs.InvalidGameDirNewTitleFormat,
                                                                  InputPath));
            
            DirectoryInfo outputPathDirInfo = new DirectoryInfo(inputPathDir);
            outputPathDirInfo.Create();

            // Update path display
            string inputFileBasePath = inputPathInfo.FullName[(inputPathDir!.Length + 1)..];
            UpdateCountProcessed(uiRef, inputFileBasePath);

            if (IsSameOutputDrive)
            {
                Logger.LogWriteLine($"[FileMigrationProcess::MoveFile()] Moving file in the same drive from: {inputPathInfo.FullName} to {outputPathInfo.FullName}", LogType.Default, true);
                inputPathInfo.MoveTo(outputPathInfo.FullName, true);
                UpdateSizeProcessed(uiRef, inputPathInfo.Length);
            }
            else
            {
                Logger.LogWriteLine($"[FileMigrationProcess::MoveFile()] Moving file across different drives from: {inputPathInfo.FullName} to {outputPathInfo.FullName}", LogType.Default, true);
                await MoveWriteFile(uiRef, inputPathInfo, outputPathInfo, TokenSource?.Token ?? CancellationToken.None);
            }

            return outputPathInfo.FullName;
        }

        private async Task<string> MoveDirectory(FileMigrationProcessUIRef uiRef)
        {
            DirectoryInfo inputPathInfo = new DirectoryInfo(InputPath);
            DirectoryInfo outputPathInfo = new DirectoryInfo(OutputPath);
            outputPathInfo.Create();

            bool isMoveBackward = inputPathInfo.FullName.StartsWith(outputPathInfo.FullName, StringComparison.OrdinalIgnoreCase) &&
                                  !inputPathInfo.FullName.Equals(outputPathInfo.FullName, StringComparison.OrdinalIgnoreCase);

            // Listing all the existed files first
            List <FileInfo> inputFileList = [];
            inputFileList.AddRange(inputPathInfo
                                  .EnumerateFiles("*", SearchOption.AllDirectories)
                                  .EnumerateNoReadOnly()
                                  .Where(x => isMoveBackward || IsNotInOutputDir(x)));

            // Check if both destination and source are SSDs. If true, enable multi-threading.
            // Disabling multi-threading while either destination or source are HDDs could help
            // reduce massive seeking, hence improving speed.
            bool isBothSsd = DriveTypeChecker.IsDriveSsd(InputPath) &&
                             DriveTypeChecker.IsDriveSsd(OutputPath);
            ParallelOptions parallelOptions = new ParallelOptions
            {
                CancellationToken = TokenSource?.Token ?? CancellationToken.None,
                MaxDegreeOfParallelism = isBothSsd ? LauncherConfig.AppCurrentThread : 1
            };

            // Get old list of empty directories so it can be removed later.
            StringComparer comparer = StringComparer.OrdinalIgnoreCase;
            HashSet<string> oldDirectoryList = new(inputFileList
                                                  .Select(x => x.DirectoryName)
                                                  .Where(x => !string.IsNullOrEmpty(x) && !x.Equals(inputPathInfo.FullName, StringComparison.OrdinalIgnoreCase))
                                                  .Distinct(comparer)
                                                  .OrderDescending(), comparer);

            // Perform file migration task
            await Parallel.ForEachAsync(inputFileList, parallelOptions, Impl);
            foreach (string dir in oldDirectoryList)
            {
                RemoveEmptyDirectory(dir);
            }

            return OutputPath;

            bool IsNotInOutputDir(FileInfo fileInfo)
            {
                bool isEmpty = string.IsNullOrEmpty(fileInfo.DirectoryName);
                if (isEmpty)
                {
                    return false;
                }

                bool isStartsWith = fileInfo.DirectoryName.StartsWith(outputPathInfo.FullName);
                return !isStartsWith;
            }

            void RemoveEmptyDirectory(string dir)
            {
                foreach (string innerDir in Directory.EnumerateDirectories(dir))
                {
                    RemoveEmptyDirectory(innerDir);
                }

                try
                {
                    _ = FindFiles.TryIsDirectoryEmpty(dir, out bool isEmpty);
                    if (!isEmpty)
                    {
                        string parentDir = Path.GetDirectoryName(dir);
                        if (!string.IsNullOrEmpty(parentDir))
                        {
                            RemoveEmptyDirectory(parentDir);
                        }
                    }

                    Directory.Delete(dir);
                    Logger.LogWriteLine($"[FileMigrationProcess::MoveDirectory()] Empty directory: {dir} has been deleted!", LogType.Default, true);
                }
                catch (IOException)
                {
                    // ignored
                }
            }

            async ValueTask Impl(FileInfo inputFileInfo, CancellationToken cancellationToken)
            {
                string inputFileRelativePath = inputFileInfo.FullName
                    .AsSpan(InputPath.Length)
                    .TrimStart("\\/")
                    .ToString();

                string outputNewFilePath = Path.Combine(OutputPath, inputFileRelativePath);
                string outputNewFileDir = Path.GetDirectoryName(outputNewFilePath) ?? Path.GetPathRoot(outputNewFilePath);
                if (!string.IsNullOrEmpty(outputNewFileDir))
                    Directory.CreateDirectory(outputNewFileDir);

                // Update path display
                UpdateCountProcessed(uiRef, inputFileRelativePath);
                if (string.IsNullOrEmpty(outputNewFileDir))
                    throw new InvalidOperationException(string.Format(Locale.Lang._Dialogs.InvalidGameDirNewTitleFormat,
                                                                      InputPath));

                if (IsSameOutputDrive)
                {
                    Logger.LogWriteLine($"[FileMigrationProcess::MoveDirectory()] Moving directory content in the same drive from: {inputFileInfo.FullName} to {outputNewFilePath}", LogType.Default, true);
                    inputFileInfo.MoveTo(outputNewFilePath, true);
                    UpdateSizeProcessed(uiRef, inputFileInfo.Length);
                }
                else
                {
                    Logger.LogWriteLine($"[FileMigrationProcess::MoveDirectory()] Moving directory content across different drives from: {inputFileInfo.FullName} to {outputNewFilePath}", LogType.Default, true);
                    FileInfo outputFileInfo = new FileInfo(outputNewFilePath);
                    await MoveWriteFile(uiRef, inputFileInfo, outputFileInfo, cancellationToken);
                }
            }
        }
    }
}
