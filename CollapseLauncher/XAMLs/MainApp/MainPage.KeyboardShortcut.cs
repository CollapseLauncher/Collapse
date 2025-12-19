using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Pages;
using CollapseLauncher.XAMLs.Theme.CustomControls.FullPageOverlay;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.System;
using static CollapseLauncher.Dialogs.KeyboardShortcuts;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

// ReSharper disable CheckNamespace
// ReSharper disable RedundantExtendsListEntry
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable IdentifierTypo
// ReSharper disable AsyncVoidMethod
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault

namespace CollapseLauncher;

using KeybindAction = TypedEventHandler<KeyboardAccelerator, KeyboardAcceleratorInvokedEventArgs>;

public partial class MainPage : Page
{
    private readonly string ExplorerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
    
    private void InitKeyboardShortcuts()
    {
        if (GetAppConfigValue("EnableShortcuts").ToBoolNullable() == null)
        {
            SetAndSaveConfigValue("EnableShortcuts", true);
            KbShortcutList = null;

            SpawnNotificationPush(
                                  Lang._AppNotification.NotifKbShortcutTitle,
                                  Lang._AppNotification.NotifKbShortcutSubtitle,
                                  NotifSeverity.Informational,
                                  -20,
                                  true,
                                  false,
                                  null,
                                  NotificationPush.GenerateNotificationButton("", Lang._AppNotification.NotifKbShortcutBtn, (_, _) => ShowKeybinds_Invoked(null, null)),
                                  true,
                                  true,
                                  true
                                 );
        }

        if (AreShortcutsEnabled) CreateKeyboardShortcutHandlers();
    }

    private void CreateKeyboardShortcutHandlers()
    {
        try
        {
            if (KbShortcutList == null || KbShortcutList.Count == 0)
                LoadKbShortcuts();

            int numIndex = 0;
            if (KbShortcutList != null)
            {
                VirtualKeyModifiers keyModifier = KbShortcutList["GameSelection"].Modifier;
                while (numIndex < LauncherMetadataHelper.CurrentGameNameCount)
                {
                    KeyboardAccelerator keystroke = new KeyboardAccelerator
                    {
                        Modifiers = keyModifier,
                        Key       = VirtualKey.Number1 + numIndex
                    };
                    keystroke.Invoked += KeyboardGameShortcut_Invoked;
                    KeyboardHandler.KeyboardAccelerators.Add(keystroke);

                    KeyboardAccelerator keystrokeNP = new KeyboardAccelerator
                    {
                        Key = VirtualKey.NumberPad1 + numIndex
                    };
                    keystrokeNP.Invoked += KeyboardGameShortcut_Invoked;
                    KeyboardHandler.KeyboardAccelerators.Add(keystrokeNP);

                    numIndex++;
                }

                numIndex    = 0;
                keyModifier = KbShortcutList["RegionSelection"].Modifier;
                while (numIndex < LauncherMetadataHelper.CurrentGameRegionMaxCount)
                {
                    KeyboardAccelerator keystroke = new KeyboardAccelerator
                    {
                        Modifiers = keyModifier,
                        Key       = VirtualKey.Number1 + numIndex
                    };
                    keystroke.Invoked += KeyboardGameRegionShortcut_Invoked;
                    KeyboardHandler.KeyboardAccelerators.Add(keystroke);

                    numIndex++;
                }
            }

            KeyboardAccelerator keystrokeF5 = new KeyboardAccelerator
            {
                Key = VirtualKey.F5
            };
            keystrokeF5.Invoked += RefreshPage_Invoked;
            KeyboardHandler.KeyboardAccelerators.Add(keystrokeF5);

            Dictionary<string, KeybindAction> actions = new()
            {
                // General
                { "KbShortcutsMenu", ShowKeybinds_Invoked },
                { "HomePage", GoHome_Invoked },
                { "SettingsPage", GoSettings_Invoked },
                { "NotificationPanel", OpenNotify_Invoked },
                { "PluginManager", OpenPluginManager_Invoked },

                // Game Related
                { "ScreenshotFolder", OpenScreenshot_Invoked},
                { "GameFolder", OpenGameFolder_Invoked },
                { "CacheFolder", OpenGameCacheFolder_Invoked },
                { "ForceCloseGame", ForceCloseGame_Invoked },

                { "RepairPage", GoGameRepair_Invoked },
                { "GameSettingsPage", GoGameSettings_Invoked },
                { "CachesPage", GoGameCaches_Invoked },

                { "ReloadRegion", RefreshPage_Invoked }
            };

            foreach (KeyValuePair<string, KeybindAction> func in actions)
            {
                if (KbShortcutList == null)
                {
                    continue;
                }

                KeyboardAccelerator kbfunc = new KeyboardAccelerator
                {
                    Modifiers = KbShortcutList[func.Key].Modifier,
                    Key       = KbShortcutList[func.Key].Key
                };
                kbfunc.Invoked += func.Value;
                KeyboardHandler.KeyboardAccelerators.Add(kbfunc);
            }
        }
        catch (Exception error)
        {
            SentryHelper.ExceptionHandler(error);
            LogWriteLine(error.ToString());
            KbShortcutList = null;
            CreateKeyboardShortcutHandlers();
        }
    }

