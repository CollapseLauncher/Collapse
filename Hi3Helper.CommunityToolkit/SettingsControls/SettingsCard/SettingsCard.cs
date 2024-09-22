// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// ReSharper disable RedundantExtendsListEntry
// ReSharper disable PartialTypeWithSinglePart
#nullable enable
namespace Hi3Helper.CommunityToolkit.WinUI.Controls;

/// <summary>
/// This is the base control to create consistent settings experiences, inline with the Windows 11 design language.
/// A SettingsCard can also be hosted within a SettingsExpander.
/// </summary>

[TemplatePart(Name = ActionIconPresenterHolder, Type = typeof(Viewbox))]
[TemplatePart(Name = HeaderPresenter, Type = typeof(ContentPresenter))]
[TemplatePart(Name = DescriptionPresenter, Type = typeof(ContentPresenter))]
[TemplatePart(Name = HeaderIconPresenterHolder, Type = typeof(Viewbox))]

[TemplateVisualState(Name = NormalState, GroupName = CommonStates)]
[TemplateVisualState(Name = PointerOverState, GroupName = CommonStates)]
[TemplateVisualState(Name = PressedState, GroupName = CommonStates)]
[TemplateVisualState(Name = DisabledState, GroupName = CommonStates)]

[TemplateVisualState(Name = RightState, GroupName = ContentAlignmentStates)]
[TemplateVisualState(Name = RightWrappedState, GroupName = ContentAlignmentStates)]
[TemplateVisualState(Name = RightWrappedNoIconState, GroupName = ContentAlignmentStates)]
[TemplateVisualState(Name = LeftState, GroupName = ContentAlignmentStates)]
[TemplateVisualState(Name = VerticalState, GroupName = ContentAlignmentStates)]

[TemplateVisualState(Name = NoContentSpacingState, GroupName = ContentSpacingStates)]
[TemplateVisualState(Name = ContentSpacingState, GroupName = ContentSpacingStates)]

public partial class SettingsCard : ButtonBase
{
    internal const string CommonStates = "CommonStates";
    internal const string NormalState = "Normal";
    internal const string PointerOverState = "PointerOver";
    internal const string PressedState = "Pressed";
    internal const string DisabledState = "Disabled";

    internal const string ContentAlignmentStates = "ContentAlignmentStates";
    internal const string RightState = "Right";
    internal const string RightWrappedState = "RightWrapped";
    internal const string RightWrappedNoIconState = "RightWrappedNoIcon";
    internal const string LeftState = "Left";
    internal const string VerticalState = "Vertical";

    internal const string ContentSpacingStates = "ContentSpacingStates";
    internal const string NoContentSpacingState = "NoContentSpacing";
    internal const string ContentSpacingState = "ContentSpacing";

    internal const string ActionIconPresenterHolder = "PART_ActionIconPresenterHolder";
    internal const string HeaderPresenter = "PART_HeaderPresenter";
    internal const string DescriptionPresenter = "PART_DescriptionPresenter";
    internal const string HeaderIconPresenterHolder = "PART_HeaderIconPresenterHolder";


    /// <summary>
    /// Creates a new instance of the <see cref="SettingsCard"/> class.
    /// </summary>
    public SettingsCard()
    {
        DefaultStyleKey = typeof(SettingsCard);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        IsEnabledChanged -= OnIsEnabledChanged;
        OnActionIconChanged();
        OnHeaderChanged();
        OnHeaderIconChanged();
        OnDescriptionChanged();
        OnIsClickEnabledChanged();
        CheckInitialVisualState();
        SetAccessibleContentName();
        RegisterPropertyChangedCallback(ContentProperty, OnContentChanged);
        IsEnabledChanged += OnIsEnabledChanged;     
    }

    private void CheckInitialVisualState()
    {
        VisualStateManager.GoToState(this, IsEnabled ? NormalState : DisabledState, true);

        if (GetTemplateChild("ContentAlignmentStates") is VisualStateGroup contentAlignmentStatesGroup)
        {
            contentAlignmentStatesGroup.CurrentStateChanged -= ContentAlignmentStates_Changed;
            CheckVerticalSpacingState(contentAlignmentStatesGroup.CurrentState);
            contentAlignmentStatesGroup.CurrentStateChanged += ContentAlignmentStates_Changed;
        }
    }

    // We automatically set the AutomationProperties.Name of the Content if not configured.
    private void SetAccessibleContentName()
    {
        if (Header is string headerString && headerString != string.Empty)
        {
            // We don't want to override an AutomationProperties.Name that is manually set, or if the Content basetype is of type ButtonBase (the ButtonBase.Content will be used then)
            if (Content is UIElement element && string.IsNullOrEmpty(AutomationProperties.GetName(element)) && element.GetType().BaseType != typeof(ButtonBase) && element.GetType() != typeof(TextBlock))
            {
                AutomationProperties.SetName(element, headerString);
            }
        }
    }

