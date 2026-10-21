// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.IO;

using PhotoSauce.MagicScaler;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Hi3Helper.MagicScaler.LibheifCL.Libheif;
// ReSharper disable IdentifierTypo
#pragma warning disable CA1816

namespace Hi3Helper.MagicScaler.LibheifCL;

internal sealed unsafe class HeifContainer : IImageContainer
{
	private          void*         _handle;
	private readonly StreamWrapper _stream;

	private HeifContainer(string mime, StreamWrapper stm, void* frm)
	{
		MimeType = mime;
		_stream  = stm;
		_handle  = frm;
	}

	public string MimeType { get; }

	int IImageContainer.FrameCount => 1;

	IImageFrame IImageContainer.GetFrame(int index)
	{
		if (index != 0)
			throw new ArgumentOutOfRangeException(nameof(index), "Invalid frame index.");

		return new HeifFrame(this);
	}

	public static HeifContainer? TryLoad(Stream imgStream, IDecoderOptions? opts)
	{
		byte* buff = stackalloc byte[12];
		_ = imgStream.Read(new Span<byte>(buff, 12));
		imgStream.Seek(-12, SeekOrigin.Current);

		string mime = HeifFactory.GetMimeType(buff, 12);

		StreamWrapper stream = StreamWrapper.Read(imgStream);
		void* ctx = heif_context_alloc();
		if (Succeeded(heif_context_read_from_memory_without_copy(ctx, stream.Buffer, stream.Length, 0)))
		{
			void* hfrm;
			if (Succeeded(heif_context_get_primary_image_handle(ctx, &hfrm)))
			{
				heif_context_free(ctx);
				return new HeifContainer(mime, stream, hfrm);
			}
		}

		heif_context_free(ctx);
		stream.Dispose();
		imgStream.Position = 0;
		return null;
	}

	public void CheckResult(heif_error err)
	{
		if (err.code != heif_error_code.heif_error_Ok)
			throw new InvalidOperationException(new string(err.message));
	}

	public static bool Succeeded(heif_error err) => err.code == heif_error_code.heif_error_Ok;

	private void EnsureHandle()
	{
		ObjectDisposedException.ThrowIf(_handle == null, typeof(HeifContainer));
	}

	~HeifContainer() => Dispose();

	public void Dispose()
	{
		if (_handle is null)
			return;

		heif_image_handle_release(_handle);
		_handle = null;

		_stream.Dispose();

		GC.SuppressFinalize(this);
	}

	private sealed class HeifFrame(HeifContainer cont) : IImageFrame
	{
		private HeifPixelSource? _pixsrc;

		public IPixelSource PixelSource => _pixsrc ??= new HeifPixelSource(cont);

		public void Dispose()
		{
			_pixsrc?.Dispose();
			cont.Dispose();
		}
	}

	private sealed class HeifPixelSource : IPixelSource, IDisposable
	{
		private static ref readonly Guid GuidWicPixelFormat32BppRgba
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				ReadOnlySpan<byte> data = [
					0x2D, 0xAD, 0xC7, 0xF5,
					0x8D, 0x6A,
					0xDD, 0x43,
					0xA7,
					0xA8,
					0xA2,
					0x99,
					0x35,
					0x26,
					0x1A,
					0xE9
				];

				Debug.Assert(data.Length == Unsafe.SizeOf<Guid>());
				return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
			}
		}

		private static ref readonly Guid GuidWicPixelFormat24BppRgb
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				ReadOnlySpan<byte> data = [
					0x24, 0xC3, 0xDD, 0x6F,
					0x03, 0x4E,
					0xFE, 0x4B,
					0xB1,
					0x85,
					0x3D,
					0x77,
					0x76,
					0x8D,
					0xC9,
					0x0D
				];

				Debug.Assert(data.Length == Unsafe.SizeOf<Guid>());
				return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
			}
		}

		private readonly HeifContainer _container;
		private          void*         _handle;
		private          byte*         _pixels;
		private          int           _stride;
		private readonly bool          _hasAlpha;
		private readonly int           _bpp;

		public  Guid Format { get; }

		public int Width { get; }
		public int Height { get; }

		public HeifPixelSource(HeifContainer cont)
		{
			cont.EnsureHandle();

			Width     = heif_image_handle_get_width(cont._handle);
			Height    = heif_image_handle_get_height(cont._handle);
			_hasAlpha = heif_image_handle_has_alpha_channel(cont._handle) != 0;
			Format    = _hasAlpha ? GuidWicPixelFormat32BppRgba : GuidWicPixelFormat24BppRgb;
			_bpp      = _hasAlpha ? 32 : 24;

			_container = cont;
		}

		public void CopyPixels(Rectangle sourceArea, int cbStride, Span<byte> buffer)
		{
			if (_pixels is null)
				DecodeImage();

			int   chan     = _bpp / 8;
			byte* pbBuffer = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(buffer));
			for (int y = 0; y < sourceArea.Height; y++)
				Buffer.MemoryCopy(_pixels + (sourceArea.Y + y) * _stride + sourceArea.X * chan, pbBuffer + y * cbStride, cbStride, sourceArea.Width * chan);
		}

		private void DecodeImage()
		{
			const heif_channel chan = heif_channel.heif_channel_interleaved;
			_container.EnsureHandle();

			heif_decoding_options* options = heif_decoding_options_alloc();
			options->num_library_threads = 1;
			options->num_codec_threads = 1;

			void* img;
			heif_chroma decodeChroma = _hasAlpha ? heif_chroma.heif_chroma_interleaved_RGBA : heif_chroma.heif_chroma_interleaved_RGB;
			heif_error res = heif_decode_image(_container._handle, &img, heif_colorspace.heif_colorspace_RGB, decodeChroma, options);

			heif_decoding_options_free(options);
			_container.CheckResult(res);

			int hasChanHeif = heif_image_has_channel(img, chan);
			int bppHeif     = heif_image_get_bits_per_pixel(img, chan);
			if (hasChanHeif == 0 || bppHeif != _bpp)
				throw new InvalidDataException("Image did not decode to expected format");

			// https://github.com/strukturag/libheif/issues/1131
			if (heif_image_get_width(img, chan) != Width || heif_image_get_height(img, chan) != Height)
				throw new InvalidDataException("Image did not decode to expected size");

			int strideout;
			_pixels = heif_image_get_plane_readonly(img, chan, &strideout);
			_stride = strideout;
			_handle = img;
		}

		public void Dispose()
		{
			if (_handle is null)
				return;

			heif_image_release(_handle);
			_handle = null;
			_pixels = null;
		}

		public override string ToString() => nameof(HeifPixelSource);
	}
}
