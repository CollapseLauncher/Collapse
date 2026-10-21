// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal enum heif_colorspace
{
    heif_colorspace_undefined = 99,
    heif_colorspace_YCbCr = 0,
    heif_colorspace_RGB = 1,
    heif_colorspace_monochrome = 2,
    heif_colorspace_custom = 3,
    heif_colorspace_filter_array = 4,
}
