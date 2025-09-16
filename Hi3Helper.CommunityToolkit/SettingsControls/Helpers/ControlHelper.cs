// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// ReSharper disable NotResolvedInText
namespace Hi3Helper.CommunityToolkit.WinUI.Controls;
internal static class ControlHelpers
{
    internal static bool IsXamlRootAvailable { get; } = Windows.Foundation.Metadata.ApiInformation.IsPropertyPresent("Windows.UI.Xaml.UIElement", "XamlRoot");
}
