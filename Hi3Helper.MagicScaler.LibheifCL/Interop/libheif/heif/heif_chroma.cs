// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal enum heif_chroma
{
    heif_chroma_undefined = 99,
    heif_chroma_planar = 0,
    heif_chroma_420 = 1,
    heif_chroma_422 = 2,
    heif_chroma_444 = 3,
    heif_chroma_interleaved_RGB = 10,
    heif_chroma_interleaved_RGBA = 11,
    heif_chroma_interleaved_RRGGBB_BE = 12,
    heif_chroma_interleaved_RRGGBBAA_BE = 13,
    heif_chroma_interleaved_RRGGBB_LE = 14,
    heif_chroma_interleaved_RRGGBBAA_LE = 15,
}
