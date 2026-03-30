// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Markdig.Helpers;
using Markdig.Syntax;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Text;
// ReSharper disable CommentTypo
#pragma warning disable IDE0130

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

internal class MyCodeBlock : IAddChild
{
    private readonly Paragraph _paragraph;

    public TextElement TextElement => _paragraph;

    public MyCodeBlock(CodeBlock codeBlock)
    {
        _paragraph = new Paragraph();
        InlineUIContainer container = new();
        Border border    = new()
        {
            Background   = (Brush)Application.Current.Resources["ExpanderHeaderBackground"],
            Padding      = MarkdownConfig.Themes.Padding,
            Margin       = MarkdownConfig.Themes.InternalMargin,
            CornerRadius = MarkdownConfig.Themes.CornerRadius
        };
        RichTextBlock richTextBlock = new();

        if (codeBlock is FencedCodeBlock fencedCodeBlock)
        {
            //#if !WINAPPSDK
            //            var formatter = new ColorCode.RichTextBlockFormatter(Extensions.GetOneDarkProStyle());
            //#else
            //            var formatter = new ColorCode.RichTextBlockFormatter(Extensions.GetOneDarkProStyle());
            //#endif
            StringBuilder stringBuilder = new();

            // go through all the lines backwards and only add the lines to a stack if we have encountered the first non-empty line
            StringLine[]  lines                        = fencedCodeBlock.Lines.Lines;
            Stack<string> stack                        = new();
            bool           encounteredFirstNonEmptyLine = false;
            if (lines.Length != 0)
            {
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    StringLine line = lines[i];
                    if (string.IsNullOrWhiteSpace(line.ToString()) && !encounteredFirstNonEmptyLine)
                    {
                        continue;
                    }

                    encounteredFirstNonEmptyLine = true;
                    stack.Push(line.ToString());
                }

                // append all the lines in the stack to the string builder
                while (stack.Count > 0)
                {
                    stringBuilder.AppendLine(stack.Pop());
                }
            }

            //formatter.FormatRichTextBlock(stringBuilder.ToString(), fencedCodeBlock.ToLanguage(), richTextBlock);
        }
        else
        {
            foreach (StringLine line in codeBlock.Lines.Lines)
            {
                Paragraph paragraph  = new();
                string    lineString = line.ToString();
                if (!string.IsNullOrWhiteSpace(lineString))
                {
                    paragraph.Inlines.Add(new Run { Text = lineString });
                }
                richTextBlock.Blocks.Add(paragraph);
            }
        }
        border.Child = richTextBlock;
        container.Child = border;
        _paragraph.Inlines.Add(container);
    }

    public void AddChild(IAddChild child) { }
}
