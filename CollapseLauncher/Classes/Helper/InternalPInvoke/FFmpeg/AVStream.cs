using System.Runtime.InteropServices;
// ReSharper disable IdentifierTypo
// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
#pragma warning disable IDE0130

namespace CollapseLauncher.Helper.InternalPInvoke.FFmpeg;

[StructLayout(LayoutKind.Sequential)]
public struct AVStream
{
    public void* av_class;

    public int index;
    public int id;

    public AVCodecParameters* codecpar;
}