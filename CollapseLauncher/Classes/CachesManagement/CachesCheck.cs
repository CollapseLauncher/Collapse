using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public sealed partial class CachesPage : Page
    {
        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        ObservableCollection<DataPropertiesUI> brokenCachesListUI = new ObservableCollection<DataPropertiesUI>();

        Http http = new Http();
        Stream cachesStream;
        FileInfo cachesFileInfo;
        string[] cacheRegionalCheckName = new string[1] { "sprite" };
        string cachesLanguage;
        string cachesAPIURL, cachesURL, cachesEndpointURL, cachesBasePath, cachesPath;
        HMACSHA1 hashTool;

        string Pkcs1Salt;

        int cachesTotalCount,
            cachesCount;

        long cachesTotalSize,
             cachesRead;

        List<DataProperties> cachesList;
        List<DataProperties> brokenCachesList;

        private async Task DoCachesCheck()
        {
            try
            {
                cachesRead = 0;
                cachesCount = 0;
                cachesTotalCount = 0;
                cachesTotalSize = 0;
                cancellationTokenSource = new CancellationTokenSource();
                DispatcherQueue.TryEnqueue(() =>
                {
                    CachesDataTableGrid.Visibility = Visibility.Collapsed;
                    brokenCachesListUI.Clear();
                    CancelBtn.Visibility = Visibility.Visible;
                    CheckUpdateBtn.IsEnabled = false;
                    CancelBtn.IsEnabled = true;
                });
                cachesLanguage = CurrentRegion.GetGameLanguage();
                await FetchCachesAPI();
                DispatcherQueue.TryEnqueue(() => CachesDataTableGrid.Visibility = Visibility.Visible);
                await CheckCachesIntegrity();
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
                http.DownloadProgress -= DataFetchingProgress;
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex);
                LogWriteLine(ex.ToString(), LogType.Error, true);
            }
        }

        private async Task FetchCachesAPI()
        {
            DataProperties cacheCatalog;
            cachesList = new List<DataProperties>();
            foreach (CachesType type in Enum.GetValues(typeof(CachesType)))
            {
                using (cachesStream = new MemoryStream())
                {
                    cachesAPIURL = string.Format(CurrentRegion.CachesListAPIURL, (byte)type, CurrentRegion.CachesListGameVerID);
                    LogWriteLine($"Fetching CachesType: {type}");

                    DispatcherQueue.TryEnqueue(() => CachesStatus.Text = string.Format(Lang._CachesPage.CachesStatusFetchingType, type));

                    http.DownloadProgress += DataFetchingProgress;
                    await http.DownloadStream(cachesAPIURL, cachesStream, cancellationTokenSource.Token);
                    http.DownloadProgress -= DataFetchingProgress;

                    cacheCatalog = JsonConvert.DeserializeObject<DataProperties>(
                        Encoding.UTF8.GetString(
                            (cachesStream as MemoryStream).GetBuffer()
                        ));

                    if (cacheCatalog.HashSalt != null)
                        Pkcs1Salt = cacheCatalog.HashSalt;

                    EliminateNonRegionalCaches(cacheCatalog);

                    cacheCatalog.DataType = type;
                }

                LogWriteLine($"Cache Metadata:"
                    + $"\r\n\t\tDate (Local Time) = {DateTimeOffset.FromUnixTimeSeconds(cacheCatalog.Timestamp).ToLocalTime().ToString("dddd, dd MMMM yyyy HH:mm:ss")}"
                    + $"\r\n\t\tVersion = {cacheCatalog.PackageVersion}"
                    + $"\r\n\t\tCache Count = {cacheCatalog.Content.Count}"
                    + $"\r\n\t\tCache Size = {SummarizeSizeSimple(cacheCatalog.Content.Sum(x => x.CS))}", LogType.NoTag);

                cachesList.Add(cacheCatalog);
            }

            cachesTotalCount = cachesList.Sum(x => x.Content.Count);
            cachesTotalSize = cachesList.Sum(x => x.Content.Sum(y => y.CS));

            LogWriteLine($"Cache Counts (in Catalog): {cachesTotalCount} | Cache Size (in Catalog): {SummarizeSizeSimple(cachesTotalSize)}");
        }

        private void EliminateNonRegionalCaches(in DataProperties data) => data.Content = new List<DataPropertiesContent>(data.Content.Where(x => FilterRegion(x.N, cachesLanguage) > 0).ToList());

        private byte FilterRegion(string input, string regionName)
        {
            foreach (string word in cacheRegionalCheckName)
            {
                if (input.Contains(word))
                    if (input.Contains($"{word}_{regionName}"))
                        return 1;
                    else
                        return 0;
            }
            return 2;
        }

        private async Task CheckCachesIntegrity()
        {
            cachesBasePath = Path.Combine(GameAppDataFolder, Path.GetFileName(CurrentRegion.ConfigRegistryLocation));
            string cachesPathType;
            string hash;
            List<DataPropertiesContent> brokenCaches;
            brokenCachesList = new List<DataProperties>();
            byte[] salt = new mhySHASaltTool(Pkcs1Salt).GetSalt();
            hashTool = new HMACSHA1(salt);

            foreach (DataProperties dataType in cachesList)
            {
                brokenCaches = new List<DataPropertiesContent>();

                switch (dataType.DataType)
                {
                    case CachesType.Data:
                        cachesPathType = Path.Combine(cachesBasePath, "Data");
                        break;
                    default:
                        cachesPathType = Path.Combine(cachesBasePath, "Resources");
                        break;
                }

                CleanUpCaches(dataType, cachesPathType);

                foreach (DataPropertiesContent content in dataType.Content)
                {
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    cachesCount++;
                    cachesPath = Path.Combine(cachesPathType, NormalizePath(content.ConcatN()));
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        CachesStatus.Text = string.Format(Lang._CachesPage.CachesStatusChecking, dataType.DataType, content.N);
                        CachesTotalStatus.Text = string.Format(Lang._CachesPage.CachesTotalStatusChecking, cachesCount, cachesTotalCount);
                        CachesTotalProgressBar.Value = GetPercentageNumber(cachesCount, cachesTotalCount);
                    });

                    cachesFileInfo = new FileInfo(cachesPath);

                    if (cachesFileInfo.Exists)
                    {
                        using (cachesStream = new FileStream(cachesPath, FileMode.Open, FileAccess.Read))
                        {
                            await hashTool.ComputeHashAsync(cachesStream);
                            hash = BytesToHex(hashTool.Hash);

                            if (hash != content.CRC)
                            {
                                content.Status = CachesDataStatus.Obsolete;
                                brokenCaches.Add(content);
                                DispatcherQueue.TryEnqueue(() => brokenCachesListUI.Add(new DataPropertiesUI
                                {
                                    FileName = Path.GetFileName(content.N),
                                    FileSizeStr = SummarizeSizeSimple(content.CS),
                                    CacheStatus = CachesDataStatus.Obsolete,
                                    DataType = dataType.DataType,
                                    FileSource = Path.GetDirectoryName(content.N),
                                    FileLastModified = File.GetLastWriteTime(cachesPath).ToLocalTime().ToString("yyyy/MM/dd HH:mm"),
                                    FileNewModified = DateTimeOffset.FromUnixTimeSeconds(dataType.Timestamp).ToLocalTime().ToString("yyyy/MM/dd HH:mm")
                                }));
                                LogWriteLine($"Obsolete: {content.N}", LogType.Warning);
                            }
                        }
                    }
                    else
                    {
                        content.Status = CachesDataStatus.Missing;
                        brokenCaches.Add(content);
                        DispatcherQueue.TryEnqueue(() => brokenCachesListUI.Add(new DataPropertiesUI
                        {
                            FileName = Path.GetFileName(content.N),
                            FileSizeStr = SummarizeSizeSimple(content.CS),
                            CacheStatus = CachesDataStatus.Missing,
                            DataType = dataType.DataType,
                            FileSource = Path.GetDirectoryName(content.N),
                            FileLastModified = File.GetLastWriteTime(cachesPath).ToLocalTime().ToString("yyyy/MM/dd HH:mm"),
                            FileNewModified = DateTimeOffset.FromUnixTimeSeconds(dataType.Timestamp).ToLocalTime().ToString("yyyy/MM/dd HH:mm")
                        }));
                        LogWriteLine($"Missing: {content.N}", LogType.Warning, true);
                    }
                }

                if (brokenCaches.Count > 0)
                {
                    brokenCachesList.Add(new DataProperties
                    {
                        DataType = dataType.DataType,
                        PackageVersion = dataType.PackageVersion,
                        Timestamp = dataType.Timestamp,
                        Content = brokenCaches
                    });
                }
            }

            if (brokenCachesList.Count > 0)
            {
                cachesTotalCount = brokenCachesList.Sum(x => x.Content.Count);
                cachesTotalSize = brokenCachesList.Sum(x => x.Content.Sum(y => y.CS));
                DispatcherQueue.TryEnqueue(() =>
                {
                    CachesStatus.Text = string.Format(Lang._CachesPage.CachesStatusNeedUpdate, cachesTotalCount, SummarizeSizeSimple(cachesTotalSize));
                    CachesTotalStatus.Text = Lang._CachesPage.CachesTotalStatusNone;
                    CachesTotalProgressBar.Value = 0;
                    CheckUpdateBtn.Visibility = Visibility.Collapsed;
                    UpdateCachesBtn.Visibility = Visibility.Visible;
                    UpdateCachesBtn.IsEnabled = true;
                    CancelBtn.IsEnabled = false;
                });
                LogWriteLine($"{cachesTotalCount} caches ({SummarizeSizeSimple(cachesTotalSize)}) is available to be updated.", LogType.Default, true);
            }
            else
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    CachesStatus.Text = Lang._CachesPage.CachesStatusUpToDate;
                    CachesTotalStatus.Text = Lang._CachesPage.CachesTotalStatusNone;
                    CachesTotalProgressBar.Value = 0;
                    CheckUpdateBtn.Visibility = Visibility.Visible;
                    CheckUpdateBtn.IsEnabled = true;
                    UpdateCachesBtn.Visibility = Visibility.Collapsed;
                    CancelBtn.IsEnabled = false;
                });
            }
        }

        private void CleanUpCaches(DataProperties prop, string cachesLocalPath)
        {
            if (!Directory.Exists(Path.Combine(cachesLocalPath, prop.DataType.ToString().ToLower())))
                return;

            IEnumerable<string> localFiles = Directory.GetFiles(Path.Combine(cachesLocalPath, prop.DataType.ToString().ToLower()), "*.*", SearchOption.AllDirectories);
            string localFileName;
            foreach (string file in localFiles)
            {
                localFileName = Path.GetFileNameWithoutExtension(file);
                if (!prop.Content.Where(x => Path.GetFileNameWithoutExtension(x.ConcatN()).Contains(localFileName)).Any())
                {
                    LogWriteLine($"Removing unused cache: {file}", LogType.Default, true);
                    File.Delete(file);
                }
            }
        }

        private void DataFetchingProgress(object sender, DownloadEvent e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                CachesTotalStatus.Text = string.Format(Lang._Misc.Speed, SummarizeSizeSimple(e.Speed));
            });
        }
    }
}
