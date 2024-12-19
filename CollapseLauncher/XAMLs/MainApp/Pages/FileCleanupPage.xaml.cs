using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Loading;
using CollapseLauncher.InstallManager.Base;
using CommunityToolkit.WinUI;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Win32.Native.ManagedTools;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

// ReSharper disable CheckNamespace
// ReSharper disable IdentifierTypo

#nullable enable
namespace CollapseLauncher.Pages
{
    public sealed partial class FileCleanupPage
    {
        internal static FileCleanupPage?                    Current { get; set; }
        internal        ObservableCollection<LocalFileInfo> FileInfoSource = [];

    #pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public FileCleanupPage()
        {
            InitializeComponent();
            Current = this;
            Loaded += (_, _) =>
                      {
                          FileInfoSource.CollectionChanged += UpdateUIOnCollectionChange;
                      };
        }
    #pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        private int    _selectedAssetsCount;
        private long   _assetTotalSize;
        private string _assetTotalSizeString = string.Empty;
        private long   _assetSelectedSize;

        private ObservableCollection<LocalFileInfo> _localFileCollection;
        public async Task InjectFileInfoSource(IEnumerable<LocalFileInfo> fileInfoList)
        {
            var s = new Stopwatch();
            s.Start();
            
            FileInfoSource.Clear();
            _assetTotalSize = 0;
            int batchSize = Math.Clamp(FileInfoSource.Count / (Environment.ProcessorCount * 4), 50, 1000);
            
            _localFileCollection = new ObservableCollection<LocalFileInfo>(fileInfoList);

            var batches = _localFileCollection
                         .Select((file, index) => new { File = file, Index = index })
                         .GroupBy(x => x.Index / batchSize)
                         .Select(group => group.Select(x => x.File).ToList());
            var tasks = new List<Task>();
            Logger.LogWriteLine($"[FileCleanupPage::InjectFileInfoSource] Starting to inject file info source with {_localFileCollection.Count} items", LogType.Scheme);
            
            LoadingMessageHelper.SetMessage(Locale.Lang._FileCleanupPage.LoadingTitle, "TODO: Translate TempAsync1");

            int b = 0;
            await Task.Run(() =>
                           {
                               foreach (var batch in batches)
                               {
                                   tasks.Add(EnqueueOnDispatcherQueueAsync(() =>
                                                                           {
                                                                               var sI = new Stopwatch();
                                                                               s.Start();
                                                                               foreach (var fileInfoInner in batch)
                                                                               {
                                                                                   FileInfoSource.Add(fileInfoInner);
                                                                                   _localFileCollection.Remove(fileInfoInner);
                                                                               }
                                                                               sI.Stop();
                                                                               Logger.LogWriteLine($"[FileCleanupPage::InjectFileInfoSource] Finished batch #{b} with {batch.Count} items after {s.ElapsedMilliseconds} ms", LogType.Scheme);
                                                                           }));
                                   
                               }
                           });
            await Task.WhenAll(tasks);

            await EnqueueOnDispatcherQueueAsync(() =>
                                          {
                                              var sI = new Stopwatch();
                                              sI.Start();
                                              while (_localFileCollection.Count > 0)
                                              {
                                                  FileInfoSource.Add(_localFileCollection[0]);
                                                  _localFileCollection.RemoveAt(0);
                                              }

                                              sI.Stop();
                                              Logger
                                                 .LogWriteLine($"[FileCleanupPage::InjectFileInfoSource] Finished last batch at #{b} after {_localFileCollection.Count} items",
                                                               LogType.Scheme);
                                          });
            
            while (_localFileCollection.Count != 0)
            {
                FileInfoSource.Add(_localFileCollection[0]);
                _localFileCollection.RemoveAt(0);
            }
            
            await Task.Run(() => _assetTotalSizeString = ConverterTool.SummarizeSizeSimple(_assetTotalSize));
            await DispatcherQueue.EnqueueAsync(() => UpdateUIOnCollectionChange(FileInfoSource, null));
            s.Stop();
            Logger.LogWriteLine($"InjectFileInfoSource done after {s.ElapsedMilliseconds} ms", LogType.Scheme);

            if (ListViewTable.Items.Count < 1000)
            {
                CheckAll();
            }
            else
            {
                ToggleCheckAllCheckBox.Content = Locale.Lang._FileCleanupPage.BottomCheckboxNoFileSelected;
                DeleteSelectedFilesText.Text =
                    string.Format(Locale.Lang._FileCleanupPage.BottomButtonDeleteSelectedFiles, 0);
            }

        }

