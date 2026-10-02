using System.Runtime.InteropServices;
// ReSharper disable IdentifierTypo
// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
#pragma warning disable IDE0130

namespace CollapseLauncher.Helper.InternalPInvoke.FFmpeg;

[StructLayout(LayoutKind.Sequential)]
public struct AVCodecParameters
{
    public AVMediaType codec_type;
    public AVCodecID   codec_id;

    public uint codec_tag;

    public byte* extradata;
    public int   extradata_size;

    public void* coded_side_data;
    public int   nb_coded_side_data;

    public int format;
}