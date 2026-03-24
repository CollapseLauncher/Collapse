// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Block = Microsoft.UI.Xaml.Documents.Block;
using Inline = Microsoft.UI.Xaml.Documents.Inline;
#pragma warning disable IDE0130

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

public class MyFlowDocument : IAddChild
{
    // useless property
    public TextElement TextElement { get; set; } = new Run();
    //

    public RichTextBlock RichTextBlock { get; set; } = new();

    public void AddChild(IAddChild child)
    {
        TextElement element = child.TextElement;
        if (element == null) return;
        if (element is Block block)
        {
            RichTextBlock.Blocks.Add(block);
        }
        else if (element is Inline inline)
        {
            Paragraph paragraph = new();
            paragraph.Inlines.Add(inline);
            RichTextBlock.Blocks.Add(paragraph);
        }
    }
}
