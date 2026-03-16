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

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.Renderers.ObjectRenderers;

internal partial class HtmlBlockRenderer : MarkdownObjectRenderer<WinUIRenderer, HtmlBlock>
{
    [GeneratedRegex(@"\t|\n|\r", RegexOptions.NonBacktracking, 10000)]
    internal static partial Regex GetTabAndNewLineMatch();

    [GeneratedRegex("&nbsp;", RegexOptions.NonBacktracking, 10000)]
    internal static partial Regex GetNonBreakingSpaceMatch();

    protected override void Write(WinUIRenderer renderer, HtmlBlock obj)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(obj);

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

        string html = GetTabAndNewLineMatch().Replace(stringBuilder.ToString(), "");
        html = GetNonBreakingSpaceMatch().Replace(html, " ");
        HtmlDocument doc = new();
        doc.LoadHtml(html);
        HtmlWriter.WriteHtml(renderer, doc.DocumentNode.ChildNodes);
    }
}
