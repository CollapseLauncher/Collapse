using System;
using System.Collections.Generic;
using System.Linq;
//using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
//using System.Runtime.InteropServices;
using System.IO;
using Hi3HelperGUI.Preset;
using Hi3HelperGUI.Data;

using static Hi3HelperGUI.Logger;
using static Hi3HelperGUI.Data.ConverterTool;

namespace Hi3HelperGUI
{
    public partial class MainWindow
    {
        CancellationTokenSource DownloadTokenSource;
        private void SetMirrorChange(object sender, SelectionChangedEventArgs e) => ChangeMirrorSelection((byte)MirrorSelector.SelectedIndex);
        private void UpdateCheckStart(object sender, RoutedEventArgs e) => FetchUpdateData();
        private void UpdateDownloadStart(object sender, RoutedEventArgs e) => DoUpdateDownload();
        private void UpdateDownloadCancel(object sender, RoutedEventArgs e)
        {
            LogWriteLine($"Cancelling update...", LogType.Warning);
            DownloadTokenSource.Cancel();
        }

        UpdateData UpdateDataUtil;

        private async void FetchUpdateData()
        {
            await Task.Run(() =>
            {
                (ConfigStore.UpdateFilesTotalSize, ConfigStore.UpdateFilesTotalDownloaded) = (0, 0);
                RefreshUpdateProgressBar();
                ConfigStore.UpdateFiles = new List<UpdateDataProperties>();
                RefreshUpdateProgressLabel();
                ChangeUpdateDownload(false);
                foreach (PresetConfigClasses i in ConfigStore.Config)
                {
                    LogWriteLine($"Fetching update data for \u001b[34;1m{i.ZoneName}\u001b[0m (\u001b[32;1m{Path.GetFileName(i.InstallRegistryLocation)}\u001b[0m) version... ");
                    UpdateDataUtil = new UpdateData(i);
                    for (byte j = 0; j < 3; j++)
                    {
                        ChangeUpdateStatus($"Fetching update data for {i.ZoneName} {Enum.GetName(typeof(ConfigStore.DataType), j)} zone...", false);
                        UpdateDataUtil.GetDataDict(i, j);
                    }
                }
                ConfigStore.UpdateFilesTotalSize = ConfigStore.UpdateFiles.Sum(item => item.ECS);
                StoreToListView(ConfigStore.UpdateFiles);
                if (!(ConfigStore.UpdateFilesTotalSize > 0))
                {
                    ChangeUpdateStatus($"Your files are already up-to-date!", true);
                    return;
                }
                ChangeUpdateStatus($"{ConfigStore.UpdateFiles.Count} files ({SummarizeSizeSimple(ConfigStore.UpdateFilesTotalSize)}) are ready to be updated. Click Download to start the update!", true);
                ChangeUpdateDownload(true);
                LogWriteLine($"{ConfigStore.UpdateFiles.Count} files ({SummarizeSizeSimple(ConfigStore.UpdateFilesTotalSize)}) will be updated", LogType.Default, true);
                return;
            });
        }

        private void StoreToListView(List<UpdateDataProperties> i) => Dispatcher.Invoke(() => UpdateListView.ItemsSource = i);

        private async void DoUpdateDownload()
        {
            DownloadTokenSource = new CancellationTokenSource();
            CancellationToken token = DownloadTokenSource.Token;

            Dispatcher.Invoke(() => UpdateDownloadBtn.IsEnabled = false );
            // await Task.Run(() => DownloadUpdateFiles(ConfigStore.UpdateFiles), token);
            ToggleUpdateCancelBtnState(true);
            try
            {
                await DownloadUpdateFilesAsync(DownloadTokenSource, token);
            }
            catch (TaskCanceledException)
            {
                ToggleUpdateCancelBtnState(false);
                LogWriteLine($"Update is cancelled!", LogType.Warning, true);
                ChangeUpdateStatus($"Update is cancelled! To resume, click Check Update and then click Download.", true);
                return;
            }
            finally
            {
                DownloadTokenSource.Dispose();
            }

            RefreshUpdateProgressLabel();
            ToggleUpdateCancelBtnState(false);
            // await DownloadTask;
            ChangeUpdateStatus($"{ConfigStore.UpdateFiles.Count} files have been downloaded!", true);
            LogWriteLine($"{ConfigStore.UpdateFiles.Count} files have been downloaded!", LogType.Default, true);
        }

