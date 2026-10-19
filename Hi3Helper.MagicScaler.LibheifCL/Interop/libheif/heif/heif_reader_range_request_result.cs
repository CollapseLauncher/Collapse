// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal unsafe partial struct heif_reader_range_request_result
{
    public heif_reader_grow_status status;

    public ulong range_end;

    public int reader_error_code;

    public sbyte* reader_error_msg;
}
