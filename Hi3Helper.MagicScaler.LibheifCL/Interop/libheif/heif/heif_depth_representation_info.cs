// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal unsafe partial struct heif_depth_representation_info
{
    public byte version;

    public byte has_z_near;

    public byte has_z_far;

    public byte has_d_min;

    public byte has_d_max;

    public double z_near;

    public double z_far;

    public double d_min;

    public double d_max;

    public heif_depth_representation_type depth_representation_type;

    public uint disparity_reference_view;

    public uint depth_nonlinear_representation_model_size;

    public byte* depth_nonlinear_representation_model;
}
