using CollapseLauncher.Helper.InternalPInvoke.FFmpeg;
using System.Runtime.InteropServices;
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
#pragma warning disable IDE0130

namespace CollapseLauncher.Helper.InternalPInvoke;

public static unsafe partial class FFmpegPInvoke
{
    public static unsafe partial class ExternUtil
    {
        [LibraryImport(Const.AVUtilPrefix,
                       EntryPoint = "av_pix_fmt_desc_get")]
        public static partial AVPixFmtDescriptor* av_pix_fmt_desc_get(
            AVPixelFormat pixelFormat);
    }
}
