﻿using CollapseLauncher.Helper.Animation;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Windows.UI.Text;

namespace CollapseLauncher.Extension
{
    internal enum CornerRadiusKind { Normal, Rounded }
    internal static class UIElementExtensions
    {
        internal static TButtonBase CreateButtonWithIcon<TButtonBase>(string text, string iconGlyph, string iconFontFamily = "FontAwesome", string buttonStyle = "DefaultButtonStyle")
            where TButtonBase : ButtonBase, new()
        {
            TButtonBase buttonReturn = new TButtonBase();
            StackPanel contentPanel = new StackPanel { Orientation = Orientation.Horizontal };
            contentPanel.AddElementToStackPanel(new FontIcon
            {
                Glyph = iconGlyph,
                FontSize = 16,
                Margin = new Thickness(0d, 1d, 0d, 0d),
                FontFamily = GetApplicationResource<FontFamily>(iconFontFamily)
            });
            contentPanel.AddElementToStackPanel(new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(8d, 0d, 0d, 0d)
            });
            buttonReturn.CornerRadius = new CornerRadius(14);
            buttonReturn.Content = contentPanel;
            buttonReturn.Style = GetApplicationResource<Style>(buttonStyle);
            return buttonReturn;
        }

        internal static ref TElement AddElementToStackPanel<TElement>(this Panel stackPanel, TElement element)
            where TElement : FrameworkElement
        {
            stackPanel.Children.Add(element);
            return ref Unsafe.AsRef(ref element);
        }

        internal static void AddGridColumns(this Grid grid, int count, GridLength? columnWidth = null)
        {
            for (; count > 0; count--) grid.ColumnDefinitions.Add(new ColumnDefinition()
            {
                Width = columnWidth ?? GridLength.Auto
            });
        }

        internal static void AddGridRows(this Grid grid, int count, GridLength? columnHeight = null)
        {
            for (; count > 0; count--) grid.RowDefinitions.Add(new RowDefinition()
            {
                Height = columnHeight ?? GridLength.Auto
            });
        }

        internal static ref TElement AddElementToGridRowColumn<TElement>(this Grid grid, TElement element, int rowIndex = 0, int columnIndex = 0, int rowSpan = 0, int columnSpan = 0)
            where TElement : FrameworkElement
        {
            grid.Children.Add(element);
            SetElementGridRowPosition(element, rowIndex, rowSpan);
            SetElementGridColumnPosition(element, columnIndex, columnSpan);
            return ref Unsafe.AsRef(ref element);
        }

        internal static ref TElement AddElementToGridRow<TElement>(this Grid grid, TElement element, int index, int span = 0)
            where TElement : FrameworkElement
        {
            grid.Children.Add(element);
            SetElementGridRowPosition(element, index, span);
            return ref Unsafe.AsRef(ref element);
        }

        internal static ref TElement AddElementToGridColumn<TElement>(this Grid grid, TElement element, int index, int span = 0)
            where TElement : FrameworkElement
        {
            grid.Children.Add(element);
            SetElementGridColumnPosition(element, index, span);
            return ref Unsafe.AsRef(ref element);
        }

        internal static void SetElementGridRowPosition<TElement>(TElement element, int index, int span = 0)
            where TElement : FrameworkElement
        {
            Grid.SetRow(element, index);
            if (span > 0) Grid.SetRowSpan(element, span);
        }

        internal static void SetElementGridColumnPosition<TElement>(TElement element, int index, int span = 0)
            where TElement : FrameworkElement
        {
            Grid.SetColumn(element, index);
            if (span > 0) Grid.SetColumnSpan(element, span);
        }

        internal static void AddTextBlockNewLine(this TextBlock textBlock, int count = 1)
        {
            while (count-- > 0) { textBlock.Inlines.Add(new LineBreak()); }
        }

        internal static void AddTextBlockLine(this TextBlock textBlock, string message, FontWeight? weight = null, double size = 14d)
        {
            if (!weight.HasValue) weight = FontWeights.Normal;
            textBlock.Inlines.Add(new Run { Text = message, FontWeight = weight.Value, FontSize = size });
        }

        internal static TReturnType GetApplicationResource<TReturnType>(string resourceKey)
        {
            if (!Application.Current.Resources.ContainsKey(resourceKey))
                throw new KeyNotFoundException($"Application resource with key: {resourceKey} does not exist!");

            return (TReturnType)Application.Current.Resources[resourceKey];
        }

        internal static CornerRadius GetElementCornerRadius(FrameworkElement element, CornerRadiusKind kind = CornerRadiusKind.Normal)
        {
            switch (kind)
            {
                default:
                    return new CornerRadius(8);
                case CornerRadiusKind.Rounded:
                    double radiusSize = element.ActualHeight / 2;
                    return new CornerRadius(radiusSize);
            }
        }

        internal static CornerRadius AttachRoundedKindCornerRadius(Control element)
        {
            CornerRadius initialRadius = GetElementCornerRadius(element, CornerRadiusKind.Rounded);
            element.SizeChanged += (sender, _) =>
            {
                if (sender != null && (sender is Control control))
                    control.CornerRadius = GetElementCornerRadius(element, CornerRadiusKind.Rounded);
            };

            return initialRadius;
        }

        internal static void FindAndSetTextBlockWrapping(this UIElement element, TextWrapping wrap = TextWrapping.Wrap, HorizontalAlignment posAlign = HorizontalAlignment.Center, TextAlignment textAlign = TextAlignment.Center, bool recursiveAssignment = false, bool isParentAButton = false)
        {
            if (element is not null && element is TextBlock textBlock)
            {
                textBlock.TextWrapping = wrap;
                if (isParentAButton)
                {
                    textBlock.HorizontalAlignment = posAlign;
                    textBlock.HorizontalTextAlignment = textAlign;
                }
            }

            if (!recursiveAssignment) return;

            if (element is ButtonBase button)
            {
                if (button.Content is UIElement buttonContent)
                    buttonContent.FindAndSetTextBlockWrapping(wrap, posAlign, textAlign, recursiveAssignment, true);
                else if (button.Content is string buttonString)
                    button.Content = new TextBlock { Text = buttonString, TextWrapping = wrap, HorizontalAlignment = HorizontalAlignment.Center };
            }

            if (element is Panel panel)
                foreach (UIElement childrenElement in panel.Children!)
                    childrenElement.FindAndSetTextBlockWrapping(wrap, posAlign, textAlign, recursiveAssignment, isParentAButton);

            if (element is ScrollViewer scrollViewer && scrollViewer.Content is UIElement elementInner)
                elementInner.FindAndSetTextBlockWrapping(wrap, posAlign, textAlign, recursiveAssignment, isParentAButton);

            if (element is ContentControl contentControl && (element is SettingsCard || element is Expander) && contentControl.Content is UIElement contentControlInner)
            {
                contentControlInner.FindAndSetTextBlockWrapping(wrap, posAlign, textAlign, recursiveAssignment, isParentAButton);

                if (contentControl is Expander expander && expander.Header is UIElement expanderHeader)
                    expanderHeader.FindAndSetTextBlockWrapping(wrap, posAlign, textAlign, recursiveAssignment, isParentAButton);
            }

            if (element is InfoBar infoBar && infoBar.Content is UIElement infoBarInner)
                infoBarInner.FindAndSetTextBlockWrapping(wrap, posAlign, textAlign, recursiveAssignment, isParentAButton);
        }
    }
}
