// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

using System;

#pragma warning disable IDE0130

namespace CollapseLauncher.Helper.InternalPInvoke.FFmpeg;

[Flags]
public enum PixelColorModel : ulong
{
    Unknown,
    BigEndian = 1UL << 0,
    Pal       = 1UL << 1,
    Bitstream = 1UL << 2,
    Hardware  = 1UL << 3,
    Planar    = 1UL << 4,
    Rgb       = 1UL << 5,
    Alpha     = 1UL << 7,
    Bayer     = 1UL << 8,
    Float     = 1UL << 9,
    Xyz       = 1UL << 10,
}