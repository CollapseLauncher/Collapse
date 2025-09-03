using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Animation;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Statics;
using CommunityToolkit.WinUI.Animations;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Win32.FileDialogCOM;
using Hi3Helper.Win32.ManagedTools;
using Microsoft.UI.Composition;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static CollapseLauncher.Helper.Background.BackgroundMediaUtility;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using UIElementExtensions = CollapseLauncher.Extension.UIElementExtensions;
// ReSharper disable InconsistentNaming
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable AsyncVoidMethod
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable CheckNamespace

namespace CollapseLauncher.Pages;

public sealed partial class HomePage
{
    #region Preload
    private async void SpawnPreloadBox()
    {
        if (CurrentGameProperty.GameInstall?.IsUseSophon ?? false)
        {
            DownloadModeLabelPreload.Visibility = Visibility.Visible;
            DownloadModeLabelPreloadText.Text   = Lang._Misc.DownloadModeLabelSophon;
        }

        if (CurrentGameProperty.GameInstall?.IsRunning ?? false)
        {
            // TODO
            PauseDownloadPreBtn.Visibility  = Visibility.Visible;
            ResumeDownloadPreBtn.Visibility = Visibility.Collapsed;
            PreloadDialogBox.IsClosable     = false;

            IsSkippingUpdateCheck     = true;
            DownloadPreBtn.Visibility = Visibility.Collapsed;
            if (CurrentGameProperty.GameInstall.IsUseSophon)
            {
                ProgressPreSophonStatusGrid.Visibility = Visibility.Visible;
                ProgressPreStatusGrid.Visibility       = Visibility.Collapsed;
            }
            else
            {
                ProgressPreStatusGrid.Visibility = Visibility.Visible;
            }
            ProgressPreButtonGrid.Visibility = Visibility.Visible;
            PreloadDialogBox.Title           = Lang._HomePage.PreloadDownloadNotifbarTitle;
            PreloadDialogBox.Message         = Lang._HomePage.PreloadDownloadNotifbarSubtitle;

            CurrentGameProperty.GameInstall.ProgressChanged += PreloadDownloadProgress;
            CurrentGameProperty.GameInstall.StatusChanged   += PreloadDownloadStatus;
            SpawnPreloadDialogBox();
            return;
        }

        string ver = CurrentGameProperty.GameVersion?.GetGameVersionApiPreload()?.VersionString;

        try
        {
            if (CurrentGameProperty.GameVersion?.IsGameHasDeltaPatch() ?? false)
            {
                PreloadDialogBox.Title    = string.Format(Lang._HomePage.PreloadNotifDeltaDetectTitle, ver);
                PreloadDialogBox.Message  = Lang._HomePage.PreloadNotifDeltaDetectSubtitle;
                DownloadPreBtn.Visibility = Visibility.Collapsed;
                SpawnPreloadDialogBox();
                return;
            }

            if (!await CurrentGameProperty.GameInstall!.IsPreloadCompleted(PageToken.Token))
            {
                PreloadDialogBox.Title = string.Format(Lang._HomePage.PreloadNotifTitle, ver);
            }
            else
            {
                PreloadDialogBox.Title      = Lang._HomePage.PreloadNotifCompleteTitle;
                PreloadDialogBox.Message    = string.Format(Lang._HomePage.PreloadNotifCompleteSubtitle, ver);
                PreloadDialogBox.IsClosable = true;
                DownloadPreBtn.Content = UIElementExtensions.CreateIconTextGrid(
                                                                                text: Lang._HomePage.PreloadNotifIntegrityCheckBtn,
                                                                                iconGlyph: "",
                                                                                iconFontFamily: "FontAwesomeSolid",
                                                                                textWeight: FontWeights.Medium
                                                                               );
            }
            SpawnPreloadDialogBox();
        }
        catch (Exception ex)
        {
            await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
            LogWriteLine($"An error occured while trying to determine delta-patch availability\r\n{ex}", LogType.Error, true);
        }
    }
    
    private async void SpawnPreloadDialogBox()
    {
        PreloadDialogBox.IsOpen      = true;
        PreloadDialogBox.Translation = new Vector3(0, 0, 16);
        Compositor compositor = CompositionTarget.GetCompositorForCurrentThread();

        PreloadDialogBox.Opacity = 0.0f;
        const float toScale = 0.98f;
        Vector3 toTranslate = new Vector3(-((float)(PreloadDialogBox?.ActualWidth ?? 0) * (toScale - 1f) / 2),
                                          -((float)(PreloadDialogBox?.ActualHeight ?? 0) * (toScale - 1f)) - 16, 0);

        await PreloadDialogBox.StartAnimation(TimeSpan.FromSeconds(0.5),
                                              compositor.CreateScalarKeyFrameAnimation("Opacity", 1.0f, 0.0f),
                                              compositor.CreateVector3KeyFrameAnimation("Scale",
                                                  new Vector3(1.0f, 1.0f, PreloadDialogBox!.Translation.Z),
                                                  new Vector3(toScale, toScale,
                                                              PreloadDialogBox.Translation.Z)),
                                              compositor.CreateVector3KeyFrameAnimation("Translation", PreloadDialogBox.Translation, toTranslate)
                                             );
    }

