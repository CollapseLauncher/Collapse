// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal partial struct heif_color_conversion_options_ext
{
    public byte version;

    public heif_alpha_composition_mode alpha_composition_mode;

    public ushort background_red;

    public ushort background_green;

    public ushort background_blue;

    public ushort secondary_background_red;

    public ushort secondary_background_green;

    public ushort secondary_background_blue;

    public ushort checkerboard_square_size;
}
