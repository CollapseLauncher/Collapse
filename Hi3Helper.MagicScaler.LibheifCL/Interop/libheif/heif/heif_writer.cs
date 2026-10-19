// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal unsafe partial struct heif_writer
{
    public int writer_api_version;

    public delegate* unmanaged[Cdecl]<void*, void*, nuint, void*, heif_error> write;
}
