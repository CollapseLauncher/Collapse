// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal enum heif_orientation
{
    heif_orientation_normal = 1,
    heif_orientation_flip_horizontally = 2,
    heif_orientation_rotate_180 = 3,
    heif_orientation_flip_vertically = 4,
    heif_orientation_rotate_90_cw_then_flip_horizontally = 5,
    heif_orientation_rotate_90_cw = 6,
    heif_orientation_rotate_90_cw_then_flip_vertically = 7,
    heif_orientation_rotate_270_cw = 8,
}