    private void RefreshPage_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (CannotUseKbShortcuts || !IsLoadRegionComplete)
            return;

        switch (PreviousTag)
        {
            case "launcher":
                RestoreCurrentRegion();
                ChangeRegionNoWarning(IsShowRegionChangeWarning ? ChangeRegionConfirmBtn : ChangeRegionConfirmBtnNoWarning, null);
                return;
            case "settings":
                return;
            default:
                string itemTag = PreviousTag;
                PreviousTag = "Empty";
                NavigateInnerSwitch(itemTag);
                if (LauncherFrame != null && LauncherFrame.BackStack is { Count: > 0 })
                    LauncherFrame.BackStack.RemoveAt(LauncherFrame.BackStack.Count - 1);
                if (PreviousTagString is { Count: > 0 })
                    PreviousTagString.RemoveAt(PreviousTagString.Count - 1);
                return;
        }
    }

    private void DeleteKeyboardShortcutHandlers() => KeyboardHandler.KeyboardAccelerators.Clear();

    private void RestoreCurrentRegion()
    {
        string gameName = GetAppConfigValue("GameCategory")!;
    #nullable enable
        List<string>? gameNameCollection = LauncherMetadataHelper.GetGameNameCollection();
        _ = LauncherMetadataHelper.GetGameRegionCollection(gameName);

        var indexCategory                    = gameNameCollection?.IndexOf(gameName) ?? -1;
        if (indexCategory < 0) indexCategory = 0;

        var indexRegion = LauncherMetadataHelper.GetPreviousGameRegion(gameName);

        ComboBoxGameCategory.SelectedIndex = indexCategory;
        ComboBoxGameRegion.SelectedIndex   = indexRegion;
    }

    private void KeyboardGameShortcut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        int index = (int)sender.Key; index -= index < 96 ? 49 : 97;

        DisableInstantRegionChange = true;
        RestoreCurrentRegion();
            
        if (CannotUseKbShortcuts || !IsLoadRegionComplete
                                 || index >= ComboBoxGameCategory.Items.Count
                                 || ComboBoxGameCategory.SelectedValue == ComboBoxGameCategory.Items[index]
           )
        {
            DisableInstantRegionChange = false;
            return;
        }

        ComboBoxGameCategory.SelectedValue = ComboBoxGameCategory.Items[index];
        ComboBoxGameRegion.SelectedIndex = GetIndexOfRegionStringOrDefault(GetComboBoxGameRegionValue(ComboBoxGameCategory.SelectedValue));
        ChangeRegionNoWarning(ChangeRegionConfirmBtn, null);
        ChangeRegionConfirmBtn.IsEnabled          = false;
        ChangeRegionConfirmBtnNoWarning.IsEnabled = false;
        CannotUseKbShortcuts                      = true;
        DisableInstantRegionChange                = false;
    }

    private void KeyboardGameRegionShortcut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        int index = (int)sender.Key; index -= index < 96 ? 49 : 97;

        DisableInstantRegionChange = true;
        RestoreCurrentRegion();
            

        if (CannotUseKbShortcuts || !IsLoadRegionComplete
                                 || index >= ComboBoxGameRegion.Items.Count 
                                 || ComboBoxGameRegion.SelectedValue == ComboBoxGameRegion.Items[index])
        {
            DisableInstantRegionChange = false;
            return;
        }

        ComboBoxGameRegion.SelectedValue          = ComboBoxGameRegion.Items[index];
        ChangeRegionConfirmBtn.IsEnabled          = false;
        ChangeRegionConfirmBtnNoWarning.IsEnabled = false;
        CannotUseKbShortcuts                      = true;
        DisableInstantRegionChange                = false;
        ChangeRegionNoWarning(ChangeRegionConfirmBtn, null);
    }

    private async void ShowKeybinds_Invoked(KeyboardAccelerator? sender, KeyboardAcceleratorInvokedEventArgs? args)
    {
        if (CannotUseKbShortcuts) return;

        await Dialog_ShowKbShortcuts(this);
    }

    private void GoHome_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!IsLoadRegionComplete || CannotUseKbShortcuts) return;

        if (NavigationViewControl.SelectedItem == NavigationViewControl.MenuItems[0]) return;

        NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems[0];
        NavigateInnerSwitch("launcher");

    }

    private void GoSettings_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!IsLoadRegionComplete || CannotUseKbShortcuts) return;

        if (NavigationViewControl.SelectedItem == NavigationViewControl.SettingsItem) return;

        NavigationViewControl.SelectedItem = NavigationViewControl.SettingsItem;
        Navigate(typeof(SettingsPage), "settings");
    }

    private void OpenNotify_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ToggleNotificationPanelBtn.IsChecked = !ToggleNotificationPanelBtn.IsChecked;
        ToggleNotificationPanelBtnClick(null, null);
    }

    private void OpenPluginManager_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        FullPageOverlay overlayMenu = new FullPageOverlay(new PluginManagerPage(), XamlRoot, true)
        {
            Size = FullPageOverlaySize.Full,
            OverlayTitleSource = () => Lang._PluginManagerPage.PageTitle,
            OverlayTitleIcon = new FontIconSource
            {
                Glyph = "\uE912",
                FontSize = 16
            }
        };

        _ = overlayMenu.ShowAsync();
    }

    private string GameDirPath => CurrentGameProperty.GameVersion?.GameDirPath!;
    private void OpenScreenshot_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!IsGameInstalled()) return;

        string ScreenshotFolder = Path.Combine(NormalizePath(GameDirPath), CurrentGameProperty.GameVersion?.GamePreset.GameType switch
                                                                           {
                                                                               GameNameType.StarRail => $"{Path.GetFileNameWithoutExtension(CurrentGameProperty.GameVersion.GamePreset.GameExecutableName)}_Data\\ScreenShots",
                                                                               _ => "ScreenShot"
                                                                           });

        LogWriteLine($"Opening Screenshot Folder:\r\n\t{ScreenshotFolder}");

        if (!Directory.Exists(ScreenshotFolder))
            Directory.CreateDirectory(ScreenshotFolder);

        new Process
        {
            StartInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName        = ExplorerPath,
                Arguments       = ScreenshotFolder
            }
        }.Start();
    }

    private async void OpenGameFolder_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        try
        {
            if (!IsGameInstalled()) return;

            string GameFolder = NormalizePath(GameDirPath);
            LogWriteLine($"Opening Game Folder:\r\n\t{GameFolder}");
            await Task.Run(() =>
                               new Process
                               {
                                   StartInfo = new ProcessStartInfo
                                   {
                                       UseShellExecute = true,
                                       FileName        = ExplorerPath,
                                       Arguments       = GameFolder
                                   }
                               }.Start());
        }
        catch (Exception ex)
        {
            LogWriteLine($"Failed when trying to open game folder!\r\n{ex}", LogType.Error, true);
            ErrorSender.SendException(ex);
        }
    }

    private async void OpenGameCacheFolder_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        try
        {
            if (!IsGameInstalled()) return;

            var gameFolder = CurrentGameProperty.GameVersion?.GameDirAppDataPath ??
                             CurrentGameProperty.GameVersion?.GameDirPath ?? 
                             null;
            
            if (string.IsNullOrEmpty(gameFolder)) return;
            LogWriteLine($"Opening Game Folder:\r\n\t{gameFolder}");
            await Task.Run(() =>
                               new Process
                               {
                                   StartInfo = new ProcessStartInfo
                                   {
                                       UseShellExecute = true,
                                       FileName        = ExplorerPath,
                                       Arguments       = gameFolder
                                   }
                               }.Start());
        }
        catch (Exception ex)
        {
            LogWriteLine($"Failed when trying to open game cache folder!\r\n{ex}", LogType.Error, true);
            ErrorSender.SendException(ex);
        }
    }

    private void ForceCloseGame_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!CurrentGameProperty.IsGameRunning) return;

        PresetConfig gamePreset         = CurrentGameProperty.GameVersion?.GamePreset!;
        string?      gamePresetExecName = gamePreset.GameExecutableName;
        if (string.IsNullOrEmpty(gamePresetExecName))
        {
            return;
        }

        try
        {
            Process[] gameProcess = Process.GetProcessesByName(gamePresetExecName.Split('.')[0]);
            foreach (var p in gameProcess)
            {
                LogWriteLine($"Trying to stop game process {gamePresetExecName.Split('.')[0]} at PID {p.Id}", LogType.Scheme, true);
                p.Kill();
            }
        }
        catch (Win32Exception ex)
        {
            SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
            LogWriteLine($"There is a problem while trying to stop Game with Region: {gamePreset.ZoneName}\r\nTraceback: {ex}", LogType.Error, true);
        }
    }
    private void GoGameRepair_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!IsLoadRegionComplete || CannotUseKbShortcuts) return;

        if (NavigationViewControl.SelectedItem == NavigationViewControl.MenuItems[2]) return;

        NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems[2];
        NavigateInnerSwitch("repair");
    }

    private void GoGameCaches_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!IsLoadRegionComplete || CannotUseKbShortcuts)
            return;
        if (NavigationViewControl.SelectedItem == NavigationViewControl.MenuItems[3])
            return;

        NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems[3];
        NavigateInnerSwitch("caches");
    }

    private void GoGameSettings_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!IsLoadRegionComplete || CannotUseKbShortcuts)
            return;

        if (NavigationViewControl.SelectedItem == NavigationViewControl.FooterMenuItems.Last())
            return;

        NavigationViewControl.SelectedItem = NavigationViewControl.FooterMenuItems.Last();
        switch (CurrentGameProperty.GamePreset)
        {
            case { GameType: GameNameType.Honkai }:
                Navigate(typeof(HonkaiGameSettingsPage), "honkaigamesettings");
                break;
            case { GameType: GameNameType.Genshin }:
                Navigate(typeof(GenshinGameSettingsPage), "genshingamesettings");
                break;
            case { GameType: GameNameType.StarRail }:
                Navigate(typeof(StarRailGameSettingsPage), "starrailgamesettings");
                break;
            case { GameType: GameNameType.Zenless }:
                Navigate(typeof(ZenlessGameSettingsPage), "zenlessgamesettings");
                break;
        }
    }

    private static bool AreShortcutsEnabled
    {
        get => GetAppConfigValue("EnableShortcuts").ToBool(true);
    }

    private void SettingsPage_KeyboardShortcutsEvent(object sender, int e)
    {
        switch (e)
        {
            case 0:
                CreateKeyboardShortcutHandlers();
                break;
            case 1:
                DeleteKeyboardShortcutHandlers();
                CreateKeyboardShortcutHandlers();
                break;
            case 2:
                DeleteKeyboardShortcutHandlers();
                break;
        }
    }
}