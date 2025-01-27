// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.Renderers;
using Markdig.Renderers;
using Markdig.Syntax;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.Renderers;

public abstract class UWPObjectRenderer<TObject> : MarkdownObjectRenderer<WinUIRenderer, TObject>
    where TObject : MarkdownObject;
