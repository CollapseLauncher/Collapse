using CollapseLauncher.Helper.InternalPInvoke.FFmpeg;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#nullable enable
namespace CollapseLauncher.Helper.FFmpegPInvoke;

public static unsafe partial class FFmpegPInvoke
{
    public static Dictionary<int, FFmpegLibraryNames> FFmpegVersionLibNames = new Dictionary<int, FFmpegLibraryNames>()
    {
        { 70, new FFmpegLibraryNames("avcodec-61.dll", "avdevice-61.dll", "avfilter-10.dll", "avformat-61.dll", "avutil-59.dll", "swresample-5.dll", "swscale-8.dll", "postproc-58.dll") },
        { 80, new FFmpegLibraryNames("avcodec-62.dll", "avdevice-62.dll", "avfilter-11.dll", "avformat-62.dll", "avutil-60.dll", "swresample-6.dll", "swscale-9.dll", "postproc-59.dll") }
    };

    [LibraryImport("avcodec")]
    public static partial AVCodec* av_codec_iterate(ref nint opaque);

    public static IEnumerable<AVCodec> EnumerateSupportedCodecs()
        => EnumerateSupportedCodecs([]);

    public static IEnumerable<AVCodec> EnumerateSupportedCodecs(params AVMediaType[] codecTypes)
    {
        nint opaque = nint.Zero;
        scoped ref AVCodec codecPtr = ref Unsafe.NullRef<AVCodec>();

    Iterate:
        codecPtr = ref IterateCodec(ref opaque);
        if (Unsafe.IsNullRef(in codecPtr))
        {
            yield break;
        }

        if (codecTypes.Length != 0 &&
            !codecTypes.Contains(codecPtr.Type))
        {
            goto Iterate;
        }

        yield return codecPtr;
        goto Iterate;
    }

    private static ref AVCodec IterateCodec(ref nint opaque)
        => ref Unsafe.AsRef<AVCodec>(av_codec_iterate(ref opaque));

    public record struct FFmpegLibraryNames(string Codec, string Device, string Filter, string Format, string Util, string Resample, string Scale, string PostProc);
}
