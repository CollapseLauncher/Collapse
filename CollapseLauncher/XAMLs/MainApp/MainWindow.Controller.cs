using Hi3Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using static Hi3Helper.Win32.ManagedTools.Controller;
using static Hi3Helper.Win32.ManagedTools.Keyboard;

namespace CollapseLauncher;

public sealed partial class MainWindow
{
    private DispatcherTimer _controllerTimer;
    private XInputButtons _previousButtonState;
    private static readonly XInputButtons[] _buttonValues =
        Enum.GetValues<XInputButtons>();


    private void StartControllerLoop()
    {
        _controllerTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };

        _controllerTimer.Tick += (_, _) => ControllerPoller();
        _controllerTimer.Start();
    }
    
    private void StopControllerLoop() => _controllerTimer.Stop();

    private void ControllerPoller()
    {
        var btn = GetButtonState(ILoggerHelper.GetILogger("MainWindow::Controller"));
        if (!btn.HasValue) return;

        var cur = btn.Value;

        var pressed  = cur & ~_previousButtonState;
        var released = _previousButtonState & ~cur;


        HandleButtons(pressed, true);
        HandleButtons(released, false);

        _previousButtonState = cur;
    }

    private void HandleButtons(XInputButtons input, bool pressed)
    {
        if (!Helper.WindowUtility.IsCurrentWindowInFocus()) return; // Do not process input when window is not in focus.

        foreach (var buttons in _buttonValues)
        {
            if ((buttons & input) == 0)
                continue;
            
            Logger.LogWriteLine($"Button {input} is {(pressed ? "pressed" : "released")}");
            
            if (!pressed) return;
            switch (buttons)
            {
                case XInputButtons.DPadUp:
                    MoveFocus(FocusNavigationDirection.Up);
                    break;
                case XInputButtons.DPadDown:
                    MoveFocus(FocusNavigationDirection.Down);
                    break;
                case XInputButtons.DPadLeft:
                    MoveFocus(FocusNavigationDirection.Left);
                    break;
                case XInputButtons.DPadRight:
                    MoveFocus(FocusNavigationDirection.Right);
                    break;
                case XInputButtons.A:
                    InvokeFocusedElement();
                    break;
                case XInputButtons.B:
                    LoseFocus();
                    break;
                case XInputButtons.X:
                    MoveFocus(FocusNavigationDirection.Next);
                    break;
                case XInputButtons.Y:
                    MoveFocus(FocusNavigationDirection.Previous);
                    break;
                // TODO: More buttons
                    
            }
        }
    }
    
    private static void SendKey(KeyboardButtons vk, bool down)
    {
        if (down)
            KeyboardDown(vk);
        else
            KeyboardUp(vk);
    }

    private static bool IsBadFocusTarget(object obj) =>
        obj is ScrollViewer
        or ScrollContentPresenter
        or ItemsPresenter
        or Panel
        or Border;

    private void MoveFocus(FocusNavigationDirection direction)
    {
        Task.Run(EnsureInitialFocus);
        
        var root = Content as DependencyObject;
        if (root == null) return;

        for (int i = 0; i < 50; i++) // yes, 50 retries sometime is needed for getting out of stuck focus...
        {
            FocusManager.TryMoveFocus(direction,
                new FindNextElementOptions { SearchRoot = root });

            var focused = FocusManager.GetFocusedElement(InnerLauncherConfig.m_mainPage.XamlRoot);

            if (focused == null || !IsBadFocusTarget(focused))
                return;
        }
    }

    private async Task EnsureInitialFocus()
    {
        var focused = FocusManager.GetFocusedElement(InnerLauncherConfig.m_mainPage.XamlRoot);
        if (focused != null)
            return;

        var root = Content as DependencyObject;
        if (root == null)
            return;

        await FocusManager.TryFocusAsync(
            root,
            FocusState.Programmatic);

        return;
    }

    private void LoseFocus()
    {
        var root = Content as DependencyObject;
        
        if (root == null)
        {
            Logger.LogWriteLine("[MainWindow.Controller::LoseFocus] root is empty!");
            return;
        }

        var obj = FocusManager.GetFocusedElement(InnerLauncherConfig.m_mainPage.XamlRoot) as DependencyObject;
        if (obj == null)
        {
            Logger.LogWriteLine("[MainWindow.Controller::LoseFocus] obj is empty!");
            return;
        }

        CollapseFirstClosable(obj);
    }

    private void CollapseFirstClosable(DependencyObject start)
    {
        var current = start;

        while (current != null)
        {
            Logger.LogWriteLine(
                $"[Controller::LoseFocus] checking {current.GetType().Name}");

            switch (current)
            {
                case FlyoutBase flyout:
                    flyout.Hide();
                    return;

                case FlyoutPresenter presenter:
                    var flyoutInner = presenter.Parent;
                    if (flyoutInner is Popup)
                    {
                        (flyoutInner as Popup).IsOpen = false;
                    }
                    return;

                case Popup popup:
                    popup.IsOpen = false;
                    return;

                case ContentDialog dialog:
                    dialog.Hide();
                    return;

                case ComboBoxItem cbi:
                    _comboBoxTracker?.IsDropDownOpen = false;
                    _comboBoxTracker = null;
                    return;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        Logger.LogWriteLine(
            "[Controller::LoseFocus] no collapsable parent found");
    }

    private ComboBox? _comboBoxTracker;
    private void InvokeFocusedElement()
    {
        if (InnerLauncherConfig.m_mainPage == null!)
        {
            return;
        }
        var focusedElement = FocusManager.GetFocusedElement(InnerLauncherConfig.m_mainPage.XamlRoot);
        Logger.LogWriteLine($"[MainWindow.Controller::InvokeFocusedElement] Got {focusedElement.GetType().Name} element!");
        switch (focusedElement)
        {
            case Button button:
                (new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke) as IInvokeProvider)?.Invoke();
                break;
            
            case ToggleButton toggle:
                toggle.IsChecked = !toggle.IsChecked;
                break;

            case ComboBox comboBox:
                comboBox.IsDropDownOpen = true;
                _comboBoxTracker = comboBox;
                break;

            case ComboBoxItem comboBoxItem:
                _comboBoxTracker.SelectedItem = comboBoxItem.DataContext;
                _comboBoxTracker.IsDropDownOpen = false;
                _comboBoxTracker = null;
                break;

            case NavigationViewItem nvi:
                var nviTag = nvi.Tag.ToString();
                if (!string.IsNullOrEmpty(nviTag))
                {
                    InnerLauncherConfig.m_mainPage.NavigationViewControl.SelectedItem = nvi;
                    InnerLauncherConfig.m_mainPage.NavigateInnerSwitch(nviTag);
                }
                break;
            
            default:
                InvokeParents(focusedElement as DependencyObject);
                break;
        }
    }

    private void InvokeParents(DependencyObject start)
    {
        var current = start;

        while (current != null)
        {
            Logger.LogWriteLine($"[MainWindow.Controller::InvokeFocusedElement] Checking parent, got {current.GetType().Name}");



            current = VisualTreeHelper.GetParent(current);
        }
    }
}