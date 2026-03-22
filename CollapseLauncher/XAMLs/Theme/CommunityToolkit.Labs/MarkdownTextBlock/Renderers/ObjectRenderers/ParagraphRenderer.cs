// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;
using Markdig.Renderers;
using Markdig.Syntax;
using System;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.Renderers.ObjectRenderers;

internal class ParagraphRenderer : MarkdownObjectRenderer<WinUIRenderer, ParagraphBlock>
{
    protected override void Write(WinUIRenderer renderer, ParagraphBlock obj)
    {
        if (renderer == null) throw new ArgumentNullException(nameof(renderer));
        if (obj == null) throw new ArgumentNullException(nameof(obj));

        var paragraph = new MyParagraph();
        // set style
        renderer.Push(paragraph);
        renderer.WriteLeafInline(obj);
        renderer.Pop();
    }
}
