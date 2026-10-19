// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal enum heif_channel
{
    heif_channel_Y = 0,
    heif_channel_Cb = 1,
    heif_channel_Cr = 2,
    heif_channel_R = 3,
    heif_channel_G = 4,
    heif_channel_B = 5,
    heif_channel_Alpha = 6,
    heif_channel_interleaved = 10,
    heif_channel_filter_array = 11,
    heif_channel_depth = 12,
    heif_channel_disparity = 13,
    heif_channel_unknown = 65535,
}
