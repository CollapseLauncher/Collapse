// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using HtmlAgilityPack;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using System.Linq;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements.Html;

// block
internal class MyDetails : IAddChild
{
    private HtmlNode _htmlNode;
    private InlineUIContainer _inlineUIContainer;
    private Expander _expander;
    private MyFlowDocument _flowDocument;
    private Paragraph _paragraph;

    public TextElement TextElement
    {
        get => _paragraph;
    }

    public MyDetails(HtmlNode details)
    {
        _htmlNode = details;

        var header = _htmlNode.ChildNodes
            .FirstOrDefault(
                x => x.Name == "summary" ||
                x.Name == "header");

        _inlineUIContainer = new InlineUIContainer();
        _expander = new Expander();
        _expander.HorizontalAlignment = HorizontalAlignment.Stretch;
        _flowDocument = new MyFlowDocument(details);
        _flowDocument.RichTextBlock.HorizontalAlignment = HorizontalAlignment.Stretch;
        _expander.Content = _flowDocument.RichTextBlock;
        var headerBlock = new TextBlock()
        {
            Text = header?.InnerText
        };
        headerBlock.HorizontalAlignment = HorizontalAlignment.Stretch;
        _expander.Header = headerBlock;
        _inlineUIContainer.Child = _expander;
        _paragraph = new Paragraph();
        _paragraph.Inlines.Add(_inlineUIContainer);
    }

    public void AddChild(IAddChild child)
    {
        _flowDocument.AddChild(child);
    }
}
