using CollapseLauncher.CustomControls;
using CollapseLauncher.GameSettings.Genshin;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.ShortcutUtils;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Win32.FileDialogCOM;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

// ReSharper disable InconsistentNaming
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable AsyncVoidMethod
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable CheckNamespace

namespace CollapseLauncher.Pages;

public partial class HomePage
{
    private bool? _regionPlayingRpc;
    private bool ToggleRegionPlayingRpc
    {
        get => _regionPlayingRpc ??= CurrentGameProperty.GameSettings?.AsIGameSettingsUniversal()
                                                        .SettingsCollapseMisc.IsPlayingRpc ?? false;
        set
        {
            if (CurrentGameProperty.GameSettings == null)
                return;

            CurrentGameProperty.GameSettings.AsIGameSettingsUniversal()
                               .SettingsCollapseMisc.IsPlayingRpc = value;
            _regionPlayingRpc = value;
            CurrentGameProperty.GameSettings.SaveSettings();
        }
    }
    
    private async void CollapsePrioControl(Process proc)
    {
        try
        {
            using (Process collapseProcess = Process.GetCurrentProcess())
            {
                collapseProcess.PriorityBoostEnabled = false;
                collapseProcess.PriorityClass        = ProcessPriorityClass.BelowNormal;
                LogWriteLine($"Collapse process [PID {collapseProcess.Id}] priority is set to Below Normal, " +
                             $"PriorityBoost is off, carousel is temporarily stopped", LogType.Default, true);
            }

            await CarouselStopScroll();
            await proc.WaitForExitAsync();

            using (Process collapseProcess = Process.GetCurrentProcess())
            {
                collapseProcess.PriorityBoostEnabled = true;
                collapseProcess.PriorityClass        = ProcessPriorityClass.Normal;
                LogWriteLine($"Collapse process [PID {collapseProcess.Id}] priority is set to Normal, " +
                             $"PriorityBoost is on, carousel is started", LogType.Default, true);
            }

            await CarouselRestartScroll();
        }
        catch (Exception ex)
        {
            await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
            LogWriteLine($"Error in Collapse Priority Control module!\r\n{ex}", LogType.Error, true);
        }
    }

    private static void GenshinHDREnforcer()
    {
        WindowsHDR GenshinHDR = new WindowsHDR();
        try
        {
            WindowsHDR.Load();
            GenshinHDR.isHDR = true;
            GenshinHDR.Save();
            LogWriteLine("Successfully forced Genshin HDR settings on!", LogType.Scheme, true);
        }
        catch (Exception ex)
        {
            SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
            LogWriteLine($"There was an error trying to force enable HDR on Genshin!\r\n{ex}", LogType.Error, true);
        }
    }

    private int GameBoostInvokeTryCount { get; set; }
    private async Task GameBoost_Invoke(GamePresetProperty gameProp)
    {
    #nullable enable
        string processName = gameProp.GameExecutableName;
        int    processId   = -1;
        try
        {
            while (!gameProp.TryGetGameProcessIdWithActiveWindow(out processId, out _))
            {
                await Task.Delay(1000); // Waiting the process to be found
            }

            LogWriteLine($"[HomePage::GameBoost_Invoke] Found target process! Waiting 10 seconds for process initialization...\r\n\t" +
                         $"Target Process : {processName} [{processId}]", LogType.Default, true);

            // Wait 20 (or 10 if it's not first try) seconds before applying
            if (GameBoostInvokeTryCount == 0)
            {
                await Task.Delay(20000);
            }
            else
            {
                await Task.Delay(10000);
            }

            // Check early exit
            if (!gameProp.GetIsGameProcessRunning(processId))
            {
                LogWriteLine($"[HomePage::GameBoost_Invoke] Game process {processName} [{processId}] has exited!",
                             LogType.Warning, true);
                return;
            }

            var result =
                gameProp.TrySetGameProcessPriority(processId,
                                                   Hi3Helper.Win32.Native.Enums.PriorityClass
                                                            .ABOVE_NORMAL_PRIORITY_CLASS);
            var lastError = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            
            if (!result && lastError != 0)
            {
                LogWriteLine($"[HomePage::GameBoost_Invoke] Failed to boost game process {processName} [{processId}] " +
                             $"priority to Above Normal! Last Win32 Error: {lastError}",
                             LogType.Error, true);
                throw new Win32Exception(lastError);
            }

            GameBoostInvokeTryCount = 0;
            LogWriteLine($"[HomePage::GameBoost_Invoke] Game process {processName} " +
                         $"[{processId}] priority is boosted to above normal!", LogType.Warning, true);
        }
        catch (Exception ex) when (GameBoostInvokeTryCount < 5)
        {
            LogWriteLine($"[HomePage::GameBoost_Invoke] (Try #{GameBoostInvokeTryCount})" +
                         $"There has been error while boosting game priority to Above Normal! Retrying...\r\n" +
                         $"\tTarget Process : {processName} [{processId}]\r\n{ex}",
                         LogType.Error, true);
            GameBoostInvokeTryCount++;
            _ = Task.Run(async () => { await GameBoost_Invoke(gameProp); });
        }
        catch (Exception ex)
        {
            LogWriteLine($"[HomePage::GameBoost_Invoke] There has been error while boosting game priority to Above Normal!\r\n" +
                         $"\tTarget Process : {processName} [{processId}]\r\n{ex}",
                         LogType.Error, true);
        }
    #nullable restore
    }
    
