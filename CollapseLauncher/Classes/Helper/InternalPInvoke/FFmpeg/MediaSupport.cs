// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
#pragma warning disable IDE0130

#nullable enable

namespace CollapseLauncher.Helper.InternalPInvoke.FFmpeg;
internal readonly record struct StreamSupport(
    bool Exists,
    bool DecoderAvailable,
    int  StreamIndex);

internal readonly record struct VideoPixelFormatInfo(
    AVPixelFormat   Format,
    string?         Name,
    PixelColorModel ColorModel,
    bool            IsGrayscale,
    int             ComponentCount,
    int             Log2ChromaWidth,
    int             Log2ChromaHeight);

internal readonly record struct MediaSupport(
    StreamSupport        Video,
    VideoPixelFormatInfo VideoPixelFormatInfo,
    StreamSupport        Audio)
{
    public bool IsSupported =>
        (!Video.Exists || Video.DecoderAvailable) &&
        (!Audio.Exists || Audio.DecoderAvailable);
}