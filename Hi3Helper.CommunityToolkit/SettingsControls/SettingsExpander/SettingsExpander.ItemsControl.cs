// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Hi3Helper.CommunityToolkit.WinUI.Controls;

//// Implement properties for ItemsControl like behavior.
public partial class SettingsExpander
{
    /// <summary>
    /// Gets or sets the collection of items to display.
    /// </summary>
    public IList<object> Items
    {
        get { return (IList<object>)GetValue(ItemsProperty); }
        set { SetValue(ItemsProperty, value); }
    }

    /// <summary>
    /// Identifies the <see cref="Items"/> DependencyProperty.
    /// </summary>
    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(nameof(Items), typeof(IList<object>), typeof(SettingsExpander), new PropertyMetadata(null, OnItemsConnectedPropertyChanged));

    /// <summary>
    /// Gets or sets the value to use for the inner <see cref="ItemsRepeater.ItemsSource"/>.
    /// </summary>
    public object ItemsSource
    {
        get { return GetValue(ItemsSourceProperty); }
        set { SetValue(ItemsSourceProperty, value); }
    }

    /// <summary>
    /// Identifies the <see cref="ItemsSource"/> DependencyProperty.
    /// </summary>
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(object), typeof(SettingsExpander), new PropertyMetadata(null, OnItemsConnectedPropertyChanged));

    /// <summary>
    /// Gets or sets the value to use for the inner <see cref="ItemsRepeater.ItemTemplate"/>.
    /// </summary>
    public object ItemTemplate
    {
        get { return GetValue(ItemTemplateProperty); }
        set { SetValue(ItemTemplateProperty, value); }
    }

    /// <summary>
    /// Identifies the <see cref="ItemTemplate"/> DependencyProperty.
    /// </summary>
    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(nameof(ItemTemplate), typeof(object), typeof(SettingsExpander), new PropertyMetadata(null));

    /// <summary>
    /// Gets or sets the value to use for the ItemContainerStyle applied to the inner <see cref="ItemsRepeater"/>.
    /// </summary>
    public StyleSelector ItemContainerStyleSelector
    {
        get { return (StyleSelector)GetValue(ItemContainerStyleSelectorProperty); }
        set { SetValue(ItemContainerStyleSelectorProperty, value); }
    }

    /// <summary>
    /// Identifies the <see cref="ItemContainerStyleSelector"/> DependencyProperty.
    /// </summary>
    public static readonly DependencyProperty ItemContainerStyleSelectorProperty =
        DependencyProperty.Register(nameof(ItemContainerStyleSelector), typeof(StyleSelector), typeof(SettingsExpander), new PropertyMetadata(null));

    private static void OnItemsConnectedPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not SettingsExpander { _itemsRepeater: not null } expander)
        {
            return;
        }

        var datasource = expander.ItemsSource ?? expander.Items;

        expander._itemsRepeater.ItemsSource = datasource;
    }

    private void ItemsRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (ItemContainerStyleSelector != null &&
            args.Element is FrameworkElement element &&
            element.ReadLocalValue(StyleProperty) == DependencyProperty.UnsetValue)
        {
            // TODO: Get item from args.Index?
            element.Style = ItemContainerStyleSelector.SelectStyle(null, element);
        }
    }
}