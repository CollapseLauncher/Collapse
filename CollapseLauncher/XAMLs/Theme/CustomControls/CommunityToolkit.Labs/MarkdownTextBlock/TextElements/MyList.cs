// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Markdig.Syntax;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using RomanNumerals;
using System.Globalization;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

internal class MyList : IAddChild
{
    private Paragraph _paragraph;
    private StackPanel _stackPanel;
    private BulletType _bulletType;
    private bool _isOrdered;
    private int _index = 1;
    private const string _dot = "• ";

    public TextElement TextElement
    {
        get => _paragraph;
    }

    public MyList(ListBlock listBlock)
    {
        _paragraph = new Paragraph();
        InlineUIContainer _container = new InlineUIContainer();
        _stackPanel = new StackPanel();

        if (listBlock.IsOrdered)
        {
            _isOrdered = true;
            _bulletType = ToBulletType(listBlock.BulletType);

            if (listBlock.OrderedStart != null && (listBlock.DefaultOrderedStart != listBlock.OrderedStart))
            {
                int _startIndex = int.Parse(listBlock.OrderedStart, NumberFormatInfo.InvariantInfo);
                _index = _startIndex;
            }
        }

        _stackPanel.Orientation = Orientation.Vertical;
        _container.Child = _stackPanel;
        _paragraph.Inlines.Add(_container);
    }

    public void AddChild(IAddChild child)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
        grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
        string bullet;
        if (_isOrdered)
        {
            bullet = _bulletType switch
            {
                BulletType.Number => $"{_index}. ",
                BulletType.LowerAlpha => $"{_index.ToAlphabetical()}. ",
                BulletType.UpperAlpha => $"{_index.ToAlphabetical().ToUpper()}. ",
                BulletType.LowerRoman => $"{_index.ToRomanNumerals().ToLower()} ",
                BulletType.UpperRoman => $"{_index.ToRomanNumerals().ToUpper()} ",
                BulletType.Circle => _dot,
                _ => _dot
            };
            _index++;
        }
        else
        {
            bullet = _dot;
        }
        var textBlock = new TextBlock()
        {
            Text = bullet,
        };
        textBlock.SetValue(Grid.ColumnProperty, 0);
        textBlock.VerticalAlignment = VerticalAlignment.Top;
        grid.Children.Add(textBlock);
        var flowDoc = new MyFlowDocument();
        flowDoc.AddChild(child);

        flowDoc.RichTextBlock.SetValue(Grid.ColumnProperty, 1);
        flowDoc.RichTextBlock.Padding = new Thickness(8, 0, 0, 0);
        flowDoc.RichTextBlock.VerticalAlignment = VerticalAlignment.Top;
        grid.Children.Add(flowDoc.RichTextBlock);

        _stackPanel.Children.Add(grid);
    }

    private BulletType ToBulletType(char bullet)
    {
        // Gets or sets the type of the bullet (e.g: '1', 'a', 'A', 'i', 'I').
        switch (bullet)
        {
            case '1':
                return BulletType.Number;
            case 'a':
                return BulletType.LowerAlpha;
            case 'A':
                return BulletType.UpperAlpha;
            case 'i':
                return BulletType.LowerRoman;
            case 'I':
                return BulletType.UpperRoman;
            default:
                return BulletType.Circle;
        }
    }
}

internal enum BulletType
{
    Circle,
    Number,
    LowerAlpha,
    UpperAlpha,
    LowerRoman,
    UpperRoman
}
