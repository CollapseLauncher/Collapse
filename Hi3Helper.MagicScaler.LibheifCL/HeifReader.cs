// Copyright © Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;
// ReSharper disable IdentifierTypo

namespace Hi3Helper.MagicScaler.LibheifCL;

internal readonly unsafe struct HeifReader
{
	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvCdecl) ])]
	static
	private long GetPosition(void* pinst)
	{
		var stm = (StreamWrapper*)pinst;
		try
		{
			return stm->Seek(0, SeekOrigin.Current);
		}
		catch (Exception ex) when (StreamWrapper.CaptureExceptions)
		{
			stm->SetException(ExceptionDispatchInfo.Capture(ex));
			return -1;
		}
	}

	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvCdecl) ])]
	static
	private int Read(void* pv, nuint cb, void* pinst)
	{
		var stm = (StreamWrapper*)pinst;
		try
		{
			uint read = stm->Read(pv, checked((uint)cb));
			return read == cb ? 0 : 1;
		}
		catch (Exception ex) when (StreamWrapper.CaptureExceptions)
		{
			stm->SetException(ExceptionDispatchInfo.Capture(ex));
			return -1;
		}
	}

	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvCdecl) ])]
	static
	private int Seek(long npos, void* pinst)
	{
		var stm = (StreamWrapper*)pinst;
		try
		{
			long cpos = stm->Seek(npos, SeekOrigin.Begin);
			return cpos == npos ? 0 : 1;
		}
		catch (Exception ex) when (StreamWrapper.CaptureExceptions)
		{
			stm->SetException(ExceptionDispatchInfo.Capture(ex));
			return -1;
		}
	}

	[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvCdecl) ])]
	static
	private heif_reader_grow_status WaitForLength(long len, void* pinst)
	{
		var stm = (StreamWrapper*)pinst;
		try
		{
			return stm->IsPastEnd(len)
				? heif_reader_grow_status.heif_reader_grow_status_size_beyond_eof
				: heif_reader_grow_status.heif_reader_grow_status_size_reached;
		}
		catch (Exception ex) when (StreamWrapper.CaptureExceptions)
		{
			stm->SetException(ExceptionDispatchInfo.Capture(ex));
			return (heif_reader_grow_status)(-1);
		}
	}

	public static readonly heif_reader* Impl = CreateImpl();

	private static heif_reader* CreateImpl()
	{
		var impl = (heif_reader*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(HeifReader), sizeof(heif_reader));
		impl->reader_api_version = 1;
		impl->get_position = &GetPosition;
		impl->read = &Read;
		impl->seek = &Seek;
		impl->wait_for_file_size = &WaitForLength;

		return impl;
	}
}