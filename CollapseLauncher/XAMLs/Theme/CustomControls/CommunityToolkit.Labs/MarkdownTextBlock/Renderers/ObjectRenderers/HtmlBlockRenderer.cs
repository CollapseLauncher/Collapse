// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using HtmlAgilityPack;
using Markdig.Helpers;
using Markdig.Renderers;
using Markdig.Syntax;
using System;
using System.Text;
using System.Text.RegularExpressions;
#pragma warning disable SYSLIB1045

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.Renderers.ObjectRenderers;

internal class HtmlBlockRenderer : MarkdownObjectRenderer<WinUIRenderer, HtmlBlock>
{
    protected override void Write(WinUIRenderer renderer, HtmlBlock obj)
    {
        if (renderer == null) throw new ArgumentNullException(nameof(renderer));
        if (obj == null) throw new ArgumentNullException(nameof(obj));

        StringBuilder stringBuilder = new();
        foreach (StringLine line in obj.Lines.Lines)
        {
            string lineText = line.Slice.ToString().Trim();
            if (string.IsNullOrWhiteSpace(lineText))
            {
                continue;
            }
            stringBuilder.AppendLine(lineText);
        }

        string html = Regex.Replace(stringBuilder.ToString(), @"\t|\n|\r", "", RegexOptions.Compiled);
        html = Regex.Replace(html, "&nbsp;", " ", RegexOptions.Compiled);
        HtmlDocument doc = new();
        doc.LoadHtml(html);
        HtmlWriter.WriteHtml(renderer, doc.DocumentNode.ChildNodes);
    }
}
