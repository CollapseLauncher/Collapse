// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal partial struct heif_color_conversion_options
{
    public byte version;

    public heif_chroma_downsampling_algorithm preferred_chroma_downsampling_algorithm;

    public heif_chroma_upsampling_algorithm preferred_chroma_upsampling_algorithm;

    public byte only_use_preferred_chroma_algorithm;
}
