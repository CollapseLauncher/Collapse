using CollapseLauncher.RepairManagement.StarRail.Struct.Assets;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher.RepairManagement.StarRail.Struct;

public abstract class StarRailBinaryData
{
    protected abstract ReadOnlySpan<byte>             MagicSignature { get; }
    public             StarRailBinaryDataHeaderStruct Header         { get; protected set; }

    public static T CreateDefault<T>() where T : StarRailBinaryData, new() => new();

    /// <summary>
    /// Parse the binary data from the provided <see cref="Stream"/> and populate the <see cref="StarRailBinaryData{TAsset}.DataList"/>.
    /// </summary>
    /// <param name="dataStream">The <see cref="Stream"/> which provides the source of the data to be parsed.</param>
    /// <param name="seekToEnd">
    /// Whether to seek the data <see cref="Stream"/> to the end, even though not all data being read.<br/>
    /// Keep in mind that this operation will actually read all remaining data from the <see cref="Stream"/> and discard it.
    /// </param>
    /// <param name="token">Cancellation token for cancelling asynchronous operations.</param>
    /// <exception cref="InvalidOperationException"></exception>
    public virtual async Task ParseAsync(Stream dataStream, bool seekToEnd = false, CancellationToken token = default)
    {
        (Header, int offset) = await ReadHeaderCoreAsync(dataStream, token);
        if (!Header.MagicSignature.SequenceEqual(MagicSignature))
        {
            throw new InvalidOperationException($"Magic Signature doesn't match! Expecting: {Encoding.UTF8.GetString(MagicSignature)} but got: {Encoding.UTF8.GetString(Header.MagicSignature)} instead.");
        }

        await ReadDataCoreAsync(offset, dataStream, token);
        if (seekToEnd) // Read all remained data to null stream, even though not all data is being read.
        {
            await dataStream.CopyToAsync(Stream.Null, token);
        }
    }

    protected virtual async ValueTask<(StarRailBinaryDataHeaderStruct Header, int Offset)> ReadHeaderCoreAsync(
        Stream            dataStream,
        CancellationToken token = default)
    {
        return await dataStream
           .ReadDataAssertAndSeekAsync<StarRailBinaryDataHeaderStruct>(x => x.HeaderOrDataLength,
                                                                       token);
    }

    protected abstract ValueTask<long> ReadDataCoreAsync(long currentOffset, Stream dataStream, CancellationToken token = default);
}

public abstract class StarRailBinaryData<TAsset> : StarRailBinaryData
{
    public List<TAsset> DataList { get; protected set; } = [];

    protected StarRailBinaryData(ReadOnlySpan<byte> magicSignature,
                                 short              parentTypeFlag,
                                 short              typeVersionFlag,
                                 int                headerOrDataLength,
                                 short              subStructCount,
                                 short              subStructSize)
    {
        StarRailBinaryDataHeaderStruct header = default;
        magicSignature.CopyTo(header.MagicSignature);

        header.ParentTypeFlag     = parentTypeFlag;
        header.TypeVersionFlag    = typeVersionFlag;
        header.HeaderOrDataLength = headerOrDataLength;
        header.SubStructCount     = subStructCount;
        header.SubStructSize      = subStructSize;

        Header = header;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
public unsafe struct StarRailBinaryDataHeaderStruct
{
    private fixed byte  _magicSignature[4];
    public        short ParentTypeFlag;
    public        short TypeVersionFlag;
    public        int   HeaderOrDataLength;
    public        short SubStructCount;
    public        short SubStructSize;

    public Span<byte> MagicSignature
    {
        get
        {
            fixed (byte* magicP = _magicSignature)
            {
                return new Span<byte>(magicP, 4);
            }
        }
    }

    public override string ToString() => $"{Encoding.UTF8.GetString(MagicSignature)} | ParentTypeFlag: {ParentTypeFlag} | TypeVersionFlag: {TypeVersionFlag} | SubStructCount: {SubStructCount} | SubStructSize: {SubStructSize} | HeaderReportedLength: {HeaderOrDataLength} bytes";
}