    private async void PredownloadDialog(object sender, RoutedEventArgs e)
    {
        ((Button)sender).IsEnabled = false;

        PauseDownloadPreBtn.Visibility  = Visibility.Visible;
        ResumeDownloadPreBtn.Visibility = Visibility.Collapsed;
        PreloadDialogBox.IsClosable     = false;

        try
        {
            MainWindow.IsCriticalOpInProgress = true;
            // Prevent device from sleep
            Sleep.PreventSleep(ILoggerHelper.GetILogger());
            // Set the notification trigger to "Running" state
            CurrentGameProperty.GameInstall?.UpdateCompletenessStatus(CompletenessStatus.Running);

            IsSkippingUpdateCheck     = true;
            DownloadPreBtn.Visibility = Visibility.Collapsed;
            if (CurrentGameProperty.GameInstall?.IsUseSophon ?? false)
            {
                ProgressPreSophonStatusGrid.Visibility = Visibility.Visible;
                ProgressPreStatusGrid.Visibility       = Visibility.Collapsed;
            }
            else
            {
                ProgressPreStatusGrid.Visibility = Visibility.Visible;
            }
            ProgressPreButtonGrid.Visibility = Visibility.Visible;
            PreloadDialogBox.Title           = Lang._HomePage.PreloadDownloadNotifbarTitle;
            PreloadDialogBox.Message         = Lang._HomePage.PreloadDownloadNotifbarSubtitle;

            if (CurrentGameProperty.GameInstall != null)
            {
                CurrentGameProperty.GameInstall.ProgressChanged += PreloadDownloadProgress;
                CurrentGameProperty.GameInstall.StatusChanged   += PreloadDownloadStatus;

                int verifResult = 0;
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                while (verifResult != 1)
                {
                    await CurrentGameProperty.GameInstall.StartPackageDownload(true);

                    PauseDownloadPreBtn.IsEnabled = false;
                    PreloadDialogBox.Title        = Lang._HomePage.PreloadDownloadNotifbarVerifyTitle;

                    verifResult = await CurrentGameProperty.GameInstall.StartPackageVerification();

                    // Restore sleep before the dialog
                    // so system won't be stuck when download is finished because of the download verified dialog
                    Sleep.RestoreSleep();

                    switch (verifResult)
                    {
                        case -1:
                            ReturnToHomePage();
                            return;
                        case 1:
                            await Dialog_PreDownloadPackageVerified();
                            ReturnToHomePage();
                            return;
                    }
                }

                // Set the notification trigger to "Completed" state
                CurrentGameProperty.GameInstall.UpdateCompletenessStatus(CompletenessStatus.Completed);
            }

            // If the current window is not in focus, then spawn the notification toast
            if (WindowUtility.IsCurrentWindowInFocus())
            {
                return;
            }

            string gameNameLocale = LauncherMetadataHelper.GetTranslatedCurrentGameTitleRegionString();

            WindowUtility.Tray_ShowNotification(
                                                string.Format(Lang._NotificationToast.GamePreloadCompleted_Title, gameNameLocale),
                                                Lang._NotificationToast.GenericClickNotifToGoBack_Subtitle
                                               );
        }
        catch (OperationCanceledException)
        {
            LogWriteLine("Pre-Download paused!", LogType.Warning);
            // Set the notification trigger
            CurrentGameProperty.GameInstall?.UpdateCompletenessStatus(CompletenessStatus.Cancelled);
        }
        catch (Exception ex)
        {
            LogWriteLine($"An error occurred while starting preload process: {ex}", LogType.Error, true);
            ErrorSender.SendException(ex);
            // Set the notification trigger
            CurrentGameProperty.GameInstall?.UpdateCompletenessStatus(CompletenessStatus.Cancelled);
        }
        finally
        {
            IsSkippingUpdateCheck                           =  false;
            if (CurrentGameProperty.GameInstall != null)
            {
                CurrentGameProperty.GameInstall.ProgressChanged -= PreloadDownloadProgress;
                CurrentGameProperty.GameInstall.StatusChanged   -= PreloadDownloadStatus;
                CurrentGameProperty.GameInstall.Flush();
            }

            // Turn the sleep back on
            Sleep.RestoreSleep();
            MainWindow.IsCriticalOpInProgress = false;
        }
    }

    private void PreloadDownloadStatus(object sender, TotalPerFileStatus e)
    {
        DispatcherQueue?.TryEnqueue(() => ProgressPrePerFileStatusFooter.Text = e.ActivityStatus);
    }

