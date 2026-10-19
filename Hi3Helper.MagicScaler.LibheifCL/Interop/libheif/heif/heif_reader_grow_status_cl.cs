// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal enum heif_reader_grow_status
{
    heif_reader_grow_status_size_reached,
    heif_reader_grow_status_timeout,
    heif_reader_grow_status_size_beyond_eof,
    heif_reader_grow_status_error,
}
