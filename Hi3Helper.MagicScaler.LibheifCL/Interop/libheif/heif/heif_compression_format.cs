// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal enum heif_compression_format
{
    heif_compression_undefined = 0,
    heif_compression_HEVC = 1,
    heif_compression_AVC = 2,
    heif_compression_JPEG = 3,
    heif_compression_AV1 = 4,
    heif_compression_VVC = 5,
    heif_compression_EVC = 6,
    heif_compression_JPEG2000 = 7,
    heif_compression_uncompressed = 8,
    heif_compression_mask = 9,
    heif_compression_HTJ2K = 10,
}