    private void EnableButtonInteraction()
    {
        DisableButtonInteraction();

        IsTabStop = true;
        PointerEntered += Control_PointerEntered;
        PointerExited += Control_PointerExited;
        PointerCaptureLost += Control_PointerCaptureLost;
        PointerCanceled += Control_PointerCanceled;
        PreviewKeyDown += Control_PreviewKeyDown;
        PreviewKeyUp += Control_PreviewKeyUp;
    }

    private void DisableButtonInteraction()
    {
        IsTabStop = false;
        PointerEntered -= Control_PointerEntered;
        PointerExited -= Control_PointerExited;
        PointerCaptureLost -= Control_PointerCaptureLost;
        PointerCanceled -= Control_PointerCanceled;
        PreviewKeyDown -= Control_PreviewKeyDown;
        PreviewKeyUp -= Control_PreviewKeyUp;
    }

    private void Control_PreviewKeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.Space || e.Key == VirtualKey.GamepadA)
        {
            VisualStateManager.GoToState(this, NormalState, true);
        }
    }

    private void Control_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.Space || e.Key == VirtualKey.GamepadA)
        {
            // Check if the active focus is on the card itself - only then we show the pressed state.
            if (GetFocusedElement() is SettingsCard)
            {
                VisualStateManager.GoToState(this, PressedState, true);
            }
        }
    }

    public void Control_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);
        VisualStateManager.GoToState(this, PointerOverState, true);
    }
    
    public void Control_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        base.OnPointerExited(e);
        VisualStateManager.GoToState(this, NormalState, true);
    }
    
    private void Control_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        VisualStateManager.GoToState(this, NormalState, true);
    }

    private void Control_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        base.OnPointerCanceled(e);
        VisualStateManager.GoToState(this, NormalState, true);
    }

    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        //  e.Handled = true;
        if (IsClickEnabled)
        {
            base.OnPointerPressed(e);
            VisualStateManager.GoToState(this, PressedState, true);
        }
    }
    
    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        if (IsClickEnabled)
        {
            base.OnPointerReleased(e);
            VisualStateManager.GoToState(this, NormalState, true);
        }
    }

    /// <summary>
    /// Creates AutomationPeer
    /// </summary>
    /// <returns>An automation peer for <see cref="SettingsCard"/>.</returns>
    protected override AutomationPeer OnCreateAutomationPeer() => new SettingsCardAutomationPeer(this);

    private void OnIsClickEnabledChanged()
    {
        OnActionIconChanged();
        if (IsClickEnabled)
        {
            EnableButtonInteraction();
        }
        else
        {
            DisableButtonInteraction();
        }
    }

    private void OnIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        VisualStateManager.GoToState(this, IsEnabled ? NormalState : DisabledState, true);
    }

    private void OnActionIconChanged()
    {
        if (GetTemplateChild(ActionIconPresenterHolder) is FrameworkElement actionIconPresenter)
        {
            if (IsClickEnabled && IsActionIconVisible)
            {
                actionIconPresenter.Visibility = Visibility.Visible;
            }
            else
            {
                actionIconPresenter.Visibility =Visibility.Collapsed;
            }
        }
    }

    private void OnHeaderIconChanged()
    {
        if (GetTemplateChild(HeaderIconPresenterHolder) is FrameworkElement headerIconPresenter)
        {
            headerIconPresenter.Visibility = HeaderIcon != null
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void OnDescriptionChanged()
    {
        if (GetTemplateChild(DescriptionPresenter) is FrameworkElement descriptionPresenter)
        {
            descriptionPresenter.Visibility = IsNullOrEmptyString(Description)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

    }

    private void OnHeaderChanged()
    {
        if (GetTemplateChild(HeaderPresenter) is FrameworkElement headerPresenter)
        {
            headerPresenter.Visibility = IsNullOrEmptyString(Header)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
       
    }

    private void ContentAlignmentStates_Changed(object sender, VisualStateChangedEventArgs e) => CheckVerticalSpacingState(e.NewState);

    private void CheckVerticalSpacingState(VisualState s)
    {
        // On state change, checking if the Content should be wrapped (e.g. when the card is made smaller or the ContentAlignment is set to Vertical). If the Content and the Header or Description are not null, we add spacing between the Content and the Header/Description.

        if (s != null && s.Name is RightWrappedState or RightWrappedNoIconState or VerticalState && Content != null && (!IsNullOrEmptyString(Header) || !IsNullOrEmptyString(Description)))
        {
            VisualStateManager.GoToState(this, ContentSpacingState, true);
        }
        else
        {
            VisualStateManager.GoToState(this, NoContentSpacingState, true);
        }
    }

    private FrameworkElement? GetFocusedElement()
    {
        if (ControlHelpers.IsXamlRootAvailable && XamlRoot != null)
        {
            return FocusManager.GetFocusedElement(XamlRoot) as FrameworkElement;
        }

        return FocusManager.GetFocusedElement() as FrameworkElement;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNullOrEmptyString(object obj) => obj is string objString && string.IsNullOrEmpty(objString);
}
