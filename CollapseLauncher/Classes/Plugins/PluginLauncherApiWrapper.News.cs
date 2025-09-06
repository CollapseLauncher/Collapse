using CollapseLauncher.Helper.LauncherApiLoader.Legacy;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Shared.Region;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher.Plugins;

#nullable enable

internal partial class PluginLauncherApiWrapper
{
    internal async Task ConvertNewsAndCarouselEntries(LauncherGameNewsData newsData, CancellationToken token)
    {
        using PluginDisposableMemory<LauncherNewsEntry> newsEntry = PluginDisposableMemoryExtension.ToManagedSpan<LauncherNewsEntry>(_pluginNewsApi.GetNewsEntries);
        if (!newsEntry.IsEmpty)
        {
            ConvertNewsEntriesInner(newsData, newsEntry);
        }

        using PluginDisposableMemory<LauncherCarouselEntry> carouselEntry = PluginDisposableMemoryExtension.ToManagedSpan<LauncherCarouselEntry>(_pluginNewsApi.GetCarouselEntries);
        if (!carouselEntry.IsEmpty)
        {
            await ConvertCarouselEntriesInner(newsData, carouselEntry, token);
        }
    }

    internal static void ConvertNewsEntriesInner(LauncherGameNewsData newsData, PluginDisposableMemory<LauncherNewsEntry> entrySpan)
    {
        int count = entrySpan.Length;
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

            newsData.NewsPost ??= [];
            newsData.NewsPost.Add(new LauncherGameNewsPost
            {
                PostDate    = postDate,
                PostId      = Guid.CreateVersion7().ToString(),
                Title       = title,
                Description = description,
                PostOrder   = i,
                PostUrl     = clickUrl,
                PostType    = newsType switch
                              {
                                  LauncherNewsEntryType.Info => LauncherGameNewsPostType.POST_TYPE_INFO,
                                  LauncherNewsEntryType.Notice => LauncherGameNewsPostType.POST_TYPE_ANNOUNCE,
                                  LauncherNewsEntryType.Event => LauncherGameNewsPostType.POST_TYPE_ACTIVITY,
                                  _ => LauncherGameNewsPostType.POST_TYPE_INFO
                              }
            });
        }
    }

    internal async Task ConvertCarouselEntriesInner(LauncherGameNewsData newsData, PluginDisposableMemory<LauncherCarouselEntry> entrySpan, CancellationToken token)
    {
        string spriteFolder = Path.Combine(LauncherConfig.AppGameImgFolder, "cached");

        int count = entrySpan.Length;
        for (int i = 0; i < count; i++)
        {
            using LauncherCarouselEntry entry = entrySpan[i];

            string? imageUrl  = entry.ImageUrl;
            string? imagePath = await CopyOverUrlData(_plugin, _pluginNewsApi, spriteFolder, imageUrl, token);
            if (string.IsNullOrEmpty(imagePath))
            {
                continue;
            }

            string? description = entry.Description;
            string? clickUrl    = entry.ClickUrl;

            newsData.NewsCarousel ??= [];
            newsData.NewsCarousel.Add(new LauncherGameNewsCarousel
            {
                CarouselId         = Guid.CreateVersion7().ToString(),
                CarouselImg        = imageUrl,
                CarouselOrder      = i,
                CarouselTitle      = description,
                CarouselUrl        = clickUrl,
                IsImageUrlHashable = false
            });
        }
    }
}
