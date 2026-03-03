using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.Helper.Update;
using CollapseLauncher.Pages.OOBE;
using CollapseLauncher.Statics;
using Hi3Helper;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using Hi3Helper.Win32.WinRT.ToastCOM.Notification;
using InnoSetupHelper;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Notifications;
using DispatcherQueueExtensions = CollapseLauncher.Extension.DispatcherQueueExtensions;
using UIElementExtensions = CollapseLauncher.Extension.UIElementExtensions;
// ReSharper disable StringLiteralTypo
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher;

public partial class MainPage
{
    private ObservableCollection<UIElement> NotificationElementsCollection { get; }= [];

    private void NotificationInvoker_EventInvoker(object sender, NotificationInvokerProp e)
    {
        if (e.IsCustomNotif)
        {
            if (e is { CustomNotifAction: NotificationCustomAction.Add, OtherContent: InfoBar contentAsInfoBar })
            {
                SpawnNotificationUI(e.Notification.MsgId, contentAsInfoBar);
            }
            else
            {
                RemoveNotificationUI(e.Notification.MsgId);
            }
            return;
        }

        SpawnNotificationPush(e.Notification.Title, e.Notification.Message, e.Notification.Severity,
                              e.Notification.MsgId, e.Notification.IsClosable ?? true, e.Notification.IsDisposable ?? true, e.CloseAction,
                              e.OtherContent, e.IsAppNotif, e.Notification.Show, e.Notification.IsForceShowNotificationPanel);
    }

    private async Task FetchNotificationFeed()
    {
        try
        {
            InnerLauncherConfig.NotificationData = new NotificationPush();
            _isLoadNotifComplete                 = false;

            CancellationTokenSource tokenSource = new();
            RunTimeoutCancel(tokenSource);

            string prefixUrl = string.Format(LauncherConfig.AppNotifURLPrefix, LauncherConfig.IsPreview ? "preview" : "stable");
            await using Stream networkStream = await FallbackCDNUtil.TryGetCDNFallbackStream(prefixUrl, token: tokenSource.Token);
            InnerLauncherConfig.NotificationData = await networkStream.DeserializeAsync(NotificationPushJsonContext.Default.NotificationPush, token: tokenSource.Token);

            _isLoadNotifComplete = true;

            InnerLauncherConfig.NotificationData?.EliminatePushList();
        }
        catch (Exception ex)
        {
            await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
            Logger.LogWriteLine($"Failed to load notification push!\r\n{ex}", LogType.Warning, true);
        }
    }

    private async Task GenerateLocalAppNotification()
    {
        InnerLauncherConfig.NotificationData?.AppPush.Add(new NotificationProp
        {
            Show = true,
            MsgId = 0,
            IsDisposable = false,
            Severity = NotifSeverity.Success,
            Title = Locale.Lang._AppNotification.NotifFirstWelcomeTitle,
            Message = string.Format(Locale.Lang._AppNotification.NotifFirstWelcomeSubtitle,
                                    Locale.Lang._AppNotification.NotifFirstWelcomeBtn),
            OtherUIElement = GenerateNotificationButtonStartProcess("",
                                                                    "https://github.com/CollapseLauncher/Collapse/wiki",
                                                                    Locale.Lang._AppNotification.NotifFirstWelcomeBtn)
        });

        if (LauncherConfig.IsPreview)
        {
            InnerLauncherConfig.NotificationData?.AppPush.Add(new NotificationProp
            {
                Show = true,
                MsgId = -1,
                IsDisposable = true,
                Severity = NotifSeverity.Informational,
                Title = Locale.Lang._AppNotification.NotifPreviewBuildUsedTitle,
                Message = string.Format(Locale.Lang._AppNotification.NotifPreviewBuildUsedSubtitle,
                                        Locale.Lang._AppNotification.NotifPreviewBuildUsedBtn),
                OtherUIElement = GenerateNotificationButtonStartProcess("",
                                                                        "https://github.com/CollapseLauncher/Collapse/issues",
                                                                        Locale.Lang._AppNotification.NotifPreviewBuildUsedBtn)
            });
        }

        if (!_isNotificationPanelShow && LauncherConfig.IsFirstInstall)
        {
            await ForceShowNotificationPanel();
        }
    }

    private static Button GenerateNotificationButtonStartProcess(string iconGlyph, string pathOrURL, string text, bool isUseShellExecute = true)
    {
        return NotificationPush.GenerateNotificationButton(iconGlyph, text, (_, _) =>
                                                                            {
                                                                                new Process
                                                                                {
                                                                                    StartInfo = new ProcessStartInfo
                                                                                    {
                                                                                        UseShellExecute = isUseShellExecute,
                                                                                        FileName = pathOrURL
                                                                                    }
                                                                                }.Start();
                                                                            });
    }

