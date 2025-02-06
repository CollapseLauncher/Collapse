using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Interfaces;
using CollapseLauncher.Pages;
using CommunityToolkit.WinUI;
using Hi3Helper;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Linq;
using System.Threading.Tasks;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.Statics.GamePropertyVault;
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

public partial class MainPage : Page
{
    private void InitializeNavigationItems(bool ResetSelection = true)
    {
        DispatcherQueue.TryEnqueue(() =>
                                   {
                                       NavigationViewControl.IsSettingsVisible = true;
                                       NavigationViewControl.MenuItems.Clear();
                                       NavigationViewControl.FooterMenuItems.Clear();

                                       IGameVersion CurrentGameVersionCheck = GetCurrentGameProperty().GameVersion;

                                       FontIcon IconLauncher     = new FontIcon { Glyph = "" };
                                       FontIcon IconRepair       = new FontIcon { Glyph = "" };
                                       FontIcon IconCaches       = new FontIcon { Glyph = m_isWindows11 ? "" : "" };
                                       FontIcon IconGameSettings = new FontIcon { Glyph = "" };
                                       FontIcon IconAppSettings  = new FontIcon { Glyph = "" };

                                       if (m_appMode == AppMode.Hi3CacheUpdater)
                                       {
                                           if (CurrentGameVersionCheck.GamePreset.IsCacheUpdateEnabled ?? false)
                                           {
                                               NavigationViewControl.MenuItems.Add(new NavigationViewItem
                                                       { Icon = IconCaches, Tag = "caches" }
                                                  .BindNavigationViewItemText("_CachesPage", "PageTitle"));
                                           }
                                           return;
                                       }

                                       NavigationViewControl.MenuItems.Add(new NavigationViewItem
                                                                                   { Icon = IconLauncher, Tag = "launcher" }
                                                                              .BindNavigationViewItemText("_HomePage", "PageTitle"));

                                       NavigationViewControl.MenuItems.Add(new NavigationViewItemHeader()
                                                                              .BindNavigationViewItemText("_MainPage", "NavigationUtilities"));

                                       if (CurrentGameVersionCheck.GamePreset.IsRepairEnabled ?? false)
                                       {
                                           NavigationViewControl.MenuItems.Add(new NavigationViewItem
                                                                                       { Icon = IconRepair, Tag = "repair" }
                                                                                  .BindNavigationViewItemText("_GameRepairPage", "PageTitle"));
                                       }

                                       if (CurrentGameVersionCheck.GamePreset.IsCacheUpdateEnabled ?? false)
                                       {
                                           NavigationViewControl.MenuItems.Add(new NavigationViewItem
                                                                                       { Icon = IconCaches, Tag = "caches" }
                                                                                  .BindNavigationViewItemText("_CachesPage", "PageTitle"));
                                       }

                                       switch (CurrentGameVersionCheck.GameType)
                                       {
                                           case GameNameType.Honkai:
                                               NavigationViewControl.FooterMenuItems.Add(new NavigationViewItem
                                                       { Icon = IconGameSettings, Tag = "honkaigamesettings" }
                                                  .BindNavigationViewItemText("_GameSettingsPage", "PageTitle"));
                                               break;
                                           case GameNameType.StarRail:
                                               NavigationViewControl.FooterMenuItems.Add(new NavigationViewItem
                                                       { Icon = IconGameSettings, Tag = "starrailgamesettings" }
                                                  .BindNavigationViewItemText("_StarRailGameSettingsPage", "PageTitle"));
                                               break;
                                           case GameNameType.Genshin:
                                               NavigationViewControl.FooterMenuItems.Add(new NavigationViewItem
                                                       { Icon = IconGameSettings, Tag = "genshingamesettings" }
                                                  .BindNavigationViewItemText("_GenshinGameSettingsPage", "PageTitle"));
                                               break;
                                           case GameNameType.Zenless:
                                               NavigationViewControl.FooterMenuItems.Add(new NavigationViewItem
                                                       { Icon = IconGameSettings, Tag = "zenlessgamesettings" }
                                                  .BindNavigationViewItemText("_GameSettingsPage", "PageTitle"));
                                               break;
                                       }

                                       if (NavigationViewControl.SettingsItem is NavigationViewItem SettingsItem)
                                       {
                                           SettingsItem.Icon = IconAppSettings;
                                           _ = SettingsItem.BindNavigationViewItemText("_SettingsPage", "PageTitle");
                                       }

                                       foreach (var dependency in NavigationViewControl.FindDescendants().OfType<FrameworkElement>())
                                       {
                                           // Avoid any icons to have shadow attached if it's not from this page
                                           if (dependency.BaseUri.AbsolutePath != BaseUri.AbsolutePath)
                                           {
                                               continue;
                                           }

                                           switch (dependency)
                                           {
                                               case FontIcon icon:
                                                   AttachShadowNavigationPanelItem(icon);
                                                   break;
                                               case AnimatedIcon animIcon:
                                                   AttachShadowNavigationPanelItem(animIcon);
                                                   break;
                                           }
                                       }
                                       AttachShadowNavigationPanelItem(IconAppSettings);

                                       if (ResetSelection)
                                       {
                                           NavigationViewControl.SelectedItem = (NavigationViewItem)NavigationViewControl.MenuItems[0];
                                       }

                                       NavigationViewControl.ApplyNavigationViewItemLocaleTextBindings();

                                       InputSystemCursor handCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
                                       MainPageGrid.SetAllControlsCursorRecursive(handCursor);
                                   });
    }

