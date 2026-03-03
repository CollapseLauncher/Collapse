using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.Api;
using System;
using System.Collections.Generic;
#pragma warning disable IDE0130

namespace CollapseLauncher.Plugins;

#nullable enable

internal sealed partial class PluginLauncherApiWrapper
{
    private void ConvertNewsAndCarouselEntries(HypLauncherContentApi contentApi)
    {
        contentApi.Data         ??= new HypLauncherContentData();
        contentApi.Data.Content ??= new HypLauncherContentKind();

        List<HypLauncherMediaContentData>    newsList     = contentApi.Data.Content.News;
        List<HypLauncherCarouselContentData> carouselList = contentApi.Data.Content.Carousel;

        newsList.Clear();
        contentApi.Data.Content.ResetCachedNews();
        carouselList.Clear();

        using PluginDisposableMemory<LauncherNewsEntry> newsEntry = PluginDisposableMemoryExtension.ToManagedSpan<LauncherNewsEntry>(_pluginNewsApi.GetNewsEntries);
        if (!newsEntry.IsEmpty)
        {
            ConvertNewsEntriesInner(newsList, newsEntry);
        }

        using PluginDisposableMemory<LauncherCarouselEntry> carouselEntry = PluginDisposableMemoryExtension.ToManagedSpan<LauncherCarouselEntry>(_pluginNewsApi.GetCarouselEntries);
        if (!carouselEntry.IsEmpty)
        {
            ConvertCarouselEntriesInner(carouselList, carouselEntry);
        }
    }

    private static void ConvertNewsEntriesInner(List<HypLauncherMediaContentData>         contentList,
                                                PluginDisposableMemory<LauncherNewsEntry> entrySpan)
    {
        int count = entrySpan.Length;
        if (count == 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            using LauncherNewsEntry entry = entrySpan[i];

            string?               title       = entry.Title;
            string?               clickUrl    = entry.Url;
            string?               postDate    = entry.PostDate;
            LauncherNewsEntryType newsType    = entry.Type;
            string?               description = entry.Description;

            if (string.IsNullOrEmpty(clickUrl) ||
                string.IsNullOrEmpty(title))
            {
                continue;
            }

            contentList.Add(new HypLauncherMediaContentData
            {
                Title       = title,
                Description = description,
                ClickLink   = clickUrl,
                Date        = postDate,

                ContentType = newsType switch
                              {
                                  LauncherNewsEntryType.Info   => LauncherGameNewsPostType.POST_TYPE_INFO,
                                  LauncherNewsEntryType.Notice => LauncherGameNewsPostType.POST_TYPE_ANNOUNCE,
                                  LauncherNewsEntryType.Event  => LauncherGameNewsPostType.POST_TYPE_ACTIVITY,
                                  _                            => LauncherGameNewsPostType.POST_TYPE_INFO
                              }
            });
        }
    }

    private static void ConvertCarouselEntriesInner(List<HypLauncherCarouselContentData>          contentList,
                                                    PluginDisposableMemory<LauncherCarouselEntry> entrySpan)
    {
        int count = entrySpan.Length;
        if (count == 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            using LauncherCarouselEntry entry = entrySpan[i];

            string? imageUrl  = entry.ImageUrl;
            string? imagePath = ImageLoaderHelper.CopyToLocalIfBase64(imageUrl);
            if (string.IsNullOrEmpty(imagePath))
            {
                continue;
            }

            string? description = entry.Description;
            string? clickUrl    = entry.ClickUrl;

            contentList.Add(new HypLauncherCarouselContentData
            {
                Id = Guid.CreateVersion7().ToString(),
                Image = new HypLauncherMediaContentData
                {
                    ClickLink   = clickUrl,
                    ContentType = LauncherGameNewsPostType.POST_TYPE_INFO, // Dummy
                    ImageUrl    = imageUrl,
                    Title       = description
                }
            });
        }
    }
}
