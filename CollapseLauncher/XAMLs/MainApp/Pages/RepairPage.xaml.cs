using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Shared.Region.InstallationManagement;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static Hi3Helper.Preset.ConfigV2Store;
using System.Threading.Tasks;
using System;

namespace CollapseLauncher.Pages
{
    public sealed partial class RepairPage : Page
    {
        IRepair _repairTool;
        public RepairPage()
        {
            string gamePath = NormalizePath(gameIni.Profile["launcher"]["game_install_path"].ToString());
            int threadVal = (byte)GetAppConfigValue("ExtractionThread").ToInt();
            byte thread = (byte)(threadVal <= 0 ? Environment.ProcessorCount : threadVal);

            if (CurrentConfigV2.IsGenshin ?? false)
            {
                _repairTool = new GenshinRepair(this, regionResourceProp.data.game.latest.version, gamePath, CurrentConfigV2);
            }
            else
            {
                _repairTool = new HonkaiRepair(this, regionResourceProp.data.game.latest.version, gamePath, regionResourceProp.data.game.latest.decompressed_path, CurrentConfigV2, thread);
            }

            this.InitializeComponent();
        }

        private async void StartGameCheck(object sender, RoutedEventArgs e)
        {
            CheckFilesBtn.IsEnabled = false;
            CancelBtn.IsEnabled = true;

            try
            {
                AddRepairEvent();

                bool IsGameBroken = await _repairTool.StartCheckRoutine();

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
                RemoveRepairEvent();
            }
        }

        private async void StartGameRepair(object sender, RoutedEventArgs e)
        {
            RepairFilesBtn.IsEnabled = false;
            CancelBtn.IsEnabled = true;

            try
            {
                AddRepairEvent();

                await _repairTool.StartRepairRoutine();

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
                RemoveRepairEvent();
            }
        }

        private void AddRepairEvent()
        {
            _repairTool.ProgressChanged += _repairTool_ProgressChanged;
            _repairTool.StatusChanged += _repairTool_StatusChanged;

            RepairTotalProgressBar.IsIndeterminate = true;
            RepairPerFileProgressBar.IsIndeterminate = true;
        }

        private void RemoveRepairEvent()
        {
            _repairTool.ProgressChanged -= _repairTool_ProgressChanged;
            _repairTool.StatusChanged -= _repairTool_StatusChanged;

            RepairTotalProgressBar.IsIndeterminate = false;
            RepairPerFileProgressBar.IsIndeterminate = false;
        }

        private void _repairTool_StatusChanged(object sender, RepairStatus e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RepairDataTableGrid.Visibility = e.IsAssetEntryPanelShow ? Visibility.Visible : Visibility.Collapsed;
                RepairStatus.Text = e.RepairActivityStatus;

                RepairPerFileStatus.Text = e.RepairActivityPerFile;
                RepairTotalStatus.Text = e.RepairActivityTotal;
                RepairTotalProgressBar.IsIndeterminate = e.IsProgressTotalIndetermined;
                RepairPerFileProgressBar.IsIndeterminate = e.IsProgressPerFileIndetermined;
            });
        }

        private void _repairTool_ProgressChanged(object sender, RepairProgress e)
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
            _repairTool.CancelRoutine();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _repairTool.CancelRoutine();
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
