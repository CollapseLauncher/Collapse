using FFmpegInteropX;
using Hi3Helper.Data;
using Hi3Helper.Win32.WinRT.WindowsStream;
using Microsoft.UI;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;
// ReSharper disable InconsistentNaming
// ReSharper disable AccessToDisposedClosure
// ReSharper disable AccessToModifiedClosure
// ReSharper disable IdentifierTypo

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

internal static partial class LayeredBackgroundImageExtensions
{
    internal static async Task<(MediaPlayer? MediaPlayer, FFmpegMediaSource? FFmpegMediaSource)>
        GetMediaSourceAsync(
            object?           source,
            bool              useFFmpeg,
            VideoDecoderMode  ffmpegDecoderMode,
            WindowId          windowId,
            bool              isAutoPlay,
            bool              isMuted,
            Action?           onMediaOpened  = null,
            TimeSpan          lastOpenedTime = default,
            CancellationToken token          = default)
    {
        if (token.IsCancellationRequested)
            return (null, null);

        MediaPlayer? mediaPlayer = new()
        {
            IsLoopingEnabled          = true,
            IsVideoFrameServerEnabled = true,
            IsMuted                   = isMuted,
            CommandManager            = { IsEnabled = false },
            AutoPlay                  = isAutoPlay,
            Volume                    = 0
        };

        if (onMediaOpened != null)
        {
            mediaPlayer.MediaOpened += MediaPlayerOnMediaOpened;
        }

        IMediaPlaybackSource? mediaPlaybackSource = null;
        FFmpegMediaSource?    ffmpegMediaSource   = null;

        try
        {
            mediaPlaybackSource = !useFFmpeg ? CreateMediaSourceFromMFTAsync(source) : null;

            if (useFFmpeg)
            {
                MediaSourceConfig ffmpegConfig = new()
                {
                    Video =
                    {
                        MaxDecoderThreads = (uint)Environment.ProcessorCount,
                        VideoDecoderMode  = ffmpegDecoderMode
                    },
                    General =
                    {
                        ReadAheadBufferEnabled  = true,
                        ReadAheadBufferDuration = TimeSpan.FromSeconds(15)
                    }
                };

                if (token.IsCancellationRequested)
                    return (null, null);

                if (source is IRandomAccessStream or Stream)
                {
                    IRandomAccessStream? randomStream = source as IRandomAccessStream;
                    randomStream ??= (source as Stream)?.AsRandomAccessStream(true, true);

                    ffmpegMediaSource = await FFmpegMediaSource
                       .CreateFromStreamAsync(randomStream,
                                              ffmpegConfig,
                                              windowId.Value);
                }

                if (source is Uri sourceAsUri ||
                    (source is string sourceUriAsString &&
                     Uri.TryCreate(sourceUriAsString, UriKind.Absolute, out sourceAsUri!)))
                {
                    if (sourceAsUri.IsFile)
                    {
                        string fullyQualifiedPath = sourceAsUri.LocalPath.GetFullyQualifiedPath();
                        ffmpegMediaSource = await FFmpegMediaSource
                           .CreateFromFileAsync(fullyQualifiedPath,
                                                ffmpegConfig,
                                                windowId.Value);
                    }

                    ffmpegMediaSource ??= await FFmpegMediaSource
                       .CreateFromUriAsync(sourceAsUri.ToString(),
                                           ffmpegConfig,
                                           windowId.Value);
                }

                if (source is string sourceAsString)
                {
                    sourceAsString = sourceAsString.GetFullyQualifiedPath();
                    if (Uri.TryCreate(sourceAsString, UriKind.Absolute, out Uri? localPathUri))
                    {
                        ffmpegMediaSource = await FFmpegMediaSource
                           .CreateFromFileAsync(localPathUri.LocalPath,
                                                ffmpegConfig,
                                                windowId.Value);
                    }
                }

                if (ffmpegMediaSource != null)
                {
                    mediaPlaybackSource = ffmpegMediaSource.CreateMediaPlaybackItem(lastOpenedTime);
                }
            }

            if (mediaPlaybackSource != null)
            {
                mediaPlayer.Source = mediaPlaybackSource;
            }
        }
        finally
        {
            if (mediaPlaybackSource == null)
            {
                mediaPlayer.Dispose();
                mediaPlayer = null;
            }
        }

        return (mediaPlayer, ffmpegMediaSource);

        void MediaPlayerOnMediaOpened(MediaPlayer sender, object args)
        {
            mediaPlayer.MediaOpened -= MediaPlayerOnMediaOpened;
            onMediaOpened();
        }
    }

    private static MediaSource? CreateMediaSourceFromMFTAsync(object? source)
    {
        try
        {
            if (source is IRandomAccessStream or Stream)
            {
                IRandomAccessStream? randomStream = source as IRandomAccessStream;
                randomStream ??= (source as Stream)?.AsRandomAccessStream(true, true);

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