// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

namespace Hi3Helper.MagicScaler.LibheifCL;

internal enum heif_matrix_coefficients
{
    heif_matrix_coefficients_RGB_GBR = 0,
    heif_matrix_coefficients_ITU_R_BT_709_5 = 1,
    heif_matrix_coefficients_unspecified = 2,
    heif_matrix_coefficients_US_FCC_T47 = 4,
    heif_matrix_coefficients_ITU_R_BT_470_6_System_B_G = 5,
    heif_matrix_coefficients_ITU_R_BT_601_6 = 6,
    heif_matrix_coefficients_SMPTE_240M = 7,
    heif_matrix_coefficients_YCgCo = 8,
    heif_matrix_coefficients_ITU_R_BT_2020_2_non_constant_luminance = 9,
    heif_matrix_coefficients_ITU_R_BT_2020_2_constant_luminance = 10,
    heif_matrix_coefficients_SMPTE_ST_2085 = 11,
    heif_matrix_coefficients_chromaticity_derived_non_constant_luminance = 12,
    heif_matrix_coefficients_chromaticity_derived_constant_luminance = 13,
    heif_matrix_coefficients_ICtCp = 14,
}