    private async void RunTimeoutCancel(CancellationTokenSource token)
    {
        try
        {
            await Task.Delay(10000);
            if (_isLoadNotifComplete)
            {
                return;
            }

            Logger.LogWriteLine("Cancel to load notification push! > 10 seconds", LogType.Error, true);
            await token.CancelAsync();
        }
        catch
        {
            // ignored
        }
    }

    private async Task SpawnPushAppNotification()
    {
        if (InnerLauncherConfig.NotificationData?.AppPush == null) return;
        foreach (NotificationProp entry in InnerLauncherConfig.NotificationData.AppPush.ToList())
        {
            // Check for Close Action for certain MsgIds
            TypedEventHandler<InfoBar, object>? clickCloseAction = entry.MsgId switch
                                                                   {
                                                                       0 => (_, _) =>
                                                                            {
                                                                                InnerLauncherConfig.NotificationData.AddIgnoredMsgIds(0);
                                                                                InnerLauncherConfig.SaveLocalNotificationData();
                                                                            },
                                                                       _ => null
                                                                   };

            GameVersion? validForVerBelow = entry.ValidForVerBelow;
            GameVersion? validForVerAbove = entry.ValidForVerAbove;

            if ((entry.ValidForVerBelow == null && IsNotificationTimestampValid(entry))
                || (LauncherUpdateHelper.LauncherCurrentVersion < validForVerBelow
                    && validForVerAbove < LauncherUpdateHelper.LauncherCurrentVersion)
                || LauncherUpdateHelper.LauncherCurrentVersion < validForVerBelow)
            {
                if (entry.ActionProperty != null)
                {
                    entry.OtherUIElement = entry.ActionProperty.GetFrameworkElement();
                }

                SpawnNotificationPush(entry.Title,
                                      entry.Message,
                                      entry.Severity,
                                      entry.MsgId,
                                      entry.IsClosable ?? true,
                                      entry.IsDisposable ?? true,
                                      clickCloseAction,
                                      entry.OtherUIElement as FrameworkElement,
                                      true,
                                      entry.Show,
                                      entry.IsForceShowNotificationPanel);
            }
            await Task.Delay(250);
        }
    }

