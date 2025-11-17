using CollapseLauncher.Extension;
using CollapseLauncher.GameManagement.WpfPackage;
using CollapseLauncher.Helper.Animation;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Button = Microsoft.UI.Xaml.Controls.Button;
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

    private void WpfPackageBtn_OnClick(object sender, RoutedEventArgs e)
    {
        if ((WpfFlyoutBase?.IsOpen ?? true) ||
            sender is not Button button)
        {
            return;
        }

        FlyoutBase.ShowAttachedFlyout(button);
    }

    private void WpfPackageBtnToolTip_OnOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ToolTip { Tag: Button { IsPointerOver: true } button } ||
            (WpfFlyoutBase?.IsOpen ?? true))
        {
            return;
        }

        FlyoutBase.ShowAttachedFlyout(button);
    }

    private void WpfPackageBtnFlyoutGrid_OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe)
        {
            return;
        }

        bool isOutFlyoutArea = IsOutsideWpfArea(fe,            e, new Thickness(0, 0,  0, 16));
        bool isOutButtonArea = IsOutsideWpfArea(WpfPackageBtn, e, new Thickness(0, 16, 0, 0));

        if ((!isOutFlyoutArea &&
             !isOutButtonArea) ||
            WpfPackageBtn.IsPointerOver)
        {
            return;
        }

        WpfFlyoutBase?.Hide();
    }

    private void WpfPackageBtn_OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Button btn ||
            btn.IsPointerOver)
        {
            return;
        }

        bool isOutButtonArea = IsOutsideWpfArea(btn,           e, new Thickness(0, 16, 0, 0));
        bool isOutFlyoutArea = IsOutsideWpfArea(WpfPackageBtn, e, new Thickness(0, 0,  0, 16));

        if ((!isOutFlyoutArea &&
            !isOutButtonArea) ||
            btn.IsPointerOver)
        {
            return;
        }

        WpfFlyoutBase?.Hide();
    }

    private static bool IsOutsideWpfArea(FrameworkElement element, PointerRoutedEventArgs e, Thickness margin = default)
    {
        Point pos = e.GetCurrentPoint(element).Position;
        bool isOutsideButtonArea = pos.Y < -margin.Top ||
                                   pos.Y > element.ActualHeight + margin.Bottom ||
                                   pos.X < -margin.Left ||
                                   pos.X > element.ActualWidth + margin.Right;

        return isOutsideButtonArea;
    }

    private void WpfPackageCheckBoxText_OnPressed(object sender, PointerRoutedEventArgs e)
    {
        bool checkedFirst = WpfPackageAutoCheckBox.IsChecked ?? false;
        WpfPackageAutoCheckBox.IsChecked = !checkedFirst;
    }

    private async void WpfPackageBtn_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (WpfContext == null)
        {
            return;
        }

        InputSystemCursor? cursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        WpfPackageBtn.SetCursor(cursor);
        WpfPackageStartUpdateBtn.SetCursor(cursor);
        WpfPackageCancelUpdateBtn.SetCursor(cursor);
        WpfPackageCancelUpdateBtn.SetCursor(cursor);
        WpfPackageAutoUpdateGrid.SetCursor(cursor);
        WpfPackageBtnFlyoutGrid.EnableImplicitAnimation(true);

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
