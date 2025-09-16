// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Hi3Helper.CommunityToolkit.WinUI.Controls;

public partial class SettingsCard
{
    /// <summary>
    /// The backing <see cref="DependencyProperty"/> for the <see cref="Header"/> property.
    /// </summary>
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header),
        typeof(object),
        typeof(SettingsCard),
        new PropertyMetadata(defaultValue: null, (d, e) => ((SettingsCard)d).OnHeaderPropertyChanged(e.OldValue, e.NewValue)));

    /// <summary>
    /// The backing <see cref="DependencyProperty"/> for the <see cref="Description"/> property.
    /// </summary>
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description),
        typeof(object),
        typeof(SettingsCard),
        new PropertyMetadata(defaultValue: null, (d, e) => ((SettingsCard)d).OnDescriptionPropertyChanged(e.OldValue, e.NewValue)));

    /// <summary>
    /// The backing <see cref="DependencyProperty"/> for the <see cref="HeaderIcon"/> property.
    /// </summary>
    public static readonly DependencyProperty HeaderIconProperty = DependencyProperty.Register(
        nameof(HeaderIcon),
        typeof(IconElement),
        typeof(SettingsCard),
        new PropertyMetadata(defaultValue: null, (d, e) => ((SettingsCard)d).OnHeaderIconPropertyChanged((IconElement)e.OldValue, (IconElement)e.NewValue)));

    /// <summary>
    /// The backing <see cref="DependencyProperty"/> for the <see cref="ActionIcon"/> property.
    /// </summary>
    public static readonly DependencyProperty ActionIconProperty = DependencyProperty.Register(
        nameof(ActionIcon),
        typeof(IconElement),
        typeof(SettingsCard),
        new PropertyMetadata(defaultValue: "\ue974"));

    /// <summary>
    /// The backing <see cref="DependencyProperty"/> for the <see cref="ActionIconToolTip"/> property.
    /// </summary>
    public static readonly DependencyProperty ActionIconToolTipProperty = DependencyProperty.Register(
        nameof(ActionIconToolTip),
        typeof(string),
        typeof(SettingsCard),
        new PropertyMetadata(defaultValue: null));

    /// <summary>
    /// The backing <see cref="DependencyProperty"/> for the <see cref="IsClickEnabled"/> property.
    /// </summary>
    public static readonly DependencyProperty IsClickEnabledProperty = DependencyProperty.Register(
        nameof(IsClickEnabled),
        typeof(bool),
        typeof(SettingsCard),
        new PropertyMetadata(defaultValue: false, (d, e) => ((SettingsCard)d).OnIsClickEnabledPropertyChanged((bool)e.OldValue, (bool)e.NewValue)));

    /// <summary>
    /// The backing <see cref="DependencyProperty"/> for the <see cref="ContentAlignment"/> property.
    /// </summary>
    public static readonly DependencyProperty ContentAlignmentProperty = DependencyProperty.Register(
        nameof(ContentAlignment),
        typeof(ContentAlignment),
        typeof(SettingsCard),
        new PropertyMetadata(defaultValue: ContentAlignment.Right));

    /// <summary>
    /// The backing <see cref="DependencyProperty"/> for the <see cref="IsActionIconVisible"/> property.
    /// </summary>
    public static readonly DependencyProperty IsActionIconVisibleProperty = DependencyProperty.Register(
        nameof(IsActionIconVisible),
        typeof(bool),
        typeof(SettingsCard),
        new PropertyMetadata(defaultValue: true, (d, e) => ((SettingsCard)d).OnIsActionIconVisiblePropertyChanged((bool)e.OldValue, (bool)e.NewValue)));

    /// <summary>
    /// Gets or sets the Header.
    /// </summary>
    public object Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
#pragma warning disable CS0109 // Member does not hide an inherited member; new keyword is not required
    public new object Description
#pragma warning restore CS0109 // Member does not hide an inherited member; new keyword is not required
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>
    /// Gets or sets the icon on the left.
    /// </summary>
    public IconElement HeaderIcon
    {
        get => (IconElement)GetValue(HeaderIconProperty);
        set => SetValue(HeaderIconProperty, value);
    }

    /// <summary>
    /// Gets or sets the icon that is shown when IsClickEnabled is set to true.
    /// </summary>
    public IconElement ActionIcon
    {
        get => (IconElement)GetValue(ActionIconProperty);
        set => SetValue(ActionIconProperty, value);
    }

    /// <summary>
    /// Gets or sets the tooltip of the ActionIcon.
    /// </summary>
    public string ActionIconToolTip
    {
        get => (string)GetValue(ActionIconToolTipProperty);
        set => SetValue(ActionIconToolTipProperty, value);
    }

    /// <summary>
    /// Gets or sets if the card can be clicked.
    /// </summary>
    public bool IsClickEnabled
    {
        get => (bool)GetValue(IsClickEnabledProperty);
        set => SetValue(IsClickEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the alignment of the Content
    /// </summary>
    public ContentAlignment ContentAlignment
    {
        get => (ContentAlignment)GetValue(ContentAlignmentProperty);
        set => SetValue(ContentAlignmentProperty, value);
    }

    /// <summary>
    /// Gets or sets if the ActionIcon is shown.
    /// </summary>
    public bool IsActionIconVisible
    {
        get => (bool)GetValue(IsActionIconVisibleProperty);
        set => SetValue(IsActionIconVisibleProperty, value);
    }

    /// <summary>
    /// Called when the IsClickEnabled property changes.
    /// </summary>
    protected virtual void OnIsClickEnabledPropertyChanged(bool oldValue, bool newValue)
    {
        OnIsClickEnabledChanged();
    }

    /// <summary>
    /// Called when the HeaderIcon property changes.
    /// </summary>
    protected virtual void OnHeaderIconPropertyChanged(IconElement oldValue, IconElement newValue)
    {
        OnHeaderIconChanged();
    }

    /// <summary>
    /// Called when the Header property changes.
    /// </summary>
    protected virtual void OnHeaderPropertyChanged(object oldValue, object newValue)
    {
        OnHeaderChanged();
    }

    /// <summary>
    /// Called when the Description property changes.
    /// </summary>
    protected virtual void OnDescriptionPropertyChanged(object oldValue, object newValue)
    {
        OnDescriptionChanged();
    }

    /// <summary>
    /// Called when the IsActionIconVisible property changes.
    /// </summary>
    protected virtual void OnIsActionIconVisiblePropertyChanged(bool oldValue, bool newValue)
    {
        OnActionIconChanged();
    }
}

/// <summary>
/// The alignment of Content.
/// </summary>
public enum ContentAlignment
{
    /// <summary>
    /// The Content is aligned to the right. Default state.
    /// </summary>
    Right,

    /// <summary>
    /// The Content is left-aligned while the Header, HeaderIcon and Description are collapsed. This is commonly used for Content types such as CheckBoxes, RadioButtons and custom layouts.
    /// </summary>
    Left,

    /// <summary>
    /// The Content is vertically aligned.
    /// </summary>
    Vertical
}