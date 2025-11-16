using CollapseLauncher.Extension;
using CollapseLauncher.GameManagement.WpfPackage;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using System;
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable AsyncVoidMethod
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Pages;

public sealed partial class HomePage
{
    private WpfPackageContext? WpfContext
    {
        get => CurrentGameProperty.GameWpfContext;
    }

    private FlyoutBase? WpfFlyoutBase
    {
        get => field ??= WpfPackageBtnFlyout;
    }

    private void WpfPackageBtn_OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Grid asGrid ||
            WpfFlyoutBase == null ||
            WpfFlyoutBase.IsOpen)
        {
            return;
        }

        WpfFlyoutBase.ShowAt(asGrid);
    }

    private async void WpfPackageBtn_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (WpfContext == null)
        {
            return;
        }

        InputSystemCursor? cursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        WpfPackageStartUpdateBtn.SetCursor(cursor);
        WpfPackageCancelUpdateBtn.SetCursor(cursor);

        if (WpfContext.IsUpdateAvailable &&
            WpfContext.IsAutoUpdateEnabled &&
            !WpfContext.IsCheckAlreadyPerformed)
        {
            await WpfContext.StartUpdateCheckAsync();
        }
    }

    private async void WpfPackageStartUpdateBtn_OnClick(object sender, RoutedEventArgs e)
    {
        if (WpfContext == null)
        {
            return;
        }

        if (WpfContext.IsUpdateAvailable)
        {
            await WpfContext.StartUpdateCheckAsync();
        }
    }

    private void WpfPackageCancelUpdateBtn_OnClick(object sender, RoutedEventArgs e)
    {
        if (WpfContext == null)
        {
            return;
        }

        if (WpfContext.IsUpdateAvailable)
        {
            WpfContext.CancelRoutine();
        }
    }
}
