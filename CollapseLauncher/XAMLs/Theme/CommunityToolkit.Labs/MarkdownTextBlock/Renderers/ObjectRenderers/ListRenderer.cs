// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;
using Markdig.Renderers;
using Markdig.Syntax;
using System;
#pragma warning disable IDE0130

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.Renderers.ObjectRenderers;

internal class ListRenderer : MarkdownObjectRenderer<WinUIRenderer, ListBlock>
{
    protected override void Write(WinUIRenderer renderer, ListBlock listBlock)
    {
        if (renderer == null) throw new ArgumentException(nameof(renderer));
        if (listBlock == null) throw new ArgumentNullException(nameof(listBlock));

        MyList list = new(listBlock);

        renderer.Push(list);

        foreach (Block item in listBlock)
        {
            ListItemBlock listItemBlock = (ListItemBlock)item;
            MyBlockContainer listItem = new();
            renderer.Push(listItem);
            renderer.WriteChildren(listItemBlock);
            renderer.Pop();
        }

        renderer.Pop();
    }
}
