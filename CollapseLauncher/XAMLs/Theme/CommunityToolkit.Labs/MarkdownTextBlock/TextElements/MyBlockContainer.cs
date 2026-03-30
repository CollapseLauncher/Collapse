// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Documents;
#pragma warning disable IDE0130

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

internal class MyBlockContainer : IAddChild
{
    private readonly MyFlowDocument _flowDocument;
    private readonly Paragraph      _paragraph;

    public TextElement TextElement => _paragraph;

    public MyBlockContainer()
    {
        InlineUIContainer inlineUIContainer = new();
        _flowDocument = new MyFlowDocument();
        inlineUIContainer.Child = _flowDocument.RichTextBlock;
        _paragraph = new Paragraph();
        _paragraph.Inlines.Add(inlineUIContainer);
    }

    public void AddChild(IAddChild child)
    {
        _flowDocument.AddChild(child);
    }
}
