using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable CommentTypo

#pragma warning disable IDE0290 // Shut the fuck up
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher.RepairManagement.StarRail.Struct;

/// <summary>
/// Generic/Abstract Star Rail Binary Data. Do not use this class directly.
/// </summary>
public abstract class StarRailBinaryData
{
    /// <summary>
    /// Magic Signature of the binary data. This property must be overriden by the derivative instances.
    /// </summary>
    protected abstract ReadOnlySpan<byte> MagicSignature { get; }

    /// <summary>
    /// The header of the parsed binary data.
    /// </summary>
    public StarRailBinaryDataHeaderStruct Header { get; protected set; }

    /// <summary>
    /// Create a default instance of the <see cref="StarRailBinaryData"/> members.
    /// </summary>
    /// <typeparam name="T">Member type of <see cref="StarRailBinaryData"/>.</typeparam>
    /// <returns>A parser instance which is a member of <see cref="StarRailBinaryData"/>.</returns>
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

    /// <summary>
    /// Reads the header and perform assertion on the header.
    /// </summary>
    /// <param name="dataStream">The <see cref="Stream"/> which provides the source of the data to be parsed.</param>
    /// <param name="token">Cancellation token for cancelling asynchronous operations.</param>
    /// <returns>
    /// This returns the <see cref="StarRailBinaryDataHeaderStruct"/> and the current offset/position of the data stream after reading the header.
    /// </returns>
    protected virtual async ValueTask<(StarRailBinaryDataHeaderStruct Header, int Offset)> ReadHeaderCoreAsync(
        Stream            dataStream,
        CancellationToken token = default)
    {
        return await dataStream
           .ReadDataAssertAndSeekAsync<StarRailBinaryDataHeaderStruct>(x => x.HeaderOrDataLength,
                                                                       token);
    }

    /// <summary>
    /// Reads the body of the binary data.
    /// </summary>
    /// <param name="currentOffset">The current offset of the data stream.</param>
    /// <param name="dataStream">The <see cref="Stream"/> which provides the source of the data to be parsed.</param>
    /// <param name="token">Cancellation token for cancelling asynchronous operations.</param>
    /// <returns>
    /// This returns the current offset/position of the data stream after reading the data.
    /// </returns>
    protected abstract ValueTask<long> ReadDataCoreAsync(long currentOffset, Stream dataStream, CancellationToken token = default);
}

/// <summary>
/// Generic/Abstract Star Rail Binary Data which contain the list of assets. Do not use this class directly.
/// </summary>
public abstract class StarRailBinaryData<TAsset> : StarRailBinaryData
{
    /// <summary>
    /// List of the assets parsed from the binary data.
    /// </summary>
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

/// <summary>
/// Generic Star Rail Binary Data Header Structure.<br/>
/// This header is globally used by SRMI, SRBM, SRAM and Signatureless metadata format.
/// </summary>
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
