// ReSharper disable IdentifierTypo
#pragma warning disable IDE0130
namespace CollapseLauncher.Helper.InternalPInvoke.FFmpeg;

public struct FFmpegLibraryNames
{
    internal FFmpegLibraryNames(
        int codecNum,
        int deviceNum,
        int filterNum,
        int formatNum,
        int utilNum,
        int resampleNum,
        int scaleNum,
        int postprocNum)
    {
        Codec    = Const.AVCodecPrefix + '-' + codecNum + CollapsePInvoke.LibraryExtension;
        Device   = Const.AVDevicePrefix + '-' + deviceNum + CollapsePInvoke.LibraryExtension;
        Filter   = Const.AVFilterPrefix + '-' + filterNum + CollapsePInvoke.LibraryExtension;
        Format   = Const.AVFormatPrefix + '-' + formatNum + CollapsePInvoke.LibraryExtension;
        Util     = Const.AVUtilPrefix + '-' + utilNum + CollapsePInvoke.LibraryExtension;
        Resample = Const.SWResamplePrefix + '-' + resampleNum + CollapsePInvoke.LibraryExtension;
        Scale    = Const.SWScalePrefix + '-' + scaleNum + CollapsePInvoke.LibraryExtension;
        PostProc = Const.PostprocPrefix + '-' + postprocNum + CollapsePInvoke.LibraryExtension;
    }

    public string Codec;
    public string Device;
    public string Filter;
    public string Format;
    public string Util;
    public string Resample;
    public string Scale;
    public string PostProc;
}
