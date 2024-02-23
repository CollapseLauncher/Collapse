// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Markdig.Renderers;
using Markdig.Syntax.Inlines;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.Renderers.ObjectRenderers.Inlines;

internal class ContainerInlineRenderer : MarkdownObjectRenderer<WinUIRenderer, ContainerInline>
{
    protected override void Write(WinUIRenderer renderer, ContainerInline obj)
    {
        foreach (var inline in obj)
        {
            renderer.Write(inline);
        }
    }
}
