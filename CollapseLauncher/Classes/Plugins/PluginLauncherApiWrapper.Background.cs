using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management.Api;
using System;
using System.IO;
using System.Runtime.CompilerServices;
#pragma warning disable IDE0130

namespace CollapseLauncher.Plugins;

#nullable enable

internal sealed partial class PluginLauncherApiWrapper
{
    private void ConvertBackgroundImageEntries(HypLauncherBackgroundList newsData)
    {
        using PluginDisposableMemory<LauncherPathEntry> backgroundEntries =
            PluginDisposableMemoryExtension.ToManagedSpan<LauncherPathEntry>(_pluginMediaApi.GetBackgroundEntries);
        using PluginDisposableMemory<LauncherPathEntry> overlayEntries =
            PluginDisposableMemoryExtension.ToManagedSpan<LauncherPathEntry>(_pluginMediaApi.GetLogoOverlayEntries);
        int count = backgroundEntries.Length;

        newsData.GameContentList.Clear();
        HypLauncherBackgroundContentList content = new();
        newsData.GameContentList.Add(content);
        if (count == 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            if (!TryGetEntryAtIndex(backgroundEntries, i, out LauncherPathEntry backgroundEntry))
            {
                continue;
            }

            TryGetEntryAtIndex(overlayEntries, i, out LauncherPathEntry overlayEntry);

            using (backgroundEntry)
                using (overlayEntry)
                {
                    string? backgroundUrl = backgroundEntry.Path;
                    string? overlayUrl    = overlayEntry.Path;

                    if (string.IsNullOrEmpty(backgroundUrl))
                    {
                        continue;
                    }

                    backgroundUrl = ImageLoaderHelper.CopyToLocalIfBase64(backgroundUrl);
                    overlayUrl    = ImageLoaderHelper.CopyToLocalIfBase64(overlayUrl);

                    HypLauncherMediaContentData backgroundData = new()
                    {
                        ImageUrl = backgroundUrl
                    };

                    HypLauncherMediaContentData? overlayData = string.IsNullOrEmpty(overlayUrl)
                        ? null
                        : new HypLauncherMediaContentData
                        {
                            ImageUrl = overlayUrl
                        };

                    ReadOnlySpan<char> backgroundExt = Path.GetExtension(backgroundUrl);
                    bool isBackgroundVideo = LayeredBackgroundImage
                                            .SupportedVideoExtensionsLookup
                                            .Contains(backgroundExt);

                    content.Backgrounds.Add(new HypLauncherBackgroundContentKindData
                    {
                        BackgroundImage   = isBackgroundVideo ? null : backgroundData,
                        BackgroundVideo   = isBackgroundVideo ? backgroundData : null,
                        BackgroundOverlay = overlayData
                    });
                }
        }
    }

    private static bool TryGetEntryAtIndex(
        PluginDisposableMemory<LauncherPathEntry> entries,
        int                                       index,
        out LauncherPathEntry                     entry)
    {
        Unsafe.SkipInit(out entry);
        bool isAvailable = !entries.IsEmpty && entries.Length > index;

        if (isAvailable)
        {
            entry = entries[index];
        }

        return isAvailable;
    }
}
