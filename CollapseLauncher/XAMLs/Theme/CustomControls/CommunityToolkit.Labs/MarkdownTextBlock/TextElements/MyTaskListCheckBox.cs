// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Markdig.Extensions.TaskLists;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Media3D;

namespace CommunityToolkit.Labs.WinUI.MarkdownTextBlock.TextElements;

internal class MyTaskListCheckBox : IAddChild
{
    private TaskList _taskList;
    public TextElement TextElement { get; private set; }

    public MyTaskListCheckBox(TaskList taskList)
    {
        _taskList = taskList;
        var grid = new Grid();
        CompositeTransform3D transform = new CompositeTransform3D();
        transform.TranslateY = 2;
        grid.Transform3D = transform;
        grid.Width = 16;
        grid.Height = 16;
        grid.Margin = new Thickness(2, 0, 2, 0);
        grid.BorderThickness = new Thickness(1);
        grid.BorderBrush = new SolidColorBrush(Colors.Gray);
        FontIcon icon = new FontIcon();
        icon.FontSize = 16;
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        icon.VerticalAlignment = VerticalAlignment.Center;
        icon.Glyph = "\uE73E";
        grid.Children.Add(taskList.Checked ? icon : new TextBlock());
        grid.Padding = new Thickness(0);
        grid.CornerRadius = new CornerRadius(2);
        var inlineUIContainer = new InlineUIContainer();
        inlineUIContainer.Child = grid;
        TextElement = inlineUIContainer;
    }

    public void AddChild(IAddChild child)
    {
    }
}
