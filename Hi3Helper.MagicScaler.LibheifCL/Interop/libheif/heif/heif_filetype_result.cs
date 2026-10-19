// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal enum heif_filetype_result
{
    heif_filetype_no,
    heif_filetype_yes_supported,
    heif_filetype_yes_unsupported,
    heif_filetype_maybe,
}
