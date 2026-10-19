// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.Linq;

using PhotoSauce.MagicScaler;
using static Hi3Helper.MagicScaler.LibheifCL.Libheif;
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

namespace Hi3Helper.MagicScaler.LibheifCL;

internal static unsafe class HeifFactory
{
	public const string DisplayName = $"{Libheif} {LIBHEIF_VERSION}";
	public const string Libheif = nameof(Libheif);
	public const uint Libver = LIBHEIF_NUMERIC_VERSION;

	private static readonly Lazy<bool> DependencyValid = new(() => {
		uint ver = heif_get_version_number();
		if (ver != Libver)
			throw new NotSupportedException($"Incorrect {Libheif} version was loaded.  Expected 0x{Libver:x8}, found 0x{ver:x8}.");

		return true;
	});

	public static void* CreateContext() => DependencyValid.Value ? heif_context_alloc() : null;

	public static string GetMimeType(byte* data, int len) => DependencyValid.Value ? new string(heif_get_file_mime_type(data, len)) : "image/heif";
}

/// <inheritdoc cref="WindowsCodecExtensions" />
public static class CodecCollectionExtensions
{
	/// <inheritdoc cref="WindowsCodecExtensions.UseWicCodecs(CodecCollection, WicCodecPolicy)" />
	public static void UseLibheifCL(this CodecCollection codecs, bool removeExisting = true)
	{
		ArgumentNullException.ThrowIfNull(codecs);

		if (removeExisting)
		{
			foreach (IImageDecoderInfo codec in codecs.OfType<IImageDecoderInfo>().Where(c => c.MimeTypes.Any(m => m is ImageMimeTypes.Heic or ImageMimeTypes.Avif)).ToList())
				codecs.Remove(codec);
		}

		codecs.Add(new DecoderInfo(
			HeifFactory.DisplayName,
			[ ImageMimeTypes.Heic, ImageMimeTypes.Avif ],
			[ ImageFileExtensions.Heic, ImageFileExtensions.Avif ],
			[
				new ContainerPattern(0, [.. "\0\0\0\0ftypheic"u8], [ 0xff, 0xff, 0xff, 0, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff ]),
				new ContainerPattern(0, [.. "\0\0\0\0ftyphevc"u8], [ 0xff, 0xff, 0xff, 0, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff ]),
				new ContainerPattern(0, [.. "\0\0\0\0ftypavif"u8], [ 0xff, 0xff, 0xff, 0, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff ]),
				new ContainerPattern(0, [.. "\0\0\0\0ftypmif1"u8], [ 0xff, 0xff, 0xff, 0, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff ]),
				new ContainerPattern(0, [.. "\0\0\0\0ftypmiaf"u8], [ 0xff, 0xff, 0xff, 0, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff ]),
				new ContainerPattern(0, [.. "\0\0\0\0ftyp1pic"u8], [ 0xff, 0xff, 0xff, 0, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff ])
			],
			null,
			HeifContainer.TryLoad
		));
	}
}
