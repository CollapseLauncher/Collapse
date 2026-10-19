// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal unsafe partial struct heif_encoding_options
{
    public byte version;

    public byte save_alpha_channel;

    public byte macOS_compatibility_workaround;

    public byte save_two_colr_boxes_when_ICC_and_nclx_available;

    public heif_color_profile_nclx* output_nclx_profile;

    public byte macOS_compatibility_workaround_no_nclx_profile;

    public heif_orientation image_orientation;

    public heif_color_conversion_options color_conversion_options;
}
