using System.Runtime.InteropServices;
// ReSharper disable IdentifierTypo
// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
#pragma warning disable IDE0130

namespace CollapseLauncher.Helper.InternalPInvoke.FFmpeg;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVFormatContext
{
    public void* av_class;
    public void* iformat;
    public void* oformat;
    public void* priv_data;
    public void* pb;

    public int  ctx_flags;
    public uint nb_streams;

    public AVStream** streams;
}