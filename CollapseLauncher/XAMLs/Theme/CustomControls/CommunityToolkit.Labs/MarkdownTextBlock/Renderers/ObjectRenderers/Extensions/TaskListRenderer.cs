// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;
using Markdig.Extensions.TaskLists;
using Markdig.Renderers;
using System;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.Renderers.ObjectRenderers.Extensions;

internal class TaskListRenderer : MarkdownObjectRenderer<WinUIRenderer, TaskList>
{
    protected override void Write(WinUIRenderer renderer, TaskList taskList)
    {
        if (renderer == null) throw new ArgumentNullException(nameof(renderer));
        if (taskList == null) throw new ArgumentNullException(nameof(taskList));

        var checkBox = new MyTaskListCheckBox(taskList);
        renderer.WriteInline(checkBox);
    }
}
