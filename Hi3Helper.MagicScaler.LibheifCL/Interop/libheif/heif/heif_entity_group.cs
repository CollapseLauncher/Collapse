// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal unsafe partial struct heif_entity_group
{
    public uint entity_group_id;

    public uint entity_group_type;

    public uint* entities;

    public uint num_entities;
}
