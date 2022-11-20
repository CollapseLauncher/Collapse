using Hi3Helper;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Preset.ConfigV2Store;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public sealed partial class CachesPage : Page
    {
        int DownloadThread = GetAppConfigValue("DownloadThread").ToInt();
        private async Task DoCachesUpdate()
        {
            try
            {
                SpeedStopwatch = Stopwatch.StartNew();
                cachesRead = 0;
                cachesCount = 0;
                cancellationTokenSource = new CancellationTokenSource();

                UpdateCachesBtn.IsEnabled = false;
                CancelBtn.IsEnabled = true;
                await DownloadCachesUpdate();
                CacheReindexing();

                CachesStatus.Text = Lang._Misc.Completed;
                CachesTotalStatus.Text = Lang._CachesPage.CachesTotalStatusNone;
                CachesTotalProgressBar.Value = 0;
                UpdateCachesBtn.IsEnabled = true;
                CheckUpdateBtn.IsEnabled = true;
                UpdateCachesBtn.Visibility = Visibility.Collapsed;
                CheckUpdateBtn.Visibility = Visibility.Visible;
                CancelBtn.IsEnabled = false;

                ResetCacheList();
            }
            catch (OperationCanceledException)
            {
                LogWriteLine("Caches Update check cancelled!", LogType.Warning);
                CachesStatus.Text = Lang._CachesPage.CachesStatusCancelled;
                CachesTotalStatus.Text = Lang._CachesPage.CachesTotalStatusNone;
                CachesTotalProgressBar.Value = 0;
                CheckUpdateBtn.Visibility = Visibility.Visible;
                CheckUpdateBtn.IsEnabled = true;
                UpdateCachesBtn.Visibility = Visibility.Collapsed;
                CancelBtn.IsEnabled = false;

                ResetCacheList();
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex);
                ResetCacheList();
            }
        }

        private async Task DownloadCachesUpdate()
        {
            string cachesPathType;
            await Task.Run(() =>
            {
                using (Http _client = new Http())
                {
                    foreach (DataProperties dataType in BrokenCachesProperties)
                    {
                        cachesPathType = GetCachePathByType(dataType.DataType);

                        cachesEndpointURL = string.Format(CurrentConfigV2.CachesEndpointURL, dataType.DataType.ToString().ToLower());
                        LogWriteLine($"Downloading Cache Type {dataType.DataType} with endpoint: {cachesEndpointURL}", LogType.Default, true);

                        _client.DownloadProgress += CachesDownloadProgress;
                        foreach (DataPropertiesContent content in dataType.Content)
                        {
                            cachesCount++;
                            cachesURL = cachesEndpointURL + content.ConcatNRemote;
                            cachesPath = Path.Combine(cachesPathType, NormalizePath(content.ConcatN));

                            if (!Directory.Exists(Path.GetDirectoryName(cachesPath)))
                                Directory.CreateDirectory(Path.GetDirectoryName(cachesPath));

                            DispatcherQueue.TryEnqueue(() => CachesStatus.Text = string.Format(Lang._Misc.Downloading + " {0}: {1}", dataType.DataType, content.N));

                            if (content.CS >= 10 << 20)
                            {
                                _client.DownloadSync(cachesURL, cachesPath, (byte)DownloadThread, true, cancellationTokenSource.Token);
                                _client.MergeSync();
                            }
                            else
                            {
                                _client.DownloadSync(cachesURL, cachesPath, true, null, null, cancellationTokenSource.Token);
                            }

                            LogWriteLine($"Downloaded: {content.N}", LogType.Default, true);

                            DispatcherQueue.TryEnqueue(() => brokenCachesListUI.RemoveAt(0));
                        }
                        _client.DownloadProgress -= CachesDownloadProgress;
                    }
                }
            });
        }

        private void CacheReindexing()
        {
            string cachesPathType;
            string indexPath = Path.Combine(cachesBasePath, "Data", "Verify.txt");

            if (File.Exists(indexPath))
                File.Delete(indexPath);

            using (StreamWriter sw = new StreamWriter(indexPath))
            {
                foreach (DataProperties dataType in CacheProperties)
                {
                    cachesPathType = GetCachePathByType(dataType.DataType);

                    foreach (DataPropertiesContent content in dataType.Content)
                    {
                        // Why concating "/" between them?
                        // Well, It's just miHoYo's thing.
                        cachesPath = cachesPathType.Replace('\\', '/') + $"//{content.ConcatN}";
                        sw.WriteLine(cachesPath);
                    }
                }
            }
        }

        Stopwatch SpeedStopwatch = Stopwatch.StartNew();
        string timeLeftString;
        private void CachesDownloadProgress(object sender, DownloadEvent e)
        {
            if (e.State != MultisessionState.Merging)
            {
                cachesRead += e.Read;
            }

            timeLeftString = string.Format(Lang._Misc.TimeRemainHMSFormat, TimeSpan.FromSeconds((cachesTotalSize - cachesRead) / Unzeroed((long)(cachesRead / SpeedStopwatch.Elapsed.TotalSeconds))));

            DispatcherQueue.TryEnqueue(() =>
            {
                CachesTotalStatus.Text = string.Format(Lang._Misc.Downloading + ": {0}/{1} ", cachesCount, cachesTotalCount)
                                       + string.Format($"({Lang._Misc.SpeedPerSec})", SummarizeSizeSimple(e.Speed))
                                       + $" | {timeLeftString}";
                CachesTotalProgressBar.Value = GetPercentageNumber(cachesRead, cachesTotalSize);
            });
        }
    }
}
