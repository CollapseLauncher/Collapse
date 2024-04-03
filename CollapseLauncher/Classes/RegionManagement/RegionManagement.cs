using CollapseLauncher.Helper.Loading;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Pages;
using CollapseLauncher.Statics;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.RegionResourceListHelper;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public class CancellationTokenSourceWrapper : CancellationTokenSource
    {
        public bool IsDisposed = false;
        public bool IsCancelled = false;

        public new async ValueTask CancelAsync()
        {
            await base.CancelAsync();
            IsCancelled = true;
        }
        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    public sealed partial class MainPage : Page
    {
        private enum ResourceLoadingType
        {
            LocalizedResource,
            DownloadInformation,
            DownloadBackground
        }

        private GamePresetProperty CurrentGameProperty;
        private bool IsLoadRegionComplete;
        private bool IsExplicitCancel { get; set; } = false;
        private bool IsLoadRegionCancellationRequestEnabled { get; set; } = false;

        private uint MaxRetry = 5; // Max 5 times of retry attempt
        private uint LoadTimeout = 10; // 10 seconds of initial Load Timeout
        private uint BackgroundImageLoadTimeout = 3600; // Give background image download 1 hour of timeout
        private uint LoadTimeoutStep = 5; // Step 5 seconds for each timeout retries
        private CancellationTokenSourceWrapper CurrentRegionLoadTokenSource;

        private string RegionToChangeName { get => $"{GetGameTitleRegionTranslationString(LauncherMetadataHelper.CurrentMetadataConfigGameName, Lang._GameClientTitles)} - {GetGameTitleRegionTranslationString(LauncherMetadataHelper.CurrentMetadataConfigGameRegion, Lang._GameClientRegions)}"; }
        private List<object> LastNavigationItem;
        private HomeMenuPanel LastRegionNewsProp;
        public static string PreviousTag = string.Empty;

        internal async Task<bool> LoadRegionFromCurrentConfigV2(PresetConfig preset)
        {
            IsExplicitCancel = false;
            LogWriteLine($"Initializing {RegionToChangeName}...", LogType.Scheme, true);

            // Set IsLoadRegionComplete and IsLoadRegionCancellationRequestEnabled to false
            IsLoadRegionComplete = false;
            IsLoadRegionCancellationRequestEnabled = false;

            // Clear MainPage State, like NavigationView, Load State, etc.
            ClearMainPageState();

            bool IsLoadLocalizedResourceSuccess = await TryLoadResourceInfo(ResourceLoadingType.LocalizedResource, preset),
                 IsLoadResourceRegionSuccess = false;

            // Load Region Resource from Launcher API
            if (IsLoadLocalizedResourceSuccess) IsLoadResourceRegionSuccess = await TryLoadResourceInfo(ResourceLoadingType.DownloadInformation, preset);

            if (IsExplicitCancel)
            {
                // If explicit cancel was triggered, restore the navigation menu item then return false
                foreach (object item in LastNavigationItem)
                {
                    NavigationViewControl.MenuItems.Add(item);
                }
                NavigationViewControl.IsSettingsVisible = true;
                regionNewsProp = LastRegionNewsProp.Copy();
                LastRegionNewsProp = null;
                LastNavigationItem.Clear();
                if (m_arguments.StartGame != null)
                    m_arguments.StartGame.Play = false;
                return false;
            }

            if (!IsLoadLocalizedResourceSuccess || !IsLoadResourceRegionSuccess)
            {
                IsLoadRegionComplete = true;
                InvokeLoadingRegionPopup(false);
                if (m_arguments.StartGame != null)
                    m_arguments.StartGame.Play = false;
                MainFrameChanger.ChangeWindowFrame(typeof(DisconnectedPage));
                return false;
            }

            // Load the background image asynchronously
            ChangeBackgroundImageAsRegionAsync();

            // Finalize Region Load
            FinalizeLoadRegion(preset);
            CurrentGameProperty = GamePropertyVault.GetCurrentGameProperty();

            GamePropertyVault.AttachNotifForCurrentGame(GamePropertyVault.LastGameHashID);
            GamePropertyVault.DetachNotifForCurrentGame(GamePropertyVault.CurrentGameHashID);

            // Set IsLoadRegionComplete to false
            IsLoadRegionComplete = true;
            DisableKbShortcuts();

            return true;
        }

        public void ClearMainPageState()
        {
            // Clear NavigationViewControl Items and Reset Region props
            LastNavigationItem = new List<object>(NavigationViewControl.MenuItems);
            NavigationViewControl.MenuItems.Clear();
            NavigationViewControl.IsSettingsVisible = false;
            PreviousTag = "launcher";
            PreviousTagString.Clear();
            PreviousTagString.Add(PreviousTag);
            LauncherFrame.BackStack.Clear();
            ResetRegionProp();
        }

        private async ValueTask<bool> TryLoadResourceInfo(ResourceLoadingType resourceType, PresetConfig preset, bool ShowLoadingMsg = true)
        {
            uint CurrentTimeout = resourceType == ResourceLoadingType.DownloadBackground ? BackgroundImageLoadTimeout : LoadTimeout;
            uint RetryCount = 0;
            while (RetryCount < MaxRetry)
            {
                using (CancellationTokenSourceWrapper tokenSource = new CancellationTokenSourceWrapper())
                {
                    // Register token source for cancellation registration
                    CurrentRegionLoadTokenSource = tokenSource;

                    // Watch for timeout
                    WatchAndCancelIfTimeout(tokenSource, CurrentTimeout);

                    // Assign task based on type
                    ConfiguredValueTaskAwaitable loadTask = (resourceType switch
                    {
                        ResourceLoadingType.LocalizedResource => FetchLauncherLocalizedResources(tokenSource.Token, preset),
                        ResourceLoadingType.DownloadInformation => FetchLauncherDownloadInformation(tokenSource.Token, preset),
                        ResourceLoadingType.DownloadBackground => DownloadBackgroundImage(tokenSource.Token),
                        _ => throw new InvalidOperationException($"Operation is not supported!")
                    }).ConfigureAwait(false);

                    try
                    {
                        // Run and await task
                        await loadTask;

                        // Return true as successful
                        return true;
                    }
                    catch (OperationCanceledException)
                    {
                        CurrentTimeout = SendTimeoutCancelationMessage(new OperationCanceledException($"Loading was cancelled because timeout has been exceeded!"), CurrentTimeout, ShowLoadingMsg);
                    }
                    catch (Exception ex)
                    {
                        CurrentTimeout = SendTimeoutCancelationMessage(ex, CurrentTimeout, ShowLoadingMsg);
                    }

                    // If explicit cancel was triggered, then return false
                    if (IsExplicitCancel)
                    {
                        return false;
                    }

                    // Increment retry count
                    RetryCount++;
                }
            }

            // Return false as fail
            return false;
        }

        private async ValueTask FetchLauncherLocalizedResources(CancellationToken Token, PresetConfig Preset)
        {
            regionBackgroundProp = Preset.LauncherSpriteURLMultiLang ?? false ?
                await TryGetMultiLangResourceProp(Token, Preset) :
                await TryGetSingleLangResourceProp(Token, Preset);

            GetLauncherAdvInfo(Token, Preset);
            GetLauncherCarouselInfo(Token);
            GetLauncherEventInfo();
            GetLauncherPostInfo();
        }

        private async ValueTask DownloadBackgroundImage(CancellationToken Token)
        {
            // Get and set the current path of the image
            string backgroundFolder = Path.Combine(AppGameImgFolder, "bg");
            string backgroundFileName = Path.GetFileName(regionBackgroundProp.data.adv.background);
            regionBackgroundProp.imgLocalPath = Path.Combine(backgroundFolder, backgroundFileName);
            SetAndSaveConfigValue("CurrentBackground", regionBackgroundProp.imgLocalPath);

            // Check if the background folder exist
            if (!Directory.Exists(backgroundFolder))
                Directory.CreateDirectory(backgroundFolder);

            // Start downloading the background image
            await DownloadAndEnsureCompleteness(regionBackgroundProp.data.adv.background, regionBackgroundProp.imgLocalPath, Token);
        }

        internal static async ValueTask DownloadAndEnsureCompleteness(string url, string outputPath, CancellationToken token)
        {
            // Initialize the FileInfo and check if the file is exist
            FileInfo fI = new FileInfo(outputPath);
            bool isFileExist = IsFileCompletelyDownloaded(fI);

            // If the file and the file assumed to be exist, then return
            if (isFileExist) return;

            // If not, then try download the file
            await TryDownloadToCompleteness(url, fI, token);
        }

        internal static bool IsFileCompletelyDownloaded(FileInfo fileInfo)
        {
            // Get the parent path and file name
            string outputParentPath = Path.GetDirectoryName(fileInfo.FullName);
            string outputFileName = Path.GetFileName(fileInfo.FullName);

            // Try get the prop file which includes the filename + the suggested size provided
            // by the network stream if it has been downloaded before
            string propFilePath = Directory.EnumerateFiles(outputParentPath, $"{outputFileName}#*", SearchOption.TopDirectoryOnly).FirstOrDefault();
            // Check if the file is found (not null), then try parse the information
            if (!string.IsNullOrEmpty(propFilePath))
            {
                // Try split the filename into a segment by # char
                string[] propSegment = Path.GetFileName(propFilePath).Split('#');
                // Assign the check if the condition met and set the file existence status
                return propSegment.Length >= 2
                    && long.TryParse(propSegment[1], null, out long suggestedSize)
                    && fileInfo.Exists && fileInfo.Length == suggestedSize;
            }

            // If the prop doesn't exist, then return false to assume that the file doesn't exist
            return false;
        }

        internal static async void TryDownloadToCompletenessAsync(string url, FileInfo fileInfo, CancellationToken token)
            => await TryDownloadToCompleteness(url, fileInfo, token);

        internal static async ValueTask TryDownloadToCompleteness(string url, FileInfo fileInfo, CancellationToken token)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(4 << 10);
            try
            {
                LogWriteLine($"Start downloading resource from: {url}", LogType.Default, true);

                // Try get the remote stream and download the file
                using Stream netStream = await FallbackCDNUtil.GetHttpStreamFromResponse(url, token);
                using Stream outStream = fileInfo.Open(new FileStreamOptions()
                {
                    Access = FileAccess.Write,
                    Mode = FileMode.Create,
                    Share = FileShare.ReadWrite,
                    Options = FileOptions.Asynchronous
                });

                // Get the file length
                long fileLength = netStream.Length;

                // Create the prop file for download completeness checking
                string outputParentPath = Path.GetDirectoryName(fileInfo.FullName);
                string outputFilename = Path.GetFileName(fileInfo.FullName);
                string propFilePath = Path.Combine(outputParentPath, $"{outputFilename}#{netStream.Length}");
                File.Create(propFilePath).Dispose();

                // Copy (and download) the remote streams to local
                int read = 0;
                while ((read = await netStream.ReadAsync(buffer, token)) > 0)
                    await outStream.WriteAsync(buffer, 0, read, token);

                LogWriteLine($"Resource download from: {url} has been completed and stored locally into:"
                    + $"\"{fileInfo.FullName}\" with size: {ConverterTool.SummarizeSizeSimple(fileLength)} ({fileLength} bytes)", LogType.Default, true);
            }
#if DEBUG
            catch
            {
                throw;
#else
            catch (Exception ex)
            {
                ErrorSender.SendException(ex, ErrorType.Connection);
#endif
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private string GetDeviceId(PresetConfig Preset)
        {
            var deviceId = (string)Registry.GetValue(Preset.InstallRegistryLocation, "UUID", null);
            if (deviceId == null)
            {
                const string regKeyCryptography = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Cryptography";
                var guid = (string)Registry.GetValue(regKeyCryptography, "MachineGuid", null) ?? Guid.NewGuid().ToString();
                deviceId = guid.Replace("-", "") + (long)DateTime.Now.Subtract(DateTime.UnixEpoch).TotalMilliseconds;
                Registry.SetValue(Preset.InstallRegistryLocation, "UUID", deviceId);
            }
            return deviceId;
        }

        private async ValueTask FetchLauncherDownloadInformation(CancellationToken token, PresetConfig Preset)
        {
            _gameAPIProp = await FallbackCDNUtil.DownloadAsJSONType<RegionResourceProp>(Preset.LauncherResourceURL, InternalAppJSONContext.Default, token);
            if (!string.IsNullOrEmpty(Preset.LauncherPluginURL))
            {
                RegionResourceProp _pluginAPIProp = await FallbackCDNUtil.DownloadAsJSONType<RegionResourceProp>(string.Format(Preset.LauncherPluginURL, GetDeviceId(Preset)), InternalAppJSONContext.Default, token);
                if (_pluginAPIProp?.data != null && _pluginAPIProp?.data?.plugins != null)
                {
#if DEBUG
                    LogWriteLine("[FetchLauncherDownloadInformation] Loading plugin handle!");
#endif
                    _gameAPIProp.data.plugins = _pluginAPIProp.data.plugins.Copy();
                }
            }

#if DEBUG
            if (_gameAPIProp.data.game.latest.decompressed_path != null) LogWriteLine($"Decompressed Path: {_gameAPIProp.data.game.latest.decompressed_path}", LogType.Default, true);
            if (_gameAPIProp.data.game.latest.path != null) LogWriteLine($"ZIP Path: {_gameAPIProp.data.game.latest.path}", LogType.Default, true);
            if (_gameAPIProp.data.pre_download_game?.latest?.decompressed_path != null) LogWriteLine($"Decompressed Path Pre-load: {_gameAPIProp.data.pre_download_game?.latest?.decompressed_path}", LogType.Default, true);
            if (_gameAPIProp.data.sdk?.path != null) LogWriteLine($"SDK found! Path: {_gameAPIProp.data.sdk.path}", LogType.Default, true);
            if (_gameAPIProp.data.pre_download_game?.latest?.path != null) LogWriteLine($"ZIP Path Pre-load: {_gameAPIProp.data.pre_download_game?.latest?.path}", LogType.Default, true);
#endif

#if SIMULATEPRELOAD && !SIMULATEAPPLYPRELOAD
            if (_gameAPIProp.data.pre_download_game == null)
            {
                LogWriteLine("[FetchLauncherDownloadInformation] SIMULATEPRELOAD: Simulating Pre-load!");
                RegionResourceVersion simDataLatest = _gameAPIProp.data.game.latest.Copy();
                IList<RegionResourceVersion> simDataDiff = _gameAPIProp.data.game.diffs.Copy();

                simDataLatest.version = new GameVersion(simDataLatest.version).GetIncrementedVersion().ToString();
                _gameAPIProp.data.pre_download_game = new RegionResourceLatest() { latest = simDataLatest };

                if (simDataDiff == null || simDataDiff.Count == 0) return;
                foreach (RegionResourceVersion diff in simDataDiff)
                {
                    diff.version = new GameVersion(diff.version)
                        .GetIncrementedVersion()
                        .ToString();
                }
                _gameAPIProp.data.pre_download_game.diffs = simDataDiff;
            }
#endif
#if !SIMULATEPRELOAD && SIMULATEAPPLYPRELOAD
            if (_gameAPIProp.data.pre_download_game != null)
            {
                _gameAPIProp.data.game = _gameAPIProp.data.pre_download_game;
            }
#endif
        }

        private async ValueTask<RegionResourceProp> TryGetMultiLangResourceProp(CancellationToken Token, PresetConfig Preset)
        {
            RegionResourceProp ret = await GetMultiLangResourceProp(Lang.LanguageID.ToLower(), Token, Preset);

            return ret.data.adv == null
              || ((ret.data.adv.version ?? 5) <= 4
                && Preset.GameType == GameNameType.Honkai) ?
                    await GetMultiLangResourceProp(Preset.LauncherSpriteURLMultiLangFallback ?? "en-us", Token, Preset) :
                    ret;
        }

        private async ValueTask<RegionResourceProp> GetMultiLangResourceProp(string langID, CancellationToken token, PresetConfig Preset)
            => await FallbackCDNUtil.DownloadAsJSONType<RegionResourceProp>(string.Format(Preset.LauncherSpriteURL, langID), InternalAppJSONContext.Default, token);


        private async ValueTask<RegionResourceProp> TryGetSingleLangResourceProp(CancellationToken token, PresetConfig Preset)
            => await FallbackCDNUtil.DownloadAsJSONType<RegionResourceProp>(Preset.LauncherSpriteURL, InternalAppJSONContext.Default, token);

        private void ResetRegionProp()
        {
            LastRegionNewsProp = regionNewsProp.Copy();
            regionNewsProp = new HomeMenuPanel()
            {
                sideMenuPanel = null,
                imageCarouselPanel = null,
                articlePanel = null,
                eventPanel = null
            };
        }

        private void GetLauncherAdvInfo(CancellationToken Token, PresetConfig Preset)
        {
            if (regionBackgroundProp.data.icon.Count == 0) return;

            regionNewsProp.sideMenuPanel = new List<MenuPanelProp>();
            foreach (RegionSocMedProp item in regionBackgroundProp.data.icon)
            {
                // Default: links
                // Fallback: url/title + other_links
                List<LinkProp> links = item.links;
                if (links == null && !string.IsNullOrEmpty(item.url))
                {
                    links = new List<LinkProp>
                    {
                        new() { title = item.title, url = item.url }
                    };
                    links = links.Concat(item.other_links).ToList();
                }

                string url = item.icon_link;
                if (string.IsNullOrEmpty(url) && links.Any() && !string.IsNullOrEmpty(links[0].url))
                {
                    url = links[0].url;
                }

                // Add missing *key* parameter to QQ group link
                if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(Preset.LauncherSpriteURL))
                {
                    if (new Uri(url).Segments.Last() == "qq")
                    {
                        var query = HttpUtility.ParseQueryString(new Uri(Preset.LauncherSpriteURL).Query);
                        string key = query.Get("key");
                        if (!string.IsNullOrEmpty(key))
                        {
                            url += "&key=" + key;
                        }
                    }
                }

                string desc = url;
                if (!Preset.IsHideSocMedDesc ?? false)
                {
                    desc = item.tittle;
                    if (string.IsNullOrEmpty(desc) && links.Any() && !string.IsNullOrEmpty(links[0].title))
                    {
                        desc = links[0].title;
                    }
                }

                regionNewsProp.sideMenuPanel.Add(new MenuPanelProp(Token)
                {
                    URL = url,
                    Icon = item.img,
                    IconHover = item.img_hover,
                    QR = string.IsNullOrEmpty(item.qr_img) ? null : item.qr_img,
                    QR_Description = string.IsNullOrEmpty(item.qr_desc) ? null : item.qr_desc,
                    Description = desc,
                    Links = links
                });
            }
        }

        private void GetLauncherCarouselInfo(CancellationToken Token)
        {
            if (regionBackgroundProp.data.banner.Count == 0) return;

            regionNewsProp.imageCarouselPanel = new List<MenuPanelProp>();
            foreach (RegionSocMedProp item in regionBackgroundProp.data.banner)
            {
                regionNewsProp.imageCarouselPanel.Add(new MenuPanelProp(Token)
                {
                    URL = item.url,
                    Icon = item.img,
                    Description = item.name == "" ? null : item.name
                });
            }
        }

        private void GetLauncherEventInfo()
        {
            if (string.IsNullOrEmpty(regionBackgroundProp.data.adv.icon)) return;

            regionNewsProp.eventPanel = new RegionBackgroundProp
            {
                url = regionBackgroundProp.data.adv.url,
                icon = regionBackgroundProp.data.adv.icon
            };
        }

        private void GetLauncherPostInfo()
        {
            if (regionBackgroundProp.data.post.Count == 0) return;

            regionNewsProp.articlePanel = new PostCarouselTypes();
            foreach (RegionSocMedProp item in regionBackgroundProp.data.post)
            {
                switch (item.type)
                {
                    case PostCarouselType.POST_TYPE_ACTIVITY:
                        regionNewsProp.articlePanel.Events.Add(item);
                        break;
                    case PostCarouselType.POST_TYPE_ANNOUNCE:
                        regionNewsProp.articlePanel.Notices.Add(item);
                        break;
                    case PostCarouselType.POST_TYPE_INFO:
                        regionNewsProp.articlePanel.Info.Add(item);
                        break;
                }
            }
        }

        public static string GetCachedSprites(string URL, CancellationToken token)
        {
            if (string.IsNullOrEmpty(URL)) return URL;

            string cachePath = Path.Combine(AppGameImgCachedFolder, Path.GetFileNameWithoutExtension(URL));
            if (!Directory.Exists(AppGameImgCachedFolder))
                Directory.CreateDirectory(AppGameImgCachedFolder);

            FileInfo fInfo = new FileInfo(cachePath);
            if (!IsFileCompletelyDownloaded(fInfo))
            {
                TryDownloadToCompletenessAsync(URL, fInfo, token);
                return URL;
            }

            return cachePath;
        }

        public static async ValueTask<string> GetCachedSpritesAsync(string URL, CancellationToken token)
        {
            if (string.IsNullOrEmpty(URL)) return URL;

            string cachePath = Path.Combine(AppGameImgCachedFolder, Path.GetFileNameWithoutExtension(URL));
            if (!Directory.Exists(AppGameImgCachedFolder))
                Directory.CreateDirectory(AppGameImgCachedFolder);

            FileInfo fInfo = new FileInfo(cachePath);
            if (!IsFileCompletelyDownloaded(fInfo))
            {
                await TryDownloadToCompleteness(URL, fInfo, token);
            }
            return cachePath;
        }

        private uint SendTimeoutCancelationMessage(Exception ex, uint currentTimeout, bool ShowLoadingMsg)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (ShowLoadingMsg)
                {
                    // Send the message to loading status
                    LoadingMessageHelper.SetMessage(null, string.Format(Lang._MainPage.RegionLoadingSubtitleTimeOut, $"{LauncherMetadataHelper.CurrentMetadataConfigGameName} - {LauncherMetadataHelper.CurrentMetadataConfigGameRegion}", currentTimeout));
                    if (!IsLoadRegionCancellationRequestEnabled)
                    {
                        IsLoadRegionCancellationRequestEnabled = true;
                        LoadingMessageHelper.ShowActionButton(Lang._Misc.Cancel, "", CancelRegionLoadingHandler);
                    }
                }

                // Send the exception without changing into the Error page
                LogWriteLine($"Loading Game: {LauncherMetadataHelper.CurrentMetadataConfigGameName} - {LauncherMetadataHelper.CurrentMetadataConfigGameRegion} has timed-out (> {currentTimeout} seconds). Retrying...", Hi3Helper.LogType.Warning);
                ErrorSender.SendExceptionWithoutPage(ex, ErrorType.Connection);
            });

            // Increment the timeout per step
            currentTimeout += LoadTimeoutStep;

            // Return new timeout second
            return currentTimeout;
        }

        private void CancelRegionLoadingHandler(object sender, RoutedEventArgs args)
        {
            IsExplicitCancel = true;
            LockRegionChangeBtn = false;
            IsLoadRegionCancellationRequestEnabled = false;

            if (CurrentRegionLoadTokenSource != null && !CurrentRegionLoadTokenSource.IsDisposed)
                CurrentRegionLoadTokenSource.Cancel();

            ChangeRegionConfirmProgressBar.Visibility = Visibility.Collapsed;
            ChangeRegionConfirmBtn.IsEnabled = true;
            ChangeRegionConfirmBtnNoWarning.IsEnabled = true;
            ChangeRegionBtn.IsEnabled = true;
            InvokeLoadingRegionPopup(false);
            LoadingMessageHelper.HideActionButton();

            DisableKbShortcuts();
        }

        private async void WatchAndCancelIfTimeout(CancellationTokenSourceWrapper TokenSource, uint Timeout)
        {
            // Wait until it timeout
            await Task.Delay((int)Timeout * 1000);

            // If cancel has been triggered, then return
            if (TokenSource.IsCancellationRequested) return;

            // If InnerTask still not loaded successfully, then cancel it
            if (!IsLoadRegionComplete && !TokenSource.IsDisposed)
            {
                TokenSource.Cancel();
                DisableKbShortcuts();
            }
        }

        private void FinalizeLoadRegion(PresetConfig preset)
        {
            // Log if region has been successfully loaded
            LogWriteLine($"Initializing Region {preset.ZoneFullname} Done!", LogType.Scheme, true);

            // Initializing Game Statics
            LoadGameStaticsByGameType(preset);

            // Init NavigationPanel Items
            InitializeNavigationItems();
        }

        private void LoadGameStaticsByGameType(PresetConfig preset)
        {
            GamePropertyVault.AttachNotifForCurrentGame();
            DisposeAllPageStatics();

            GamePropertyVault.LoadGameProperty(this, _gameAPIProp, preset);

            // Spawn Region Notification
            SpawnRegionNotification(preset.ProfileName);
        }

        private void DisposeAllPageStatics()
        {
            // CurrentGameProperty._GameInstall?.CancelRoutine();
            CurrentGameProperty?._GameRepair?.CancelRoutine();
            CurrentGameProperty?._GameRepair?.Dispose();
            CurrentGameProperty?._GameCache?.CancelRoutine();
            CurrentGameProperty?._GameCache?.Dispose();
#if DEBUG
            LogWriteLine("Page statics have been disposed!", LogType.Debug, true);
#endif
        }

        private async void SpawnRegionNotification(string RegionProfileName)
        {
            // Wait until the notification is ready
            while (!IsLoadNotifComplete)
            {
                await Task.Delay(250);
            }

            if (NotificationData.RegionPush == null) return;

            foreach (NotificationProp Entry in NotificationData.RegionPush)
            {
                NotificationInvokerProp toEntry = new NotificationInvokerProp
                {
                    CloseAction = null,
                    IsAppNotif = false,
                    Notification = Entry,
                    OtherContent = null
                };

                if (Entry.ActionProperty != null)
                {
                    toEntry.OtherContent = Entry.ActionProperty.GetFrameworkElement();
                }

                GameVersion? ValidForVerBelow = Entry.ValidForVerBelow != null ? new GameVersion(Entry.ValidForVerBelow) : null;
                GameVersion? ValidForVerAbove = Entry.ValidForVerAbove != null ? new GameVersion(Entry.ValidForVerAbove) : null;

                if (Entry.RegionProfile == RegionProfileName && IsNotificationTimestampValid(Entry) && (Entry.ValidForVerBelow == null
                        || (LauncherUpdateWatcher.CompareVersion(AppCurrentVersion, ValidForVerBelow)
                        && LauncherUpdateWatcher.CompareVersion(ValidForVerAbove, AppCurrentVersion))
                        || LauncherUpdateWatcher.CompareVersion(AppCurrentVersion, ValidForVerBelow)))
                {
                    NotificationSender.SendNotification(toEntry);
                }
                await Task.Delay(250);
            }
        }

        private bool IsNotificationTimestampValid(NotificationProp Entry)
        {
            long nowDateTime = DateTime.Now.ToLocalTime().ToFileTime();
            long? beginDateTime = Entry.TimeBegin?.ToLocalTime().ToFileTime() ?? 0;
            long? endDateTime = Entry.TimeEnd?.ToLocalTime().ToFileTime() ?? 0;

            bool isBeginValid = Entry.TimeBegin.HasValue ? beginDateTime < nowDateTime : true;
            bool isEndValid = Entry.TimeEnd.HasValue ? endDateTime > nowDateTime : true;

            return isBeginValid && isEndValid;
        }

        private async void ChangeRegionNoWarning(object sender, RoutedEventArgs e)
        {
            (sender as Button).IsEnabled = false;
            CurrentGameCategory = ComboBoxGameCategory.SelectedIndex;
            CurrentGameRegion = ComboBoxGameRegion.SelectedIndex;
            await LoadRegionRootButton();
            InvokeLoadingRegionPopup(false);
            MainFrameChanger.ChangeMainFrame(m_appMode == AppMode.Hi3CacheUpdater ? typeof(CachesPage) : typeof(HomePage));
            LauncherFrame.BackStack.Clear();
        }

        private async void ChangeRegionInstant()
        {
            CurrentGameCategory = ComboBoxGameCategory.SelectedIndex;
            CurrentGameRegion = ComboBoxGameRegion.SelectedIndex;
            await LoadRegionRootButton();
            InvokeLoadingRegionPopup(false);
            MainFrameChanger.ChangeMainFrame(m_appMode == AppMode.Hi3CacheUpdater ? typeof(CachesPage) : typeof(HomePage));
            LauncherFrame.BackStack.Clear();
        }

        private async void ChangeRegion(object sender, RoutedEventArgs e)
        {
            // Disable ChangeRegionBtn and hide flyout
            ToggleChangeRegionBtn(sender, true);
            if (await LoadRegionRootButton())
            {
                // Finalize loading
                ToggleChangeRegionBtn(sender, false);
                CurrentGameCategory = ComboBoxGameCategory.SelectedIndex;
                CurrentGameRegion = ComboBoxGameRegion.SelectedIndex;
            }
        }

        private async Task<bool> LoadRegionRootButton()
        {
            string GameCategory = GetComboBoxGameRegionValue(ComboBoxGameCategory.SelectedValue);
            string GameRegion = GetComboBoxGameRegionValue(ComboBoxGameRegion.SelectedValue);

            // Set and Save CurrentRegion in AppConfig
            SetAndSaveConfigValue("GameCategory", GameCategory);
            LauncherMetadataHelper.SetPreviousGameRegion(GameCategory, GameRegion);

            // Load Game ConfigV2 List before loading the region
            IsLoadRegionComplete = false;
            PresetConfig Preset = LauncherMetadataHelper.GetMetadataConfig(GameCategory, GameRegion);

            // Start region loading
            ShowAsyncLoadingTimedOutPill();
            if (await LoadRegionFromCurrentConfigV2(Preset))
            {
                LogWriteLine($"Region changed to {Preset.ZoneFullname}", LogType.Scheme, true);
#if !DISABLEDISCORD
                if (GetAppConfigValue("EnableDiscordRPC").ToBool())
                    AppDiscordPresence.SetupPresence();
#endif
                return true;
            }

            return false;
        }

        private void ToggleChangeRegionBtn(in object sender, bool IsHide)
        {
            if (IsHide)
            {
                // Hide element
                ChangeRegionConfirmBtn.Flyout.Hide();
                ChangeRegionConfirmProgressBar.Visibility = Visibility.Visible;
            }
            else
            {
                // Show element
                ChangeRegionConfirmBtn.IsEnabled = false;
                ChangeRegionConfirmProgressBar.Visibility = Visibility.Collapsed;
                InvokeLoadingRegionPopup(false);
                MainFrameChanger.ChangeMainFrame(m_appMode == AppMode.Hi3CacheUpdater ? typeof(CachesPage) : typeof(HomePage));
                LauncherFrame.BackStack.Clear();
            }

            (sender as Button).IsEnabled = !IsHide;
        }

        private async void ShowAsyncLoadingTimedOutPill()
        {
            await Task.Delay(1000);
            if (!IsLoadRegionComplete)
            {
                InvokeLoadingRegionPopup(true, Lang._MainPage.RegionLoadingTitle, RegionToChangeName);
                // MainFrameChanger.ChangeMainFrame(typeof(BlankPage));
                while (!IsLoadRegionComplete) { await Task.Delay(1000); }
            }
            InvokeLoadingRegionPopup(false);
        }

        private void InvokeLoadingRegionPopup(bool ShowLoadingMessage = true, string Title = null, string Message = null)
        {
            if (ShowLoadingMessage)
            {
                LoadingMessageHelper.SetMessage(Title, Message);
                LoadingMessageHelper.SetProgressBarState(isProgressIndeterminate: true);
                LoadingMessageHelper.ShowLoadingFrame();
                return;
            }

            LoadingMessageHelper.HideLoadingFrame();
        }
    }
}
