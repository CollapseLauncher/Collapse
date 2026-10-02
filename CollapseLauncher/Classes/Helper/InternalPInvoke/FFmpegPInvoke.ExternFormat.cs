using CollapseLauncher.Helper.InternalPInvoke.FFmpeg;
using System.Runtime.InteropServices;
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
#pragma warning disable IDE0130

namespace CollapseLauncher.Helper.InternalPInvoke;

public static unsafe partial class FFmpegPInvoke
{
    public static unsafe partial class ExternFormat
    {
        [LibraryImport(Const.AVFormatPrefix,
                       EntryPoint = "avformat_open_input",
                       StringMarshalling = StringMarshalling.Utf8)]
        public static partial int avformat_open_input(
            AVFormatContext** context,
            string            url,
            void*             fmt,
            void*             options);

        [LibraryImport(Const.AVFormatPrefix,
                       EntryPoint = "avformat_find_stream_info")]
        public static partial int avformat_find_stream_info(
            AVFormatContext* context,
            void*           options);

        [LibraryImport(Const.AVFormatPrefix,
                       EntryPoint = "av_find_best_stream")]
        public static partial int av_find_best_stream(
            AVFormatContext* context,
            AVMediaType      type,
            int              wantedStreamNumber,
            int              relatedStream,
            ref nint         decoder,
            int              flags);

        [LibraryImport(Const.AVFormatPrefix,
                       EntryPoint = "avformat_close_input")]
        public static partial void avformat_close_input(
            AVFormatContext** context);
    }
}
