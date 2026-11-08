using Hi3Helper.Log;
using Hi3Helper.Win32.Native.Enums;
using Hi3Helper.Win32.Native.LibraryImport;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics;
using WinRT.Interop;
using NativeRect = Hi3Helper.Win32.Native.Structs.Rect;
using ShellTrayIcon = H.NotifyIcon.Core.TrayIcon;

#nullable enable

namespace CollapseLauncher;

internal sealed partial class TrayMenuHost(MenuFlyout menu, ShellTrayIcon icon, Action updateMenu) : IDisposable
{
    private Window? _window;
    private nint _handle;
    private TaskCompletionSource<XamlRoot>? _anchorReady;

    internal async ValueTask ShowAsync(Point point, ElementTheme theme, FlowDirection flowDirection)
    {
        if (menu.IsOpen || _anchorReady is not null) return;

        Window window = _window ?? CreateWindow();
        var anchor = (FrameworkElement)window.Content;
        anchor.RequestedTheme = theme;
        anchor.FlowDirection = flowDirection;

        NativeRect rect = GetAnchorRect(icon.Id, point);
        // Only the windowed flyout should occupy screen space.
        if (!PInvoke.SetWindowPos(_handle, 0, rect.X, rect.Y, 0, 0,
                                  SWP_FLAGS.SWP_NOZORDER | SWP_FLAGS.SWP_NOACTIVATE | SWP_FLAGS.SWP_NOSENDCHANGING))
            throw new InvalidOperationException("Could not position the tray menu anchor.");

        window.Activate();
        if (PInvoke.GetForegroundWindow() != _handle)
            PInvoke.SetForegroundWindow(_handle);

        XamlRoot? root = anchor.XamlRoot;
        if (root is null)
        {
            _anchorReady = new TaskCompletionSource<XamlRoot>(TaskCreationOptions.RunContinuationsAsynchronously);
            anchor.Loaded += OnAnchorLoaded;
            try
            {
                root = await _anchorReady.Task.WaitAsync(TimeSpan.FromSeconds(1));
            }
            finally
            {
                anchor.Loaded -= OnAnchorLoaded;
                _anchorReady = null;
            }
        }

        if (_window != window) return;
        double? scale = IsInWorkArea(rect) ? root.RasterizationScale : null;
        updateMenu();
        menu.ShowAt(anchor, new FlyoutShowOptions
        {
            Position = new Windows.Foundation.Point(0, 0),
            ExclusionRect = scale is { } factor
                ? new Windows.Foundation.Rect(0, 0, rect.Width / factor, rect.Height / factor)
                : null,
            ShowMode = FlyoutShowMode.Standard
        });
        if (!menu.IsOpen)
            throw new InvalidOperationException("The tray menu did not open.");
    }

    private void OnAnchorLoaded(object sender, RoutedEventArgs args)
    {
        if (((FrameworkElement)sender).XamlRoot is { } root)
            _anchorReady?.TrySetResult(root);
    }

    private Window CreateWindow()
    {
        var window = new Window();
        _window = window;
        _handle = WindowNative.GetWindowHandle(window);
        window.Content = new Grid();

        var presenter = (OverlappedPresenter)window.AppWindow.Presenter;
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsAlwaysOnTop = true;
        H.NotifyIcon.Core.WindowUtilities.HideWindowInTaskbar(_handle);

        menu.ShouldConstrainToRootBounds = false;
        menu.Closed += OnMenuClosed;
        window.Activated += OnWindowActivated;
        return window;
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated && menu.IsOpen && sender is Window window)
            window.DispatcherQueue.TryEnqueue(() =>
            {
                if (_window == window && PInvoke.GetForegroundWindow() != _handle)
                    menu.Hide();
            });
    }

    private void OnMenuClosed(object? sender, object args)
    {
        bool returnFocus = PInvoke.GetForegroundWindow() == _handle;
        _window?.AppWindow.Hide();
        if (!returnFocus || !icon.IsCreated) return;

        try
        {
            icon.SetFocus();
        }
        catch (InvalidOperationException ex)
        {
            Logger.WriteLine($"[TrayIcon] Could not return focus to the notification area. {ex.Message}", LogType.Warning, true);
        }
    }

    public void Dispose()
    {
        if (_window is not { } window) return;

        _window = null;
        _anchorReady?.TrySetCanceled();
        menu.Closed -= OnMenuClosed;
        window.Activated -= OnWindowActivated;
        menu.Hide();
        window.Close();
    }

    private static unsafe NativeRect GetAnchorRect(Guid id, Point point)
    {
        var identifier = new NotifyIconIdentifier(id);
        NativeRect rect;
        return Shell_NotifyIconGetRect(&identifier, &rect) == 0 && rect.Width > 0 && rect.Height > 0
            ? rect
            : NativeRect.FromXYWH(point.X, point.Y, 0, 0);
    }

    private static bool IsInWorkArea(NativeRect rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return false;

        // WinUI already flips a point anchored outside the work area back inside.
        var display = DisplayArea.GetFromPoint(new PointInt32(rect.X, rect.Y), DisplayAreaFallback.Nearest);
        RectInt32 workArea = display.WorkArea;
        RectInt32 bounds = display.OuterBounds;
        int x = rect.X - bounds.X - workArea.X;
        int y = rect.Y - bounds.Y - workArea.Y;
        return x >= 0 && x + rect.Width <= workArea.Width && y >= 0 && y + rect.Height <= workArea.Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NotifyIconIdentifier
    {
        private readonly uint _size;
        private readonly nint _windowHandle;
        private readonly uint _id;
        private readonly Guid _guid;

        internal unsafe NotifyIconIdentifier(Guid guid)
        {
            _size = (uint)sizeof(NotifyIconIdentifier);
            _guid = guid;
        }
    }

    [LibraryImport("shell32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial int Shell_NotifyIconGetRect(NotifyIconIdentifier* identifier, NativeRect* rect);
}
