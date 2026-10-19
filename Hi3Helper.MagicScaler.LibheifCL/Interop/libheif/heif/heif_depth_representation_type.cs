// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal enum heif_depth_representation_type
{
    heif_depth_representation_type_uniform_inverse_Z = 0,
    heif_depth_representation_type_uniform_disparity = 1,
    heif_depth_representation_type_uniform_Z = 2,
    heif_depth_representation_type_nonuniform_disparity = 3,
}