    private void PreloadDownloadProgress(object sender, TotalPerFileProgress e)
    {
        DispatcherQueue?.TryEnqueue(() =>
                                    {
                                        string installDownloadSpeedString = SummarizeSizeSimple(e.ProgressAllSpeed);
                                        string installDownloadSizeString = SummarizeSizeSimple(e.ProgressAllSizeCurrent);
                                        string installDownloadPerSizeString = SummarizeSizeSimple(e.ProgressPerFileSizeCurrent);
                                        string downloadSizeString = SummarizeSizeSimple(e.ProgressAllSizeTotal);
                                        string downloadPerSizeString = SummarizeSizeSimple(e.ProgressPerFileSizeTotal);

                                        ProgressPreStatusSubtitle.Text = string.Format(Lang._Misc.PerFromTo, installDownloadSizeString, downloadSizeString);
                                        ProgressPrePerFileStatusSubtitle.Text = string.Format(Lang._Misc.PerFromTo, installDownloadPerSizeString, downloadPerSizeString);
                                        ProgressPreStatusFooter.Text = string.Format(Lang._Misc.Speed, installDownloadSpeedString);
                                        ProgressPreTimeLeft.Text = string.Format(Lang._Misc.TimeRemainHMSFormat, e.ProgressAllTimeLeft);
                                        progressPreBar.Value = Math.Round(e.ProgressAllPercentage, 2);
                                        progressPrePerFileBar.Value = Math.Round(e.ProgressPerFilePercentage, 2);
                                        progressPreBar.IsIndeterminate = false;
                                        progressPrePerFileBar.IsIndeterminate = false;
                                    });
    }
    #endregion

    #region Game Install
    private async void InstallGameDialog(object sender, RoutedEventArgs e)
    {
        bool isUseSophon = CurrentGameProperty.GameInstall?.IsUseSophon ?? false;
        try
        {
            MainWindow.IsCriticalOpInProgress = true;
            // Prevent device from sleep
            Sleep.PreventSleep(ILoggerHelper.GetILogger());
            // Set the notification trigger to "Running" state
            CurrentGameProperty.GameInstall?.UpdateCompletenessStatus(CompletenessStatus.Running);

            IsSkippingUpdateCheck = true;

            HideImageCarousel(true);

            progressRing.Value           = 0;
            progressRing.IsIndeterminate = true;
            InstallGameBtn.Visibility    = Visibility.Collapsed;
            CancelDownloadBtn.Visibility = Visibility.Visible;
            ProgressTimeLeft.Visibility  = Visibility.Visible;

            if (isUseSophon)
            {
                SophonProgressStatusGrid.Visibility               =  Visibility.Visible;
                SophonProgressStatusSizeDownloadedGrid.Visibility =  Visibility.Collapsed;
                CurrentGameProperty.GameInstall.ProgressChanged   += GameInstallSophon_ProgressChanged;
                CurrentGameProperty.GameInstall.StatusChanged     += GameInstallSophon_StatusChanged;
            }
            else
            {
                ProgressStatusGrid.Visibility                   =  Visibility.Visible;
                if (CurrentGameProperty.GameInstall != null)
                {
                    CurrentGameProperty.GameInstall.ProgressChanged += GameInstall_ProgressChanged;
                    CurrentGameProperty.GameInstall.StatusChanged   += GameInstall_StatusChanged;
                }
            }

            int dialogResult = await CurrentGameProperty.GameInstall!.GetInstallationPath();
            switch (dialogResult)
            {
                case < 0:
                    return;
                case 0:
                    CurrentGameProperty.GameInstall.ApplyGameConfig();
                    return;
            }

            if (CurrentGameProperty.GameInstall.IsUseSophon)
            {
                DownloadModeLabel.Visibility = Visibility.Visible;
                DownloadModeLabelText.Text   = Lang._Misc.DownloadModeLabelSophon;
            }

            int  verifResult;
            bool skipDialog = false;
            while ((verifResult = await CurrentGameProperty.GameInstall.StartPackageVerification()) == 0)
            {
                await CurrentGameProperty.GameInstall.StartPackageDownload(skipDialog);
                skipDialog = true;
            }

            if (verifResult == -1)
            {
                CurrentGameProperty.GameInstall.ApplyGameConfig(true);
                return;
            }

            await CurrentGameProperty.GameInstall.StartPackageInstallation();
            CurrentGameProperty.GameInstall.ApplyGameConfig(true);
            PostInstallProcedure();

            // Set the notification trigger to "Completed" state
            CurrentGameProperty.GameInstall.UpdateCompletenessStatus(CompletenessStatus.Completed);

            // If the current window is not in focus, then spawn the notification toast
            if (WindowUtility.IsCurrentWindowInFocus())
            {
                return;
            }

            string gameNameLocale = LauncherMetadataHelper.GetTranslatedCurrentGameTitleRegionString();
            WindowUtility.Tray_ShowNotification(
                                                string.Format(Lang._NotificationToast.GameInstallCompleted_Title,
                                                              gameNameLocale),
                                                string
                                                   .Format(Lang._NotificationToast.GameInstallCompleted_Subtitle,
                                                           gameNameLocale)
                                               );
        }
        catch (TaskCanceledException)
        {
            LogWriteLine($"Installation cancelled for game {CurrentGameProperty.GameVersion?.GamePreset.ZoneFullname}");
            // Set the notification trigger
            CurrentGameProperty.GameInstall?.UpdateCompletenessStatus(CompletenessStatus.Cancelled);
        }
        catch (OperationCanceledException)
        {
            LogWriteLine($"Installation cancelled for game {CurrentGameProperty.GameVersion?.GamePreset.ZoneFullname}");
            // Set the notification trigger
            CurrentGameProperty.GameInstall?.UpdateCompletenessStatus(CompletenessStatus.Cancelled);
        }
        catch (NotSupportedException ex)
        {
            await SentryHelper.ExceptionHandlerAsync(ex);
            // Set the notification trigger
            CurrentGameProperty.GameInstall?.UpdateCompletenessStatus(CompletenessStatus.Cancelled);

            IsPageUnload = true;
            LogWriteLine($"Error while installing game {CurrentGameProperty.GameVersion?.GamePreset.ZoneFullname}\r\n{ex}",
                         LogType.Error, true);
        
            await SpawnDialog(Lang._HomePage.InstallFolderRootTitle,
                              Lang._HomePage.InstallFolderRootSubtitle,
                              Content,
                              Lang._Misc.Close,
                              null, null, ContentDialogButton.Close, ContentDialogTheme.Error);
        }
        catch (NullReferenceException ex)
        {
            // Set the notification trigger
            CurrentGameProperty.GameInstall?.UpdateCompletenessStatus(CompletenessStatus.Cancelled);

            IsPageUnload = true;
            LogWriteLine($"Error while installing game {CurrentGameProperty.GameVersion?.GamePreset.ZoneFullname}\r\n{ex}",
                         LogType.Error, true);
            ErrorSender.SendException(new
                                          NullReferenceException("Collapse was not able to complete post-installation tasks, but your game has been successfully updated.\r\t" +
                                                                 $"Please report this issue to our GitHub here: https://github.com/CollapseLauncher/Collapse/issues/new or come back to the launcher and make sure to use Repair Game in Game Settings button later.\r\nThrow: {ex}",
                                                                 ex));
        }
        catch (TimeoutException ex)
        {
            // Set the notification trigger
            CurrentGameProperty.GameInstall?.UpdateCompletenessStatus(CompletenessStatus.Cancelled);
            IsPageUnload = true;
            string exMessage = $"Timeout occurred when trying to install {CurrentGameProperty.GameVersion?.GamePreset.ZoneFullname}.\r\n\t" +
                               $"Check stability of your internet! If your internet speed is slow, please lower the download thread count.\r\n\t" +
                               $"**WARNING** Changing download thread count WILL reset your download from 0, and you have to delete the existing download chunks manually!" +
                               $"\r\n{ex}";
        
            string exTitleLocalized = string.Format(Lang._HomePage.Exception_DownloadTimeout1, CurrentGameProperty.GameVersion?.GamePreset.ZoneFullname);
            string exMessageLocalized = string.Format($"{exTitleLocalized}\r\n\t" +
                                                      $"{Lang._HomePage.Exception_DownloadTimeout2}\r\n\t" +
                                                      $"{Lang._HomePage.Exception_DownloadTimeout3}");

            LogWriteLine($"{exMessage}", LogType.Error, true);
            Exception newEx = new TimeoutException(exMessageLocalized, ex);
            ErrorSender.SendException(newEx);
        }
        catch (Exception ex)
        {
            // Set the notification trigger
            CurrentGameProperty.GameInstall?.UpdateCompletenessStatus(CompletenessStatus.Cancelled);

            IsPageUnload = true;
            LogWriteLine($"Error while installing game {CurrentGameProperty.GameVersion?.GamePreset.ZoneFullname}.\r\n{ex}", LogType.Error, true);
            ErrorSender.SendException(ex);
        }
        finally
        {
            IsSkippingUpdateCheck = false;
            if (CurrentGameProperty.GameInstall != null)
            {
                CurrentGameProperty.GameInstall.PostInstallBehaviour = PostInstallBehaviour.Nothing;

                CurrentGameProperty.GameInstall.ProgressChanged -=
                    isUseSophon ? GameInstallSophon_ProgressChanged : GameInstall_ProgressChanged;
                CurrentGameProperty.GameInstall.StatusChanged -=
                    isUseSophon ? GameInstallSophon_StatusChanged : GameInstall_StatusChanged;

                await Task.Delay(200);
                CurrentGameProperty.GameInstall.Flush();
            }

            ReturnToHomePage();

            // Turn the sleep back on
            Sleep.RestoreSleep();
            MainWindow.IsCriticalOpInProgress = false;
        }
    }