    private async Task SpawnAppUpdatedNotification()
    {
        try
        {
            FileInfo updateNotifFile = new FileInfo(Path.Combine(LauncherConfig.AppDataFolder, "_NewVer"))
                                      .EnsureCreationOfDirectory()
                                      .EnsureNoReadOnly(out bool isUpdateNotifFileExist);
            FileInfo needInnoUpdateFile = new FileInfo(Path.Combine(LauncherConfig.AppDataFolder, "_NeedInnoLogUpdate"))
                                         .EnsureCreationOfDirectory()
                                         .EnsureNoReadOnly(out bool isNeedInnoUpdateFileExist);
            FileInfo innoLogFile = new FileInfo(Path.Combine(Path.GetDirectoryName(LauncherConfig.AppExecutableDir) ?? string.Empty, "unins000.dat"))
               .EnsureNoReadOnly(out bool isInnoLogFileExist);


            void ClickClose(InfoBar infoBar, object o)
            {
                _ = updateNotifFile.TryDeleteFile();
            }

            // If the update was handled by squirrel module, and if it needs Inno Setup Log file to get updated, then do the routine
            if (isNeedInnoUpdateFileExist)
            {
                try
                {
                    if (isInnoLogFileExist)
                    {
                        InnoSetupLogUpdate.UpdateInnoSetupLog(innoLogFile.FullName);
                    }
                    needInnoUpdateFile.TryDeleteFile();
                }
                catch (Exception ex)
                {
                    await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                    Logger.LogWriteLine($"Something wrong while opening the \"unins000.dat\" or deleting the \"_NeedInnoLogUpdate\" file\r\n{ex}", LogType.Error, true);
                }
            }

            if (!isUpdateNotifFileExist)
            {
                return;
            }

            string[] verStrings = await File.ReadAllLinesAsync(updateNotifFile.FullName);
            string   verString  = string.Empty;
            if (verStrings.Length > 0 && GameVersion.TryParse(verStrings[0], out GameVersion version))
            {
                verString = version.VersionString;
                SpawnNotificationPush(Locale.Lang._Misc.UpdateCompleteTitle,
                                      string.Format(Locale.Lang._Misc.UpdateCompleteSubtitle,
                                                    version.ToString("n"),
                                                    LauncherConfig.IsPreview ? "Preview" : "Stable"),
                                      NotifSeverity.Success,
                                      0xAF,
                                      true,
                                      false,
                                      ClickClose,
                                      null,
                                      true,
                                      true,
                                      true
                                     );
            }

            DirectoryInfo fold = new(Path.Combine(LauncherConfig.AppExecutableDir, "_Temp"));
            if (fold.Exists)
            {
                foreach (FileInfo file in fold.EnumerateFiles().EnumerateNoReadOnly())
                {
                    if (!file.Name.StartsWith("ApplyUpdate"))
                    {
                        continue;
                    }

                    FileInfo target = new(Path.Combine(LauncherConfig.AppExecutableDir, file.Name));
                    file.TryMoveTo(target);
                }

                fold.Delete(true);
            }

            try
            {
                // Remove update notif mark file to avoid it showing the same notification again.
                updateNotifFile.TryDeleteFile();

                // Get current game property, including game preset
                GamePresetProperty currentGameProperty = GamePropertyVault.GetCurrentGameProperty();
                (_, string? heroImage) = OOBESelectGame.GetLogoAndHeroImgPath(currentGameProperty.GamePreset);

                // Create notification
                NotificationContent toastContent = NotificationContent.Create()
                                                                      .SetTitle(Locale.Lang._NotificationToast.LauncherUpdated_NotifTitle)
                                                                      .SetContent(
                                                                            string
                                                                               .Format(Locale.Lang._NotificationToast.LauncherUpdated_NotifSubtitle,
                                                                                    verString + (LauncherConfig.IsPreview
                                                                                        ? "-preview"
                                                                                        : ""),
                                                                                    Locale.Lang._SettingsPage.PageTitle,
                                                                                    Locale.Lang._SettingsPage.Update_SeeChangelog)
                                                                           )
                                                                      .AddAppHeroImagePath(heroImage);

                // Get notification service
                ToastNotification? notificationService =
                    WindowUtility.CurrentToastNotificationService?.CreateToastNotification(toastContent);

                // Spawn notification service
                ToastNotifier? notifier =
                    WindowUtility.CurrentToastNotificationService?.CreateToastNotifier();
                notifier?.Show(notificationService);
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex);
                Logger.LogWriteLine($"[SpawnAppUpdatedNotification] Failed to spawn toast notification!\r\n{ex}",
                                    LogType.Error,
                                    true);
            }
        }
        catch
        {
            // ignored
        }
    }

    private static InfoBarSeverity NotifSeverity2InfoBarSeverity(NotifSeverity inp)
    {
        return inp switch
               {
                   NotifSeverity.Success => InfoBarSeverity.Success,
                   NotifSeverity.Warning => InfoBarSeverity.Warning,
                   NotifSeverity.Error => InfoBarSeverity.Error,
                   _ => InfoBarSeverity.Informational
               };
    }

    private void SpawnNotificationPush(string title, string textContent, NotifSeverity severity, int msgId = 0, bool isClosable = true,
                                       bool disposable = false, TypedEventHandler<InfoBar, object>? closeClickHandler = null, FrameworkElement? otherContent = null, bool isAppNotif = true,
                                       bool? show = false, bool forceShowNotificationPanel = false)
    {
        if (!(show ?? false)) return;
        if (InnerLauncherConfig.NotificationData?.CurrentShowMsgIds.Contains(msgId) ?? false) return;

        if (InnerLauncherConfig.NotificationData?.IsMsgIdIgnored(msgId) ?? false) return;

        InnerLauncherConfig.NotificationData?.CurrentShowMsgIds.Add(msgId);

        DispatcherQueueExtensions.TryEnqueue(() =>
        {
            StackPanel otherContentContainer = UIElementExtensions.CreateStackPanel().WithMargin(0d, -4d, 0d, 8d);

            InfoBar notification = new InfoBar
                                   {
                                       Title         = title,
                                       Message       = textContent,
                                       Severity      = NotifSeverity2InfoBarSeverity(severity),
                                       IsClosable    = isClosable,
                                       IsIconVisible = true,
                                       Shadow        = SharedShadow,
                                       IsOpen        = true
                                   }
                                  .WithMargin(4d, 4d, 4d, 0d).WithWidth(600)
                                  .WithCornerRadius(8)
                                  .WithHorizontalAlignment(HorizontalAlignment.Right);

            notification.Translation = new Vector3(0,0,32);

            if (otherContent != null)
                otherContentContainer.AddElementToStackPanel(otherContent);

            if (disposable)
            {
                CheckBox neverAskNotif = new()
                {
                    Content = new TextBlock
                    {
                        Text = Locale.Lang._MainPage.NotifNeverAsk,
                        FontWeight = FontWeights.Medium
                    },
                    Tag = $"{msgId},{isAppNotif}"
                };
                neverAskNotif.Checked   += NeverAskNotif_Checked;
                neverAskNotif.Unchecked += NeverAskNotif_Unchecked;

                InputCursor cursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
                neverAskNotif.SetCursor(cursor);
                otherContentContainer.AddElementToStackPanel(neverAskNotif);
            }

            if (disposable || otherContent != null)
                notification.Content = otherContentContainer;

            notification.Tag              =  msgId;
            notification.CloseButtonClick += closeClickHandler;

            SpawnNotificationUI(msgId, notification);

            if (forceShowNotificationPanel && !_isNotificationPanelShow)
            {
                _ = ForceShowNotificationPanel();
            }
        });
    }

    private void SpawnNotificationUI(int tagID, InfoBar notification)
    {
        Grid container = UIElementExtensions.CreateGrid().WithTag(tagID);
        notification.Loaded += (_, _) =>
                               {
                                   if (!(ToggleNotificationPanelBtn.IsChecked ?? false))
                                   {
                                       NewNotificationCountBadge.Value++;
                                   }
                               };

        notification.Closed += (s, _) =>
                               {
                                   s.Translation = default;
                                   s.SetHeight(0d);
                                   s.SetMargin(0d);
                                   int msg = (int)s.Tag;

                                   if (InnerLauncherConfig.NotificationData?.CurrentShowMsgIds.Contains(msg) ?? false)
                                   {
                                       InnerLauncherConfig.NotificationData.CurrentShowMsgIds.Remove(msg);
                                   }

                                   RemoveNotificationUI(msg);
                               };

        container.AddElementToGridRowColumn(notification);
        NotificationElementsCollection.Add(container);
    }

    private void RemoveNotificationUI(int tagID)
    {
        Grid?    notif    = NotificationContainer.Children.OfType<Grid>().FirstOrDefault(x => (int)x.Tag == tagID);
        InfoBar? notifBar = notif?.Children.OfType<InfoBar>().FirstOrDefault();

        if (notifBar != null && notifBar.IsClosable)
            notifBar.IsOpen = false;

        if (notif == null)
            return;

        NotificationElementsCollection.Remove(notif);
    }

    private async void ClearAllNotification(object sender, RoutedEventArgs args)
    {
        try
        {
            Button? button = sender as Button;
            if (button != null)
                button.IsEnabled = false;

            int stackIndex = 0;
            for (; stackIndex < NotificationContainer.Children.Count;)
            {
                if (NotificationContainer.Children[stackIndex] is not Grid container
                 || container.Children == null || container.Children.Count == 0
                 || container.Children[0] is not InfoBar { IsClosable: true } notifBar)
                {
                    ++stackIndex;
                    continue;
                }

                notifBar.IsOpen = false;
                NotificationElementsCollection.Remove(container);
                await Task.Delay(100);
            }

            if (NotificationContainer.Children.Count == 0)
            {
                await Task.Delay(500);
                ToggleNotificationPanelBtn.IsChecked = false;
                _isNotificationPanelShow              = false;
                ShowHideNotificationPanel();
            }

            if (button != null) button.IsEnabled = true;
        }
        catch (Exception e)
        {
            SentryHelper.ExceptionHandler(e);
            Logger.LogWriteLine($"{e}", LogType.Error, true);
        }
    }

    private static void NeverAskNotif_Checked(object sender, RoutedEventArgs e)
    {
        string[]? data = (sender as CheckBox)?.Tag.ToString()?.Split(',');
        if (data == null)
        {
            return;
        }

        InnerLauncherConfig.NotificationData?.AddIgnoredMsgIds(int.Parse(data[0]), bool.Parse(data[1]));
        InnerLauncherConfig.SaveLocalNotificationData();
    }

    private static void NeverAskNotif_Unchecked(object sender, RoutedEventArgs e)
    {
        string[]? data = (sender as CheckBox)?.Tag.ToString()?.Split(',');
        if (data == null)
        {
            return;
        }

        InnerLauncherConfig.NotificationData?.RemoveIgnoredMsgIds(int.Parse(data[0]), bool.Parse(data[1]));
        InnerLauncherConfig.SaveLocalNotificationData();
    }

    private async Task ForceShowNotificationPanel()
    {
        ToggleNotificationPanelBtn.IsChecked = true;
        _isNotificationPanelShow              = true;
        ShowHideNotificationPanel();
        await Task.Delay(250);
        double currentVOffset = NotificationContainer.ActualHeight;

        NotificationPanelScrollViewer.ScrollToVerticalOffset(currentVOffset);
    }

    private void NotificationElementsCollection_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e)
        {
            case { Action: NotifyCollectionChangedAction.Add, NewItems: not null }:
                foreach (UIElement element in e.NewItems.OfType<UIElement>())
                {
                    NotificationContainer.Children.Add(element);
                }
                return;
            case { Action: NotifyCollectionChangedAction.Remove, OldItems: not null }:
                foreach (UIElement element in e.OldItems.OfType<UIElement>())
                {
                    NotificationContainer.Children.Remove(element);
                }
                return;
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            NotificationContainer.Children.Clear();
        }
    }
}
