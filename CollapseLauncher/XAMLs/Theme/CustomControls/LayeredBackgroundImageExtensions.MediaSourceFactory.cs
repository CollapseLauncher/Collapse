using FFmpegInteropX;
using Hi3Helper.Data;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Storage.Streams;
// ReSharper disable InconsistentNaming

// ReSharper disable IdentifierTypo

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

internal static partial class LayeredBackgroundImageExtensions
{
    internal static async Task<object?> CreateMediaSourceFromObjectAsync(
        object?          source,
        bool             useFFmpeg,
        VideoDecoderMode ffmpegDecoderMode)
    {
        return useFFmpeg
            ? await CreateMediaSourceFromFFmpegAsync(source, ffmpegDecoderMode)
            : await CreateMediaSourceFromMFTAsync(source);
    }

    private static async Task<FFmpegMediaSource?> CreateMediaSourceFromFFmpegAsync(object? source, VideoDecoderMode decoderMode)
    {
        MediaSourceConfig ffmpegConfig = new()
        {
            Video =
            {
                MaxDecoderThreads     = (uint)Environment.ProcessorCount,
                VideoOutputAllow10bit = true,
                VideoOutputAllowBgra8 = true,
                VideoOutputAllowNv12  = true,
                VideoDecoderMode      = decoderMode
            },
            General =
            {
                ReadAheadBufferEnabled  = true,
                ReadAheadBufferDuration = TimeSpan.FromSeconds(15)
            }
        };

        try
        {
            if (source is IRandomAccessStream or Stream)
            {
                IRandomAccessStream? randomStream = source as IRandomAccessStream;
                randomStream ??= (source as Stream)?.AsRandomAccessStream();

                return await FFmpegMediaSource.CreateFromStreamAsync(randomStream, ffmpegConfig);
            }

            if (source is Uri sourceAsUri ||
                (source is string sourceUriAsString &&
                 Uri.TryCreate(sourceUriAsString, UriKind.Absolute, out sourceAsUri!)))
            {
                if (sourceAsUri.IsFile)
                {
                    string fullyQualifiedPath = sourceAsUri.LocalPath.GetFullyQualifiedPath();
                    return await FFmpegMediaSource.CreateFromFileAsync(fullyQualifiedPath,
                                                                       ffmpegConfig);
                }
                return await FFmpegMediaSource.CreateFromUriAsync(sourceAsUri.ToString(),
                                                                  ffmpegConfig);
            }

            if (source is string sourceAsString)
            {
                sourceAsString = sourceAsString.GetFullyQualifiedPath();
                if (Uri.TryCreate(sourceAsString, UriKind.Absolute, out Uri? localPathUri))
                {
                    return await FFmpegMediaSource.CreateFromFileAsync(localPathUri.LocalPath, ffmpegConfig);
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<MediaSource?> CreateMediaSourceFromMFTAsync(object? source)
    {
        try
        {
            if (source is IRandomAccessStream or Stream)
            {
                IRandomAccessStream? randomStream = source as IRandomAccessStream;
                randomStream ??= (source as Stream)?.AsRandomAccessStream();

                return MediaSource.CreateFromStream(randomStream, null);
            }

            if (source is Uri sourceAsUri ||
                (source is string sourceUriAsString &&
                 Uri.TryCreate(sourceUriAsString, UriKind.Absolute, out sourceAsUri!)))
            {
                return MediaSource.CreateFromUri(sourceAsUri);
            }

            if (source is string sourceAsString)
            {
                sourceAsString = sourceAsString.GetFullyQualifiedPath();
                if (Uri.TryCreate(sourceAsString, UriKind.Absolute, out Uri? localPathUri))
                {
                    return MediaSource.CreateFromUri(localPathUri);
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}