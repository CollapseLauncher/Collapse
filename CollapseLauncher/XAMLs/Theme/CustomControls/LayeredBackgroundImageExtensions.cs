using Microsoft.UI;
using Microsoft.UI.Xaml;
using System;
using System.Net.Http;
// ReSharper disable GrammarMistakeInComment
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable ConvertIfStatementToSwitchStatement

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

internal static partial class LayeredBackgroundImageExtensions
{
    private static readonly HttpClient SharedHttpClient;

    static LayeredBackgroundImageExtensions()
    {
        SharedHttpClient = CreateHttpClient();
    }

    private static HttpClient CreateHttpClient()
    {
        SocketsHttpHandler handler = new()
        {
            AllowAutoRedirect       = true,
            MaxConnectionsPerServer = Environment.ProcessorCount * 2
        };
        return new HttpClient(handler, false);
    }

    internal static WindowId GetElementWindowId(this FrameworkElement element)
    {
        XamlRoot root     = element.XamlRoot;
        WindowId windowId = root.ContentIslandEnvironment.AppWindowId;
        return windowId;
    }
}
