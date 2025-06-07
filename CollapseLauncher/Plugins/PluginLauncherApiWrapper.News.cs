using CollapseLauncher.Helper.LauncherApiLoader.Legacy;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Core.Utility;
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
            using var entry = entrySpan[i];

            string?               title       = entry.GetTitleString();
            // string?               description = entry.GetDescriptionString(); // TODO: Add description as tooltip for the news entry on UI level
            string?               clickUrl    = entry.GetUrlString();
            string?               postDate    = entry.GetPostDateString();
            LauncherNewsEntryType newsType    = entry.Type;

            if (string.IsNullOrEmpty(clickUrl) ||
                string.IsNullOrEmpty(title))
            {
                continue;
            }

            newsData.NewsPost ??= [];
            newsData.NewsPost.Add(new LauncherGameNewsPost
            {
                PostDate  = postDate,
                PostId    = Guid.CreateVersion7().ToString(),
                Title     = title,
                PostOrder = i,
                PostUrl   = clickUrl,
                PostType  = newsType switch
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
            using var entry = entrySpan[i];

            string? imageUrl  = entry.ImageUrl.CreateStringFromNullTerminated();
            string? imagePath = await CopyOverUrlData(spriteFolder, imageUrl, token);
            if (string.IsNullOrEmpty(imagePath))
            {
                continue;
            }

            string? description = entry.Description.CreateStringFromNullTerminated();
            string? clickUrl    = entry.ClickUrl.CreateStringFromNullTerminated();

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