        private void UpdateUIOnCollectionChange(object? sender, NotifyCollectionChangedEventArgs? args)
        {
            ObservableCollection<LocalFileInfo>? obj        = (ObservableCollection<LocalFileInfo>?)sender;
            int                                  count      = obj?.Count ?? 0;
            bool                                 isHasValue = count > 0;
            ListViewTable.Opacity   = isHasValue ? 1 : 0;
            NoFilesTextGrid.Opacity = isHasValue ? 0 : 1;

            ToggleCheckAllCheckBox.IsEnabled = isHasValue;
            DeleteAllFiles.IsEnabled         = isHasValue;
            DeleteSelectedFiles.IsEnabled    = isHasValue && _selectedAssetsCount > 0;

            ToggleCheckAllCheckBox.Visibility = isHasValue ? Visibility.Visible : Visibility.Collapsed;
            DeleteSelectedFiles.Visibility    = isHasValue ? Visibility.Visible : Visibility.Collapsed;
            DeleteAllFiles.Visibility         = isHasValue ? Visibility.Visible : Visibility.Collapsed;
        }


        private async void ToggleCheckAll(object sender, RoutedEventArgs e)
        {
            var s = new Stopwatch();
            LoadingMessageHelper.Initialize();
            LoadingMessageHelper.ShowLoadingFrame();
            LoadingMessageHelper.SetMessage(Locale.Lang._FileCleanupPage.LoadingTitle, "UI might freeze for a moment...");
            await Task.Delay(100);
            s.Start();
            if (sender is CheckBox checkBox)
            {
                bool toCheck = checkBox.IsChecked ?? false;
                await ToggleCheckAllInnerAsync(toCheck);
            } 
            s.Stop();
            LoadingMessageHelper.HideLoadingFrame();
            Logger.LogWriteLine("[FileCleanupPage::ToggleCheckAll] Elapsed time: " + s.ElapsedMilliseconds + "ms", LogType.Scheme);
        }

        private async void CheckAll()
        {
            await ToggleCheckAllInnerAsync(true);
        }

        private ObservableCollection<LocalFileInfo> _fileInfoSourceCopy;

        private async Task ToggleCheckAllInnerAsync(bool selectAll)
        {
            if (selectAll)
            {
                int                 batchSize = Math.Clamp(FileInfoSource.Count / (Environment.ProcessorCount * 4), 50, 1000);
                var                 tasks     = new List<Task>();
                _fileInfoSourceCopy = new ObservableCollection<LocalFileInfo>(FileInfoSource);
                int b = 0;
                await Task.Run(() =>
                               {
                                   var batches = FileInfoSource
                                                .Select((file, index) => new { File = file, Index = index })
                                                .GroupBy(x => x.Index / batchSize)
                                                .Select(group => group.Select(x => x.File).ToList());
                                   
                                   foreach (var batch in batches)
                                   {
                                       tasks.Add(EnqueueOnDispatcherQueueAsync(() =>
                                                                               {
                                                                                   var s = new Stopwatch();
                                                                                   s.Start();
                                                                                   foreach (var fileInfo in batch)
                                                                                   {
                                                                                       ListViewTable.SelectedItems.Add(fileInfo);
                                                                                       _fileInfoSourceCopy.Remove(fileInfo);
                                                                                   }
                                                                                   s.Stop();
                                                                                   Logger.LogWriteLine($"[FileCleanupPage::ToggleCheckAllInnerAsync] Finished batch #{b} with {batch.Count} items after {s.ElapsedMilliseconds} ms", LogType.Scheme);
                                                                                   
                                                                                   b++;
                                                                               }));
                                   }
                               });

                await Task.WhenAll(tasks);

                await EnqueueOnDispatcherQueueAsync(() =>
                                                    {
                                                        var i = 0;
                                                        while (_fileInfoSourceCopy.Count > 0)
                                                        {
                                                            ListViewTable.SelectedItems.Add(_fileInfoSourceCopy[0]);
                                                            _fileInfoSourceCopy.RemoveAt(0);
                                                            i++;
                                                        }

                                                        Logger
                                                           .LogWriteLine($"[FileCleanupPage::ToggleCheckAllInnerAsync] Finished last batch at #{b} after {i} items",
                                                                         LogType.Scheme);
                                                    });
            }
            else
            {
                await EnqueueOnDispatcherQueueAsync(() => ListViewTable.SelectedItems.Clear());
            }
        }

