// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Markdig.Renderers;
using Markdig.Syntax.Inlines;
using System;
// ReSharper disable GrammarMistakeInComment

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.Renderers.ObjectRenderers.Inlines;

internal class DelimiterInlineRenderer : MarkdownObjectRenderer<WinUIRenderer, DelimiterInline>
{
    protected override void Write(WinUIRenderer renderer, DelimiterInline obj)
    {
        ArgumentNullException.ThrowIfNull(renderer, nameof(renderer));
        ArgumentNullException.ThrowIfNull(obj, nameof(obj));

        // delimiters children are emphasized text, we don't need to explicitly render them
        // Just need to render the children of the delimiter, I think..
        //renderer.WriteText(obj.ToLiteral());
        renderer.WriteChildren(obj);
    }
}
