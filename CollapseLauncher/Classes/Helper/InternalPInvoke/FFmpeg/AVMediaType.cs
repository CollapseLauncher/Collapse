namespace CollapseLauncher.Helper.InternalPInvoke.FFmpeg;

// https://ffmpeg.org/doxygen/7.0/group__lavu__misc.html#ga9a84bba4713dfced21a1a56163be1f48
public enum AVMediaType
{
    AVMEDIA_TYPE_UNKNOWN = -1,
    AVMEDIA_TYPE_VIDEO,
    AVMEDIA_TYPE_AUDIO,
    AVMEDIA_TYPE_DATA,
    AVMEDIA_TYPE_SUBTITLE,
    AVMEDIA_TYPE_ATTACHMENT,
    AVMEDIA_TYPE_NB
}
