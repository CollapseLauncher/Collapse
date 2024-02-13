// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;
using Markdig.Syntax;
using System;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.Renderers.ObjectRenderers;

internal class HeadingRenderer : UWPObjectRenderer<HeadingBlock>
{
    protected override void Write(WinUIRenderer renderer, HeadingBlock obj)
    {
        if (renderer == null) throw new ArgumentNullException(nameof(renderer));
        if (obj == null) throw new ArgumentNullException(nameof(obj));

        var paragraph = new MyHeading(obj, renderer.Config);
        renderer.Push(paragraph);
        renderer.WriteLeafInline(obj);
        renderer.Pop();
    }
}
