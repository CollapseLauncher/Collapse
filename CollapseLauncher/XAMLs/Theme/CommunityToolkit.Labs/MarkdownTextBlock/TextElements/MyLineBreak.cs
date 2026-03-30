// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Documents;
#pragma warning disable IDE0130

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

internal class MyLineBreak : IAddChild
{
    private readonly LineBreak _lineBreak = new();

    public TextElement TextElement => _lineBreak;

    public void AddChild(IAddChild child) { }
}
