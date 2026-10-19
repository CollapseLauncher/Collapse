// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal enum heif_color_profile_type
{
    heif_color_profile_type_not_present = 0,
    heif_color_profile_type_nclx = (('n' << 24) | ('c' << 16) | ('l' << 8) | 'x'),
    heif_color_profile_type_rICC = (('r' << 24) | ('I' << 16) | ('C' << 8) | 'C'),
    heif_color_profile_type_prof = (('p' << 24) | ('r' << 16) | ('o' << 8) | 'f'),
}
