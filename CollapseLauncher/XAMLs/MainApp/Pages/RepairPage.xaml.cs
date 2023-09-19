﻿using CollapseLauncher.Statics;
using Hi3Helper;
#if !DISABLEDISCORD
using Hi3Helper.DiscordPresence;
#endif
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Threading.Tasks;
using static CollapseLauncher.Statics.GamePropertyVault;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public sealed partial class RepairPage : Page
    {
        private GamePresetProperty CurrentGameProperty { get; set; }
        public RepairPage()
        {
            CurrentGameProperty = GetCurrentGameProperty();
            BackgroundImgChanger.ToggleBackground(true);
            this.InitializeComponent();
#if !DISABLEDISCORD
            AppDiscordPresence.SetActivity(ActivityType.Repair);
#endif
        }

        private void StartGameCheckSplitButton(SplitButton sender, SplitButtonClickEventArgs args)
        {
            string tag = (string)sender.Tag;
            bool isFast = tag == "Fast";

            RunCheckRoutine(sender, isFast, true);
        }

        private void StartGameCheck(object sender, RoutedEventArgs e)
        {
            string tag = (string)(sender as ButtonBase).Tag;
            bool isFast = tag == "Fast";

            RunCheckRoutine(sender, isFast, false);
        }

        private async void RunCheckRoutine(object sender, bool isFast, bool isMainButton)
        {
            CheckFilesBtn.Flyout.Hide();
            CheckFilesBtn.IsEnabled = false;
            CancelBtn.IsEnabled = true;

            if (!isMainButton)
            {
                SetMainCheckFilesBtnProperty(sender);
            }

            try
            {
                AddEvent();

                bool IsGameBroken = await CurrentGameProperty._GameRepair.StartCheckRoutine(isFast);

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
            catch (Exception ex)
            {
                ErrorSender.SendException(ex);
                LogWriteLine($"An error occured while checking asset!\r\n{ex}", LogType.Error, true);
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

                await CurrentGameProperty._GameRepair.StartRepairRoutine();

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
            catch (Exception ex)
            {
                ErrorSender.SendException(ex);
                LogWriteLine($"An error occured while repairing asset!\r\n{ex}", LogType.Error, true);
            }
            finally
            {
                RemoveEvent();
            }
        }

        private void SetMainCheckFilesBtnProperty(object sender)
        {
            string btnText = ((TextBlock)((StackPanel)((Button)sender).Content).Children[1]).Text;
            string btnTag = (string)((Button)sender).Tag;
            string btnToolTip = (string)ToolTipService.GetToolTip((Button)sender);

            ((TextBlock)((StackPanel)CheckFilesBtn.Content).Children[1]).Text = btnText;
            CheckFilesBtn.Tag = btnTag;
            ToolTipService.SetToolTip(CheckFilesBtn, btnToolTip);
        }

        private void AddEvent()
        {
            CurrentGameProperty._GameRepair.ProgressChanged += _repairTool_ProgressChanged;
            CurrentGameProperty._GameRepair.StatusChanged += _repairTool_StatusChanged;

            RepairTotalProgressBar.IsIndeterminate = true;
            RepairPerFileProgressBar.IsIndeterminate = true;
        }

        private void RemoveEvent()
        {
            CurrentGameProperty._GameRepair.ProgressChanged -= _repairTool_ProgressChanged;
            CurrentGameProperty._GameRepair.StatusChanged -= _repairTool_StatusChanged;

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
                double percentage = double.IsInfinity(e.ProgressPerFilePercentage) ? 0 : e.ProgressPerFilePercentage;
                RepairPerFileProgressBar.Value = percentage;

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
            CurrentGameProperty._GameRepair?.CancelRoutine();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            CurrentGameProperty._GameRepair?.CancelRoutine();
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
            else if (CurrentGameProperty.IsGameRunning)
            {
                Overlay.Visibility = Visibility.Visible;
                PageContent.Visibility = Visibility.Collapsed;
                OverlayTitle.Text = Lang._GameRepairPage.OverlayGameRunningTitle;
                OverlaySubtitle.Text = Lang._GameRepairPage.OverlayGameRunningSubtitle;
            }
        }
    }
}
