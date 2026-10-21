// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal enum heif_transfer_characteristics
{
    heif_transfer_characteristic_ITU_R_BT_709_5 = 1,
    heif_transfer_characteristic_unspecified = 2,
    heif_transfer_characteristic_ITU_R_BT_470_6_System_M = 4,
    heif_transfer_characteristic_ITU_R_BT_470_6_System_B_G = 5,
    heif_transfer_characteristic_ITU_R_BT_601_6 = 6,
    heif_transfer_characteristic_SMPTE_240M = 7,
    heif_transfer_characteristic_linear = 8,
    heif_transfer_characteristic_logarithmic_100 = 9,
    heif_transfer_characteristic_logarithmic_100_sqrt10 = 10,
    heif_transfer_characteristic_IEC_61966_2_4 = 11,
    heif_transfer_characteristic_ITU_R_BT_1361 = 12,
    heif_transfer_characteristic_IEC_61966_2_1 = 13,
    heif_transfer_characteristic_ITU_R_BT_2020_2_10bit = 14,
    heif_transfer_characteristic_ITU_R_BT_2020_2_12bit = 15,
    heif_transfer_characteristic_ITU_R_BT_2100_0_PQ = 16,
    heif_transfer_characteristic_SMPTE_ST_428_1 = 17,
    heif_transfer_characteristic_ITU_R_BT_2100_0_HLG = 18,
}
