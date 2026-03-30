// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
#pragma warning disable IDE0130

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

internal class MyParagraph : IAddChild
{
    private readonly Paragraph _paragraph = new();

    public TextElement TextElement => _paragraph;

    public void AddChild(IAddChild child)
    {
        if (child.TextElement is Inline inlineChild)
        {
            _paragraph.Inlines.Add(inlineChild);
        }
        else if (child.TextElement is Block blockChild)
        {
            InlineUIContainer inlineUIContainer = new();
            RichTextBlock richTextBlock     = new()
            {
                TextWrapping = TextWrapping.Wrap
            };
            richTextBlock.Blocks.Add(blockChild);
            inlineUIContainer.Child = richTextBlock;
            _paragraph.Inlines.Add(inlineUIContainer);
        }
    }
}
