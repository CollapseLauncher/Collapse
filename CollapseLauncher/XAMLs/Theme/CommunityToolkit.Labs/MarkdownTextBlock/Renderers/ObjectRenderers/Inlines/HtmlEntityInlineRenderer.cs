// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Markdig.Helpers;
using Markdig.Renderers;
using Markdig.Syntax.Inlines;
using System;
#pragma warning disable IDE0130

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.Renderers.ObjectRenderers.Inlines;

internal class HtmlEntityInlineRenderer : MarkdownObjectRenderer<WinUIRenderer, HtmlEntityInline>
{
    protected override void Write(WinUIRenderer renderer, HtmlEntityInline obj)
    {
        if (renderer == null) throw new ArgumentNullException(nameof(renderer));
        if (obj == null) throw new ArgumentNullException(nameof(obj));

        StringSlice transcoded = obj.Transcoded;
        renderer.WriteText(ref transcoded);
        // todo: wtf is this?
    }
}
