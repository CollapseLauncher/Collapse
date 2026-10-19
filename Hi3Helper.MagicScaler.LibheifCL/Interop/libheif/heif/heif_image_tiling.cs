// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal unsafe partial struct heif_image_tiling
{
    public int version;

    public uint num_columns;

    public uint num_rows;

    public uint tile_width;

    public uint tile_height;

    public uint image_width;

    public uint image_height;

    public uint top_offset;

    public uint left_offset;

    public byte number_of_extra_dimensions;

    public fixed uint extra_dimension_size[8];
}