        private async void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var removedItems = e.RemovedItems.OfType<LocalFileInfo>().ToList();
            var addedItems   = e.AddedItems.OfType<LocalFileInfo>().ToList();

            long removedSize = await Task.Run(() => SumFileSizes(removedItems));
            long addedSize   = await Task.Run(() => SumFileSizes(addedItems));

            _selectedAssetsCount += addedItems.Count - removedItems.Count;
            _assetSelectedSize   += addedSize - removedSize;

            await EnqueueOnDispatcherQueueAsync(() =>
            {
                if (_selectedAssetsCount > 0)
                {
                    ToggleCheckAllCheckBox.Content = string.Format(
                                                                   Locale.Lang._FileCleanupPage.BottomCheckboxFilesSelected,
                                                                    _selectedAssetsCount,
                                                                    ConverterTool.SummarizeSizeSimple(_assetSelectedSize),
                                                                    _assetTotalSizeString);
                }
                else
                {
                    ToggleCheckAllCheckBox.Content = Locale.Lang._FileCleanupPage.BottomCheckboxNoFileSelected;
                }

                DeleteSelectedFilesText.Text =
                    string.Format(Locale.Lang._FileCleanupPage.BottomButtonDeleteSelectedFiles, _selectedAssetsCount);
                DeleteSelectedFiles.IsEnabled = _selectedAssetsCount > 0;

                ToggleCheckAllCheckBox.IsChecked = _selectedAssetsCount == 0
                    ? false
                    : _selectedAssetsCount == FileInfoSource.Count ? true : null;
            });
        }

        private long SumFileSizes(List<LocalFileInfo> items)
        {
            var vectorSize = Vector<long>.Count;
            var sumVector  = Vector<long>.Zero;

            int i = 0;

            // Vectorized loop
            for (; i <= items.Count - vectorSize; i += vectorSize)
            {
                var fileSizeArray = new long[vectorSize];
                for (int j = 0; j < vectorSize; j++)
                {
                    fileSizeArray[j] = items[i + j].FileSize;
                }

                var vector = new Vector<long>(fileSizeArray);
                sumVector += vector;
            }

            // Aggregate SIMD results
            long total = 0;
            for (int j = 0; j < vectorSize; j++)
            {
                total += sumVector[j];
            }

            // Remainder loop for non-vectorized items
            for (; i < items.Count; i++)
            {
                total += items[i].FileSize;
            }

            return total;
        }

        private Task EnqueueOnDispatcherQueueAsync(Action action)
        {
            var tcs = new TaskCompletionSource();
            DispatcherQueue.TryEnqueue(() =>
                                       {
                                           try
                                           {
                                               action();
                                               tcs.SetResult();
                                           }
                                           catch (Exception ex)
                                           {
                                               tcs.SetException(ex);
                                           }
                                       });
            return tcs.Task;
        }

        private async void DeleteAllFiles_Click(object sender, RoutedEventArgs e)
        {
            IList<LocalFileInfo> fileInfoList = FileInfoSource
               .ToList();
            long size = fileInfoList.Sum(x => x.FileSize);
            await PerformRemoval(fileInfoList, size);
        }

        private async void DeleteSelectedFiles_Click(object sender, RoutedEventArgs e)
        {
            IList<LocalFileInfo> fileInfoList = ListViewTable.SelectedItems
                                                             .OfType<LocalFileInfo>()
                                                             .ToList();
            long size = fileInfoList.Sum(x => x.FileSize);
            await PerformRemoval(fileInfoList, size);
        }

