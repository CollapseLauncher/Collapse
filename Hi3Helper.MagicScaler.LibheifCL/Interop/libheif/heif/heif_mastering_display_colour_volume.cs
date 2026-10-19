// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal unsafe partial struct heif_mastering_display_colour_volume
{
    public fixed ushort display_primaries_x[3];

    public fixed ushort display_primaries_y[3];

    public ushort white_point_x;

    public ushort white_point_y;

    public uint max_display_mastering_luminance;

    public uint min_display_mastering_luminance;
}
