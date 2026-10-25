// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using PhotoSauce.MagicScaler;
using System;
using System.Linq;
using static Hi3Helper.MagicScaler.LibheifCL.Libheif;
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

namespace Hi3Helper.MagicScaler.LibheifCL;

internal static unsafe class HeifFactory
{
	public const string DisplayName = $"{Libheif} {LIBHEIF_VERSION}";
	public const string Libheif = nameof(Libheif);
	
	public static void* CreateContext() => heif_context_alloc();

	public static string GetMimeType(byte* data, int len) => new(heif_get_file_mime_type(data, len));
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
