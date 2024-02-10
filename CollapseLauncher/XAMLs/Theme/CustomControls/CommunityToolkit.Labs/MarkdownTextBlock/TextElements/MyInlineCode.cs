// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Markdig.Syntax.Inlines;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media.Media3D;
using Microsoft.UI.Xaml.Media;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;

internal class MyInlineCode : IAddChild
{
    private CodeInline _codeInline;
    private InlineUIContainer _inlineContainer;
    private MarkdownConfig _config;

    public TextElement TextElement
    {
        get => _inlineContainer;
    }

    public MyInlineCode(CodeInline codeInline, MarkdownConfig config)
    {
        _codeInline = codeInline;
        _config = config;
        _inlineContainer = new InlineUIContainer();
        var border = new Border();
        border.VerticalAlignment = VerticalAlignment.Bottom;
        border.Background = _config.Themes.InlineCodeBackground;
        // border.BorderBrush = _config.Themes.InlineCodeBorderBrush;
        // border.BorderThickness = _config.Themes.InlineCodeBorderThickness;
        border.CornerRadius = _config.Themes.InlineCodeCornerRadius;
        border.Padding = _config.Themes.InlineCodePadding;
        CompositeTransform3D transform = new CompositeTransform3D();
        transform.TranslateY = 4;
        border.Transform3D = transform;
        var textBlock = new TextBlock();
        textBlock.FontFamily = _config.Themes.InlineCodeFontFamily;
        textBlock.FontSize = _config.Themes.InlineCodeFontSize;
        textBlock.FontWeight = _config.Themes.InlineCodeFontWeight;
        textBlock.Text = codeInline.Content.ToString();
        border.Child = textBlock;
        _inlineContainer.Child = border;
    }


    public void AddChild(IAddChild child) { }
}