    private static void AttachShadowNavigationPanelItem(FrameworkElement element)
    {
        bool             isAppLight       = IsAppThemeLight;
        Windows.UI.Color shadowColor      = isAppLight ? Colors.White : Colors.Black;
        double           shadowBlurRadius = isAppLight ? 20 : 15;
        double           shadowOpacity    = isAppLight ? 0.5 : 0.3;

        element.ApplyDropShadow(shadowColor, shadowBlurRadius, shadowOpacity);
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        foreach (NavigationViewItemBase item in NavigationViewControl.MenuItems)
        {
            if (item is not NavigationViewItem || item.Tag.ToString() != "launcher")
            {
                continue;
            }

            NavigationViewControl.SelectedItem = item;
            break;
        }

        NavViewPaneBackground.OpacityTransition = new ScalarTransition
        {
            Duration = TimeSpan.FromMilliseconds(150)
        };
        NavViewPaneBackground.TranslationTransition = new Vector3Transition
        {
            Duration = TimeSpan.FromMilliseconds(150)
        };

        var paneMainGrid = NavigationViewControl.FindDescendant("PaneContentGrid");
        if (paneMainGrid is Grid paneMainGridAsGrid)
        {
            paneMainGridAsGrid.PointerEntered += NavView_PanePointerEntered;
            paneMainGridAsGrid.PointerExited  += NavView_PanePointerExited;
        }

        // The toggle button is not a part of pane. Why Microsoft!!!
        var paneToggleButtonGrid = (Grid)NavigationViewControl.FindDescendant("PaneToggleButtonGrid");
        if (paneToggleButtonGrid != null)
        {
            paneToggleButtonGrid.PointerEntered += NavView_PanePointerEntered;
            paneToggleButtonGrid.PointerExited  += NavView_PanePointerExited;
        }

        // var backIcon = NavigationViewControl.FindDescendant("NavigationViewBackButton")?.FindDescendant<AnimatedIcon>();
        // backIcon?.ApplyDropShadow(Colors.Gray, 20);

        var toggleIcon = NavigationViewControl.FindDescendant("TogglePaneButton")?.FindDescendant<AnimatedIcon>();
        toggleIcon?.ApplyDropShadow(Colors.Gray, 20);
    }

    private void NavView_PanePointerEntered(object sender, PointerRoutedEventArgs e)
    {
        IsCursorInNavBarHoverArea         = true;
        NavViewPaneBackground.Opacity     = 1;
        NavViewPaneBackground.Translation = new System.Numerics.Vector3(0, 0, 32);
    }

    private bool IsCursorInNavBarHoverArea;

