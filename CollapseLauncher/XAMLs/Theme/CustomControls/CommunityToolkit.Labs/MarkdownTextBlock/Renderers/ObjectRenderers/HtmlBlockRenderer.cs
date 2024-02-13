// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using HtmlAgilityPack;
using Markdig.Syntax;
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.Renderers.ObjectRenderers;

internal class HtmlBlockRenderer : UWPObjectRenderer<HtmlBlock>
{
    protected override void Write(WinUIRenderer renderer, HtmlBlock obj)
    {
        if (renderer == null) throw new ArgumentNullException(nameof(renderer));
        if (obj == null) throw new ArgumentNullException(nameof(obj));

        var stringBuilder = new StringBuilder();
        foreach (var line in obj.Lines.Lines)
        {
            var lineText = line.Slice.ToString().Trim();
            if (string.IsNullOrWhiteSpace(lineText))
            {
                continue;
            }
            stringBuilder.AppendLine(lineText);
        }

        var html = Regex.Replace(stringBuilder.ToString(), @"\t|\n|\r", "", RegexOptions.Compiled);
        html = Regex.Replace(html, @"&nbsp;", " ", RegexOptions.Compiled);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        HtmlWriter.WriteHtml(renderer, doc.DocumentNode.ChildNodes);
    }
}
