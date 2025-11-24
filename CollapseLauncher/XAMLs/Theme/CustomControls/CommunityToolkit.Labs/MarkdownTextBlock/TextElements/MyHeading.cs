// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using HtmlAgilityPack;
using Markdig.Syntax;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;
// ReSharper disable UnusedMember.Global

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

internal class MyHeading : IAddChild
{
    private readonly Paragraph _paragraph;
    private readonly HtmlNode? _htmlNode;

    public bool IsHtml => _htmlNode != null;

    public TextElement TextElement
    {
        get => _paragraph;
    }

    public MyHeading(HeadingBlock headingBlock, MarkdownConfig config)
    {
        _paragraph = new Paragraph();

        var level = headingBlock.Level;
        _paragraph.FontSize = level switch
        {
            1 => MarkdownConfig.Themes.H1FontSize,
            2 => MarkdownConfig.Themes.H2FontSize,
            3 => MarkdownConfig.Themes.H3FontSize,
            4 => MarkdownConfig.Themes.H4FontSize,
            5 => MarkdownConfig.Themes.H5FontSize,
            _ => MarkdownConfig.Themes.H6FontSize
        };
        _paragraph.Foreground = MarkdownConfig.Themes.HeadingForeground;
        _paragraph.FontWeight = level switch
        {
            1 => MarkdownConfig.Themes.H1FontWeight,
            2 => MarkdownConfig.Themes.H2FontWeight,
            3 => MarkdownConfig.Themes.H3FontWeight,
            4 => MarkdownConfig.Themes.H4FontWeight,
            5 => MarkdownConfig.Themes.H5FontWeight,
            _ => MarkdownConfig.Themes.H6FontWeight
        };
        _paragraph.Margin = new Thickness(0, 8, 0, level switch
        {
            1 => 16,
            2 => 10,
            3 => 8,
            _ => 4
        });
    }

    public MyHeading(HtmlNode htmlNode, MarkdownConfig config)
    {
        _htmlNode = htmlNode;
        _paragraph = new Paragraph();

        var align = _htmlNode?.GetAttributeValue("align", "left");
        _paragraph.TextAlignment = align switch
        {
            "left" => TextAlignment.Left,
            "right" => TextAlignment.Right,
            "center" => TextAlignment.Center,
            "justify" => TextAlignment.Justify,
            _ => TextAlignment.Left
        };

        var level = int.Parse(htmlNode.Name.Substring(1));
        _paragraph.FontSize = level switch
        {
            1 => MarkdownConfig.Themes.H1FontSize,
            2 => MarkdownConfig.Themes.H2FontSize,
            3 => MarkdownConfig.Themes.H3FontSize,
            4 => MarkdownConfig.Themes.H4FontSize,
            5 => MarkdownConfig.Themes.H5FontSize,
            _ => MarkdownConfig.Themes.H6FontSize
        };
        _paragraph.Foreground = MarkdownConfig.Themes.HeadingForeground;
        _paragraph.FontWeight = level switch
        {
            1 => MarkdownConfig.Themes.H1FontWeight,
            2 => MarkdownConfig.Themes.H2FontWeight,
            3 => MarkdownConfig.Themes.H3FontWeight,
            4 => MarkdownConfig.Themes.H4FontWeight,
            5 => MarkdownConfig.Themes.H5FontWeight,
            _ => MarkdownConfig.Themes.H6FontWeight
        };
    }

    public void AddChild(IAddChild child)
    {
        if (child.TextElement is Inline inlineChild)
        {
            _paragraph.Inlines.Add(inlineChild);
        }
    }
}