    private void GameInstall_StatusChanged(object sender, TotalPerFileStatus e)
    {
        if (DispatcherQueue != null && DispatcherQueue.HasThreadAccessSafe())
            GameInstall_StatusChanged_Inner(e);
        else
            DispatcherQueue?.TryEnqueue(() => GameInstall_StatusChanged_Inner(e));
    }

    private void GameInstall_StatusChanged_Inner(TotalPerFileStatus e)
    {
        ProgressStatusTitle.Text   = e.ActivityStatus;
        progressPerFile.Visibility = e.IsIncludePerFileIndicator ? Visibility.Visible : Visibility.Collapsed;

        progressRing.IsIndeterminate        = e.IsProgressAllIndetermined;
        progressRingPerFile.IsIndeterminate = e.IsProgressPerFileIndetermined;

        ProgressStatusIconDisk.Visibility = e.ActivityStatusInternet ? Visibility.Collapsed : Visibility.Visible;
        ProgressStatusIconInternet.Visibility = e.ActivityStatusInternet ? Visibility.Visible : Visibility.Collapsed;
    }

    private void GameInstall_ProgressChanged(object sender, TotalPerFileProgress e)
    {
        if (DispatcherQueue != null && DispatcherQueue.HasThreadAccessSafe())
            GameInstall_ProgressChanged_Inner(e);
        else
            DispatcherQueue?.TryEnqueue(() => GameInstall_ProgressChanged_Inner(e));
    }

