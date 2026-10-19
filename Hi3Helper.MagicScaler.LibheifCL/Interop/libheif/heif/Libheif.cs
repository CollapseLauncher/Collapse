// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

// Ported from libheif headers (heif.h)
// Original source Copyright (c) struktur AG, Dirk Farin
// See third-party-notices in the repository root for more information.

using System.Runtime.InteropServices;
using static Hi3Helper.MagicScaler.LibheifCL.heif_chroma;
using static Hi3Helper.MagicScaler.LibheifCL.heif_colorspace;

namespace Hi3Helper.MagicScaler.LibheifCL;

internal static unsafe partial class Libheif
{
    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern uint heif_get_version_number();

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_get_width(void* img, heif_channel channel);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_get_height(void* img, heif_channel channel);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_get_bits_per_pixel(void* param0, heif_channel channel);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_has_channel(void* param0, heif_channel channel);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern byte* heif_image_get_plane_readonly(void* param0, heif_channel channel, int* out_stride);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_release(void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern sbyte* heif_get_file_mime_type(byte* data, int len);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void* heif_context_alloc();

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_context_free(void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_read_from_reader(void* param0, heif_reader* reader, void* userdata, void* param3);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_context_get_primary_image_handle(void* ctx, void** param1);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_decoding_options* heif_decoding_options_alloc();

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_decoding_options_free(heif_decoding_options* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern heif_error heif_decode_image(void* in_handle, void** out_img, heif_colorspace colorspace, heif_chroma chroma, heif_decoding_options* options);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void heif_image_handle_release(void* param0);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_width(void* handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_get_height(void* handle);

    [DllImport("heif", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern int heif_image_handle_has_alpha_channel(void* param0);

    public const int LIBHEIF_NUMERIC_VERSION = ((1 << 24) | (23 << 16) | (5 << 8) | 0);

    public const string LIBHEIF_VERSION = "1.23.5";
}
