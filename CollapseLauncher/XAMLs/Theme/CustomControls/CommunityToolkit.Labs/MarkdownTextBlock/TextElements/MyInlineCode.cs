// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using Markdig.Syntax.Inlines;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media.Media3D;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

internal class MyInlineCode : IAddChild
{
    private readonly InlineUIContainer _inlineContainer;

    public TextElement TextElement
    {
        get => _inlineContainer;
    }

    public MyInlineCode(CodeInline codeInline, MarkdownConfig config)
    {
        _inlineContainer = new InlineUIContainer();
        var border = new Border
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Background        = config.Themes.InlineCodeBackground,
            // border.BorderBrush = _config.Themes.InlineCodeBorderBrush;
            // border.BorderThickness = _config.Themes.InlineCodeBorderThickness;
            CornerRadius      = config.Themes.InlineCodeCornerRadius,
            Padding           = config.Themes.InlineCodePadding
        };
        CompositeTransform3D transform = new CompositeTransform3D
        {
            TranslateY = 4
        };
        border.Transform3D = transform;
        var textBlock = new TextBlock
        {
            FontFamily = config.Themes.InlineCodeFontFamily,
            FontSize   = config.Themes.InlineCodeFontSize,
            FontWeight = config.Themes.InlineCodeFontWeight,
            Text       = codeInline.Content
        };
        border.Child = textBlock;
        _inlineContainer.Child = border;
    }


    public void AddChild(IAddChild child) { }
}
