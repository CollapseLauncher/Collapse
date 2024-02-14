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

        internal static ref TElement AddElementToStackPanel<TElement>(this StackPanel stackPanel, TElement element)
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

        internal static CornerRadius GetElementCornerRadius<T>(object sender, CornerRadiusKind kind = CornerRadiusKind.Normal)
            where T : Control
        {
            Control element = sender as Control;
            switch (kind)
            {
                default:
                    return element.CornerRadius;
                case CornerRadiusKind.Rounded:
                    double radiusSize = element.ActualHeight / 2;
                    return new CornerRadius(radiusSize);
            }
        }

        internal static CornerRadius AttachRoundedKindCornerRadius(Control element)
        {
            CornerRadius initialRadius = GetElementCornerRadius<Control>(element, CornerRadiusKind.Rounded);
            element.SizeChanged += (sender, _) => (sender as Control).CornerRadius = GetElementCornerRadius<Control>(element, CornerRadiusKind.Rounded);

            return initialRadius;
        }
    }
}
