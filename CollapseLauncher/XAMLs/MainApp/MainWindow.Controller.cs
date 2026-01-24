using Hi3Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using System;
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

    private bool _isWoken;
    private void MoveFocus(FocusNavigationDirection direction)
    {
        if (!_isWoken) EnsureInitialFocus();
        
        var root = Content as DependencyObject;

        if (root == null)
        {
            Logger.LogWriteLine("[MainWindow.Controller::MoveFocus] root is empty!");
            return;
        }
        
        FocusManager.TryMoveFocus(direction,
                                  new FindNextElementOptions
                                  {
                                      SearchRoot = root
                                  });
    }

    private void EnsureInitialFocus()
    {
        FocusManager.TryMoveFocus(FocusNavigationDirection.Next,
                                  new FindNextElementOptions
                                  {
                                      SearchRoot = Content
                                  });
        _isWoken = true;
    }
    private void LoseFocus()
    {
        var root = Content as DependencyObject;
        
        if (root == null)
        {
            Logger.LogWriteLine("[MainWindow.Controller::MoveFocus] root is empty!");
            return;
        }

        var obj = FocusManager.GetFocusedElement();
        if (obj == null)
        {
            Logger.LogWriteLine("[MainWindow.Controller::MoveFocus] obj is empty!");
            return;
        }

        Logger.LogWriteLine($"[MainWindow.Controller::LoseFocus] trying to exit from {obj.GetType().Name}");
        switch (obj)
        {
            case FlyoutBase flyout:
                flyout.Hide();
                break;
            
            
            
        }
    }

    private void InvokeFocusedElement()
    {
        if (InnerLauncherConfig.m_mainPage == null!)
        {
            return;
        }
        var focusedElement = FocusManager.GetFocusedElement(InnerLauncherConfig.m_mainPage.XamlRoot);
        switch (focusedElement)
        {
            case Button button:
                (new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke) as IInvokeProvider)?.Invoke();
                break;
            
            case ToggleButton toggle:
                toggle.IsChecked = !toggle.IsChecked;
                break;
            
            case Control control:
                control.Focus(FocusState.Programmatic);
                break;
            
            case NavigationViewItem nvi:
                nvi.
            
            default:
                KeyboardDown(KeyboardButtons.Enter);
                KeyboardUp(KeyboardButtons.Enter);
                break;
        }
    }
}