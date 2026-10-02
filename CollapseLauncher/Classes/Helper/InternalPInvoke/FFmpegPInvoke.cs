using CollapseLauncher.Helper.InternalPInvoke.FFmpeg;
using Hi3Helper.Shared.Region;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.InternalPInvoke;

public static unsafe partial class FFmpegPInvoke
{
    static FFmpegPInvoke()
    {
        CollapsePInvoke.CustomResolvers.Add(Const.AVCodecPrefix,    FFmpegCustomDllImportResolver);
        CollapsePInvoke.CustomResolvers.Add(Const.AVDevicePrefix,   FFmpegCustomDllImportResolver);
        CollapsePInvoke.CustomResolvers.Add(Const.AVFilterPrefix,   FFmpegCustomDllImportResolver);
        CollapsePInvoke.CustomResolvers.Add(Const.AVFormatPrefix,   FFmpegCustomDllImportResolver);
        CollapsePInvoke.CustomResolvers.Add(Const.AVUtilPrefix,     FFmpegCustomDllImportResolver);
        CollapsePInvoke.CustomResolvers.Add(Const.SWResamplePrefix, FFmpegCustomDllImportResolver);
        CollapsePInvoke.CustomResolvers.Add(Const.SWScalePrefix,    FFmpegCustomDllImportResolver);
        CollapsePInvoke.CustomResolvers.Add(Const.PostprocPrefix,   FFmpegCustomDllImportResolver);
    }

    private const int    GlobalFFmpegDefaultVersionKey     = 80;
    private const string GlobalFFmpegCustomPathConfigKey   = "GlobalFFmpegCustomPath";
    private const string GlobalFFmpegVersionToUseConfigKey = "GlobalFFmpegVersionToUse";

    public static Dictionary<int, FFmpegLibraryNames> FFmpegVersionLibNames = new()
    {
        { 70, new FFmpegLibraryNames(61, 61, 10, 61, 59, 5, 8, 58) },
        { 80, new FFmpegLibraryNames(62, 62, 11, 62, 60, 6, 9, 59) }
    };

    public static int FFmpegVersionToUse
    {
        get
        {
            int version = LauncherConfig.GetAppConfigValue(GlobalFFmpegVersionToUseConfigKey);
            return FFmpegVersionLibNames.ContainsKey(version) ?
                version :
                GlobalFFmpegDefaultVersionKey;
        }
        set
        {
            if (!FFmpegVersionLibNames.ContainsKey(value))
            {
                return;
            }

            LauncherConfig.SetAndSaveConfigValue(GlobalFFmpegVersionToUseConfigKey, value);
        }
    }

    public static FFmpegLibraryNames FFmpegLibraryNamings
    {
        get
        {
            if (FFmpegVersionLibNames.TryGetValue(FFmpegVersionToUse, out FFmpegLibraryNames names))
            {
                return names;
            }

            return FFmpegVersionLibNames.TryGetValue(GlobalFFmpegDefaultVersionKey, out names) ? names :
                FFmpegVersionLibNames.Values.FirstOrDefault();
        }
    }

    public static string? CustomFFmpegPath
    {
        get => LauncherConfig.GetAppConfigValue(GlobalFFmpegCustomPathConfigKey);
        set => LauncherConfig.SetAndSaveConfigValue(GlobalFFmpegCustomPathConfigKey, value);
    }

    private static nint FFmpegCustomDllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        FFmpegLibraryNames namings = FFmpegLibraryNamings;
        string libraryFilename = libraryName switch
        {
            Const.AVCodecPrefix    => namings.Codec,
            Const.AVDevicePrefix   => namings.Device,
            Const.AVFilterPrefix   => namings.Filter,
            Const.AVFormatPrefix   => namings.Format,
            Const.AVUtilPrefix     => namings.Util,
            Const.SWResamplePrefix => namings.Resample,
            Const.SWScalePrefix    => namings.Scale,
            Const.PostprocPrefix   => namings.PostProc,
            _ => throw new InvalidOperationException($"{libraryName} is not included in FFmpegPInvoke list")
        };

        string? customPath = CustomFFmpegPath;

        // 1. Try to load from custom path first
        if (!string.IsNullOrEmpty(customPath) &&
            IsFFmpegAvailable(customPath, namings, out _) &&
            TryLoad(Path.Combine(customPath, libraryFilename), out nint handle))
        {
            return handle;
        }

        // 2. Try to find on the current linked path
        if (IsFFmpegAvailable(LauncherConfig.AppExecutableDir, namings, out _) &&
            TryLoad(Path.Combine(LauncherConfig.AppExecutableDir, libraryFilename), out handle))
        {
            return handle;
        }

        // 3. Try to find from environment variable
        if (TryFindFFmpegInstallFromEnvVar(namings, out string? envVarPath, out _) &&
            TryLoad(Path.Combine(envVarPath, libraryFilename), out handle))
        {
            return handle;
        }

        // Otherwise, return null
        return nint.Zero;

