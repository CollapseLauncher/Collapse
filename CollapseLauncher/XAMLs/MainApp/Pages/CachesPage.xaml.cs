using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.Statics.PageStatics;
using static Hi3Helper.Locale;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public sealed partial class CachesPage : Page
    {
        public CachesPage()
        {
            this.InitializeComponent();
        }

        ~CachesPage() => _GameCache?.CancelRoutine();

        public async void StartCachesUpdate(object sender, RoutedEventArgs e)
        {
            UpdateCachesBtn.IsEnabled = false;
            CancelBtn.IsEnabled = true;

            try
            {
                AddEvent();

                await _GameCache.StartUpdateRoutine();

                UpdateCachesBtn.IsEnabled = false;
                CheckUpdateBtn.IsEnabled = true;
                CancelBtn.IsEnabled = false;

                UpdateCachesBtn.Visibility = Visibility.Collapsed;
                CheckUpdateBtn.Visibility = Visibility.Visible;
            }
            catch (TaskCanceledException)
            {
                ResetStatusAndButtonState();
            }
            catch (OperationCanceledException)
            {
                ResetStatusAndButtonState();
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex, ErrorType.GameError);
            }
            finally
            {
                RemoveEvent();
            }
        }

        public async void StartCachesCheck(object sender, RoutedEventArgs e)
        {
            CheckUpdateBtn.IsEnabled = false;
            CancelBtn.IsEnabled = true;

            try
            {
                AddEvent();

                bool IsNeedUpdate = await _GameCache.StartCheckRoutine();

                UpdateCachesBtn.IsEnabled = IsNeedUpdate;
                CheckUpdateBtn.IsEnabled = !IsNeedUpdate;
                CancelBtn.IsEnabled = false;

                UpdateCachesBtn.Visibility = IsNeedUpdate ? Visibility.Visible : Visibility.Collapsed;
                CheckUpdateBtn.Visibility = IsNeedUpdate ? Visibility.Collapsed : Visibility.Visible;
            }
            catch (TaskCanceledException)
            {
                ResetStatusAndButtonState();
            }
            catch (OperationCanceledException)
            {
                ResetStatusAndButtonState();
            }
            finally
            {
                RemoveEvent();
            }
        }

        private void AddEvent()
        {
            _GameCache.ProgressChanged += _cacheTool_ProgressChanged;
            _GameCache.StatusChanged += _cacheTool_StatusChanged;

            CachesTotalProgressBar.IsIndeterminate = true;
        }

        private void RemoveEvent()
        {
            _GameCache.ProgressChanged -= _cacheTool_ProgressChanged;
            _GameCache.StatusChanged -= _cacheTool_StatusChanged;

            CachesTotalProgressBar.IsIndeterminate = false;
        }

        private void _cacheTool_StatusChanged(object sender, TotalPerfileStatus e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                CachesDataTableGrid.Visibility = e.IsAssetEntryPanelShow ? Visibility.Visible : Visibility.Collapsed;
                CachesStatus.Text = e.ActivityStatus;

                CachesTotalStatus.Text = e.ActivityTotal;
                CachesTotalProgressBar.IsIndeterminate = e.IsProgressTotalIndetermined;
            });
        }

        private void _cacheTool_ProgressChanged(object sender, TotalPerfileProgress e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (!double.IsInfinity(e.ProgressTotalPercentage))
                {
                    CachesTotalProgressBar.Value = e.ProgressTotalPercentage;
                }
                else
                {
                    CachesTotalProgressBar.Value = 0;
                }
            });
        }

        private void ResetStatusAndButtonState()
        {
            CachesStatus.Text = Lang._GameRepairPage.Status1;

            CancelBtn.IsEnabled = false;
            CheckUpdateBtn.Visibility = Visibility.Visible;
            CheckUpdateBtn.IsEnabled = true;
            UpdateCachesBtn.Visibility = Visibility.Collapsed;
        }

        public void CancelOperation(object sender, RoutedEventArgs e)
        {
            _GameCache.CancelRoutine();
        }

        private void InitializeLoaded(object sender, RoutedEventArgs e)
        {
            if (m_appMode == AppMode.Hi3CacheUpdater) return;

            if (GameInstallationState == GameInstallStateEnum.NotInstalled
                || GameInstallationState == GameInstallStateEnum.NeedsUpdate
                || GameInstallationState == GameInstallStateEnum.GameBroken)
            {
                Overlay.Visibility = Visibility.Visible;
                PageContent.Visibility = Visibility.Collapsed;
                OverlayTitle.Text = Lang._CachesPage.OverlayNotInstalledTitle;
                OverlaySubtitle.Text = Lang._CachesPage.OverlayNotInstalledSubtitle;
            }
            else if (App.IsGameRunning)
            {
                Overlay.Visibility = Visibility.Visible;
                PageContent.Visibility = Visibility.Collapsed;
                OverlayTitle.Text = Lang._CachesPage.OverlayGameRunningTitle;
                OverlaySubtitle.Text = Lang._CachesPage.OverlayGameRunningSubtitle;
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _GameCache.CancelRoutine();
        }
    }
}
