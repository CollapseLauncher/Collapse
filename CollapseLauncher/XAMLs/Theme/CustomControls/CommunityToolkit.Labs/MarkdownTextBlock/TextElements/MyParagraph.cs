// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Markdig.Syntax;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

internal class MyParagraph : IAddChild
{
    private ParagraphBlock _paragraphBlock;
    private Paragraph _paragraph;

    public TextElement TextElement
    {
        get => _paragraph;
    }

    public MyParagraph(ParagraphBlock paragraphBlock)
    {
        _paragraphBlock = paragraphBlock;
        _paragraph = new Paragraph();
    }

    public void AddChild(IAddChild child)
    {
        if (child.TextElement is Inline inlineChild)
        {
            _paragraph.Inlines.Add(inlineChild);
        }
        else if (child.TextElement is Microsoft.UI.Xaml.Documents.Block blockChild)
        {
            var inlineUIContainer = new InlineUIContainer();
            var richTextBlock = new RichTextBlock();
            richTextBlock.TextWrapping = TextWrapping.Wrap;
            richTextBlock.Blocks.Add(blockChild);
            inlineUIContainer.Child = richTextBlock;
            _paragraph.Inlines.Add(inlineUIContainer);
        }
    }
}