        static bool TryLoad(string fullPath, out nint handle)
        {
            if (FileUtility.IsFileExistOrSymbolicLinkResolved(fullPath, out string? resolvedPath, out _))
            {
                fullPath = resolvedPath;
            }

            return NativeLibrary.TryLoad(fullPath, out handle);
        }
    }

    internal static bool IsFFmpegAvailable(
        string?            checkOnDirectory,
        FFmpegLibraryNames libraries,
        [NotNullWhen(false)]
        out Exception? exception)
    {
        if (string.IsNullOrEmpty(checkOnDirectory))
        {
            exception = new NullReferenceException($"Argument: {nameof(checkOnDirectory)} is null!");
            return false;
        }

        checkOnDirectory = FileUtility.GetFullyQualifiedPath(checkOnDirectory);

        string dllPathAvcodec    = Path.Combine(checkOnDirectory, libraries.Codec);
        string dllPathAvdevice   = Path.Combine(checkOnDirectory, libraries.Device);
        string dllPathAvfilter   = Path.Combine(checkOnDirectory, libraries.Filter);
        string dllPathAvformat   = Path.Combine(checkOnDirectory, libraries.Format);
        string dllPathAvutil     = Path.Combine(checkOnDirectory, libraries.Util);
        string dllPathSwresample = Path.Combine(checkOnDirectory, libraries.Resample);
        string dllPathSwscale    = Path.Combine(checkOnDirectory, libraries.Scale);

        return FileUtility.IsFileExistOrSymbolicLinkResolved(dllPathAvcodec,    out _, out exception) &&
               FileUtility.IsFileExistOrSymbolicLinkResolved(dllPathAvdevice,   out _, out exception) &&
               FileUtility.IsFileExistOrSymbolicLinkResolved(dllPathAvfilter,   out _, out exception) &&
               FileUtility.IsFileExistOrSymbolicLinkResolved(dllPathAvformat,   out _, out exception) &&
               FileUtility.IsFileExistOrSymbolicLinkResolved(dllPathAvutil,     out _, out exception) &&
               FileUtility.IsFileExistOrSymbolicLinkResolved(dllPathSwresample, out _, out exception) &&
               FileUtility.IsFileExistOrSymbolicLinkResolved(dllPathSwscale,    out _, out exception);
    }

    internal static bool TryFindFFmpegInstallFromEnvVar(
        FFmpegLibraryNames                 libraries,
        [NotNullWhen(true)] out string?    path,
        out                     Exception? exception)
    {
        return FindIn(EnvironmentVariableTarget.User, out path, out exception) ||
               FindIn(EnvironmentVariableTarget.Machine, out path, out exception);

        bool FindIn(EnvironmentVariableTarget target, [NotNullWhen(true)] out string? innerPath, out Exception? exception)
        {
            const string separators = ";,";
            Unsafe.SkipInit(out innerPath);
            Unsafe.SkipInit(out exception);

            foreach (object? varValue in Environment.GetEnvironmentVariables(target))
            {
                if (varValue is not DictionaryEntry { Value: string varValueStr })
                {
                    continue;
                }

                ReadOnlySpan<char> envVar = varValueStr;
                foreach (Range envVarRange in envVar.SplitAny(separators))
                {
                    ReadOnlySpan<char> envVarPath = envVar[envVarRange].Trim(" '\"");
                    if (envVarPath.IsEmpty)
                    {
                        continue;
                    }

                    string thisPath = envVarPath.ToString();

                    if (!Path.IsPathFullyQualified(thisPath) ||
                        !IsFFmpegAvailable(thisPath, libraries, out exception)) continue;

                    innerPath = thisPath;
                    return true;
                }
            }

            return false;
        }
    }

    internal static string? FindFFmpegInstallFolder(string checkOnDirectory, FFmpegLibraryNames libraries)
    {
        try
        {
            if (IsFFmpegAvailable(checkOnDirectory, libraries, out _))
            {
                return checkOnDirectory;
            }

            foreach (string dirPath in FileUtility.EnumerateDirectoryRecursive(checkOnDirectory))
            {
                if (IsFFmpegAvailable(dirPath, libraries, out _))
                {
                    return dirPath;
                }
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    internal static unsafe MediaSupport GetMediaSupport(string filePath)
    {
        AVFormatContext* formatContext = null;
        int result = ExternFormat.avformat_open_input(&formatContext,
                                                      filePath,
                                                      null,
                                                      null);

        if (result < 0)
        {
            return default;
        }

        try
        {
            result = ExternFormat.avformat_find_stream_info(formatContext, null);
            if (result < 0)
            {
                return default;
            }

            StreamSupport video = CheckAVFormatStream(formatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, out int videoStreamIndex);
            StreamSupport audio = CheckAVFormatStream(formatContext, AVMediaType.AVMEDIA_TYPE_AUDIO, out _);

            VideoPixelFormatInfo videoPixelFormat = !video.DecoderAvailable
                ? default
                : GetAVFormatVideoPixelFormat(formatContext, videoStreamIndex);

            return new MediaSupport(video, videoPixelFormat, audio);
        }
        finally
        {
            ExternFormat.avformat_close_input(&formatContext);
        }
    }

    private static unsafe VideoPixelFormatInfo GetAVFormatVideoPixelFormat(AVFormatContext* context, int streamIndex)
    {
        AVStream*          stream      = context->streams[streamIndex];
        AVCodecParameters* parameters  = stream->codecpar;
        AVPixelFormat      pixelFormat = (AVPixelFormat)parameters->format;

        if (pixelFormat == AVPixelFormat.AV_PIX_FMT_NONE)
        {
            return new VideoPixelFormatInfo(pixelFormat,
                                            null,
                                            PixelColorModel.Unknown,
                                            false,
                                            0,
                                            0,
                                            0);
        }

        AVPixFmtDescriptor* descriptor = ExternUtil.av_pix_fmt_desc_get(pixelFormat);
        string?             name       = ToString(descriptor->Name);

        PixelColorModel model = GetPixelColorModel(descriptor, out bool isGrayscale);
        return new VideoPixelFormatInfo(pixelFormat,
                                        name,
                                        model,
                                        isGrayscale,
                                        descriptor->NbComponents,
                                        descriptor->Log2ChromaW,
                                        descriptor->Log2ChromaH);

        static string? ToString(byte* ptr) => ptr == null ? null : Marshal.PtrToStringUTF8((nint)ptr);
    }

    private static StreamSupport CheckAVFormatStream(
        AVFormatContext* context,
        AVMediaType     type,
        out int         streamIndex)
    {
        streamIndex = ExternFormat.av_find_best_stream(context,
                                                       type,
                                                       -1,
                                                       -1,
                                                       ref Unsafe.NullRef<nint>(),
                                                       0);

        if (streamIndex < 0)
        {
            return new StreamSupport(Exists: false,
                                     DecoderAvailable: false,
                                     StreamIndex: -1);
        }

        nint decoder = nint.Zero;
        int result = ExternFormat.av_find_best_stream(context,
                                                      type,
                                                      streamIndex,
                                                      -1,
                                                      ref decoder,
                                                      0);

        return new StreamSupport(Exists: true,
                                 DecoderAvailable: result >= 0 && decoder != nint.Zero,
                                 StreamIndex: streamIndex);
    }

    private static unsafe PixelColorModel GetPixelColorModel(AVPixFmtDescriptor* desc, out bool isGrayscale)
    {
        const ulong AV_PIX_FMT_FLAG_BE        = 1UL << 0;
        const ulong AV_PIX_FMT_FLAG_PAL       = 1UL << 1;
        const ulong AV_PIX_FMT_FLAG_BITSTREAM = 1UL << 2;
        const ulong AV_PIX_FMT_FLAG_HWACCEL   = 1UL << 3;
        const ulong AV_PIX_FMT_FLAG_PLANAR    = 1UL << 4;
        const ulong AV_PIX_FMT_FLAG_RGB       = 1UL << 5;
        const ulong AV_PIX_FMT_FLAG_ALPHA     = 1UL << 7;
        const ulong AV_PIX_FMT_FLAG_BAYER     = 1UL << 8;
        const ulong AV_PIX_FMT_FLAG_FLOAT     = 1UL << 9;
        const ulong AV_PIX_FMT_FLAG_XYZ       = 1UL << 10;

        ulong           flags       = desc->Flags;
        PixelColorModel pixelFormat = 0;

        if ((flags & AV_PIX_FMT_FLAG_BE) != 0)
            pixelFormat |= PixelColorModel.BigEndian;

        if ((flags & AV_PIX_FMT_FLAG_PAL) != 0)
            pixelFormat |= PixelColorModel.Pal;

        if ((flags & AV_PIX_FMT_FLAG_BITSTREAM) != 0)
            pixelFormat |= PixelColorModel.Bitstream;

        if ((flags & AV_PIX_FMT_FLAG_HWACCEL) != 0)
            pixelFormat |= PixelColorModel.Hardware;

        if ((flags & AV_PIX_FMT_FLAG_PLANAR) != 0)
            pixelFormat |= PixelColorModel.Planar;

        if ((flags & AV_PIX_FMT_FLAG_RGB) != 0)
            pixelFormat |= PixelColorModel.Rgb;

        if ((flags & AV_PIX_FMT_FLAG_ALPHA) != 0)
            pixelFormat |= PixelColorModel.Rgb;

        if ((flags & AV_PIX_FMT_FLAG_BAYER) != 0)
            pixelFormat |= PixelColorModel.Bayer;

        if ((flags & AV_PIX_FMT_FLAG_FLOAT) != 0)
            pixelFormat |= PixelColorModel.Float;

        if ((flags & AV_PIX_FMT_FLAG_XYZ) != 0)
            pixelFormat |= PixelColorModel.Xyz;

        isGrayscale = desc->NbComponents <= 2;
        return pixelFormat;
    }
}
