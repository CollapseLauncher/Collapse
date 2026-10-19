// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal unsafe partial struct heif_decoding_options
{
    public byte version;

    public byte ignore_transformations;

    public delegate* unmanaged[Cdecl]<heif_progress_step, int, void*, void> start_progress;

    public delegate* unmanaged[Cdecl]<heif_progress_step, int, void*, void> on_progress;

    public delegate* unmanaged[Cdecl]<heif_progress_step, void*, void> end_progress;

    public void* progress_user_data;

    public byte convert_hdr_to_8bit;

    public byte strict_decoding;

    public sbyte* decoder_id;

    public heif_color_conversion_options color_conversion_options;

    public delegate* unmanaged[Cdecl]<void*, int> cancel_decoding;

    public heif_color_conversion_options_ext* color_conversion_options_ext;

    public int ignore_sequence_editlist;

    public heif_color_profile_nclx* output_image_nclx_profile;

    public int num_library_threads;

    public int num_codec_threads;

    public byte autocorrect_broken_input;

    public byte output_image_nclx_profile_passthrough;
}
