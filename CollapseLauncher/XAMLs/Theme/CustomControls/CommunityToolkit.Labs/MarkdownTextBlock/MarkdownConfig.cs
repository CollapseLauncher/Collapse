// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
#nullable enable
using CommunityToolkit.WinUI.Controls.MarkdownTextBlockRns;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock;

public record MarkdownConfig
{
    public        string?         BaseUrl       { get; set; }
    public        IImageProvider? ImageProvider { get; set; }
    public        ISVGRenderer?   SVGRenderer   { get; set; }
    public static MarkdownThemes  Themes        => MarkdownThemes.Default;
}
