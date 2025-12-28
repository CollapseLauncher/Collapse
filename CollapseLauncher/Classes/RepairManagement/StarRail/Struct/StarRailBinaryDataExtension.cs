using Hi3Helper.EncTool;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable InconsistentNaming

#pragma warning disable IDE0130

namespace CollapseLauncher.RepairManagement.StarRail.Struct;

internal static class StarRailBinaryDataExtension
{
    internal static async ValueTask<(T Data, int Read)> ReadDataAssertAndSeekAsync<T>(
        this Stream       stream,
        Func<T, int>      minimalSizeAssertGet,
        CancellationToken token)
        where T : unmanaged
    {
        T   result              = await stream.ReadAsync<T>(token).ConfigureAwait(false);
        int sizeOfImplemented   = Unsafe.SizeOf<T>();
        int minimalSizeToAssert = minimalSizeAssertGet(result);

        // ASSERT: Make sure the struct size is versionable and at least, bigger than what we currently implement.
        //         (cuz we know you might change this in the future, HoYo :/)
        if (sizeOfImplemented > minimalSizeToAssert)
        {
            throw new InvalidOperationException($"Game data use {minimalSizeToAssert} bytes of struct for {nameof(T)} while current implementation only supports struct with size >= {sizeOfImplemented}. Please contact @neon-nyan or ping us on our Official Discord to report this issue :D");
        }

        // ASSERT: Make sure to advance the stream position if the struct is bigger than what we currently implement.
        int read     = sizeOfImplemented;
        int remained = minimalSizeToAssert - read;
        read += await stream.SeekForwardAsync(remained, token);

        return (result, read);
    }

    internal static unsafe void ReverseReorderBy4X4HashData(Span<byte> data)
    {
        if (data.Length != 16)
            throw new ArgumentException("Data length must be multiple of 4x4.", nameof(data));

        void* dataP = Unsafe.AsPointer(ref MemoryMarshal.GetReference(data));
        if (Sse3.IsSupported)
        {
            ReverseByInt32X4Sse3(dataP);
            return;
        }

        if (Sse2.IsSupported)
        {
            ReverseByInt32X4Sse2(dataP);
            return;
        }

        ReverseByInt32X4Scalar(dataP);
    }

    private static readonly Vector128<byte> ReverseByInt32X4ByteMask =
        Vector128.Create((byte)3, 2, 1, 0, 7, 6, 5, 4, 11, 10, 9, 8, 15, 14, 13, 12);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ReverseByInt32X4Sse3(void* int32X4VectorP)
        => *(Vector128<byte>*)int32X4VectorP = Ssse3.Shuffle(*(Vector128<byte>*)int32X4VectorP, ReverseByInt32X4ByteMask); // Swap

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ReverseByInt32X4Sse2(void* int32X4VectorP)
    {
        Vector128<uint> vector = *(Vector128<uint>*)int32X4VectorP;

        // Masks
        var mask00FF = Vector128.Create(0x00ff00ffu);
        var maskFF00 = Vector128.Create(0xff00ff00u);

        // Swap bytes within 16-bit halves
        Vector128<uint> t1 = Sse2.ShiftLeftLogical(vector, 8);
        Vector128<uint> t2 = Sse2.ShiftRightLogical(vector, 8);

        vector = Sse2.Or(Sse2.And(t1, maskFF00),
                    Sse2.And(t2, mask00FF));

        // Swap 16-bit halves
        Vector128<uint> result = Sse2.Or(Sse2.ShiftLeftLogical(vector, 16),
                                         Sse2.ShiftRightLogical(vector, 16));
        *(Vector128<uint>*)int32X4VectorP = result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ReverseByInt32X4Scalar(void* int32X4P)
    {
        uint* uintP = (uint*)int32X4P;

        uintP[0] = BinaryPrimitives.ReverseEndianness(uintP[0]);
        uintP[1] = BinaryPrimitives.ReverseEndianness(uintP[1]);
        uintP[2] = BinaryPrimitives.ReverseEndianness(uintP[2]);
        uintP[3] = BinaryPrimitives.ReverseEndianness(uintP[3]);
    }
}

