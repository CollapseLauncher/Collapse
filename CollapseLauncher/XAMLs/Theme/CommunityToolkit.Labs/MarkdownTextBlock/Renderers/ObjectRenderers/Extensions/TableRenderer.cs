// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;
using Markdig.Extensions.Tables;
using Markdig.Renderers;
using Markdig.Syntax;
using Microsoft.UI.Xaml;
using System;
#pragma warning disable IDE0130

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.Renderers.ObjectRenderers.Extensions;

public class TableRenderer : MarkdownObjectRenderer<WinUIRenderer, Table>
{
    protected override void Write(WinUIRenderer renderer, Table table)
    {
        if (renderer == null) throw new ArgumentNullException(nameof(renderer));
        if (table == null) throw new ArgumentNullException(nameof(table));

        MyTable myTable = new(table);

        renderer.Push(myTable);

        for (int rowIndex = 0; rowIndex < table.Count; rowIndex++)
        {
            Block rowObj = table[rowIndex];
            TableRow row = (TableRow)rowObj;

            for (int i = 0; i < row.Count; i++)
            {
                Block cellObj = row[i];
                TableCell cell = (TableCell)cellObj;
                TextAlignment textAlignment = TextAlignment.Left;

                int columnIndex = i;

                if (table.ColumnDefinitions.Count > 0)
                {
                    columnIndex = cell.ColumnIndex < 0 || cell.ColumnIndex >= table.ColumnDefinitions.Count
                        ? i
                        : cell.ColumnIndex;
                    columnIndex = columnIndex >= table.ColumnDefinitions.Count ? table.ColumnDefinitions.Count - 1 : columnIndex;
                    TableColumnAlign? alignment = table.ColumnDefinitions[columnIndex].Alignment;
                    textAlignment = alignment switch
                    {
                        TableColumnAlign.Center => TextAlignment.Center,
                        TableColumnAlign.Left => TextAlignment.Left,
                        TableColumnAlign.Right => TextAlignment.Right,
                        _ => TextAlignment.Left
                    };
                }

                MyTableCell myCell = new(cell, textAlignment, row.IsHeader, columnIndex, rowIndex);

                renderer.Push(myCell);
                renderer.Write(cell);
                renderer.Pop();
            }
        }

        renderer.Pop();
    }
}
