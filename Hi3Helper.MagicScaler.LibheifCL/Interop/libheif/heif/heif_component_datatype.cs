// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal enum heif_component_datatype
{
    heif_component_datatype_unsigned_integer = 0,
    heif_component_datatype_floating_point = 1,
    heif_component_datatype_complex_number = 2,
    heif_component_datatype_signed_integer = 3,
    heif_component_datatype_undefined = 0xFF,
}
