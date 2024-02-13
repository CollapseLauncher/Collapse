// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;
using Markdig.Syntax;
using System;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.Renderers.ObjectRenderers;

internal class QuoteBlockRenderer : UWPObjectRenderer<QuoteBlock>
{
    protected override void Write(WinUIRenderer renderer, QuoteBlock obj)
    {
        if (renderer == null) throw new ArgumentNullException(nameof(renderer));
        if (obj == null) throw new ArgumentNullException(nameof(obj));

        var quote = new MyQuote(obj);

        renderer.Push(quote);
        renderer.WriteChildren(obj);
        renderer.Pop();
    }
}
