// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal partial struct heif_ambient_viewing_environment
{
    public uint ambient_illumination;
    public ushort ambient_light_x;
    public ushort ambient_light_y;
}