        private async Task PerformRemoval(ICollection<LocalFileInfo>? deletionSource, long totalSize)
        {
            if (deletionSource == null)
            {
                return;
            }

            TextBlock textBlockMsg = new TextBlock
                {
                    TextAlignment = TextAlignment.Center,
                    TextWrapping  = TextWrapping.WrapWholeWords
                }.AddTextBlockLine(Locale.Lang._FileCleanupPage.DialogDeletingFileSubtitle1, true)
                 .AddTextBlockLine(string.Format(Locale.Lang._FileCleanupPage.DialogDeletingFileSubtitle2, deletionSource.Count),
                                   true, FontWeights.Medium)
                 .AddTextBlockLine(Locale.Lang._FileCleanupPage.DialogDeletingFileSubtitle3, true)
                 .AddTextBlockLine(string.Format(Locale.Lang._FileCleanupPage.DialogDeletingFileSubtitle4, ConverterTool.SummarizeSizeSimple(totalSize)),
                                   FontWeights.Medium)
                 .AddTextBlockNewLine()
                 .AddTextBlockLine(Locale.Lang._FileCleanupPage.DialogDeletingFileSubtitle5);

            ContentDialogResult dialogResult = await SimpleDialogs.SpawnDialog(
                                                                               Locale.Lang._FileCleanupPage
                                                                                  .DialogDeletingFileTitle,
                                                                               textBlockMsg,
                                                                               this,
                                                                               Locale.Lang._Misc.NoCancel,
                                                                               Locale.Lang._Misc.YesContinue,
                                                                               Locale.Lang._FileCleanupPage
                                                                                  .DialogMoveToRecycleBin,
                                                                               ContentDialogButton.Close,
                                                                               ContentDialogTheme.Warning);

            int deleteSuccess = 0;
            int deleteFailed  = 0;

            bool isToRecycleBin = dialogResult == ContentDialogResult.Secondary;
            if (dialogResult == ContentDialogResult.None)
            {
                return;
            }

            if (isToRecycleBin)
            {
                IList<string> toBeDeleted = new List<string>();

                foreach (LocalFileInfo fileInfo in deletionSource)
                {
                    try
                    {
                        FileInfo fileInfoN = fileInfo.ToFileInfo();
                        if (fileInfoN.Exists)
                        {
                            fileInfoN.IsReadOnly = false;
                            toBeDeleted.Add(fileInfoN.FullName);
                        }
                    }
                    catch (Exception ex)
                    {
                        ++deleteFailed;
                        Logger.LogWriteLine($"Failed to remove read only attribute from this file: {fileInfo.FullPath}\r\n{ex}",
                                            LogType.Error, true);
                    }
                }

                await Task.Run(() => RecycleBin.MoveFileToRecycleBin(toBeDeleted));
                DispatcherQueue.TryEnqueue(() =>
                                           {
                                               for (int i = FileInfoSource.Count - 1; i >= 0; i--)
                                               {
                                                   if (toBeDeleted.Contains(FileInfoSource[i].ToFileInfo().FullName))
                                                   {
                                                       FileInfoSource.RemoveAt(i);
                                                   }
                                               }
                                           });
                
                deleteSuccess = toBeDeleted.Count;
            }
            else
            {
                foreach (LocalFileInfo fileInfo in deletionSource)
                {
                    try
                    {
                        FileInfo fileInfoN = fileInfo.ToFileInfo();
                        if (fileInfoN.Exists)
                        {
                            fileInfoN.IsReadOnly = false;
                            fileInfoN.Delete();
                        }

                        FileInfoSource.Remove(fileInfo);
                        ++deleteSuccess;
                    }
                    catch (Exception ex)
                    {
                        ++deleteFailed;
                        Logger.LogWriteLine($"Failed while deleting this file: {fileInfo.FullPath}\r\n{ex}",
                                            LogType.Error, true);
                    }
                }
            }

            string diagTitle = dialogResult == ContentDialogResult.Primary
                ? Locale.Lang._FileCleanupPage.DialogDeleteSuccessTitle
                : Locale.Lang._FileCleanupPage.DialogTitleMovedToRecycleBin;

            await SimpleDialogs.SpawnDialog(diagTitle,
                                            string.Format(Locale.Lang._FileCleanupPage.DialogDeleteSuccessSubtitle1,
                                                          deleteSuccess)
                                            + (deleteFailed == 0
                                                ? string.Empty
                                                : ' ' +
                                                  string
                                                     .Format(Locale.Lang._FileCleanupPage.DialogDeleteSuccessSubtitle2,
                                                             deleteFailed)),
                                            this,
                                            Locale.Lang._Misc.OkayHappy,
                                            null,
                                            null,
                                            ContentDialogButton.Close,
                                            ContentDialogTheme.Success);
        }
    }
}