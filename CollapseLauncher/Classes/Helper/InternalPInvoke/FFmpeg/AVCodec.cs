using System;
using System.Runtime.InteropServices;
using System.Text;

#nullable enable
namespace CollapseLauncher.Helper.InternalPInvoke.FFmpeg;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVCodec
{
    public byte* _Name;
    public byte* _LongName;
    public AVMediaType Type;
    public AVCodecID Id;
    public int Capabilities;
    public byte MaxLowRes;
    public AVRational* SupportedFramerates;
    public AVPixelFormat* SupportedPixelFormats;
    public int* SupportedSampleRates;
    public AVSampleFormat* SupportedSampleFormats;
    public void* PrivateClass; // AVClass
    public void* Profiles; // AVProfiles
    public byte* _WrapperName;
    public void* ChannelLayouts; // AVChannelLayout

    public readonly string? Name => GetUtf8StringFrom(_Name);
    public readonly string? LongName => GetUtf8StringFrom(_LongName);
    public readonly string? WrapperName => GetUtf8StringFrom(_WrapperName);

    private static string? GetUtf8StringFrom(byte* ptr)
    {
        ReadOnlySpan<byte> bytes = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(ptr);
        return Encoding.UTF8.GetString(bytes);
    }
}
