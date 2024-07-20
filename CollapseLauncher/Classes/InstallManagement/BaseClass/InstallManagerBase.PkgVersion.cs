using CollapseLauncher.Helper;
using CollapseLauncher.Helper.JsonConverter;
using CollapseLauncher.Helper.Loading;
using CollapseLauncher.Pages;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.InstallManager.Base
{
    public class LocalFileInfo
    {
        [JsonPropertyName("remoteName")]
        [JsonConverter(typeof(SlashToBackslashConverter))]
        public string RelativePath { get; set; }

        [JsonPropertyName("md5")]
        [JsonConverter(typeof(HexStringToBytesConverter))]
        public byte[] MD5Hash { get; set; }

        [JsonPropertyName("hash")]
        [JsonConverter(typeof(HexStringToBytesConverter))]
        public byte[] XXH64Hash { get; set; }

        [JsonPropertyName("fileSize")]
        public long FileSize { get; set; }

        [JsonIgnore]
        public string FullPath { get; set; }

        [JsonIgnore]
        public string FileName { get; set; }

        [JsonIgnore]
        public bool IsFileExist { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public LocalFileInfo() { }

        public LocalFileInfo(FileInfo fileInfo, string basePath)
        {
            FullPath = fileInfo.FullName;
            FileName = Path.GetFileName(fileInfo.FullName);
            RelativePath = GetRelativePath(FullPath, basePath);
            Update();
        }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        private string GetRelativePath(string fullPath, string basePath)
        {
            ReadOnlySpan<char> relativePath = fullPath.AsSpan(basePath.Length).TrimStart('\\');
            return relativePath.ToString();
        }

        public void Update()
        {
            FileInfo fileInfo = new FileInfo(FullPath);
            IsFileExist = fileInfo.Exists;
            if (fileInfo.Exists)
            {
                FileSize = fileInfo.Length;
            }
        }

        public FileInfo ToFileInfo() => new FileInfo(string.IsNullOrEmpty(FullPath) ? RelativePath : FullPath);

        public async ValueTask UpdateFileHash(CancellationToken token)
        {
            if (!IsFileExist) return;

            using FileStream stream = File.Open(FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            MD5Hash = await MD5.HashDataAsync(stream, token);
            stream.Position = 0;
            XxHash64 xxh64 = new XxHash64();
            await xxh64.AppendAsync(stream, token);
            XXH64Hash = xxh64.GetHashAndReset();
        }

        public override string ToString()
        {
            return RelativePath ?? string.Empty;
        }
    }

    internal partial class InstallManagerBase<T>
    {
        public virtual async ValueTask CleanUpGameFiles(bool withDialog = true)
        {
            // Get the unused file info asynchronously
            List<LocalFileInfo> unusedFileInfo = await GetUnusedFileInfoList(withDialog);

            // Spawn dialog if used
            if (withDialog)
            {
                if (WindowUtility.CurrentWindow is MainWindow mainWindow)
                {
                    mainWindow?.overlayFrame.BackStack?.Clear();
                    mainWindow?.overlayFrame.Navigate(typeof(NullPage));
                    mainWindow?.overlayFrame.Navigate(typeof(FileCleanupPage), null, new DrillInNavigationTransitionInfo());
                }
                if (FileCleanupPage.Current != null)
                {
                    FileCleanupPage.Current.InjectFileInfoSource(unusedFileInfo);
                    FileCleanupPage.Current.MenuExitButton.Click += ExitFromOverlay;
                    FileCleanupPage.Current.MenuReScanButton.Click += ExitFromOverlay;
                    FileCleanupPage.Current.MenuReScanButton.Click += async (_, _) =>
                    {
                        await Task.Delay(250);
                        await CleanUpGameFiles(true);
                    };
                }

                return;
            }

            // Delete the file straight forward if dialog is not used
            foreach (LocalFileInfo fileInfo in unusedFileInfo)
                base.TryDeleteReadOnlyFile(fileInfo.FullPath);

            static void ExitFromOverlay(object? sender, RoutedEventArgs args)
            {
                if (WindowUtility.CurrentWindow is MainWindow mainWindow)
                {
                    mainWindow?.overlayFrame.GoBack();
                    mainWindow?.overlayFrame.BackStack?.Clear();
                }
            }
        }

        protected virtual async Task<List<LocalFileInfo>> GetUnusedFileInfoList(bool includeZipCheck)
        {
            LoadingMessageHelper.ShowLoadingFrame();
            try
            {
                // Initialize uninstall game property
                _uninstallGameProperty ??= AssignUninstallFolders();
                if (!_uninstallGameProperty.HasValue)
                    throw new NotSupportedException("Clean-up feature for this game is not yet supported!");

                // Initialize and get game state, then get the latest package info
                LoadingMessageHelper.SetMessage(
                    Locale.Lang._FileCleanupPage.LoadingTitle,
                    Locale.Lang._FileCleanupPage.LoadingSubtitle2);

                using Http client = new Http();
                GameInstallStateEnum gameStateEnum = await _gameVersionManager.GetGameState();
                RegionResourceVersion? packageLatestBase = _gameVersionManager
                    .GetGameLatestZip(gameStateEnum).FirstOrDefault();
                string? packageExtractBasePath = packageLatestBase?.decompressed_path;

                // Check Fail-safe: Download pkg_version files if not exist
                string pkgVersionPath = Path.Combine(_gamePath, "pkg_version");
                if (!string.IsNullOrEmpty(packageExtractBasePath))
                {
                    // Check Fail-safe: Download main pkg_version file if not exist
                    string mainPkgVersionUrl = ConverterTool.CombineURLFromString(packageExtractBasePath,
                        "pkg_version");
                    await client.Download(mainPkgVersionUrl, pkgVersionPath, _downloadThreadCount, true);
                    await client.Merge(default);

                    // Check Fail-safe: Download audio pkg_version file if not exist
                    if (!string.IsNullOrEmpty(_gameAudioLangListPathStatic) && !string.IsNullOrEmpty(packageExtractBasePath))
                    {
                        if (!File.Exists(_gameAudioLangListPathStatic))
                            throw new FileNotFoundException("Game does have audio lang index file but does not exist!"
                                + $" Expecting location: {_gameAudioLangListPathStatic}");

                        await DownloadOtherAudioPkgVersion(_gameAudioLangListPathStatic,
                            packageExtractBasePath,
                            client);
                    }
                }

                // Check Fail-safe: If the main pkg_version still not exist, throw!
                bool isMainPkgVersionExist = File.Exists(pkgVersionPath);
                if (!isMainPkgVersionExist)
                    throw new FileNotFoundException("Cannot get the file list due to pkg_version file not exist!");

                // Try parse the pkg_versions (including the audio one)
                List<LocalFileInfo> pkgFileInfo = new List<LocalFileInfo>();
                HashSet<string> pkgFileInfoHashSet = new HashSet<string>();
                await ParsePkgVersions2FileInfo(pkgFileInfo, pkgFileInfoHashSet, _token.Token);

                // Get the list of the local file paths
                List<LocalFileInfo> localFileInfo = new List<LocalFileInfo>();
                await GetRelativeLocalFilePaths(localFileInfo, includeZipCheck, _token.Token);

                // Get and filter the unused file from the pkg_versions
                List<LocalFileInfo> unusedFileInfo = new List<LocalFileInfo>();
                await Task.Run(() =>
                    Parallel.ForEach(localFileInfo,
                        new ParallelOptions { CancellationToken = _token.Token },
                        (asset, _) =>
                        {
                            if (!pkgFileInfoHashSet.Contains(asset.RelativePath))
                            {
                                lock (unusedFileInfo)
                                {
                                    unusedFileInfo.Add(asset);
                                }
                            }
                        }));

                return unusedFileInfo;
            }
            finally
            {
                LoadingMessageHelper.HideLoadingFrame();
            }
        }

        protected virtual async ValueTask DownloadOtherAudioPkgVersion(string audioListFilePath, string baseExtractUrl, Http client)
        {
            // Initialize reader
            using StreamReader reader = new StreamReader(audioListFilePath);
            // Read until EOF
            while (!reader.EndOfStream)
            {
                // Read the line and skip if it's empty
                string? line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line)) continue;

                // Get the pkg_version filename, url and then download it
                string pkgFileName = $"Audio_{line.Trim()}_pkg_version";
                string pkgPath = Path.Combine(_gamePath, pkgFileName);
                string pkgUrl = ConverterTool.CombineURLFromString(baseExtractUrl, pkgFileName);
                await client.Download(pkgUrl, pkgPath, _downloadThreadCount, true);
                await client.Merge(default);
            }
        }

        protected virtual async ValueTask ParsePkgVersions2FileInfo(List<LocalFileInfo> pkgFileInfo, HashSet<string> pkgFileInfoHashSet, CancellationToken token)
        {
            string gamePath = _gamePath;

            // Iterate the pkg_version file paths
            foreach (string pkgPath in Directory.EnumerateFiles(gamePath, "*pkg_version", SearchOption.TopDirectoryOnly))
            {
                // Parse and add the entries to the list
                await InnerParsePkgVersion2FileInfo(gamePath, pkgPath, pkgFileInfo, pkgFileInfoHashSet, token);
            }
        }

        protected virtual async ValueTask InnerParsePkgVersion2FileInfo(string gamePath, string path, List<LocalFileInfo> pkgFileInfo, HashSet<string> pkgFileInfoHashSet, CancellationToken token)
        {
            // Assign path to reader
            using StreamReader reader = new StreamReader(path, true);
            // Do loop until EOF
            while (!reader.EndOfStream)
            {
                // Read line and deserialize
                string? line = await reader.ReadLineAsync(token);
                LocalFileInfo? localFileInfo = line?.Deserialize<LocalFileInfo>(InternalAppJSONContext.Default);

                // Assign the values
                if (localFileInfo != null)
                {
                    localFileInfo.FullPath = Path.Combine(gamePath, localFileInfo.RelativePath);
                    localFileInfo.FileName = Path.GetFileName(localFileInfo.RelativePath);
                    localFileInfo.IsFileExist = File.Exists(localFileInfo.FullPath);

                    // Add it to the list and hashset
                    pkgFileInfo.Add(localFileInfo);
                    pkgFileInfoHashSet.Add(localFileInfo.RelativePath);
                }
            }
        }

        protected virtual bool IsCategorizedAsGameFile(FileInfo fileInfo, string gamePath, bool includeZipCheck, out LocalFileInfo localFileInfo)
        {
            // Convert to LocalFileInfo and get the relative path
            localFileInfo = new LocalFileInfo(fileInfo, gamePath);
            string relativePath = localFileInfo.RelativePath;
            ReadOnlySpan<char> relativePathSpan = relativePath;
            string fileName = localFileInfo.FileName;
            string gameFolder = _uninstallGameProperty?.gameDataFolderName ?? string.Empty;
            string persistentPath = Path.Combine(gameFolder, "Persistent");

            // 1st check: Ensure that the file is not a persistent file
            if (relativePathSpan.StartsWith(persistentPath, StringComparison.OrdinalIgnoreCase))
                return false;

            // 2nd check: Ensure that the file is not a config or pkg_version file
            if (relativePathSpan.EndsWith("config.ini", StringComparison.OrdinalIgnoreCase)
             || relativePathSpan.EndsWith("pkg_version", StringComparison.OrdinalIgnoreCase))
                return false;

            // 3rd check: Ensure that the file is not a web cache file
            if (relativePathSpan.Contains("webCache", StringComparison.OrdinalIgnoreCase)
             || relativePathSpan.Contains("SDKCache", StringComparison.OrdinalIgnoreCase))
                return false;

            // 4th check: Ensure that the file isn't in excluded list
            if (_uninstallGameProperty?.foldersToKeepInData
                .Any(x => relativePath
                    .AsSpan() // As Span<T> since StartsWith() in it is typically faster
                              // than the one from String primitive
                    .Contains(x.AsSpan(), StringComparison.OrdinalIgnoreCase)) ?? false)
                return false; // Return false if it's not actually in excluded list

            // 5th check: Ensure if the path includes the folder name at start
            if (!string.IsNullOrEmpty(gameFolder) && relativePathSpan
                .StartsWith(gameFolder, StringComparison.OrdinalIgnoreCase))
                return true;

            // 6th check: Ensure if the path includes the DXSETUP folder
            if (Path.GetDirectoryName(relativePathSpan)
                .EndsWith("DXSETUP", StringComparison.OrdinalIgnoreCase))
                return true;

            // 7th check: Ensure that the file is one of the files included
            //            in the Regex pattern list
            if (_uninstallGameProperty?.filesToDelete
                .Any(pattern => Regex.IsMatch(fileName,
                         pattern,
                         RegexOptions.Compiled |
                         RegexOptions.NonBacktracking
                    )) ?? false)
                return true;

            // 8th check: Ensure that the file is one of package files
            if (includeZipCheck && Regex.IsMatch(fileName,
                @"(\.[0-9][0-9][0-9]|zip|7z|patch)$",
                RegexOptions.Compiled |
                RegexOptions.NonBacktracking
             )) return true;

            // If all those matches failed, then return them as a non-game file
            return false;
        }

        protected virtual async Task GetRelativeLocalFilePaths(List<LocalFileInfo> localFileInfoList, bool includeZipCheck, CancellationToken token)
        {
            await Task.Run(() =>
            {
                int count = 0;
                long totalSize = 0;
                string gamePath = _gamePath;
                DirectoryInfo dirInfo = new DirectoryInfo(gamePath);

                // Do the do in parallel since it will be a really CPU expensiven task due to janky checks here and there.
                Parallel.ForEach(dirInfo
                    .EnumerateFiles("*", SearchOption.AllDirectories),
                    new ParallelOptions { CancellationToken = token },
                    (fileInfo, _) =>
                    {
                        // Throw if token is cancelled
                        token.ThrowIfCancellationRequested();

                        // Do the check within the lambda function to possibly check the file
                        // condition in multithread
                        if (IsCategorizedAsGameFile(fileInfo, gamePath, includeZipCheck, out LocalFileInfo localFileInfo))
                        {
                            Interlocked.Add(ref totalSize, fileInfo.Exists ? fileInfo.Length : 0);
                            Interlocked.Increment(ref count);
                            _parentUI.DispatcherQueue.TryEnqueue(() =>
                            LoadingMessageHelper.SetMessage(
                                Locale.Lang._FileCleanupPage.LoadingTitle,
                                string.Format(Locale.Lang._FileCleanupPage.LoadingSubtitle1,
                                    count,
                                    ConverterTool.SummarizeSizeSimple(totalSize))
                            ));
                            lock (localFileInfoList)
                            {
                                localFileInfoList.Add(localFileInfo);
                            }
                        }
                    });
            }, token);
        }
    }
}
