// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;
using Markdig.Renderers;
using Markdig.Syntax;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.Renderers.ObjectRenderers;

internal class CodeBlockRenderer : MarkdownObjectRenderer<WinUIRenderer, CodeBlock>
{
    protected override void Write(WinUIRenderer renderer, CodeBlock obj)
    {
        var code = new MyCodeBlock(obj, renderer.Config);
        renderer.Push(code);
        renderer.Pop();
    }
}