    private void GameInstall_ProgressChanged_Inner(TotalPerFileProgress e)
    {
        progressRing.Value = e.ProgressAllPercentage;
        progressRingPerFile.Value = e.ProgressPerFilePercentage;
        ProgressStatusSubtitle.Text = string.Format(Lang._Misc.PerFromTo, SummarizeSizeSimple(e.ProgressAllSizeCurrent), SummarizeSizeSimple(e.ProgressAllSizeTotal));
        ProgressStatusFooter.Text = string.Format(Lang._Misc.Speed, SummarizeSizeSimple(e.ProgressAllSpeed));
        ProgressTimeLeft.Text = string.Format(Lang._Misc.TimeRemainHMSFormat, e.ProgressAllTimeLeft);
    }

    private void GameInstallSophon_StatusChanged(object sender, TotalPerFileStatus e)
    {
        if (DispatcherQueue != null && DispatcherQueue.HasThreadAccessSafe())
            GameInstallSophon_StatusChanged_Inner(e);
        else
            DispatcherQueue?.TryEnqueue(() => GameInstallSophon_StatusChanged_Inner(e));
    }

    private void GameInstallSophon_ProgressChanged(object sender, TotalPerFileProgress e)
    {
        if (DispatcherQueue != null && DispatcherQueue.HasThreadAccessSafe())
            GameInstallSophon_ProgressChanged_Inner(e);
        else
            DispatcherQueue?.TryEnqueue(() => GameInstallSophon_ProgressChanged_Inner(e));
    }

    private void GameInstallSophon_StatusChanged_Inner(TotalPerFileStatus e)
    {
        SophonProgressStatusTitleText.Text = e.ActivityStatus;
        SophonProgressPerFile.Visibility   = e.IsIncludePerFileIndicator ? Visibility.Visible : Visibility.Collapsed;

        SophonProgressRing.IsIndeterminate        = e.IsProgressAllIndetermined;
        SophonProgressRingPerFile.IsIndeterminate = e.IsProgressPerFileIndetermined;
    }

    private void GameInstallSophon_ProgressChanged_Inner(TotalPerFileProgress e)
    {
        SophonProgressRing.Value        = e.ProgressAllPercentage;
        SophonProgressRingPerFile.Value = e.ProgressPerFilePercentage;

        SophonProgressStatusSizeTotalText.Text = string.Format(Lang._Misc.PerFromTo, SummarizeSizeSimple(e.ProgressAllSizeCurrent), SummarizeSizeSimple(e.ProgressAllSizeTotal));
        SophonProgressStatusSizeDownloadedText.Text = string.Format(Lang._Misc.PerFromTo, SummarizeSizeSimple(e.ProgressPerFileSizeCurrent), SummarizeSizeSimple(e.ProgressPerFileSizeTotal));
    
        SophonProgressStatusSpeedTotalText.Text = string.Format(Lang._Misc.SpeedPerSec, SummarizeSizeSimple(Math.Max(e.ProgressAllSpeed, 0)));
        SophonProgressStatusSpeedDownloadedText.Text = string.Format(Lang._Misc.SpeedPerSec, SummarizeSizeSimple(Math.Max(e.ProgressPerFileSpeed, 0)));

        SophonProgressTimeLeft.Text = string.Format(Lang._Misc.TimeRemainHMSFormat, e.ProgressAllTimeLeft);
    }

    private void CancelInstallationProcedure(object sender, RoutedEventArgs e)
    {
        switch (GameInstallationState)
        {
            case GameInstallStateEnum.NeedsUpdate:
            case GameInstallStateEnum.InstalledHavePlugin:
                CancelUpdateDownload();
                break;
            case GameInstallStateEnum.InstalledHavePreload:
                CancelPreDownload();
                break;
            case GameInstallStateEnum.NotInstalled:
            case GameInstallStateEnum.GameBroken:
            case GameInstallStateEnum.Installed:
                CancelInstallationDownload();
                break;
        }
    }

