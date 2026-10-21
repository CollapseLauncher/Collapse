// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal partial struct heif_color_profile_nclx
{
    public byte version;

    public heif_color_primaries color_primaries;

    public heif_transfer_characteristics transfer_characteristics;

    public heif_matrix_coefficients matrix_coefficients;

    public byte full_range_flag;

    public float color_primary_red_x;

    public float color_primary_red_y;

    public float color_primary_green_x;

    public float color_primary_green_y;

    public float color_primary_blue_x;

    public float color_primary_blue_y;

    public float color_primary_white_x;

    public float color_primary_white_y;
}
