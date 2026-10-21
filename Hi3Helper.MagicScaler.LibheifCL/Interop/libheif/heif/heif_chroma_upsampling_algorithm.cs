// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal enum heif_chroma_upsampling_algorithm
{
    heif_chroma_upsampling_nearest_neighbor = 1,
    heif_chroma_upsampling_bilinear = 2,
}
