// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
namespace Hi3Helper.CommunityToolkit.WinUI.Controls;

public partial class SettingsExpander
{
    /// <summary>
    /// Fires when the SettingsExpander is opened
    /// </summary>
    public event EventHandler? Expanded;

    /// <summary>
    /// Fires when the expander is closed
    /// </summary>
    public event EventHandler? Collapsed;
}
