using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.InstallManager.Base;
using Hi3Helper;
using Hi3Helper.Data;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
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
                          foreach (LocalFileInfo asset in FileInfoSource)
                          {
                              ListViewTable.SelectedItems.Add(asset);
                          }

                          FileInfoSource.CollectionChanged += UpdateUIOnCollectionChange;
                      };
        }
    #pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        private int    _selectedAssetsCount;
        private long   _assetTotalSize;
        private string _assetTotalSizeString = string.Empty;
        private long   _assetSelectedSize;

        public void InjectFileInfoSource(IEnumerable<LocalFileInfo> fileInfoList)
        {
            FileInfoSource.Clear();
            foreach (LocalFileInfo fileInfo in fileInfoList)
            {
                FileInfoSource.Add(fileInfo);
                _assetTotalSize += fileInfo.FileSize;
            }

            _assetTotalSizeString = ConverterTool.SummarizeSizeSimple(_assetTotalSize);
            UpdateUIOnCollectionChange(FileInfoSource, null);
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


        private void ToggleCheckAll(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox checkBox)
            {
                return;
            }

            bool toCheck = checkBox.IsChecked ?? false;
            SelectAllToggle(toCheck);
        }

        private void SelectAllToggle(bool selectAll)
        {
            if (selectAll)
            {
                foreach (LocalFileInfo asset in FileInfoSource)
                {
                    if (ListViewTable.SelectedItems.IndexOf(asset) < 0)
                    {
                        ListViewTable.SelectedItems.Add(asset);
                    }
                }
            }
            else
            {
                ListViewTable.SelectedItems.Clear();
            }
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedAssetsCount -= e.RemovedItems.Count;
            _assetSelectedSize   -= e.RemovedItems.OfType<LocalFileInfo>().Sum(x => x.FileSize);

            _selectedAssetsCount += e.AddedItems.Count;
            _assetSelectedSize   += e.AddedItems.OfType<LocalFileInfo>().Sum(x => x.FileSize);

            ToggleCheckAllCheckBox.Content = _selectedAssetsCount > 0
                ? string.Format(Locale.Lang._FileCleanupPage.BottomCheckboxFilesSelected,
                                _selectedAssetsCount, ConverterTool.SummarizeSizeSimple(_assetSelectedSize),
                                _assetTotalSizeString)
                : Locale.Lang._FileCleanupPage.BottomCheckboxNoFileSelected;
            DeleteSelectedFilesText.Text =
                string.Format(Locale.Lang._FileCleanupPage.BottomButtonDeleteSelectedFiles, _selectedAssetsCount);
            DeleteSelectedFiles.IsEnabled = _selectedAssetsCount > 0;

            if (_selectedAssetsCount != 0 && _selectedAssetsCount != FileInfoSource.Count)
            {
                ToggleCheckAllCheckBox.IsChecked = null;
            }
            else
            {
                ToggleCheckAllCheckBox.IsChecked = _selectedAssetsCount == FileInfoSource.Count;
            }
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

                await Task.Run(() => InvokeProp.MoveFileToRecycleBin(toBeDeleted));
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