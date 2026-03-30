// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using HtmlAgilityPack;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using System.Linq;
#pragma warning disable IDE0130

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements.Html;

// block
internal class MyDetails : IAddChild
{
    private readonly MyFlowDocument _flowDocument;
    private readonly Paragraph      _paragraph;

    public TextElement TextElement => _paragraph;

    public MyDetails(HtmlNode details)
    {
        HtmlNode header = details.ChildNodes
                                 .FirstOrDefault(x => x.Name is "summary" or "header");

        InlineUIContainer inlineUIContainer = new();
        Expander          expander          = new()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _flowDocument = new MyFlowDocument
        {
            RichTextBlock =
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            }
        };
        expander.Content = _flowDocument.RichTextBlock;
        TextBlock headerBlock = new()
        {
            Text                = header?.InnerText,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        expander.Header = headerBlock;
        inlineUIContainer.Child = expander;
        _paragraph = new Paragraph();
        _paragraph.Inlines.Add(inlineUIContainer);
    }

    public void AddChild(IAddChild child)
    {
        _flowDocument.AddChild(child);
    }
}
