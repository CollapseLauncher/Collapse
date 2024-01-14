using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.FileDialogCOM;
using Hi3Helper;
using Hi3Helper.Data;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher
{
    internal struct FileMigrationProcessUIRef
    {
        internal ContentDialogCollapse mainDialogWindow;
        internal TextBlock pathActivitySubtitle;
        internal Run speedIndicatorSubtitle;
        internal Run fileCountIndicatorSubtitle;
        internal Run fileSizeIndicatorSubtitle;
        internal ProgressBar progressBarIndicator;
    }

    internal class FileMigrationProcess
    {
        private string dialogTitle { get; set; }
        private string inputPath { get; set; }
        private string outputPath { get; set; }
        private bool isFileTransfer { get; set; }
        private UIElement parentUI { get; set; }
        private CancellationTokenSource tokenSource { get; set; }

        private long CurrentSizeMoved;
        private long CurrentFileCountMoved;
        private long TotalFileSize;
        private long TotalFileCount;
        private bool IsSameOutputDrive;
        private Stopwatch ProcessStopwatch;

        private FileMigrationProcess(UIElement parentUI, string dialogTitle, string inputPath, string outputPath, bool isFileTransfer, CancellationTokenSource tokenSource)
        {
            this.dialogTitle = dialogTitle;
            this.inputPath = inputPath;
            this.outputPath = outputPath;
            this.isFileTransfer = isFileTransfer;
            this.parentUI = parentUI;
            this.tokenSource = tokenSource;
        }

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

            ContentDialogCollapse mainDialogWindow = new ContentDialogCollapse(ContentDialogTheme.Informational)
            {
                Title = dialogTitle,
                CloseButtonText = Locale.Lang._Misc.Cancel,
                PrimaryButtonText = null,
                SecondaryButtonText = null,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = parentUI.XamlRoot
            };

            Grid mainGrid = new Grid();
            mainGrid.AddGridRows(3);
            mainGrid.AddGridColumns(1, new GridLength(1.0, GridUnitType.Star));
            mainGrid.AddGridColumns(1);

            TextBlock locateFolderSubtitle = mainGrid.AddElementToGridColumn(new TextBlock
            {
                FontSize = 16d,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                TextWrapping = TextWrapping.Wrap,
                Text = Locale.Lang._FileMigrationProcess.LocateFolderSubtitle
            }, 0, 2);

            TextBox choosePathTextBox = mainGrid.AddElementToGridRow(new TextBox
            {
                Margin = new Thickness(0d, 12d, 0d, 0d),
                IsSpellCheckEnabled = false,
                IsRightTapEnabled = false,
                Width = 500,
                PlaceholderText = Locale.Lang._FileMigrationProcess.ChoosePathTextBoxPlaceholder,
                Text = string.IsNullOrEmpty(outputPath) ? null : outputPath
            }, 1);

            Button choosePathButton = mainGrid
                .AddElementToGridRowColumn(UIElementExtensions
                    .CreateButtonWithIcon<Button>(Locale.Lang._FileMigrationProcess.ChoosePathButton, "", "FontAwesome", "AccentButtonStyle"),
                    1, 1);
            choosePathButton.Margin = new Thickness(8d, 12d, 0d, 0d);

            TextBlock warningText = mainGrid.AddElementToGridRowColumn(new TextBlock
            {
                FontStyle = Windows.UI.Text.FontStyle.Italic,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0)),
                Margin = new Thickness(0d, 12d, 0d, 0d),
                Visibility = Visibility.Collapsed,
                TextWrapping = TextWrapping.Wrap,
                Text = ""
            }, 2, 0, 0, 2);

            mainDialogWindow.Content = mainGrid;

            if (!string.IsNullOrEmpty(outputPath))
                ToggleOrCheckPathWarning(outputPath);

            choosePathButton.Click += async (sender, args) =>
            {
                string pathResult = isFileTransfer ? await FileDialogNative.GetFileSavePicker(null, dialogTitle) :
                                                       await FileDialogNative.GetFolderPicker(dialogTitle);

                choosePathTextBox.Text = string.IsNullOrEmpty(pathResult) ? null : pathResult;
            };
            choosePathTextBox.TextChanged += (sender, args) => ToggleOrCheckPathWarning(((TextBox)sender).Text);

            void ToggleOrCheckPathWarning(string path)
            {
                string parentPath = path;
                if (isFileTransfer) parentPath = Path.GetDirectoryName(path);

                if (string.IsNullOrEmpty(parentPath))
                {
                    ToggleWarningText(Locale.Lang._FileMigrationProcess.ChoosePathErrorPathUnselected);
                    return;
                }
                if (!(File.Exists(parentPath) || Directory.Exists(parentPath)))
                {
                    ToggleWarningText(Locale.Lang._FileMigrationProcess.ChoosePathErrorPathNotExist);
                    return;
                }
                if (!ConverterTool.IsUserHasPermission(parentPath))
                {
                    ToggleWarningText(Locale.Lang._FileMigrationProcess.ChoosePathErrorPathNoPermission);
                    return;
                }
                ToggleWarningText();
            }

            void ToggleWarningText(string text = null)
            {
                bool canContinue = string.IsNullOrEmpty(text);
                warningText.Visibility = canContinue ? Visibility.Collapsed : Visibility.Visible;
                warningText.Text = text;
                mainDialogWindow.PrimaryButtonText = canContinue ? Locale.Lang._Misc.Next : null;
            }

            (InnerLauncherConfig.m_window as MainWindow).ContentDialog = mainDialogWindow;
            ContentDialogResult mainDialogWindowResult = await (InnerLauncherConfig.m_window as MainWindow).ContentDialog.ShowAsync();
            return mainDialogWindowResult == ContentDialogResult.Primary ? choosePathTextBox.Text : null;
        }

        private static async Task<ContentDialogResult> ShowNotCancellableProcedureMessage(UIElement parentUI) => await SimpleDialogs.Dialog_WarningOperationNotCancellable(parentUI);

        internal async Task<string> StartRoutine()
        {
            this.CurrentSizeMoved = 0;
            this.CurrentFileCountMoved = 0;
            this.TotalFileSize = 0;
            this.TotalFileCount = 0;

            if (!await IsOutputPathSpaceSufficient(this.inputPath, this.outputPath))
                throw new OperationCanceledException($"Disk space is not sufficient. Cancelling!");

            FileMigrationProcessUIRef uiRef = BuildMainMigrationUI();
            string outputPath = await StartRoutineInner(uiRef);
            uiRef.mainDialogWindow.Hide();
            return outputPath;
        }

        private async Task<string> StartRoutineInner(FileMigrationProcessUIRef uiRef)
        {
            this.ProcessStopwatch = Stopwatch.StartNew();
            return this.isFileTransfer ? await MoveFile(uiRef) : await MoveDirectory(uiRef);
        }

        private async Task<string> MoveFile(FileMigrationProcessUIRef uiRef)
        {
            FileInfo inputPathInfo = new FileInfo(this.inputPath);
            FileInfo outputPathInfo = new FileInfo(this.outputPath);

            string inputPathDir = Path.GetDirectoryName(inputPathInfo.FullName);
            string outputPathDir = Path.GetDirectoryName(outputPathInfo.FullName);

            if (!Directory.Exists(outputPathDir))
                Directory.CreateDirectory(outputPathDir);

            // Update path display
            string inputFileBasePath = inputPathInfo.FullName.Substring(inputPathDir.Length + 1);
            UpdateCountProcessed(uiRef, inputFileBasePath);

            if (this.IsSameOutputDrive)
            {
                // Logger.LogWriteLine($"[FileMigrationProcess::MoveFile()] Moving file in the same drive from: {inputPathInfo.FullName} to {outputPathInfo.FullName}", LogType.Default, true);
                inputPathInfo.MoveTo(outputPathInfo.FullName);
                UpdateSizeProcessed(uiRef, inputPathInfo.Length);
            }
            else
            {
                // Logger.LogWriteLine($"[FileMigrationProcess::MoveFile()] Moving file across different drives from: {inputPathInfo.FullName} to {outputPathInfo.FullName}", LogType.Default, true);
                await MoveWriteFile(uiRef, inputPathInfo, outputPathInfo, this.tokenSource == null ? default : this.tokenSource.Token);
            }

            return outputPathInfo.FullName;
        }

        private async Task<string> MoveDirectory(FileMigrationProcessUIRef uiRef)
        {
            DirectoryInfo inputPathInfo = new DirectoryInfo(this.inputPath);
            if (!Directory.Exists(this.outputPath))
                Directory.CreateDirectory(this.outputPath);

            DirectoryInfo outputPathInfo = new DirectoryInfo(this.outputPath);

            int parentInputPathLength = inputPathInfo.Parent.FullName.Length + 1;
            string outputDirBaseNamePath = inputPathInfo.FullName.Substring(parentInputPathLength);
            string outputDirPath = Path.Combine(this.outputPath, outputDirBaseNamePath);

            await Parallel.ForEachAsync(
                inputPathInfo.EnumerateFiles("*", SearchOption.AllDirectories),
                this.tokenSource?.Token ?? default,
                async (inputFileInfo, cancellationToken) =>
                {
                    int parentInputPathLength = inputPathInfo.Parent.FullName.Length + 1;
                    string inputFileBasePath = inputFileInfo.FullName.Substring(parentInputPathLength);

                    // Update path display
                    UpdateCountProcessed(uiRef, inputFileBasePath);

                    string outputTargetPath = Path.Combine(outputPathInfo.FullName, inputFileBasePath);
                    string outputTargetDirPath = Path.GetDirectoryName(outputTargetPath);

                    if (!Directory.Exists(outputTargetDirPath))
                        Directory.CreateDirectory(outputTargetDirPath);

                    if (this.IsSameOutputDrive)
                    {
                        // Logger.LogWriteLine($"[FileMigrationProcess::MoveDirectory()] Moving directory content in the same drive from: {inputFileInfo.FullName} to {outputTargetPath}", LogType.Default, true);
                        inputFileInfo.MoveTo(outputTargetPath);
                        UpdateSizeProcessed(uiRef, inputFileInfo.Length);
                    }
                    else
                    {
                        // Logger.LogWriteLine($"[FileMigrationProcess::MoveDirectory()] Moving directory content across different drives from: {inputFileInfo.FullName} to {outputTargetPath}", LogType.Default, true);
                        FileInfo outputFileInfo = new FileInfo(outputTargetPath);
                        await MoveWriteFile(uiRef, inputFileInfo, outputFileInfo, cancellationToken);
                    }
                });

            inputPathInfo.Delete(true);
            return outputDirPath;
        }

        private async Task MoveWriteFile(FileMigrationProcessUIRef uiRef, FileInfo inputFile, FileInfo outputFile, CancellationToken token)
        {
            int bufferSize = 1 << 20; // 1MB Buffer
            if (inputFile.Length < bufferSize)
                bufferSize = (int)inputFile.Length;

            byte[] buffer = new byte[bufferSize];

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
            catch { throw; }
            finally
            {
            }
        }

        private async Task MoveWriteFileInner(FileMigrationProcessUIRef uiRef, FileStream inputStream, FileStream outputStream, Memory<byte> buffer, CancellationToken token)
        {
            int read;
            while ((read = await inputStream.ReadAsync(buffer, token)) > 0)
            {
                await outputStream.WriteAsync(buffer, token);
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

        private FileMigrationProcessUIRef BuildMainMigrationUI()
        {
            ContentDialogCollapse mainDialogWindow = new ContentDialogCollapse(ContentDialogTheme.Informational)
            {
                Title = this.dialogTitle,
                CloseButtonText = null,
                PrimaryButtonText = null,
                SecondaryButtonText = null,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.parentUI.XamlRoot
            };

            Grid mainGrid = new Grid { Width = 500d };
            mainGrid.AddGridColumns(2, new GridLength(1.0d, GridUnitType.Star));
            mainGrid.AddGridRows(1, new GridLength(1.0d, GridUnitType.Auto));
            mainGrid.AddGridRows(3, new GridLength(20d, GridUnitType.Pixel));

            // Build path indicator
            StackPanel pathActivityPanel = mainGrid.AddElementToGridRowColumn(
                new StackPanel { Margin = new Thickness(0, 0, 0, 8d) },
                0, 0, 0, 2
                );
            _ = pathActivityPanel.AddElementToStackPanel(
                new TextBlock
                {
                    FontWeight = FontWeights.Bold,
                    Text = Locale.Lang._FileMigrationProcess.PathActivityPanelTitle
                });
            TextBlock pathActivitySubtitle = pathActivityPanel.AddElementToStackPanel(
                new TextBlock { Text = Locale.Lang._Misc.Idle, FontSize = 18d, TextWrapping = TextWrapping.Wrap });

            // Build speed indicator
            TextBlock speedIndicator = mainGrid.AddElementToGridRow(
                new TextBlock { FontWeight = FontWeights.Bold },
                1);
            Run speedIndicatorTitle = new Run { Text = Locale.Lang._FileMigrationProcess.SpeedIndicatorTitle, FontWeight = FontWeights.Medium };
            Run speedIndicatorSubtitle = new Run { Text = "-" };
            speedIndicator.Inlines.Add(speedIndicatorTitle);
            speedIndicator.Inlines.Add(speedIndicatorSubtitle);

            // Build file count indicator
            TextBlock fileCountIndicator = mainGrid.AddElementToGridRow(
                new TextBlock { FontWeight = FontWeights.Bold },
                2);
            Run fileCountIndicatorTitle = new Run { Text = Locale.Lang._FileMigrationProcess.FileCountIndicatorTitle, FontWeight = FontWeights.Medium };
            Run fileCountIndicatorSubtitle = new Run { Text = Locale.Lang._Misc.PerFromToPlaceholder };
            fileCountIndicator.Inlines.Add(fileCountIndicatorTitle);
            fileCountIndicator.Inlines.Add(fileCountIndicatorSubtitle);

            // Build file size indicator
            TextBlock fileSizeIndicator = mainGrid.AddElementToGridRowColumn(
                new TextBlock
                {
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    HorizontalTextAlignment = TextAlignment.Right
                },
                1, 1);
            Run fileSizeIndicatorSubtitle = new Run { Text = Locale.Lang._Misc.PerFromToPlaceholder };
            fileSizeIndicator.Inlines.Add(fileSizeIndicatorSubtitle);

            // Build progress percentage indicator
            StackPanel progressTextIndicator = mainGrid.AddElementToGridRowColumn(
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                },
                2, 1);
            TextBlock progressTextIndicatorSubtitle = progressTextIndicator.AddElementToStackPanel(
                new TextBlock { Text = "0", FontWeight = FontWeights.Bold });
            _ = progressTextIndicator.AddElementToStackPanel(
                new TextBlock { Text = "%", FontWeight = FontWeights.Bold });

            // Build progress bar indicator
            ProgressBar progressBarIndicator = mainGrid.AddElementToGridRowColumn(
                new ProgressBar
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Value = 0d,
                    Maximum = 100d,
                    IsIndeterminate = true
                },
                3, 0, 0, 2);

            // Set progress percentage indicator subtitle with progress bar value
            BindingOperations.SetBinding(progressTextIndicatorSubtitle, TextBlock.TextProperty, new Binding()
            {
                Source = progressBarIndicator,
                Path = new PropertyPath("Value"),
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

            // Set the main dialog content
            mainDialogWindow.Content = mainGrid;
            _ = mainDialogWindow.ShowAsync();

            // Return the 
            return new FileMigrationProcessUIRef
            {
                mainDialogWindow = mainDialogWindow,
                pathActivitySubtitle = pathActivitySubtitle,
                fileCountIndicatorSubtitle = fileCountIndicatorSubtitle,
                fileSizeIndicatorSubtitle = fileSizeIndicatorSubtitle,
                progressBarIndicator = progressBarIndicator,
                speedIndicatorSubtitle = speedIndicatorSubtitle,
            };
        }

        private void UpdateCountProcessed(FileMigrationProcessUIRef uiRef, string currentPathProcessed)
        {
            lock (this)
            {
                this.CurrentFileCountMoved++;
                string fileCountProcessedString = string.Format(Locale.Lang._Misc.PerFromTo,
                    this.CurrentFileCountMoved,
                    this.TotalFileCount);

                lock (uiRef.fileCountIndicatorSubtitle)
                {
                    this.parentUI.DispatcherQueue.TryEnqueue(() =>
                    {
                        uiRef.fileCountIndicatorSubtitle.Text = fileCountProcessedString;
                        uiRef.pathActivitySubtitle.Text = currentPathProcessed;
                    });
                }
            }
        }

        private void UpdateSizeProcessed(FileMigrationProcessUIRef uiRef, long currentRead)
        {
            lock (this)
            {
                this.CurrentSizeMoved += currentRead;
                double percentage = Math.Round((double)this.CurrentSizeMoved / this.TotalFileSize * 100d, 2);
                double speed = this.CurrentSizeMoved / this.ProcessStopwatch.Elapsed.TotalSeconds;

                lock (uiRef.progressBarIndicator)
                {
                    this.parentUI.DispatcherQueue.TryEnqueue(() =>
                    {
                        string speedString = string.Format(Locale.Lang._Misc.SpeedPerSec, ConverterTool.SummarizeSizeSimple(speed));
                        string sizeProgressString = string.Format(Locale.Lang._Misc.PerFromTo,
                            ConverterTool.SummarizeSizeSimple(this.CurrentSizeMoved),
                            ConverterTool.SummarizeSizeSimple(this.TotalFileSize));

                        uiRef.speedIndicatorSubtitle.Text = speedString;
                        uiRef.fileSizeIndicatorSubtitle.Text = sizeProgressString;
                        uiRef.progressBarIndicator.Value = percentage;
                        uiRef.progressBarIndicator.IsIndeterminate = false;
                    });
                }
            }
        }
    }
}
