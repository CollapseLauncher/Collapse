// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal enum heif_cmpd_component_type
{
    heif_cmpd_component_type_monochrome = 0,
    heif_cmpd_component_type_Y = 1,
    heif_cmpd_component_type_Cb = 2,
    heif_cmpd_component_type_Cr = 3,
    heif_cmpd_component_type_red = 4,
    heif_cmpd_component_type_green = 5,
    heif_cmpd_component_type_blue = 6,
    heif_cmpd_component_type_alpha = 7,
    heif_cmpd_component_type_depth = 8,
    heif_cmpd_component_type_disparity = 9,
    heif_cmpd_component_type_palette = 10,
    heif_cmpd_component_type_filter_array = 11,
    heif_cmpd_component_type_padded = 12,
    heif_cmpd_component_type_cyan = 13,
    heif_cmpd_component_type_magenta = 14,
    heif_cmpd_component_type_yellow = 15,
    heif_cmpd_component_type_key_black = 16,
}
