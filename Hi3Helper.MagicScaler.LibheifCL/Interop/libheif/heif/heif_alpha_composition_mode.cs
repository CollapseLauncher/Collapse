// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal enum heif_alpha_composition_mode
{
    heif_alpha_composition_mode_none,
    heif_alpha_composition_mode_solid_color,
    heif_alpha_composition_mode_checkerboard,
}