    private void PostInstallProcedure()
    {
        if (CurrentGameProperty.GameVersion == null || 
            !CurrentGameProperty.GameVersion.IsGameInstalled()) return;

        var behaviour = CurrentGameProperty.GameInstall?.PostInstallBehaviour
            ?? PostInstallBehaviour.Nothing;
        switch (behaviour)
        {
            case PostInstallBehaviour.Nothing:
                break;
            case PostInstallBehaviour.StartGame:
                StartGame(null, null);
                break;
            case PostInstallBehaviour.Hibernate:
                Process.Start(new ProcessStartInfo("C:\\Windows\\System32\\shutdown.exe", "/h")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                });
                break;
            case PostInstallBehaviour.Restart:
            case PostInstallBehaviour.Shutdown:
                var shutdownTimeout = GetAppConfigValue("PostInstallShutdownTimeout").ToInt(60);
                var shutdownType = behaviour switch
                {
                    PostInstallBehaviour.Restart => "/r",
                    PostInstallBehaviour.Shutdown => "/s",
                    _ => "/a"
                };

                Process.Start(new ProcessStartInfo("C:\\Windows\\System32\\shutdown.exe", $"{shutdownType} /t {shutdownTimeout}")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                });
                break;
        }

        CurrentGameProperty.GameInstall?.PostInstallBehaviour = PostInstallBehaviour.Nothing;
    }
    #endregion

    #region Game Management Buttons
    private void RepairGameButton_Click(object sender, RoutedEventArgs e)
    {
        m_mainPage!.InvokeMainPageNavigateByTag("repair");
    }

    private async void UninstallGameButton_Click(object sender, RoutedEventArgs e)
    {
        if (await CurrentGameProperty.GameInstall!.UninstallGame())
        {
            MainFrameChanger.ChangeMainFrame(typeof(HomePage));
        }
    }

    private void ConvertVersionButton_Click(object sender, RoutedEventArgs e)
    {
        MainFrameChanger.ChangeWindowFrame(typeof(InstallationConvert));
    }

    private async void StopGameButton_Click(object sender, RoutedEventArgs e)
    {
        if (await Dialog_StopGame() != ContentDialogResult.Primary) return;
        StopGame(CurrentGameProperty.GameVersion?.GamePreset);
    }

    private async void ChangeGameBGButton_Click(object sender, RoutedEventArgs e)
    {
        var file = await FileDialogNative.GetFilePicker(ImageLoaderHelper.SupportedImageFormats);
        if (string.IsNullOrEmpty(file)) return;

        var currentMediaType = GetMediaType(file);
            
        if (currentMediaType == MediaType.StillImage)
        {
            FileStream croppedImage = await ImageLoaderHelper.LoadImage(file, true, true);
            
            if (croppedImage == null) return;
            SetAlternativeFileStream(croppedImage);
        }

        if (CurrentGameProperty?.GameSettings?.SettingsCollapseMisc != null)
        {
            CurrentGameProperty.GameSettings.SettingsCollapseMisc.CustomRegionBGPath = file;
            CurrentGameProperty.GameSettings.SaveBaseSettings();
        }
        _ = m_mainPage?.ChangeBackgroundImageAsRegionAsync();

        BGPathDisplay.Text = Path.GetFileName(file);
    }

    private async void MoveGameLocationButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!await CurrentGameProperty.GameInstall!.MoveGameLocation())
            {
                return;
            }

            CurrentGameProperty.GameInstall.ApplyGameConfig();
            ReturnToHomePage();
        }
        catch (NotSupportedException ex)
        {
            LogWriteLine($"Error has occurred while running Move Game Location tool!\r\n{ex}", LogType.Error, true);
            ex = new NotSupportedException(Lang._HomePage.GameSettings_Panel2MoveGameLocationGame_SamePath, ex);
            ErrorSender.SendException(ex, ErrorType.Warning);
        }
        catch (Exception ex)
        {
            LogWriteLine($"Error has occurred while running Move Game Location tool!\r\n{ex}", LogType.Error, true);
            ErrorSender.SendException(ex);
        }
    }
    #endregion
    
    #region Game State
    private async ValueTask GetCurrentGameState()
    {
        Visibility repairGameButtonVisible = CurrentGameProperty.GameVersion?.GamePreset.IsRepairEnabled ?? false ?
            Visibility.Visible : Visibility.Collapsed;

        if (!(CurrentGameProperty.GameVersion?.GamePreset.IsConvertible ?? false)
            || CurrentGameProperty.GameVersion.GameType != GameNameType.Honkai)
            ConvertVersionButton.Visibility = Visibility.Collapsed;

        // Clear the _CommunityToolsProperty statics
        PageStatics.CommunityToolsProperty?.Clear();

        // Check if the _CommunityToolsProperty has the official tool list for current game type
        if (PageStatics.CommunityToolsProperty?
               .OfficialToolsDictionary?
               .TryGetValue(CurrentGameProperty.GameVersion!.GameType,
                            out List<CommunityToolsEntry> officialEntryList) ?? false)
        {
            // If yes, then iterate it and add it to the list, to then getting read by the
            // DataTemplate from HomePage
            for (int index = officialEntryList.Count - 1; index >= 0; index--)
            {
                CommunityToolsEntry iconProperty = officialEntryList[index];
                if (iconProperty.Profiles?.Contains(CurrentGameProperty.GamePreset.ProfileName) ?? false)
                {
                    PageStatics.CommunityToolsProperty.OfficialToolsList?.Add(iconProperty);
                }
            }
        }

        // Check if the _CommunityToolsProperty has the community tool list for current game type
        if (PageStatics.CommunityToolsProperty?
               .CommunityToolsDictionary?
               .TryGetValue(CurrentGameProperty.GameVersion!.GameType,
                            out List<CommunityToolsEntry> communityEntryList) ?? false)
        {
            // If yes, then iterate it and add it to the list, to then getting read by the
            // DataTemplate from HomePage
            for (int index = communityEntryList.Count - 1; index >= 0; index--)
            {
                CommunityToolsEntry iconProperty = communityEntryList[index];
                if (iconProperty.Profiles?.Contains(CurrentGameProperty.GamePreset.ProfileName) ?? false)
                {
                    PageStatics.CommunityToolsProperty.CommunityToolsList?.Add(iconProperty);
                }
            }
        }

        if (CurrentGameProperty.GameVersion?.GameType == GameNameType.Genshin) OpenCacheFolderButton.Visibility = Visibility.Collapsed;
        GameInstallationState = await CurrentGameProperty.GameVersion!.GetGameState();

        switch (GameInstallationState)
        {
            case GameInstallStateEnum.Installed:
            {
                RepairGameButton.Visibility  = repairGameButtonVisible;
                InstallGameBtn.Visibility    = Visibility.Collapsed;
                StartGameBtn.Visibility      = Visibility.Visible;
                CustomStartupArgs.Visibility = Visibility.Visible;
            }
                break;
            case GameInstallStateEnum.InstalledHavePreload:
            {
                RepairGameButton.Visibility  = repairGameButtonVisible;
                CustomStartupArgs.Visibility = Visibility.Visible;
                InstallGameBtn.Visibility    = Visibility.Collapsed;
                StartGameBtn.Visibility      = Visibility.Visible;
                //NeedShowEventIcon = false;
                SpawnPreloadBox();
            }
                break;
            case GameInstallStateEnum.NeedsUpdate:
            case GameInstallStateEnum.InstalledHavePlugin:
            {
                RepairGameButton.Visibility  = repairGameButtonVisible;
                RepairGameButton.IsEnabled   = false;
                CleanupFilesButton.IsEnabled = false;
                UpdateGameBtn.Visibility     = Visibility.Visible;
                StartGameBtn.Visibility      = Visibility.Collapsed;
                InstallGameBtn.Visibility    = Visibility.Collapsed;
            }
                break;
            default:
            {
                UninstallGameButton.IsEnabled        = false;
                RepairGameButton.IsEnabled           = false;
                OpenGameFolderButton.IsEnabled       = false;
                CleanupFilesButton.IsEnabled         = false;
                OpenCacheFolderButton.IsEnabled      = false;
                ConvertVersionButton.IsEnabled       = false;
                CustomArgsTextBox.IsEnabled          = false;
                OpenScreenshotFolderButton.IsEnabled = false;
                ConvertVersionButton.Visibility      = Visibility.Collapsed;
                RepairGameButton.Visibility          = Visibility.Collapsed;
                UninstallGameButton.Visibility       = Visibility.Collapsed;
                MoveGameLocationButton.Visibility    = Visibility.Collapsed;
            }
                break;
        }

        if (CurrentGameProperty.GameInstall?.IsRunning ?? false)
            RaiseBackgroundInstallationStatus(GameInstallationState);
    }

    private void RaiseBackgroundInstallationStatus(GameInstallStateEnum gameInstallationState)
    {
        if (gameInstallationState != GameInstallStateEnum.NeedsUpdate
            && gameInstallationState != GameInstallStateEnum.InstalledHavePlugin
            && gameInstallationState != GameInstallStateEnum.GameBroken
            && gameInstallationState != GameInstallStateEnum.NotInstalled)
        {
            return;
        }

        HideImageCarousel(true);

        progressRing.Value           = 0;
        progressRing.IsIndeterminate = true;

        InstallGameBtn.Visibility    = Visibility.Collapsed;
        UpdateGameBtn.Visibility     = Visibility.Collapsed;
        CancelDownloadBtn.Visibility = Visibility.Visible;
        ProgressTimeLeft.Visibility  = Visibility.Visible;

        bool isUseSophon = CurrentGameProperty.GameInstall?.IsUseSophon ?? false;
        if (isUseSophon)
        {
            SophonProgressStatusGrid.Visibility             =  Visibility.Visible;
            CurrentGameProperty.GameInstall.ProgressChanged += GameInstallSophon_ProgressChanged;
            CurrentGameProperty.GameInstall.StatusChanged   += GameInstallSophon_StatusChanged;
        }
        else
        {
            ProgressStatusGrid.Visibility                   =  Visibility.Visible;
            if (CurrentGameProperty.GameInstall != null)
            {
                CurrentGameProperty.GameInstall.ProgressChanged += GameInstall_ProgressChanged;
                CurrentGameProperty.GameInstall.StatusChanged   += GameInstall_StatusChanged;
            }
        }
    }
    #endregion

    #region Game Update Dialog
    private async void UpdateGameDialog(object sender, RoutedEventArgs e)
    {
        bool isUseSophon     = CurrentGameProperty.GameInstall?.IsUseSophon ?? false;
        bool isUseDeltaPatch = CurrentGameProperty.GameVersion?.IsGameHasDeltaPatch() ?? false;

        HideImageCarousel(true);

        try
        {
            MainWindow.IsCriticalOpInProgress = true;
            // Prevent device from sleep
            Sleep.PreventSleep(ILoggerHelper.GetILogger());
            // Set the notification trigger to "Running" state
            CurrentGameProperty.GameInstall?.UpdateCompletenessStatus(CompletenessStatus.Running);

            IsSkippingUpdateCheck = true;

            UpdateGameBtn.Visibility     = Visibility.Collapsed;
            CancelDownloadBtn.Visibility = Visibility.Visible;

            if (isUseSophon && !isUseDeltaPatch)
            {
                SophonProgressStatusGrid.Visibility             =  Visibility.Visible;
                CurrentGameProperty.GameInstall.ProgressChanged += GameInstallSophon_ProgressChanged;
                CurrentGameProperty.GameInstall.StatusChanged   += GameInstallSophon_StatusChanged;
            }
            else
            {
                ProgressStatusGrid.Visibility                   =  Visibility.Visible;
                if (CurrentGameProperty.GameInstall != null)
                {
                    CurrentGameProperty.GameInstall.ProgressChanged += GameInstall_ProgressChanged;
                    CurrentGameProperty.GameInstall.StatusChanged   += GameInstall_StatusChanged;
                }
            }

            int  verifResult;
            bool skipDialog = false;
            while ((verifResult = await CurrentGameProperty.GameInstall!.StartPackageVerification()) == 0)
            {
                await CurrentGameProperty.GameInstall.StartPackageDownload(skipDialog);
                skipDialog = true;
            }
            if (verifResult == -1)
            {
                return;
            }

            await CurrentGameProperty.GameInstall.StartPackageInstallation();
            CurrentGameProperty.GameInstall.ApplyGameConfig(true);
            PostInstallProcedure();

            // Set the notification trigger to "Completed" state
            CurrentGameProperty.GameInstall.UpdateCompletenessStatus(CompletenessStatus.Completed);

            // If the current window is not in focus, then spawn the notification toast
            if (WindowUtility.IsCurrentWindowInFocus())
            {
                return;
            }

            string gameNameLocale    = LauncherMetadataHelper.GetTranslatedCurrentGameTitleRegionString();
            string gameVersionString = CurrentGameProperty.GameVersion?.GetGameVersionApi()?.VersionString;

            WindowUtility.Tray_ShowNotification(
                                                string.Format(Lang._NotificationToast.GameUpdateCompleted_Title,    gameNameLocale),
                                                string.Format(Lang._NotificationToast.GameUpdateCompleted_Subtitle, gameNameLocale, gameVersionString)
                                               );
        }
        catch (TaskCanceledException)
        {
            // Set the notification trigger
            CurrentGameProperty.GameInstall?.UpdateCompletenessStatus(CompletenessStatus.Cancelled);
            LogWriteLine("Update cancelled!", LogType.Warning);
        }
        catch (OperationCanceledException)
        {
            // Set the notification trigger
            CurrentGameProperty.GameInstall?.UpdateCompletenessStatus(CompletenessStatus.Cancelled);
            LogWriteLine("Update cancelled!", LogType.Warning);
        }
        catch (NullReferenceException ex)
        {
            await SentryHelper.ExceptionHandlerAsync(ex);
            // Set the notification trigger
            CurrentGameProperty.GameInstall?.UpdateCompletenessStatus(CompletenessStatus.Cancelled);

            IsPageUnload = true;
            LogWriteLine($"Update error on {CurrentGameProperty.GameVersion?.GamePreset.ZoneFullname} game!\r\n{ex}", LogType.Error, true);
            ErrorSender.SendException(new NullReferenceException("Oops, the launcher cannot finalize the installation but don't worry, your game has been totally updated.\r\t" +
                                                                 $"Please report this issue to our GitHub here: https://github.com/CollapseLauncher/Collapse/issues/new or come back to the launcher and make sure to use Repair Game in Game Settings button later.\r\nThrow: {ex}", ex));
        }
        catch (Exception ex)
        {
            // Set the notification trigger
            CurrentGameProperty.GameInstall?.UpdateCompletenessStatus(CompletenessStatus.Cancelled);

            IsPageUnload = true;
            LogWriteLine($"Update error on {CurrentGameProperty.GameVersion?.GamePreset.ZoneFullname} game!\r\n{ex}", LogType.Error, true);
            ErrorSender.SendException(ex);
        }
        finally
        {
            IsSkippingUpdateCheck                             = false;
            if (CurrentGameProperty.GameInstall != null)
            {
                CurrentGameProperty.GameInstall.PostInstallBehaviour = PostInstallBehaviour.Nothing;

                CurrentGameProperty.GameInstall.ProgressChanged -=
                    isUseSophon ? GameInstallSophon_ProgressChanged : GameInstall_ProgressChanged;
                CurrentGameProperty.GameInstall.StatusChanged -=
                    isUseSophon ? GameInstallSophon_StatusChanged : GameInstall_StatusChanged;

                await Task.Delay(200);
                CurrentGameProperty.GameInstall.Flush();
            }

            ReturnToHomePage();

            // Turn the sleep back on
            Sleep.RestoreSleep();
            MainWindow.IsCriticalOpInProgress = false;
        }
    }
    
    private async void ProgressSettingsButton_OnClick(object sender, RoutedEventArgs e) 
        => await Dialog_DownloadSettings(CurrentGameProperty);
    #endregion
    
    #region Download Cancellation
    private void CancelPreDownload()
    {
        CurrentGameProperty.GameInstall?.CancelRoutine();

        PauseDownloadPreBtn.Visibility  = Visibility.Collapsed;
        ResumeDownloadPreBtn.Visibility = Visibility.Visible;
        ResumeDownloadPreBtn.IsEnabled  = true;
    }

    private void CancelUpdateDownload()
    {
        CurrentGameProperty.GameInstall?.CancelRoutine();
    }

    private void CancelInstallationDownload()
    {
        CurrentGameProperty.GameInstall?.CancelRoutine();
    }
    #endregion
}
