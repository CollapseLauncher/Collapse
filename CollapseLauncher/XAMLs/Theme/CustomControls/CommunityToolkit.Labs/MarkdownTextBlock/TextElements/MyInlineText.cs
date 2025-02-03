// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Documents;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

internal class MyInlineText(string text) : IAddChild
{
    private readonly Run _run = new()
    {
        Text = text
    };

    public TextElement TextElement
    {
        get => _run;
    }

    public void AddChild(IAddChild child) { }
}
