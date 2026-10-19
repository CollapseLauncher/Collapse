// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal partial struct heif_security_limits
{
    public byte version;

    public ulong max_image_size_pixels;

    public ulong max_number_of_tiles;

    public uint max_bayer_pattern_pixels;

    public uint max_items;

    public uint max_color_profile_size;

    public ulong max_memory_block_size;

    public uint max_components;

    public uint max_iloc_extents_per_item;

    public uint max_size_entity_group;

    public uint max_children_per_box;

    public ulong max_total_memory;

    public uint max_sample_description_box_entries;

    public uint max_sample_group_description_box_entries;

    public uint max_sequence_frames;

    public uint max_number_of_file_brands;

    public uint max_bad_pixels;

    public uint max_iso23001_17_pixel_size_bytes;

    public heif_security_limits* parent;
}
