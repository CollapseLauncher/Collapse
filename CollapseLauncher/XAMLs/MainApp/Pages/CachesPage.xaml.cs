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
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public sealed partial class CachesPage : Page
    {
        private GamePresetProperty CurrentGameProperty { get; set; }

        public CachesPage()
        {
            CurrentGameProperty = GamePropertyVault.GetCurrentGameProperty();

            this.InitializeComponent();
            BackgroundImgChanger.ToggleBackground(true);
#if !DISABLEDISCORD
            AppDiscordPresence.SetActivity(ActivityType.Cache);
#endif
        }

        private void StartCachesCheckSplitButton(SplitButton sender, SplitButtonClickEventArgs args)
        {
            string tag = (string)sender.Tag;
            bool isFast = tag == "Fast";

            RunCheckRoutine(sender, isFast, true);
        }

        private void StartCachesCheck(object sender, RoutedEventArgs e)
        {
            string tag = (string)(sender as ButtonBase).Tag;
            bool isFast = tag == "Fast";

            RunCheckRoutine(sender, isFast, false);
        }

        public async void RunCheckRoutine(object sender, bool isFast, bool isMainButton)
        {
            CheckUpdateBtn.Flyout.Hide();
            CheckUpdateBtn.IsEnabled = false;
            CancelBtn.IsEnabled = true;

            if (!isMainButton)
            {
                SetMainCheckUpdateBtnProperty(sender);
            }

            try
            {
                AddEvent();

                bool IsNeedUpdate = await CurrentGameProperty._GameCache.StartCheckRoutine(isFast);

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
            catch (Exception ex)
            {
                ErrorSender.SendException(ex);
                LogWriteLine($"An error occured while checking cache!\r\n{ex}", LogType.Error, true);
            }
            finally
            {
                RemoveEvent();
            }
        }

        public async void StartCachesUpdate(object sender, RoutedEventArgs e)
        {
            UpdateCachesBtn.IsEnabled = false;
            CancelBtn.IsEnabled = true;

            try
            {
                AddEvent();

                await CurrentGameProperty._GameCache.StartUpdateRoutine();

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
                ErrorSender.SendException(ex);
                LogWriteLine($"An error occured while updating cache!\r\n{ex}", LogType.Error, true);
            }
            finally
            {
                RemoveEvent();
            }
        }

        private void SetMainCheckUpdateBtnProperty(object sender)
        {
            string btnText = ((TextBlock)((StackPanel)((Button)sender).Content).Children[1]).Text;
            string btnTag = (string)((Button)sender).Tag;
            string btnToolTip = (string)ToolTipService.GetToolTip((Button)sender);

            ((TextBlock)((StackPanel)CheckUpdateBtn.Content).Children[1]).Text = btnText;
            CheckUpdateBtn.Tag = btnTag;
            ToolTipService.SetToolTip(CheckUpdateBtn, btnToolTip);
        }

        private void AddEvent()
        {
            CurrentGameProperty._GameCache.ProgressChanged += _cacheTool_ProgressChanged;
            CurrentGameProperty._GameCache.StatusChanged += _cacheTool_StatusChanged;

            CachesTotalProgressBar.IsIndeterminate = true;
        }

        private void RemoveEvent()
        {
            CurrentGameProperty._GameCache.ProgressChanged -= _cacheTool_ProgressChanged;
            CurrentGameProperty._GameCache.StatusChanged -= _cacheTool_StatusChanged;

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
            CurrentGameProperty._GameCache?.CancelRoutine();
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
            else if (CurrentGameProperty.IsGameRunning)
            {
                Overlay.Visibility = Visibility.Visible;
                PageContent.Visibility = Visibility.Collapsed;
                OverlayTitle.Text = Lang._CachesPage.OverlayGameRunningTitle;
                OverlaySubtitle.Text = Lang._CachesPage.OverlayGameRunningSubtitle;
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            CurrentGameProperty._GameCache?.CancelRoutine();
        }
    }
}
