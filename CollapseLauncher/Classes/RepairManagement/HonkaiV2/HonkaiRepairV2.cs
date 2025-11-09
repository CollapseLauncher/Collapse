using CollapseLauncher.Helper;
using CollapseLauncher.Interfaces;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher;

// TODO: Implement Repair + Cache Update Mechanism just like Zenless Zone Zero's one.
internal partial class HonkaiRepairV2
    : ProgressBase<FilePropertiesRemote>,
      IRepair,
      IRepairAssetIndex
{
    private const string AssetBundleUserAgent = "UnityPlayer/2017.4.18f1 (UnityWebRequest/1.0, libcurl/7.51.0-DEV)";

    private bool       IsCacheMode           { get; }
    private bool       IsMainAssetOnlyMode   { get; }
    private HttpClient HttpClientAssetBundle { get; }
    private HttpClient HttpClientGeneric     { get; }

    public HonkaiRepairV2(
        UIElement    parentInterface,
        IGameVersion gameVersionManager)
        : this(parentInterface, gameVersionManager, null, false, false)
    {
    }

    private HonkaiRepairV2(
        UIElement    parentInterface,
        IGameVersion gameVersionManager,
        string?      useCustomVersion,
        bool         isMainAssetOnlyMode,
        bool         isCacheMode)
        : base(parentInterface,
               gameVersionManager,
               null,
               null,
               useCustomVersion)
    {
        IsCacheMode         = isCacheMode;
        IsMainAssetOnlyMode = isMainAssetOnlyMode;

        HttpClientAssetBundle = new HttpClientBuilder()
                               .UseLauncherConfig(DownloadThreadCount * DownloadThreadCount)
                               .SetAllowedDecompression(DecompressionMethods.None)
                               .SetUserAgent(AssetBundleUserAgent)
                            #if AOT
                               .SetHttpVersion(HttpVersion.Version20)
                            #else
                               .SetHttpVersion(HttpVersion.Version30)
                            #endif
                               .Create();

        HttpClientGeneric = new HttpClientBuilder()
                           .UseLauncherConfig(DownloadThreadCount * DownloadThreadCount)
                        #if AOT
                           .SetHttpVersion(HttpVersion.Version20)
                        #else
                           .SetHttpVersion(HttpVersion.Version30)
                        #endif
                           .Create();
    }

    public void Dispose()
    {
        // Cancel every routine first before disposing :D
        CancelRoutine();

        AssetIndex.Clear();

        GC.SuppressFinalize(this);
    }

    public List<FilePropertiesRemote> GetAssetIndex() => AssetIndex;

    public void CancelRoutine()
    {
        Token?.Cancel();
        Token?.Dispose();
        Token = null;
    }
}
