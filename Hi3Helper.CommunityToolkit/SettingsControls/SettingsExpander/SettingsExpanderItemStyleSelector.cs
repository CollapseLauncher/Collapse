// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// ReSharper disable PartialTypeWithSinglePart
namespace Hi3Helper.CommunityToolkit.WinUI.Controls;

/// <summary>
/// <see cref="StyleSelector"/> used by <see cref="SettingsExpander"/> to choose the proper <see cref="SettingsCard"/> container style (clickable or not).
/// </summary>
public partial class SettingsExpanderItemStyleSelector : StyleSelector
{
    /// <summary>
    /// Gets or sets the default <see cref="Style"/>.
    /// </summary>
    public Style DefaultStyle { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="Style"/> when clickable.
    /// </summary>
    public Style ClickableStyle { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsExpanderItemStyleSelector"/> class.
    /// </summary>
    public SettingsExpanderItemStyleSelector()
    {
    }

    /// <inheritdoc/>
    protected override Style SelectStyleCore(object item, DependencyObject container)
    {
        if (container is SettingsCard card && card.IsClickEnabled)
        {
            return ClickableStyle;
        }

        return DefaultStyle;
    }
}
