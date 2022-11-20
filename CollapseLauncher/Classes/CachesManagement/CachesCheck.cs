using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.UABT;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        ObservableCollection<DataPropertiesUI> brokenCachesListUI = new ObservableCollection<DataPropertiesUI>();

        Stream cachesStream;
        FileInfo cachesFileInfo;
        string[] cacheRegionalCheckName = new string[1] { "sprite" };
        string cachesLanguage;
        string cachesAPIURL, cachesURL, cachesEndpointURL, cachesBasePath, cachesPath;
        HMACSHA1 hashTool;

        int cachesTotalCount,
            cachesCount;

        long cachesTotalSize,
             cachesRead;

        List<DataProperties> CacheProperties = new List<DataProperties>();
        List<DataProperties> BrokenCachesProperties = new List<DataProperties>();

        private void ResetCacheList()
        {
            CacheProperties.Clear();
            BrokenCachesProperties.Clear();
        }

        private async Task DoCachesCheck()
        {
            try
            {
                cachesRead = 0;
                cachesCount = 0;
                cachesTotalCount = 0;
                cachesTotalSize = 0;
                cancellationTokenSource = new CancellationTokenSource();
                CachesDataTableGrid.Visibility = Visibility.Collapsed;
                brokenCachesListUI.Clear();
                CancelBtn.Visibility = Visibility.Visible;
                CheckUpdateBtn.IsEnabled = false;
                CancelBtn.IsEnabled = true;
                cachesLanguage = CurrentConfigV2.GetGameLanguage();
                await FetchCachesAPI();
                CachesDataTableGrid.Visibility = Visibility.Visible;
                await CheckCachesIntegrity();
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
                LogWriteLine(ex.ToString(), LogType.Error, true);
                ResetCacheList();
            }
        }

        private async Task<(uint, long)> ReadDataVersion(CachesType type)
        {
            using (Http _client = new Http())
            using (cachesStream = new MemoryStream())
            {
                cachesAPIURL = string.Format(CurrentConfigV2.CachesEndpointURL + "{1}Version.unity3d", type.ToString().ToLowerInvariant(), type == CachesType.Data ? "Data" : "Resource");
                LogWriteLine($"Fetching CachesType: {type}");

                CachesStatus.Text = string.Format(Lang._CachesPage.CachesStatusFetchingType, type);

                _client.DownloadProgress += DataFetchingProgress;
                await _client.Download(cachesAPIURL, cachesStream, null, null, cancellationTokenSource.Token);
                _client.DownloadProgress -= DataFetchingProgress;
                cachesStream.Position = 0;

                using (Stream stream = new XORStream(cachesStream))
                {
                    BundleFile bundleFile = new BundleFile(stream);
                    SerializedFile serializedFile = new SerializedFile(bundleFile.fileList[0].stream);
                    byte[] dataRaw = serializedFile.GetDataFirstOrDefaultByName("packageversion.txt");
                    TextAsset dataTextAsset = new TextAsset(dataRaw);
                    return BuildVersioningList(dataTextAsset.GetStringEnumeration(), type);
                }
            }
        }

        private (uint, long) BuildVersioningList(SpanLineEnumerator dataList, CachesType type)
        {
            uint count = 0;
            long size = 0;
            bool isFirst = type == CachesType.Data;

            if (isFirst)
            {
                // BuildVersioningPatchList(type);
            }

            DataProperties localProp = new DataProperties()
            {
                DataType = type
            };
            localProp.Content = new List<DataPropertiesContent>();

            foreach (ReadOnlySpan<char> data in dataList)
            {
                if (isFirst)
                {
                    isFirst = false;
                    localProp.HashSalt = data.ToString();
                    continue;
                }
                if (data.Length > 0)
                {
                    DataPropertiesContent content = (DataPropertiesContent)JsonSerializer.Deserialize(data, typeof(DataPropertiesContent), DataPropertiesContentContext.Default);
                    content.DataType = type;
                    if (FilterRegion(content.N, cachesLanguage) > 0)
                    {
                        count++;
                        size += content.CS;
                        localProp.Content.Add(content);
                    }
                }
            }

            CacheProperties.Add(localProp);

            return (count, size);
        }

        private async Task FetchCachesAPI()
        {
            foreach (CachesType type in Enum.GetValues(typeof(CachesType)))
            {
                (uint, long) Count_Size = await ReadDataVersion(type);

                LogWriteLine($"Cache Metadata:"
                    + $"\r\n\t\tCache Count = {Count_Size.Item1}"
                    + $"\r\n\t\tCache Size = {SummarizeSizeSimple(Count_Size.Item2)}", LogType.NoTag);
            }

            cachesTotalCount = CacheProperties.Sum(x => x.Content.Count);
            cachesTotalSize = CacheProperties.Sum(x => x.Content.Sum(x => x.CS));

            LogWriteLine($"Cache Counts (in Catalog): {cachesTotalCount} | Cache Size (in Catalog): {SummarizeSizeSimple(cachesTotalSize)}");
        }

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

        private string GetCachePathByType(CachesType type)
        {
            switch (type)
            {
                case CachesType.Data:
                    return Path.Combine(cachesBasePath, "Data");
                default:
                    return Path.Combine(cachesBasePath, "Resources");
            }
        }

        private async Task CheckCachesIntegrity()
        {
            cachesBasePath = Path.Combine(GameAppDataFolder, Path.GetFileName(CurrentConfigV2.ConfigRegistryLocation));
            string cachesPathType;
            string hash;
            List<DataPropertiesContent> brokenCaches;
            BrokenCachesProperties = new List<DataProperties>();
            byte[] salt = new mhyEncTool(CacheProperties.Where(x => x.DataType == CachesType.Data)
                .FirstOrDefault().HashSalt, ConfigV2.MasterKey).GetSalt();
            hashTool = new HMACSHA1(salt);

            foreach (DataProperties dataType in CacheProperties)
            {
                brokenCaches = new List<DataPropertiesContent>();

                cachesPathType = GetCachePathByType(dataType.DataType);

                CleanUpCaches(dataType, cachesPathType);

                foreach (DataPropertiesContent content in dataType.Content)
                {
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    cachesCount++;
                    cachesPath = Path.Combine(cachesPathType, NormalizePath(content.ConcatN));
                    CachesStatus.Text = string.Format(Lang._CachesPage.CachesStatusChecking, dataType.DataType, content.N);
                    CachesTotalStatus.Text = string.Format(Lang._CachesPage.CachesTotalStatusChecking, cachesCount, cachesTotalCount);
                    CachesTotalProgressBar.Value = GetPercentageNumber(cachesCount, cachesTotalCount);

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
                                brokenCachesListUI.Add(new DataPropertiesUI
                                {
                                    FileName = Path.GetFileName(content.N),
                                    FileSizeStr = SummarizeSizeSimple(content.CS),
                                    CacheStatus = CachesDataStatus.Obsolete,
                                    DataType = dataType.DataType,
                                    FileSource = Path.GetDirectoryName(content.N),
                                    FileLastModified = File.GetLastWriteTime(cachesPath).ToLocalTime().ToString("yyyy/MM/dd HH:mm"),
                                    FileNewModified = DateTimeOffset.FromUnixTimeSeconds(dataType.Timestamp).ToLocalTime().ToString("yyyy/MM/dd HH:mm")
                                });
                                LogWriteLine($"New: {content.N}", LogType.Warning, true);
                            }
                        }
                    }
                    else
                    {
                        content.Status = CachesDataStatus.New;
                        brokenCaches.Add(content);
                        brokenCachesListUI.Add(new DataPropertiesUI
                        {
                            FileName = Path.GetFileName(content.N),
                            FileSizeStr = SummarizeSizeSimple(content.CS),
                            CacheStatus = CachesDataStatus.New,
                            DataType = dataType.DataType,
                            FileSource = Path.GetDirectoryName(content.N),
                            FileLastModified = "-",
                            FileNewModified = DateTimeOffset.FromUnixTimeSeconds(dataType.Timestamp).ToLocalTime().ToString("yyyy/MM/dd HH:mm")
                        });
                        LogWriteLine($"Missing: {content.N}", LogType.Warning, true);
                    }
                }

                if (brokenCaches.Count > 0)
                {
                    BrokenCachesProperties.Add(new DataProperties
                    {
                        DataType = dataType.DataType,
                        PackageVersion = dataType.PackageVersion,
                        Timestamp = dataType.Timestamp,
                        Content = brokenCaches
                    });
                }
            }

            if (BrokenCachesProperties.Count > 0)
            {
                cachesTotalCount = BrokenCachesProperties.Sum(x => x.Content.Count);
                cachesTotalSize = BrokenCachesProperties.Sum(x => x.Content.Sum(y => y.CS));
                CachesStatus.Text = string.Format(Lang._CachesPage.CachesStatusNeedUpdate, cachesTotalCount, SummarizeSizeSimple(cachesTotalSize));
                CachesTotalStatus.Text = Lang._CachesPage.CachesTotalStatusNone;
                CachesTotalProgressBar.Value = 0;
                CheckUpdateBtn.Visibility = Visibility.Collapsed;
                UpdateCachesBtn.Visibility = Visibility.Visible;
                UpdateCachesBtn.IsEnabled = true;
                CancelBtn.IsEnabled = false;
                LogWriteLine($"{cachesTotalCount} caches ({SummarizeSizeSimple(cachesTotalSize)}) is available to be updated.", LogType.Default, true);
            }
            else
            {
                CachesStatus.Text = Lang._CachesPage.CachesStatusUpToDate;
                CachesTotalStatus.Text = Lang._CachesPage.CachesTotalStatusNone;
                CachesTotalProgressBar.Value = 0;
                CheckUpdateBtn.Visibility = Visibility.Visible;
                CheckUpdateBtn.IsEnabled = true;
                UpdateCachesBtn.Visibility = Visibility.Collapsed;
                CancelBtn.IsEnabled = false;
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
                if (!prop.Content.Where(x => Path.GetFileNameWithoutExtension(x.ConcatN).Contains(localFileName)).Any())
                {
                    LogWriteLine($"Removing unused cache: {file}", LogType.Default, true);
                    File.Delete(file);
                }
            }
        }

        private void DataFetchingProgress(object sender, DownloadEvent e)
        {
            DispatcherQueue.TryEnqueue(() => { CachesTotalStatus.Text = string.Format(Lang._Misc.Speed, SummarizeSizeSimple(e.Speed)); });
        }
    }
}