        public async Task<bool> DownloadUpdateFilesAsync(CancellationTokenSource tokenSource, CancellationToken token)
        {
            int updateFilesCount = ConfigStore.UpdateFiles.Count;
            string message;
            for (ushort p = 0; p < updateFilesCount; p++)
            {
                RemoveUpdateDownloadHandler();
                if (!Directory.Exists(Path.GetDirectoryName(ConfigStore.UpdateFiles[p].ActualPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(ConfigStore.UpdateFiles[p].ActualPath));

                UpdateHttpClient.ProgressChanged += UpdateDownloadProgressChanged;
                UpdateHttpClient.Completed += DownloadProgressCompleted;

                message = $"Down: [{ConfigStore.UpdateFiles[p].ZoneName} > {ConfigStore.UpdateFiles[p].DataType}] ({p + 1}/{ConfigStore.UpdateFiles.Count}) {Path.GetFileName(ConfigStore.UpdateFiles[p].N)}";
                //client.ProgressChanged += DownloadProgressChanges($"Down: [{ConfigStore.UpdateFiles[p].ZoneName} > {ConfigStore.UpdateFiles[p].DataType}] ({p + 1}/{ConfigStore.UpdateFiles.Count}) {Path.GetFileName(ConfigStore.UpdateFiles[p].N)}");
                //client.Completed += DownloadProgressCompleted();

                ChangeUpdateStatus(message, false);
                await Task.Run(() => {
                    while (!UpdateHttpClient.DownloadFile(ConfigStore.UpdateFiles[p].RemotePath, ConfigStore.UpdateFiles[p].ActualPath, message, -1, -1, token))
                    {
                        LogWriteLine($"Retrying...", LogType.Warning);
                    }
                }, token);
            }

            return false;
        }

        private void RefreshUpdateProgressLabel(string i = "none") => Dispatcher.Invoke(() => UpdateProgressLabel.Content = i);

        private void RefreshUpdateListView() => Dispatcher.Invoke(() => UpdateListView.Items.Refresh());

        private void RefreshUpdateProgressBar(double cur = 0, double max = 100) => Dispatcher.Invoke(() => UpdateProgressBar.Value = Math.Round((100 * cur) / max, 2), DispatcherPriority.Background);

        private void ToggleUpdateCancelBtnState(bool i) =>
            Dispatcher.Invoke(() => {
                if (i)
                {
                    UpdateCheckBtn.Visibility = Visibility.Collapsed;
                    UpdateCancelBtn.Visibility = Visibility.Visible;
                }
                else
                {
                    UpdateCheckBtn.Visibility = Visibility.Visible;
                    UpdateCancelBtn.Visibility = Visibility.Collapsed;
                }
            });

        byte? currentMirror;

        private void ChangeMirrorSelection(byte i) =>
            Dispatcher.Invoke(() =>
            {
                if (currentMirror == null)
                {
                    MirrorSelector.SelectedIndex = ConfigStore.AppConfigData.MirrorSelection;
                    MirrorSelectorStatus.Content = MirrorSelector.SelectedItem.ToString();
                    currentMirror = ConfigStore.AppConfigData.MirrorSelection;
                }
                else
                {
                    ConfigStore.AppConfigData.MirrorSelection = i;
                    currentMirror = ConfigStore.AppConfigData.MirrorSelection;
                    MirrorSelectorStatus.Content = MirrorSelector.SelectedItem.ToString();
                    LogWriteLine($"Mirror: \u001b[33;1m{MirrorSelectorStatus.Content}\u001b[0m is selected!");
                    SaveAppConfig();
                }
            });

        private void ChangeUpdateDownload(bool a) =>
            Dispatcher.Invoke(() =>
            {
                UpdateDownloadBtn.IsEnabled = a;
                UpdateListView.IsEnabled = a;
            });

        private void ChangeUpdateStatus(string s, bool b) =>
            Dispatcher.Invoke(() =>
            {
                UpdateCheckStatus.Content = s;
                UpdateCheckBtn.IsEnabled = b;
            });
    }
}
