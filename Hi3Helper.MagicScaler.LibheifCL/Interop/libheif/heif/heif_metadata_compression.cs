// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal enum heif_metadata_compression
{
    heif_metadata_compression_off = 0,
    heif_metadata_compression_auto = 1,
    heif_metadata_compression_unknown = 2,
    heif_metadata_compression_deflate = 3,
    heif_metadata_compression_zlib = 4,
    heif_metadata_compression_brotli = 5,
}
