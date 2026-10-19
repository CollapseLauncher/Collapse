// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal enum heif_omaf_image_projection
{
    heif_omaf_image_projection_equirectangular = 0x00,
    heif_omaf_image_projection_cube_map = 0x01,
    heif_omaf_image_projection_flat = 0xFF,
}
