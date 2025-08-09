using CollapseLauncher.Extension;
using CollapseLauncher.Plugins;
using Hi3Helper.Win32.FileDialogCOM;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using InternalExtension = CollapseLauncher.Extension.UIElementExtensions;

#nullable enable
namespace CollapseLauncher.Pages
{
    public sealed partial class PluginManagerPage
    {
        public static PluginManagerPageContext Context { get; }      = new();
        public static PluginManagerPage        This    { get; set; } = null!;

        public PluginManagerPage()
        {
            This = this;
            InitializeComponent();
            ImportBoxButton.SetAllControlsCursorRecursive(InternalExtension.HandCursor);
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
            int imported = 0;
            try
            {
                ImportBoxButton.IsEnabled = false;
                Dictionary<string, string> supportedFiles = new()
                {
                    { "Collapse Launcher Plugin", "*.zip;manifest.json" }
                };

                string[] selectedFiles =
                    await FileDialogNative.GetMultiFilePicker(supportedFiles, "Import Plugin Files");
                if (selectedFiles.Length == 0)
                {
                    return;
                }

                List<Exception> exceptions = [];

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
                        exceptions.Add(exception);
                    }
                }

                if (exceptions.Count > 0)
                {
                    throw new AggregateException(exceptions);
                }
            }
            catch (Exception ex)
            {
                string messageHead = imported > 0 ?
                    $"{imported} plugin(s) have been imported! But some error has occurred while importing other plugins:" :
                    "No plugin has been imported due to following error:";

                ErrorSender.SendException(new UnknownPluginException(messageHead, ex));
            }
            finally
            {
                ImportBoxButton.IsEnabled = true;
            }
        }
    }
}
