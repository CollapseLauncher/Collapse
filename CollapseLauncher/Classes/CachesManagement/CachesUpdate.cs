using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Http;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Logger;
using static Hi3Helper.Locale;
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

                DispatcherQueue.TryEnqueue(() =>
                {
                    UpdateCachesBtn.IsEnabled = false;
                    CancelBtn.IsEnabled = true;
                });
                await DownloadCachesUpdate();
                CacheReindexing();

                DispatcherQueue.TryEnqueue(() =>
                {
                    CachesStatus.Text = Lang._Misc.Completed;
                    CachesTotalStatus.Text = Lang._CachesPage.CachesTotalStatusNone;
                    CachesTotalProgressBar.Value = 0;
                    UpdateCachesBtn.IsEnabled = true;
                    CheckUpdateBtn.IsEnabled = true;
                    UpdateCachesBtn.Visibility = Visibility.Collapsed;
                    CheckUpdateBtn.Visibility = Visibility.Visible;
                    CancelBtn.IsEnabled = false;
                });
            }
            catch (OperationCanceledException)
            {
                LogWriteLine("Caches Update check cancelled!", LogType.Warning);
                DispatcherQueue.TryEnqueue(() =>
                {
                    CachesStatus.Text = Lang._CachesPage.CachesStatusCancelled;
                    CachesTotalStatus.Text = Lang._CachesPage.CachesTotalStatusNone;
                    CachesTotalProgressBar.Value = 0;
                    CheckUpdateBtn.Visibility = Visibility.Visible;
                    CheckUpdateBtn.IsEnabled = true;
                    UpdateCachesBtn.Visibility = Visibility.Collapsed;
                    CancelBtn.IsEnabled = false;
                });
                http.DownloadProgress -= CachesDownloadProgress;
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex);
            }
        }

        private async Task DownloadCachesUpdate()
        {
            string cachesPathType;
            byte DownloadThread = (byte)GetAppConfigValue("DownloadThread").ToInt();
            foreach (DataProperties dataType in brokenCachesList)
            {
                switch (dataType.DataType)
                {
                    case CachesType.Data:
                        cachesPathType = Path.Combine(cachesBasePath, "Data");
                        break;
                    default:
                        cachesPathType = Path.Combine(cachesBasePath, "Resources");
                        break;
                }

                cachesEndpointURL = string.Format(CurrentRegion.CachesEndpointURL, dataType.DataType.ToString().ToLower());
                LogWriteLine($"Downloading Cache Type {dataType.DataType} with endpoint: {cachesEndpointURL}", LogType.Default, true);
                foreach (DataPropertiesContent content in dataType.Content)
                {
                    cachesCount++;
                    cachesURL = cachesEndpointURL + content.ConcatNRemote();
                    cachesPath = Path.Combine(cachesPathType, NormalizePath(content.ConcatN()));

                    if (!Directory.Exists(Path.GetDirectoryName(cachesPath)))
                        Directory.CreateDirectory(Path.GetDirectoryName(cachesPath));

                    cachesFileInfo = new FileInfo(cachesPath);

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        CachesStatus.Text = string.Format(Lang._Misc.Downloading + " {0}: {1}", dataType.DataType, content.N);
                    });

                    if (content.CS >= 4 << 20)
                    {
                        http.DownloadProgress += CachesDownloadProgress;
                        await http.DownloadMultisession(cachesURL, cachesPath, true, DownloadThread, cancellationTokenSource.Token);
                        http.DownloadProgress -= CachesDownloadProgress;
                        await http.MergeMultisession(cachesPath, DownloadThread, cancellationTokenSource.Token);
                    }
                    else
                    {
                        http.DownloadProgress += CachesDownloadProgress;
                        using (cachesStream = cachesFileInfo.Create())
                            await http.DownloadStream(cachesURL, cachesStream, cancellationTokenSource.Token);
                        http.DownloadProgress -= CachesDownloadProgress;
                    }

                    LogWriteLine($"Downloaded: {content.N}", LogType.Default, true);

                    DispatcherQueue.TryEnqueue(() => brokenCachesListUI.RemoveAt(0));
                }
            }
        }

        private void CacheReindexing()
        {
            string cachesPathType;
            string indexPath = Path.Combine(cachesBasePath, "Data", "Verify.txt");

            if (File.Exists(indexPath))
                File.Delete(indexPath);

            using (StreamWriter sw = new StreamWriter(indexPath))
            {
                foreach (DataProperties dataType in cachesList)
                {
                    switch (dataType.DataType)
                    {
                        case CachesType.Data:
                            cachesPathType = Path.Combine(cachesBasePath, "Data");
                            break;
                        default:
                            cachesPathType = Path.Combine(cachesBasePath, "Resources");
                            break;
                    }

                    foreach (DataPropertiesContent content in dataType.Content)
                    {
                        // Why concating "/" between them?
                        // Well, It's just miHoYo's thing.
                        cachesPath = cachesPathType.Replace('\\', '/') + $"//{content.ConcatN()}";
                        sw.WriteLine(cachesPath);
                    }
                }
            }
        }

        Stopwatch refreshTime = Stopwatch.StartNew();
        Stopwatch SpeedStopwatch = Stopwatch.StartNew();
        string timeLeftString;
        private void CachesDownloadProgress(object sender, DownloadEvent e)
        {
            if (http.SessionState != MultisessionState.Merging)
                cachesRead += e.Read;

            if (refreshTime.Elapsed.Milliseconds >= 500)
            {
                refreshTime = Stopwatch.StartNew();
                timeLeftString = string.Format(Lang._Misc.TimeRemainHMSFormat, TimeSpan.FromSeconds((cachesTotalSize - cachesRead) / Unzeroed((long)(cachesRead / SpeedStopwatch.Elapsed.TotalSeconds))));
            }

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
