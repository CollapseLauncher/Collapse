using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Loading;
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.Statics.GamePropertyVault;
using static Hi3Helper.Logger;

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

#nullable enable
namespace CollapseLauncher;

file static class NavigationExtension
{
    public static void Add<TItem>(this IList<object> list, string localePropertyPath, string? iconGlyph = null, object? tag = null)
        where TItem : NavigationViewItemBase, new()
    {
        TItem item = new() { Tag = tag };
        item.BindNavigationViewItemText(Locale.Current, localePropertyPath);

        if (item is NavigationViewItem asItem && iconGlyph != null)
        {
            asItem.Icon = new FontIcon { Glyph = iconGlyph };
        }

        list.Add(item);
    }

    public static void Add<TItem, TPage>(this IList<object> list, string localePropertyPath, string? iconGlyph = null)
        where TItem : NavigationViewItemBase, new()
        where TPage : notnull
    {
        Type navigationType = typeof(TPage);
        list.Add<TItem>(localePropertyPath, iconGlyph, navigationType);
    }
}

public partial class MainPage : Page
{
    private void InitializeNavigationItems(bool ResetSelection = true)
    {
        DispatcherQueue.TryEnqueue(Impl);
        return;

        void Impl()
        {
            NavigationViewControl.IsSettingsVisible = true;
            NavigationViewControl.MenuItems.Clear();
            NavigationViewControl.FooterMenuItems.Clear();

            GamePresetProperty gameProperty            = GetCurrentGameProperty();
            IGameVersion?      CurrentGameVersionCheck = gameProperty.GameVersion;

            FontIcon iconAppSettings = new() { Glyph = "" };
            string   cachePageGlyph  = m_isWindows11 ? "" : "";

            if (m_appMode == AppMode.Hi3CacheUpdater)
            {
                if (CurrentGameVersionCheck?.GamePreset.IsCacheUpdateEnabled ?? false)
                {
                    NavigationViewControl.MenuItems.Add<NavigationViewItem, CachesPage>("Lang._CachesPage.PageTitle", cachePageGlyph);
                }
                return;
            }

            NavigationViewControl.MenuItems.Add<NavigationViewItem, HomePage>("Lang._HomePage.PageTitle", "");
            NavigationViewControl.MenuItems.Add<NavigationViewItemHeader>("Lang._MainPage.NavigationUtilities");

            if (CurrentGameVersionCheck?.GamePreset.IsRepairEnabled ?? false)
            {
                NavigationViewControl.MenuItems.Add<NavigationViewItem, RepairPage>("Lang._GameRepairPage.PageTitle", "");
            }

            if (CurrentGameVersionCheck?.GamePreset.IsCacheUpdateEnabled ?? false)
            {
                NavigationViewControl.MenuItems.Add<NavigationViewItem, CachesPage>("Lang._CachesPage.PageTitle", cachePageGlyph);
            }

            Type? gspPageType = CurrentGameVersionCheck?.GameType switch
            {
                GameNameType.Honkai   => typeof(HonkaiGameSettingsPage),
                GameNameType.StarRail => typeof(StarRailGameSettingsPage),
                GameNameType.Genshin  => typeof(GenshinGameSettingsPage),
                GameNameType.Zenless  => typeof(ZenlessGameSettingsPage),
                _                     => null
            };

            NavigationViewControl.FooterMenuItems.Add<NavigationViewItem>("Lang._GameSettingsPage.PageTitle", "", gspPageType);
            NavigationViewControl.FooterMenuItems.Add<NavigationViewItem>("Lang._FileCleanupPage.Title", "", "filescleanup");

            if (NavigationViewControl.SettingsItem is NavigationViewItem settingsItem)
            {
                settingsItem.Tag  = typeof(SettingsPage);
                settingsItem.Icon = iconAppSettings;
                _                 = settingsItem.BindNavigationViewItemText(Locale.Current, "Lang._SettingsPage.PageTitle");
            }

            foreach (FrameworkElement dependency in NavigationViewControl
                                                   .FindDescendants()
                                                   .OfType<FrameworkElement>())
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
            AttachShadowNavigationPanelItem(iconAppSettings);

            if (ResetSelection)
            {
                NavigationViewControl.SelectedItem = (NavigationViewItem)NavigationViewControl.MenuItems[0];
            }

            InputSystemCursor handCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
            MainPageGrid.SetAllControlsCursorRecursive(handCursor);
        }
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

        FrameworkElement? paneMainGrid = NavigationViewControl.FindDescendant("PaneContentGrid");
        if (paneMainGrid is Grid paneMainGridAsGrid)
        {
            paneMainGridAsGrid.PointerEntered += NavView_PanePointerEntered;
            paneMainGridAsGrid.PointerExited  += NavView_PanePointerExited;
        }

        // The toggle button is not a part of pane. Why Microsoft!!!
        Grid? paneToggleButtonGrid = NavigationViewControl.FindDescendant("PaneToggleButtonGrid") as Grid;
        if (paneToggleButtonGrid != null)
        {
            paneToggleButtonGrid.PointerEntered += NavView_PanePointerEntered;
            paneToggleButtonGrid.PointerExited  += NavView_PanePointerExited;
        }

        // var backIcon = NavigationViewControl.FindDescendant("NavigationViewBackButton")?.FindDescendant<AnimatedIcon>();
        // backIcon?.ApplyDropShadow(Colors.Gray, 20);

        AnimatedIcon? toggleIcon = NavigationViewControl.FindDescendant("TogglePaneButton")?.FindDescendant<AnimatedIcon>();
        toggleIcon?.ApplyDropShadow(Colors.Gray, 20);
    }

