// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;
using Markdig.Syntax.Inlines;
using System;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.Renderers.ObjectRenderers.Inlines;

internal class EmphasisInlineRenderer : UWPObjectRenderer<EmphasisInline>
{
    protected override void Write(WinUIRenderer renderer, EmphasisInline obj)
    {
        if (renderer == null) throw new ArgumentNullException(nameof(renderer));
        if (obj == null) throw new ArgumentNullException(nameof(obj));

        MyEmphasisInline? span = null;

        switch (obj.DelimiterChar)
        {
            case '*':
            case '_':
                span = new MyEmphasisInline(obj);
                if (obj.DelimiterCount == 2) { span.SetBold(); } else { span.SetItalic(); }
                break;
            case '~':
                span = new MyEmphasisInline(obj);
                if (obj.DelimiterCount == 2) { span.SetStrikeThrough(); } else { span.SetSubscript(); }
                break;
            case '^':
                span = new MyEmphasisInline(obj);
                span.SetSuperscript();
                break;
        }

        if (span != null)
        {
            renderer.Push(span);
            renderer.WriteChildren(obj);
            renderer.Pop();
        }
        else
        {
            renderer.WriteChildren(obj);
        }
    }
}
