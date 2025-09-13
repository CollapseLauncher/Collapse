using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.Helper.Update;
using CollapseLauncher.Pages.OOBE;
using Hi3Helper;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Win32.WinRT.ToastCOM.Notification;
using InnoSetupHelper;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.Statics.GamePropertyVault;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using UIElementExtensions = CollapseLauncher.Extension.UIElementExtensions;
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

public partial class MainPage : Page
{
    private void NotificationInvoker_EventInvoker(object sender, NotificationInvokerProp e)
    {
        if (e.IsCustomNotif)
        {
            if (e.CustomNotifAction == NotificationCustomAction.Add)
            {
                SpawnNotificationoUI(e.Notification.MsgId, e.OtherContent as InfoBar);
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
            NotificationData    = new NotificationPush();
            IsLoadNotifComplete = false;
            CancellationTokenSource TokenSource = new CancellationTokenSource();
            RunTimeoutCancel(TokenSource);

            await using Stream networkStream = await FallbackCDNUtil.TryGetCDNFallbackStream(string.Format(AppNotifURLPrefix, IsPreview ? "preview" : "stable"), token: TokenSource.Token);
            NotificationData = await networkStream.DeserializeAsync(NotificationPushJsonContext.Default.NotificationPush, token: TokenSource.Token);
            IsLoadNotifComplete = true;

            NotificationData?.EliminatePushList();
        }
        catch (Exception ex)
        {
            await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
            LogWriteLine($"Failed to load notification push!\r\n{ex}", LogType.Warning, true);
        }
    }

    private async Task GenerateLocalAppNotification()
    {
        NotificationData?.AppPush.Add(new NotificationProp
        {
            Show = true,
            MsgId = 0,
            IsDisposable = false,
            Severity = NotifSeverity.Success,
            Title = Lang._AppNotification.NotifFirstWelcomeTitle,
            Message = string.Format(Lang._AppNotification.NotifFirstWelcomeSubtitle, Lang._AppNotification.NotifFirstWelcomeBtn),
            OtherUIElement = GenerateNotificationButtonStartProcess(
                                                                    "",
                                                                    "https://github.com/CollapseLauncher/Collapse/wiki",
                                                                    Lang._AppNotification.NotifFirstWelcomeBtn)
        });

        if (IsPreview)
        {
            NotificationData?.AppPush.Add(new NotificationProp
            {
                Show = true,
                MsgId = -1,
                IsDisposable = true,
                Severity = NotifSeverity.Informational,
                Title = Lang._AppNotification.NotifPreviewBuildUsedTitle,
                Message = string.Format(Lang._AppNotification.NotifPreviewBuildUsedSubtitle, Lang._AppNotification.NotifPreviewBuildUsedBtn),
                OtherUIElement = GenerateNotificationButtonStartProcess(
                                                                        "",
                                                                        "https://github.com/CollapseLauncher/Collapse/issues",
                                                                        Lang._AppNotification.NotifPreviewBuildUsedBtn)
            });
        }

        if (!IsNotificationPanelShow && IsFirstInstall)
        {
            await ForceShowNotificationPanel();
        }
    }

    private static Button GenerateNotificationButtonStartProcess(string IconGlyph, string PathOrURL, string Text, bool IsUseShellExecute = true)
    {
        return NotificationPush.GenerateNotificationButton(IconGlyph, Text, (_, _) =>
                                                                            {
                                                                                new Process
                                                                                {
                                                                                    StartInfo = new ProcessStartInfo
                                                                                    {
                                                                                        UseShellExecute = IsUseShellExecute,
                                                                                        FileName = PathOrURL
                                                                                    }
                                                                                }.Start();
                                                                            });
    }

    private async void RunTimeoutCancel(CancellationTokenSource Token)
    {
        await Task.Delay(10000);
        if (IsLoadNotifComplete)
        {
            return;
        }

        LogWriteLine("Cancel to load notification push! > 10 seconds", LogType.Error, true);
        await Token.CancelAsync();
    }

    private async Task SpawnPushAppNotification()
    {
        if (NotificationData?.AppPush == null) return;
        foreach (NotificationProp Entry in NotificationData.AppPush.ToList())
        {
            // Check for Close Action for certain MsgIds
            TypedEventHandler<InfoBar, object> ClickCloseAction = Entry.MsgId switch
                                                                  {
                                                                      0 => (_, _) =>
                                                                           {
                                                                               NotificationData?.AddIgnoredMsgIds(0);
                                                                               SaveLocalNotificationData();
                                                                           },
                                                                      _ => null
                                                                  };

            GameVersion? ValidForVerBelow = Entry.ValidForVerBelow;
            GameVersion? ValidForVerAbove = Entry.ValidForVerAbove;

            if (Entry.ValidForVerBelow == null && IsNotificationTimestampValid(Entry)
                || (LauncherUpdateHelper.LauncherCurrentVersion < ValidForVerBelow
                    && ValidForVerAbove < LauncherUpdateHelper.LauncherCurrentVersion)
                || LauncherUpdateHelper.LauncherCurrentVersion < ValidForVerBelow)
            {
                if (Entry.ActionProperty != null)
                {
                    Entry.OtherUIElement = Entry.ActionProperty.GetFrameworkElement();
                }

                SpawnNotificationPush(Entry.Title, Entry.Message, Entry.Severity, Entry.MsgId, Entry.IsClosable ?? true,
                                      Entry.IsDisposable ?? true, ClickCloseAction, (FrameworkElement)Entry.OtherUIElement, true, Entry.Show, Entry.IsForceShowNotificationPanel);
            }
            await Task.Delay(250);
        }
    }

    private async Task SpawnAppUpdatedNotification()
    {
        try
        {
            FileInfo updateNotifFile = new FileInfo(Path.Combine(AppDataFolder, "_NewVer"))
                                      .EnsureCreationOfDirectory()
                                      .EnsureNoReadOnly(out bool isUpdateNotifFileExist);
            FileInfo needInnoUpdateFile = new FileInfo(Path.Combine(AppDataFolder, "_NeedInnoLogUpdate"))
                                         .EnsureCreationOfDirectory()
                                         .EnsureNoReadOnly(out bool isNeedInnoUpdateFileExist);
            FileInfo innoLogFile = new FileInfo(Path.Combine(Path.GetDirectoryName(AppExecutableDir) ?? string.Empty, "unins000.dat"))
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
                    LogWriteLine($"Something wrong while opening the \"unins000.dat\" or deleting the \"_NeedInnoLogUpdate\" file\r\n{ex}", LogType.Error, true);
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
                SpawnNotificationPush(Lang._Misc.UpdateCompleteTitle,
                                      string.Format(Lang._Misc.UpdateCompleteSubtitle, version.ToString("n"), IsPreview ? "Preview" : "Stable"),
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

            DirectoryInfo fold = new DirectoryInfo(Path.Combine(AppExecutableDir, "_Temp"));
            if (fold.Exists)
            {
                foreach (FileInfo file in fold.EnumerateFiles().EnumerateNoReadOnly())
                {
                    if (!file.Name.StartsWith("ApplyUpdate"))
                    {
                        continue;
                    }

                    var target = new FileInfo(Path.Combine(AppExecutableDir, file.Name));
                    file.TryMoveTo(target);
                }

                fold.Delete(true);
            }

            try
            {
                // Remove update notif mark file to avoid it showing the same notification again.
                updateNotifFile.TryDeleteFile();

                // Get current game property, including game preset
                GamePresetProperty currentGameProperty = GetCurrentGameProperty();
                (_, string heroImage) = OOBESelectGame.GetLogoAndHeroImgPath(currentGameProperty.GamePreset);

                // Create notification
                NotificationContent toastContent = NotificationContent.Create()
                                                                      .SetTitle(Lang._NotificationToast
                                                                                   .LauncherUpdated_NotifTitle)
                                                                      .SetContent(
                                                                            string
                                                                               .Format(Lang._NotificationToast.LauncherUpdated_NotifSubtitle,
                                                                                    verString + (IsPreview
                                                                                        ? "-preview"
                                                                                        : ""),
                                                                                    Lang._SettingsPage
                                                                                       .PageTitle,
                                                                                    Lang._SettingsPage
                                                                                       .Update_SeeChangelog)
                                                                           )
                                                                      .AddAppHeroImagePath(heroImage);

                // Get notification service
                Windows.UI.Notifications.ToastNotification notificationService =
                    WindowUtility.CurrentToastNotificationService?.CreateToastNotification(toastContent);

                // Spawn notification service
                Windows.UI.Notifications.ToastNotifier notifier =
                    WindowUtility.CurrentToastNotificationService?.CreateToastNotifier();
                notifier?.Show(notificationService);
            }
            catch (Exception ex)
            {
                LogWriteLine($"[SpawnAppUpdatedNotification] Failed to spawn toast notification!\r\n{ex}",
                             LogType.Error, true);
                await SentryHelper.ExceptionHandlerAsync(ex);
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

    private void SpawnNotificationPush(string Title, string TextContent, NotifSeverity Severity, int MsgId = 0, bool IsClosable = true,
                                       bool Disposable = false, TypedEventHandler<InfoBar, object> CloseClickHandler = null, FrameworkElement OtherContent = null, bool IsAppNotif = true,
                                       bool? Show = false, bool ForceShowNotificationPanel = false)
    {
        if (!(Show ?? false)) return;
        if (NotificationData?.CurrentShowMsgIds.Contains(MsgId) ?? false) return;

        if (NotificationData?.IsMsgIdIgnored(MsgId) ?? false) return;

        NotificationData?.CurrentShowMsgIds.Add(MsgId);

        DispatcherQueue?.TryEnqueue(() =>
                                    {
                                        StackPanel OtherContentContainer = UIElementExtensions.CreateStackPanel().WithMargin(0d, -4d, 0d, 8d);

                                        InfoBar Notification = new InfoBar
                                                               {
                                                                   Title         = Title,
                                                                   Message       = TextContent,
                                                                   Severity      = NotifSeverity2InfoBarSeverity(Severity),
                                                                   IsClosable    = IsClosable,
                                                                   IsIconVisible = true,
                                                                   Shadow        = SharedShadow,
                                                                   IsOpen        = true
                                                               }
                                                              .WithMargin(4d, 4d, 4d, 0d).WithWidth(600)
                                                              .WithCornerRadius(8).WithHorizontalAlignment(HorizontalAlignment.Right);

                                        Notification.Translation += Shadow32;

                                        if (OtherContent != null)
                                            OtherContentContainer.AddElementToStackPanel(OtherContent);

                                        if (Disposable)
                                        {
                                            CheckBox NeverAskNotif = new CheckBox
                                            {
                                                Content = new TextBlock { Text = Lang._MainPage.NotifNeverAsk, FontWeight = FontWeights.Medium },
                                                Tag = $"{MsgId},{IsAppNotif}"
                                            };
                                            NeverAskNotif.Checked   += NeverAskNotif_Checked;
                                            NeverAskNotif.Unchecked += NeverAskNotif_Unchecked;
                                            OtherContentContainer.AddElementToStackPanel(NeverAskNotif);
                                        }

                                        if (Disposable || OtherContent != null)
                                            Notification.Content = OtherContentContainer;

                                        Notification.Tag              =  MsgId;
                                        Notification.CloseButtonClick += CloseClickHandler;

                                        SpawnNotificationoUI(MsgId, Notification);

                                        if (ForceShowNotificationPanel && !IsNotificationPanelShow)
                                        {
                                            _ = this.ForceShowNotificationPanel();
                                        }
                                    });
    }

    private void SpawnNotificationoUI(int tagID, InfoBar Notification)
    {
        Grid Container = UIElementExtensions.CreateGrid().WithTag(tagID);
        Notification.Loaded += (_, _) =>
                               {
                                   NoNotificationIndicator.Opacity = NotificationContainer.Children.Count > 0 ? 0f : 1f;
                                   NewNotificationCountBadge.Visibility = Visibility.Visible;
                                   NewNotificationCountBadge.Value++;

                                   NotificationPanelClearAllGrid.Visibility = NotificationContainer.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                               };

        Notification.Closed += (s, _) =>
                               {
                                   s.Translation -= Shadow32;
                                   s.SetHeight(0d);
                                   s.SetMargin(0d);
                                   int msg = (int)s.Tag;

                                   if (NotificationData?.CurrentShowMsgIds.Contains(msg) ?? false)
                                   {
                                       NotificationData?.CurrentShowMsgIds.Remove(msg);
                                   }
                                   NotificationContainer.Children.Remove(Container);
                                   NoNotificationIndicator.Opacity = NotificationContainer.Children.Count > 0 ? 0f : 1f;

                                   if (NewNotificationCountBadge.Value > 0)
                                   {
                                       NewNotificationCountBadge.Value--;
                                   }
                                   NoNotificationIndicator.Opacity = NotificationContainer.Children.Count > 0 ? 0f : 1f;
                                   NewNotificationCountBadge.Visibility = NewNotificationCountBadge.Value > 0 ? Visibility.Visible : Visibility.Collapsed;
                                   NotificationPanelClearAllGrid.Visibility = NotificationContainer.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                               };

        Container.AddElementToGridRowColumn(Notification);
        NotificationContainer.AddElementToStackPanel(Container);
    }

    private void RemoveNotificationUI(int tagID)
    {
        Grid notif = NotificationContainer.Children.OfType<Grid>().FirstOrDefault(x => (int)x.Tag == tagID);
        if (notif != null)
        {
            NotificationContainer.Children.Remove(notif);
            InfoBar notifBar = notif.Children.OfType<InfoBar>().FirstOrDefault();
            if (notifBar != null && notifBar.IsClosable)
                notifBar.IsOpen = false;
        }
    }

    private async void ClearAllNotification(object sender, RoutedEventArgs args)
    {
        Button button                        = sender as Button;
        if (button != null) button.IsEnabled = false;

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

            NotificationContainer.Children.RemoveAt(stackIndex);
            notifBar.IsOpen = false;
            await Task.Delay(100);
        }

        if (NotificationContainer.Children.Count == 0)
        {
            await Task.Delay(500);
            ToggleNotificationPanelBtn.IsChecked = false;
            IsNotificationPanelShow              = false;
            ShowHideNotificationPanel();
        }

        if (button != null) button.IsEnabled = true;
    }

    private static void NeverAskNotif_Checked(object sender, RoutedEventArgs e)
    {
        string[] Data = (sender as CheckBox)?.Tag.ToString()?.Split(',');
        if (Data == null)
        {
            return;
        }

        NotificationData?.AddIgnoredMsgIds(int.Parse(Data[0]), bool.Parse(Data[1]));
        SaveLocalNotificationData();
    }

    private static void NeverAskNotif_Unchecked(object sender, RoutedEventArgs e)
    {
        string[] Data = (sender as CheckBox)?.Tag.ToString()?.Split(',');
        if (Data == null)
        {
            return;
        }

        NotificationData?.RemoveIgnoredMsgIds(int.Parse(Data[0]), bool.Parse(Data[1]));
        SaveLocalNotificationData();
    }

    private async Task ForceShowNotificationPanel()
    {
        ToggleNotificationPanelBtn.IsChecked = true;
        IsNotificationPanelShow              = true;
        ShowHideNotificationPanel();
        await Task.Delay(250);
        double currentVOffset = NotificationContainer.ActualHeight;

        NotificationPanelScrollViewer.ScrollToVerticalOffset(currentVOffset);
    }
}
