// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal unsafe partial struct heif_reader
{
    public int reader_api_version;

    public delegate* unmanaged[Cdecl]<void*, long> get_position;

    public delegate* unmanaged[Cdecl]<void*, nuint, void*, int> read;

    public delegate* unmanaged[Cdecl]<long, void*, int> seek;

    public delegate* unmanaged[Cdecl]<long, void*, heif_reader_grow_status> wait_for_file_size;

    public delegate* unmanaged[Cdecl]<ulong, ulong, void*, heif_reader_range_request_result> request_range;

    public delegate* unmanaged[Cdecl]<ulong, ulong, void*, void> preload_range_hint;

    public delegate* unmanaged[Cdecl]<ulong, ulong, void*, void> release_file_range;

    public delegate* unmanaged[Cdecl]<sbyte*, void> release_error_msg;
}
