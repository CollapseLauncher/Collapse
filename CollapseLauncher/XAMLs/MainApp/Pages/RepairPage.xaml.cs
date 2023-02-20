using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using static CollapseLauncher.Statics.PageStatics;
using static Hi3Helper.Locale;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public sealed partial class RepairPage : Page
    {
        public RepairPage()
        {
            this.InitializeComponent();
        }

        ~RepairPage() => _GameRepair?.CancelRoutine();

        private async void StartGameCheck(object sender, RoutedEventArgs e)
        {
            CheckFilesBtn.IsEnabled = false;
            CancelBtn.IsEnabled = true;

            try
            {
                AddEvent();

                bool IsGameBroken = await _GameRepair.StartCheckRoutine();

                RepairFilesBtn.IsEnabled = IsGameBroken;
                CheckFilesBtn.IsEnabled = !IsGameBroken;
                CancelBtn.IsEnabled = false;

                RepairFilesBtn.Visibility = IsGameBroken ? Visibility.Visible : Visibility.Collapsed;
                CheckFilesBtn.Visibility = IsGameBroken ? Visibility.Collapsed : Visibility.Visible;
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

        private async void StartGameRepair(object sender, RoutedEventArgs e)
        {
            RepairFilesBtn.IsEnabled = false;
            CancelBtn.IsEnabled = true;

            try
            {
                AddEvent();

                await _GameRepair.StartRepairRoutine();

                RepairFilesBtn.IsEnabled = false;
                CheckFilesBtn.IsEnabled = true;
                CancelBtn.IsEnabled = false;

                RepairFilesBtn.Visibility = Visibility.Collapsed;
                CheckFilesBtn.Visibility = Visibility.Visible;
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
            _GameRepair.ProgressChanged += _repairTool_ProgressChanged;
            _GameRepair.StatusChanged += _repairTool_StatusChanged;

            RepairTotalProgressBar.IsIndeterminate = true;
            RepairPerFileProgressBar.IsIndeterminate = true;
        }

        private void RemoveEvent()
        {
            _GameRepair.ProgressChanged -= _repairTool_ProgressChanged;
            _GameRepair.StatusChanged -= _repairTool_StatusChanged;

            RepairTotalProgressBar.IsIndeterminate = false;
            RepairPerFileProgressBar.IsIndeterminate = false;
        }

        private void _repairTool_StatusChanged(object sender, TotalPerfileStatus e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RepairDataTableGrid.Visibility = e.IsAssetEntryPanelShow ? Visibility.Visible : Visibility.Collapsed;
                RepairStatus.Text = e.ActivityStatus;

                RepairPerFileStatus.Text = e.ActivityPerFile;
                RepairTotalStatus.Text = e.ActivityTotal;
                RepairTotalProgressBar.IsIndeterminate = e.IsProgressTotalIndetermined;
                RepairPerFileProgressBar.IsIndeterminate = e.IsProgressPerFileIndetermined;
            });
        }

        private void _repairTool_ProgressChanged(object sender, TotalPerfileProgress e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RepairPerFileProgressBar.Value = e.ProgressPerFilePercentage;

                RepairTotalProgressBar.Value = e.ProgressTotalPercentage;
            });
        }

        private void ResetStatusAndButtonState()
        {
            RepairStatus.Text = Lang._GameRepairPage.Status1;

            CancelBtn.IsEnabled = false;
            CheckFilesBtn.Visibility = Visibility.Visible;
            CheckFilesBtn.IsEnabled = true;
            RepairFilesBtn.Visibility = Visibility.Collapsed;
        }

        private void CancelOperation(object sender, RoutedEventArgs e)
        {
            _GameRepair.CancelRoutine();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _GameRepair.CancelRoutine();
        }

        private void InitializeLoaded(object sender, RoutedEventArgs e)
        {
            if (GameInstallationState == GameInstallStateEnum.NotInstalled
                || GameInstallationState == GameInstallStateEnum.NeedsUpdate
                || GameInstallationState == GameInstallStateEnum.GameBroken)
            {
                Overlay.Visibility = Visibility.Visible;
                PageContent.Visibility = Visibility.Collapsed;
                OverlayTitle.Text = Lang._GameRepairPage.OverlayNotInstalledTitle;
                OverlaySubtitle.Text = Lang._GameRepairPage.OverlayNotInstalledSubtitle;
            }
            else if (App.IsGameRunning)
            {
                Overlay.Visibility = Visibility.Visible;
                PageContent.Visibility = Visibility.Collapsed;
                OverlayTitle.Text = Lang._GameRepairPage.OverlayGameRunningTitle;
                OverlaySubtitle.Text = Lang._GameRepairPage.OverlayGameRunningSubtitle;
            }
        }
    }
}
