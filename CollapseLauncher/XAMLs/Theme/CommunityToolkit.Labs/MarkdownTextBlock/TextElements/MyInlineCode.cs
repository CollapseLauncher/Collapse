// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using Markdig.Syntax.Inlines;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media.Media3D;
#pragma warning disable IDE0130

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

internal class MyInlineCode : IAddChild
{
    private readonly InlineUIContainer _inlineContainer;

    public TextElement TextElement => _inlineContainer;

    public MyInlineCode(CodeInline codeInline)
    {
        _inlineContainer = new InlineUIContainer();
        Border border = new()
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Background        = MarkdownConfig.Themes.InlineCodeBackground,
            // border.BorderBrush = _config.Themes.InlineCodeBorderBrush;
            // border.BorderThickness = _config.Themes.InlineCodeBorderThickness;
            CornerRadius = MarkdownConfig.Themes.InlineCodeCornerRadius,
            Padding      = MarkdownConfig.Themes.InlineCodePadding
        };
        CompositeTransform3D transform = new()
        {
            TranslateY = 4
        };
        border.Transform3D = transform;
        TextBlock textBlock = new()
        {
            FontFamily = MarkdownConfig.Themes.InlineCodeFontFamily,
            FontSize   = MarkdownConfig.Themes.InlineCodeFontSize,
            FontWeight = MarkdownConfig.Themes.InlineCodeFontWeight,
            Text       = codeInline.Content
        };
        border.Child = textBlock;
        _inlineContainer.Child = border;
    }


    public void AddChild(IAddChild child) { }
}