    private async Task CheckUserAccountControlStatus()
    {
        try
        {
            var skipChecking = GetAppConfigValue("SkipCheckingUAC").ToBool();
            if (skipChecking)
                return;

            var enabled =
                (int)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                                       "EnableLUA", 1)!;
            if (enabled != 1)
            {
                var result = await SpawnDialog(Lang._Dialogs.UACWarningTitle, Lang._Dialogs.UACWarningContent, this, Lang._Misc.Close,
                                               Lang._Dialogs.UACWarningLearnMore, Lang._Dialogs.UACWarningDontShowAgain,
                                               ContentDialogButton.Close, ContentDialogTheme.Warning);
                switch (result)
                {
                    case ContentDialogResult.Primary:
                        new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                UseShellExecute = true,
                                FileName        = "https://learn.microsoft.com/windows/security/application-security/application-control/user-account-control/settings-and-configuration?tabs=reg"
                            }
                        }.Start();
                        break;
                    case ContentDialogResult.Secondary:
                        SetAndSaveConfigValue("SkipCheckingUAC", true);
                        break;
                }
            }
        }
        catch (Exception)
        {
            // ignore
        }
    }
    
    #region Media Pack
    private async Task<bool> CheckMediaPackInstalled()
    {
        if (CurrentGameProperty.GameVersion.GameType != GameNameType.Honkai) return true;

        RegistryKey reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\WindowsFeatures\WindowsMediaVersion");
        if (reg != null)
            return true;

        LogWriteLine($"Media pack is not installed!\r\n\t" +
                     $"If you encounter the 'cry_ware_unity' error, run this script as an administrator:\r\n\t" +
                     $"{Path.Combine(AppExecutableDir, "Misc", "InstallMediaPack.cmd")}", LogType.Warning, true);

        // Skip dialog if user asked before
        if (GetAppConfigValue("HI3IgnoreMediaPack").ToBool())
            return true;

        switch (await Dialog_NeedInstallMediaPackage())
        {
            case ContentDialogResult.Primary:
                TryInstallMediaPack();
                break;
            case ContentDialogResult.Secondary:
                SetAndSaveConfigValue("HI3IgnoreMediaPack", true);
                return true;
        }
        return false;
    }

    private async void TryInstallMediaPack()
    {
        try
        {
            Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName        = Path.Combine(AppExecutableDir, "Misc", "InstallMediaPack.cmd"),
                    UseShellExecute = true,
                    Verb            = "runas"
                }
            };

            ShowLoadingPage.ShowLoading(Lang._Dialogs.InstallingMediaPackTitle,
                                        Lang._Dialogs.InstallingMediaPackSubtitle);
            MainFrameChanger.ChangeMainFrame(typeof(BlankPage));
            proc.Start();
            await proc.WaitForExitAsync();
            ShowLoadingPage.ShowLoading(Lang._Dialogs.InstallingMediaPackTitle,
                                        Lang._Dialogs.InstallingMediaPackSubtitleFinished);
            await Dialog_InstallMediaPackageFinished();
            MainFrameChanger.ChangeWindowFrame(typeof(MainPage));
        }
        catch
        {
            // ignore
        }
    }
    #endregion
    
    #region Shortcut Creation
    private async void AddToSteamButton_Click(object sender, RoutedEventArgs e)
    {
        GameStartupSettingFlyout.Hide();

        Tuple<ContentDialogResult, bool> result = await Dialog_SteamShortcutCreationConfirm();

        if (result.Item1 != ContentDialogResult.Primary)
            return;

        if (await ShortcutCreator.AddToSteam(CurrentGameProperty.GamePreset, result.Item2))
        {
            await Dialog_SteamShortcutCreationSuccess(result.Item2);
            return;
        }

        await Dialog_SteamShortcutCreationFailure();
    }

    private async void ShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        string folder = await FileDialogNative.GetFolderPicker(Lang._HomePage.CreateShortcut_FolderPicker);

        if (string.IsNullOrEmpty(folder))
            return;

        if (!IsUserHasPermission(folder))
        {
            await Dialog_InsufficientWritePermission(folder);
            return;
        }

        Tuple<ContentDialogResult, bool> result = await Dialog_ShortcutCreationConfirm(folder);

        if (result.Item1 != ContentDialogResult.Primary)
            return;

        ShortcutCreator.CreateShortcut(folder, CurrentGameProperty.GamePreset, result.Item2);
        await Dialog_ShortcutCreationSuccess(folder, result.Item2);
    }
    #endregion
}
