// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

internal class MyThematicBreak : IAddChild
{
    private Paragraph _paragraph;

    public TextElement TextElement
    {
        get => _paragraph;
    }

    public MyThematicBreak()
    {
        _paragraph = new Paragraph();

        var inlineUIContainer = new InlineUIContainer();
        var border = new Border();
        border.Width = 500;
        border.BorderThickness = new Thickness(1);
        border.Margin = new Thickness(0, 4, 0, 4);
        border.BorderBrush = new SolidColorBrush(Colors.Gray);
        border.Height = 1;
        border.HorizontalAlignment = HorizontalAlignment.Stretch;
        inlineUIContainer.Child = border;
        _paragraph.Inlines.Add(inlineUIContainer);
    }

    public void AddChild(IAddChild child) { }
}