    private void NavView_PanePointerExited(object sender, PointerRoutedEventArgs e)
    {
        PointerPoint pointerPoint = e.GetCurrentPoint(NavViewPaneBackgroundHoverArea);
        IsCursorInNavBarHoverArea = pointerPoint.Position.X <= NavViewPaneBackgroundHoverArea.Width - 8 && pointerPoint.Position.X > 4;

        switch (IsCursorInNavBarHoverArea)
        {
            case false when !NavigationViewControl.IsPaneOpen:
                NavViewPaneBackground.Opacity     = 0;
                NavViewPaneBackground.Translation = new System.Numerics.Vector3(-48, 0, 0);
                break;
            case true when !NavigationViewControl.IsPaneOpen:
                NavViewPaneBackground.Opacity     = 1;
                NavViewPaneBackground.Translation = new System.Numerics.Vector3(0, 0, 32);
                break;
        }
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (!IsLoadFrameCompleted) return;
        if (args.IsSettingsInvoked && PreviousTag != "settings") Navigate(typeof(SettingsPage), "settings");

    #nullable enable
        NavigationViewItem? item = sender.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => (x.Content as TextBlock)?.Text == (args.InvokedItem as TextBlock)?.Text);
        item ??= sender.FooterMenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => (x.Content as TextBlock)?.Text == (args.InvokedItem as TextBlock)?.Text);
        if (item == null) return;
    #nullable restore

        string itemTag = (string)item.Tag;

        NavigateInnerSwitch(itemTag);
    }

    private void NavigateInnerSwitch(string itemTag)
    {
        if (itemTag == PreviousTag) return;
        switch (itemTag)
        {
            case "launcher":
                Navigate(typeof(HomePage), itemTag);
                break;

            case "repair":
                if (!(GetCurrentGameProperty().GameVersion.GamePreset.IsRepairEnabled ?? false))
                    Navigate(typeof(UnavailablePage), itemTag);
                else
                    Navigate(IsGameInstalled() ? typeof(RepairPage) : typeof(NotInstalledPage), itemTag);
                break;

            case "caches":
                if (GetCurrentGameProperty().GameVersion.GamePreset.IsCacheUpdateEnabled ?? false)
                    Navigate(IsGameInstalled() || (m_appMode == AppMode.Hi3CacheUpdater && GetCurrentGameProperty().GameVersion.GamePreset.GameType == GameNameType.Honkai) ? typeof(CachesPage) : typeof(NotInstalledPage), itemTag);
                else
                    Navigate(typeof(UnavailablePage), itemTag);
                break;

            case "honkaigamesettings":
                Navigate(IsGameInstalled() ? typeof(HonkaiGameSettingsPage) : typeof(NotInstalledPage), itemTag);
                break;

            case "starrailgamesettings":
                Navigate(IsGameInstalled() ? typeof(StarRailGameSettingsPage) : typeof(NotInstalledPage), itemTag);
                break;

            case "genshingamesettings":
                Navigate(IsGameInstalled() ? typeof(GenshinGameSettingsPage) : typeof(NotInstalledPage), itemTag);
                break;
                
            case "zenlessgamesettings":
                Navigate(IsGameInstalled() ? typeof(ZenlessGameSettingsPage) : typeof(NotInstalledPage), itemTag);
                break;
        }
    }

    private void Navigate(Type sourceType, string tagStr)
    {
        MainFrameChanger.ChangeMainFrame(sourceType, new DrillInNavigationTransitionInfo());
        PreviousTag = tagStr;
        PreviousTagString.Add(tagStr);
        LogWriteLine($"Page changed to {sourceType.Name} with Tag: {tagStr}", LogType.Scheme);
    }

    internal void InvokeMainPageNavigateByTag(string tagStr)
    {
        NavigationViewItem item = NavigationViewControl.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => x.Tag is string tag && tag == tagStr);
        if (item == null)
        {
            return;
        }

        NavigationViewControl.SelectedItem = item;
        string tag = (string)item.Tag;
        NavigateInnerSwitch(tag);
    }

    private void ToggleNotificationPanelBtnClick(object sender, RoutedEventArgs e)
    {
        IsNotificationPanelShow = ToggleNotificationPanelBtn.IsChecked ?? false;
        ShowHideNotificationPanel();
    }

    private void ShowHideNotificationPanel()
    {
        NewNotificationCountBadge.Value      = 0;
        NewNotificationCountBadge.Visibility = Visibility.Collapsed;
        Thickness lastMargin = NotificationPanel.Margin;
        lastMargin.Right         = IsNotificationPanelShow ? 0 : NotificationPanel.ActualWidth * -1;
        NotificationPanel.Margin = lastMargin;

        ShowHideNotificationLostFocusBackground(IsNotificationPanelShow);
    }

    private async void ShowHideNotificationLostFocusBackground(bool show)
    {
        if (show)
        {
            NotificationLostFocusBackground.Visibility                =  Visibility.Visible;
            NotificationLostFocusBackground.Opacity                   =  0.3;
            NotificationPanel.Translation                             += Shadow48;
            ToggleNotificationPanelBtn.Translation                    -= Shadow16;
            ((FontIcon)ToggleNotificationPanelBtn.Content).FontFamily =  FontCollections.FontAwesomeSolid;
        }
        else
        {
            NotificationLostFocusBackground.Opacity                   =  0;
            NotificationPanel.Translation                             -= Shadow48;
            ToggleNotificationPanelBtn.Translation                    += Shadow16;
            ((FontIcon)ToggleNotificationPanelBtn.Content).FontFamily =  FontCollections.FontAwesomeRegular;
            await Task.Delay(200);
            NotificationLostFocusBackground.Visibility = Visibility.Collapsed;
        }
    }

    private void NotificationContainerBackground_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        IsNotificationPanelShow              = false;
        ToggleNotificationPanelBtn.IsChecked = false;
        ShowHideNotificationPanel();
    }

    private void NavigationViewControl_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        if (!LauncherFrame.CanGoBack || !IsLoadFrameCompleted)
        {
            return;
        }

        LauncherFrame.GoBack();
        if (PreviousTagString.Count < 1) return;

        string lastPreviousTag          = PreviousTagString[^1];
        string currentNavigationItemTag = (string)((NavigationViewItem)sender.SelectedItem).Tag;

        if (!string.Equals(lastPreviousTag, currentNavigationItemTag, StringComparison.CurrentCultureIgnoreCase))
        {
            return;
        }

        string goLastPreviousTag = PreviousTagString[^2];

    #nullable enable
        NavigationViewItem? goPreviousNavigationItem = sender.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => goLastPreviousTag == (string)x.Tag);
        goPreviousNavigationItem ??= sender.FooterMenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => goLastPreviousTag == (string)x.Tag);
    #nullable restore

        if (goLastPreviousTag == "settings")
        {
            PreviousTag = goLastPreviousTag;
            PreviousTagString.RemoveAt(PreviousTagString.Count - 1);
            sender.SelectedItem = sender.SettingsItem;
            return;
        }

        if (goPreviousNavigationItem == null)
        {
            return;
        }

        PreviousTag = goLastPreviousTag;
        PreviousTagString.RemoveAt(PreviousTagString.Count - 1);
        sender.SelectedItem = goPreviousNavigationItem;
    }

    private void NavigationPanelOpening_Event(NavigationView sender, object args)
    {
        Thickness curMargin = GridBG_Icon.Margin;
        curMargin.Left       = 48;
        GridBG_Icon.Margin   = curMargin;
        IsTitleIconForceShow = true;
        ToggleTitleIcon(false);

        NavViewPaneBackgroundHoverArea.Width = NavigationViewControl.OpenPaneLength;
    }

    private async void NavigationPanelClosing_Event(NavigationView sender, NavigationViewPaneClosingEventArgs args)
    {
        Thickness curMargin = GridBG_Icon.Margin;
        curMargin.Left       = 58;
        GridBG_Icon.Margin   = curMargin;
        IsTitleIconForceShow = false;
        ToggleTitleIcon(true);

        NavViewPaneBackgroundHoverArea.Width = NavViewPaneBackground.Width;

        await Task.Delay(200);
        if (IsCursorInNavBarHoverArea)
        {
            return;
        }

        NavViewPaneBackground.Opacity     = 0;
        NavViewPaneBackground.Translation = new System.Numerics.Vector3(-48, 0, 0);
    }
}
