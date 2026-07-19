using CollapseLauncher.Dialogs;
using CollapseLauncher.Helper;
using CollapseLauncher.XAMLs.Theme.ContentDialog;
using CollapseLauncher.Plugins;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Win32.FileDialogCOM;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Storage;
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.Pages
{
    public sealed partial class PluginManagerPage
    {
        public static PluginManagerPageContext Context { get; }              = new();
        public static PluginManagerPage        This    { get; private set; } = null!;

        private readonly DispatcherTimer _fileDragIndicatorTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        private bool _isWinUiFileDragActive;
        private bool _isDropIndicatorVisible;

        public PluginManagerPage()
        {
            This = this;
            InitializeComponent();
            _fileDragIndicatorTimer.Tick += OnFileDragIndicatorTimerTick;
            Loaded += OnPluginManagerPageLoaded;
            Unloaded += OnPluginManagerPageUnloaded;
        }

        private void OnPluginManagerPageLoaded(object sender, RoutedEventArgs e)
        {
            WindowUtility.FileDropEvent += OnNativeFileDrop;
            WindowUtility.SetFileDropEnabled(true);
            _fileDragIndicatorTimer.Start();
        }

        private void OnPluginManagerPageUnloaded(object sender, RoutedEventArgs e)
        {
            _fileDragIndicatorTimer.Stop();
            _isWinUiFileDragActive = false;
            SetImportDropIndicator(false);
            WindowUtility.SetFileDropEnabled(false);
            WindowUtility.FileDropEvent -= OnNativeFileDrop;
        }

        private void OnListViewRightClickUpdate(object sender, RightTappedRoutedEventArgs e)
        {
            Context.ListViewSelectedItemsCountUninstalled =
                (sender as ListView)?.SelectedItems.Count(x => (x as PluginInfo)?.IsMarkedForDeletion ?? false) ?? 0;
            Context.ListViewSelectedItemsCountRestored =
                (sender as ListView)?.SelectedItems.Count(x => !(x as PluginInfo)?.IsMarkedForDeletion ?? false) ?? 0;
            Context.ListViewSelectedItemsCountEnabled =
                (sender as ListView)?.SelectedItems.Count(x => (x as PluginInfo)?.IsEnabled ?? false) ?? 0;
            Context.ListViewSelectedItemsCountDisabled =
                (sender as ListView)?.SelectedItems.Count(x => !(x as PluginInfo)?.IsEnabled ?? false) ?? 0;
        }

        private void OnUninstallPlugin(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: PluginInfo pluginInfo })
            {
                return;
            }

            pluginInfo.IsMarkedForDeletion = true;
        }

        private void OnRestoreUninstalledPlugin(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: PluginInfo pluginInfo })
            {
                return;
            }

            pluginInfo.IsMarkedForDeletion = false;
        }

        private async void OnClickImportButton(object sender, RoutedEventArgs e)
        {
            try
            {
                Dictionary<string, string> supportedFiles = new()
                {
                    { Locale.Current.Lang?._PluginManagerPage?.FileDialogFileFilter1 ?? "", "*.zip;manifest.json" }
                };

                string[] selectedFiles =
                    await FileDialogNative.GetMultiFilePicker(supportedFiles, Locale.Current.Lang?._PluginManagerPage?.FileDialogTitle);
                if (selectedFiles.Length == 0)
                {
                    return;
                }

                await ImportPlugins(selectedFiles);
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex.WrapPluginException("No plugin has been imported due to following error:"));
            }
        }

        private void OnDragEnterImportBox(object sender, DragEventArgs e)
        {
            if (ImportBoxButton.IsEnabled && e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                _isWinUiFileDragActive = true;
                SetImportDropIndicator(true);
                e.AcceptedOperation = DataPackageOperation.Copy;
            }
        }

        private void OnDragLeaveImportBox(object sender, DragEventArgs e)
        {
            Point position = e.GetPosition(ImportBoxButton);
            if (position.X >= 0 && position.X <= ImportBoxButton.ActualWidth &&
                position.Y >= 0 && position.Y <= ImportBoxButton.ActualHeight)
            {
                return;
            }

            _isWinUiFileDragActive = false;
            SetImportDropIndicator(false);
        }

        private void OnDragOverImportBox(object sender, DragEventArgs e)
        {
            if (ImportBoxButton.IsEnabled && e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                _isWinUiFileDragActive = true;
                SetImportDropIndicator(true);
                e.AcceptedOperation = DataPackageOperation.Copy;
            }
        }

        private async void OnDropImportBox(object sender, DragEventArgs e)
        {
            _isWinUiFileDragActive = false;
            SetImportDropIndicator(false);
            try
            {
                IReadOnlyList<IStorageItem> storageItems = await e.DataView.GetStorageItemsAsync();
                string[] selectedFiles = storageItems.OfType<StorageFile>().Select(file => file.Path).ToArray();
                await ImportPlugins(selectedFiles);
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex.WrapPluginException("No plugin has been imported due to following error:"));
            }
        }

        private async void OnNativeFileDrop(string[] selectedFiles, PointInt32 dropPoint)
        {
            _isWinUiFileDragActive = false;
            SetImportDropIndicator(false);

            if (!IsPointInDropArea(dropPoint))
            {
                return;
            }

            await ImportPlugins(selectedFiles);
        }

        private void OnFileDragIndicatorTimerTick(object? sender, object e)
        {
            bool isExternalDragOverDropArea =
                WindowUtility.TryGetExternalDragPosition(out PointInt32 dragPosition) &&
                IsPointInDropArea(dragPosition);
            SetImportDropIndicator(_isWinUiFileDragActive || isExternalDragOverDropArea);
        }

        private bool IsPointInDropArea(PointInt32 point)
        {
            Rect dropAreaBounds = ImportBoxButton.TransformToVisual(null)
                                                     .TransformBounds(new Rect(0,
                                                                               0,
                                                                               ImportBoxButton.ActualWidth,
                                                                               ImportBoxButton.ActualHeight));
            double scaleFactor = WindowUtility.CurrentWindowMonitorScaleFactor;
            return point.X >= dropAreaBounds.Left * scaleFactor &&
                   point.X <= dropAreaBounds.Right * scaleFactor &&
                   point.Y >= dropAreaBounds.Top * scaleFactor &&
                   point.Y <= dropAreaBounds.Bottom * scaleFactor;
        }

        private void SetImportDropIndicator(bool isVisible)
        {
            if (_isDropIndicatorVisible == isVisible)
            {
                return;
            }

            _isDropIndicatorVisible = isVisible;
            ImportDropIndicator.Opacity = isVisible ? 1 : 0;
        }

        private async Task ImportPlugins(string[] selectedFiles)
        {
            if (selectedFiles.Length == 0)
            {
                return;
            }

            int imported = 0;
            List<(string FilePath, Exception Exception)> failures = [];
            try
            {
                ImportBoxButton.IsEnabled = false;

                foreach (string filePath in selectedFiles)
                {
                    try
                    {
                        PluginInfo pluginInfo = await PluginImporter.AutoGetImportFromPath(filePath, CancellationToken.None);
                        Context.PluginCollection.Add(pluginInfo);
                        imported++;
                    }
                    catch (Exception exception)
                    {
                        failures.Add((filePath, exception));
                        Logger.LogWriteLine($"[PluginManagerPage::ImportPlugins] Failed to import: {filePath}\r\n{exception}",
                                            LogType.Error,
                                            true);
                    }
                }
            }
            finally
            {
                ImportBoxButton.IsEnabled = true;
            }

            if (failures.Count == 0)
            {
                return;
            }

            string messageHead = imported > 0 ?
                $"{imported} plugin(s) were imported, but {failures.Count} file(s) could not be imported:" :
                "No plugins were imported:";
            string failureMessages = string.Join(Environment.NewLine,
                                                 failures.Select(failure =>
                                                     $"- {Path.GetFileName(failure.FilePath)}: {GetFriendlyImportError(failure.FilePath, failure.Exception)}"));

            string warningMessage = $"{messageHead}{Environment.NewLine}{Environment.NewLine}{failureMessages}";
            await SimpleDialogs.SpawnDialog(Locale.Current.Lang?._UnhandledExceptionPage?.UnhandledTitle4 ?? "Warning",
                                            warningMessage,
                                            ImportBoxButton,
                                            Locale.Current.Lang?._Misc?.Close,
                                            dialogTheme: ContentDialogTheme.Warning);
        }

        private static string GetFriendlyImportError(string filePath, Exception exception)
        {
            return exception switch
            {
                NotSupportedException        => exception.Message,
                DuplicateNameException      => exception.Message,
                UnauthorizedAccessException => "Permission was denied while reading or installing the plugin.",
                FileNotFoundException       => "The package is missing its manifest or another required file.",
                InvalidDataException        => "The archive or manifest is invalid.",
                IOException when filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) =>
                    "The archive could not be read or installed.",
                IOException => "The manifest or one of its referenced files could not be read.",
                _ => "The file is not a valid or supported plugin."
            };
        }

        internal static async void AskLauncherRestart(object? sender, RoutedEventArgs? e)
        {
            try
            {
                ContentDialogResult pluginUpdateConfirm = await SimpleDialogs.Dialog_RestartLauncher();
                if (pluginUpdateConfirm != ContentDialogResult.Primary)
                {
                    return;
                }

                MainEntryPoint.ForceRestart();
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"An error has occurred while trying to spawn restart dialog\r\n{ex}", LogType.Error, true);
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
        }

#pragma warning disable CA2012
        private void OnClickCheckForUpdatesAllButton(SplitButton button, SplitButtonClickEventArgs e)
        {
            PluginManagerPageContext.CheckUpdateEnumeratePlugins(PluginManager.PluginInstances.Values);
        }

        private void OnClickUpdateCurrentPlugin(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: PluginInfo asPluginInfo })
            {
                _ = asPluginInfo.RunUpdateTask();
            }
        }

        private void OnClickUpdateAndDownloadAllPlugin(object sender, RoutedEventArgs e)
        {
            PluginManagerPageContext.CheckAndDownloadUpdateEnumeratePlugins(PluginManager.PluginInstances.Values);
        }
#pragma warning restore CA2012

        private void OnClickGoToPluginDownloadCatalogButton(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
                     {
                         Process.Start(new ProcessStartInfo
                         {
                             FileName        = "https://collapselauncher.com/plugin/catalog.html",
                             UseShellExecute = true
                         });
                     });
        }
    }
}
