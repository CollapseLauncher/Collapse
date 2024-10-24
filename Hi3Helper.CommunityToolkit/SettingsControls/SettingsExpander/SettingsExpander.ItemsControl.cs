// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Hi3Helper.CommunityToolkit.WinUI.Controls;

//// Implement properties for ItemsControl like behavior.
public partial class SettingsExpander
{
    public IList<object> Items
    {
        get { return (IList<object>)GetValue(ItemsProperty); }
        set { SetValue(ItemsProperty, value); }
    }

    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(nameof(Items), typeof(IList<object>), typeof(SettingsExpander), new PropertyMetadata(null, OnItemsConnectedPropertyChanged));

    public object ItemsSource
    {
        get { return GetValue(ItemsSourceProperty); }
        set { SetValue(ItemsSourceProperty, value); }
    }

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(object), typeof(SettingsExpander), new PropertyMetadata(null, OnItemsConnectedPropertyChanged));

    public object ItemTemplate
    {
        get { return GetValue(ItemTemplateProperty); }
        set { SetValue(ItemTemplateProperty, value); }
    }

    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(nameof(ItemTemplate), typeof(object), typeof(SettingsExpander), new PropertyMetadata(null));

    public StyleSelector ItemContainerStyleSelector
    {
        get { return (StyleSelector)GetValue(ItemContainerStyleSelectorProperty); }
        set { SetValue(ItemContainerStyleSelectorProperty, value); }
    }

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