    private void NavView_PanePointerEntered(object sender, PointerRoutedEventArgs e)
    {
        IsCursorInNavBarHoverArea         = true;
        NavViewPaneBackground.Opacity     = 1;
        NavViewPaneBackground.Translation = new Vector3(0, 0, 32);
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
                NavViewPaneBackground.Translation = new Vector3(-48, 0, 0);
                break;
            case true when !NavigationViewControl.IsPaneOpen:
                NavViewPaneBackground.Opacity     = 1;
                NavViewPaneBackground.Translation = new Vector3(0, 0, 32);
                break;
        }
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        NavigationViewItemBase? navItem = args.InvokedItemContainer;
        if (args.IsSettingsInvoked)
        {
            TryGetNavItemFrom(typeof(SettingsPage), out navItem);
        }

        _ = TryNavigateFrom(navItem);
    }

    public bool TryGetCurrentPageObject(out object? typeOfPageObj)
    {
        typeOfPageObj = LauncherFrame.SourcePageType;
        return true;
    }

    public bool TryGetNavItemFrom(object? typeOfPageObj, [NotNullWhen(true)] out NavigationViewItemBase? navItem)
    {
        Unsafe.SkipInit(out navItem);

        switch (typeOfPageObj)
        {
            case string tagOfPageType:
                navItem = NavigationViewControl
                         .MenuItems
                         .OfType<NavigationViewItemBase>()
                         .FirstOrDefault(x => x.Tag is string asString && asString.Equals(tagOfPageType, StringComparison.OrdinalIgnoreCase));

                navItem ??= NavigationViewControl
                           .FooterMenuItems
                           .OfType<NavigationViewItemBase>()
                           .FirstOrDefault(x => x.Tag is string asString && asString.Equals(tagOfPageType, StringComparison.OrdinalIgnoreCase));

                navItem ??= NavigationViewControl.SettingsItem is NavigationViewItemBase { Tag: string asSettingsTagTypeString } settingsNavItemFromString && asSettingsTagTypeString == tagOfPageType ? settingsNavItemFromString : null;
                break;
            case Type typeOfPage:
                navItem = NavigationViewControl
                         .MenuItems
                         .OfType<NavigationViewItemBase>()
                         .FirstOrDefault(x => x.Tag is Type asType && asType == typeOfPage);

                navItem ??= NavigationViewControl
                           .FooterMenuItems
                           .OfType<NavigationViewItemBase>()
                           .FirstOrDefault(x => x.Tag is Type asType && asType == typeOfPage);

                navItem ??= NavigationViewControl.SettingsItem is NavigationViewItemBase { Tag: Type asSettingsPageType } settingsNavItem && asSettingsPageType == typeOfPage ? settingsNavItem : null;
                break;
        }

        return navItem != null;
    }

    public Task<bool> TryNavigateFrom(object? typeOfPageObj, NavigationTransitionInfo? transitionInfo = null, bool isForceLoad = false)
    {
        if (typeOfPageObj == null ||
            !TryGetNavItemFrom(typeOfPageObj, out NavigationViewItemBase? navItem))
        {
            return Task.FromResult(false);
        }

        return TryNavigateFrom(navItem, transitionInfo, isForceLoad);
    }

    public async Task<bool> TryNavigateFrom(NavigationViewItemBase? navigationItem, NavigationTransitionInfo? transitionInfo = null, bool isForceLoad = false)
    {
        if (navigationItem?.Tag is not { } pageInvokeObj)
        {
            return false;
        }

        if (!isForceLoad &&
            navigationItem.Tag is Type toInvokePageType &&
            TryGetCurrentPageObject(out object? typeOfPageObj) &&
            typeOfPageObj is Type currentPageType &&
            currentPageType == toInvokePageType)
        {
            return false;
        }

        switch (pageInvokeObj)
        {
            case Type pageType:
            {
                if (pageType.Name.EndsWith("GameSettingsPage") &&
                    !IsGameInstalled())
                {
                    pageType = typeof(NotInstalledPage);
                }

                LauncherFrame.Navigate(pageType, null, transitionInfo ?? new DrillInNavigationTransitionInfo());
                
                if (isForceLoad)
                {
                    LauncherFrame.BackStack.Clear();
                    LauncherFrame.CacheSize = 0;
                }
                break;
            }
            case "filescleanup":
                LoadingMessageHelper.ShowLoadingFrame();
                // Initialize and get game state, then get the latest package info
                LoadingMessageHelper.SetMessage(Locale.Current.Lang?._FileCleanupPage?.LoadingTitle,
                                                Locale.Current.Lang?._FileCleanupPage?.LoadingSubtitle2);

                try
                {
                    if (CurrentGameProperty?.GameInstall != null)
                        await CurrentGameProperty.GameInstall.CleanUpGameFiles();
                }
                catch (Exception ex)
                {
                    LoadingMessageHelper.HideLoadingFrame();
                    LogWriteLine($"[NavigateInnerSwitch(filescleanup] Error while calling the CleanUpGameFiles method!\r\n{ex}", LogType.Error, true);

                    ErrorSender.SendException(ex);
                }

                break;
            default:
                throw new InvalidOperationException("Type of navigation tag is not supported!");
        }

        NavigationViewControl.SelectedItem = navigationItem;
        return true;
    }

    private void ToggleNotificationPanelBtnClick(object? sender, RoutedEventArgs? e)
    {
        _isNotificationPanelShow = ToggleNotificationPanelBtn.IsChecked ?? false;
        ShowHideNotificationPanel();
    }

    private void ShowHideNotificationPanel()
    {
        Thickness lastMargin = NotificationPanel.Margin;
        lastMargin.Right         = _isNotificationPanelShow ? 0 : NotificationPanel.ActualWidth * -1;
        NotificationPanel.Margin = lastMargin;

        ShowHideNotificationLostFocusBackground(_isNotificationPanelShow);
    }

    private async void ShowHideNotificationLostFocusBackground(bool show)
    {
        if (show)
        {
            NotificationLostFocusBackground.Visibility                = Visibility.Visible;
            NotificationLostFocusBackground.Opacity                   = 0.3;
            NotificationPanel.Translation                             = new Vector3(0, 0, 48);
            ToggleNotificationPanelBtn.Translation                    = new Vector3();
            ((FontIcon)ToggleNotificationPanelBtn.Content).FontFamily = FontCollections.FontAwesomeSolid;
        }
        else
        {
            NotificationLostFocusBackground.Opacity                   = 0;
            NotificationPanel.Translation                             = new Vector3();
            ToggleNotificationPanelBtn.Translation                    = new Vector3(0, 0, 16);
            ((FontIcon)ToggleNotificationPanelBtn.Content).FontFamily = FontCollections.FontAwesomeRegular;
            await Task.Delay(200);
            NotificationLostFocusBackground.Visibility = Visibility.Collapsed;
        }
    }

    private void NotificationContainerBackground_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isNotificationPanelShow             = false;
        ToggleNotificationPanelBtn.IsChecked = false;
        ShowHideNotificationPanel();
    }

    private void NavigationViewControl_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        if (!LauncherFrame.CanGoBack)
        {
            return;
        }

        LauncherFrame.GoBack();
        if (!TryGetNavItemFrom(LauncherFrame.SourcePageType, out NavigationViewItemBase? navItem))
            return;

        sender.SelectedItem = navItem;
    }

    private void NavigationPanelOpening_Event(NavigationView sender, object args)
    {
        Thickness curMargin = GridBGIcon.Margin;
        curMargin.Left       = 48;
        GridBGIcon.Margin   = curMargin;
        _isTitleIconForceShow = true;
        ToggleTitleIcon(false);

        NavViewPaneBackgroundHoverArea.Width = NavigationViewControl.OpenPaneLength;
    }

    private async void NavigationPanelClosing_Event(NavigationView sender, NavigationViewPaneClosingEventArgs args)
    {
        Thickness curMargin = GridBGIcon.Margin;
        curMargin.Left       = 58;
        GridBGIcon.Margin   = curMargin;
        _isTitleIconForceShow = false;
        ToggleTitleIcon(true);

        NavViewPaneBackgroundHoverArea.Width = NavViewPaneBackground.Width;

        await Task.Delay(200);
        if (IsCursorInNavBarHoverArea)
        {
            return;
        }

        NavViewPaneBackground.Opacity     = 0;
        NavViewPaneBackground.Translation = new Vector3(-48, 0, 0);
    }
}
