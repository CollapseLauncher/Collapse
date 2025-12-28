using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable IDE0130

namespace CollapseLauncher.RepairManagement.StarRail.Struct;

internal abstract class StarRailBinaryData<TAsset>
{
    protected abstract ReadOnlySpan<byte>   MagicSignature { get; }
    public             StarRailBinaryHeader Header         { get; private set; }
    public             List<TAsset>         DataList       { get; protected set; }

    public virtual async Task<StarRailBinaryData<TAsset>> ParseAsync(Stream dataStream, CancellationToken token)
    {
        (Header, int offset) = await dataStream.ReadDataAssertAndSeekAsync<StarRailBinaryHeader>(x => x.SubStructStartOffset, token);
        if (!Header.MagicSignature.SequenceEqual(MagicSignature))
        {
            throw new InvalidOperationException($"Magic Signature doesn't match! Expecting: {Encoding.UTF8.GetString(MagicSignature)} but got: {Encoding.UTF8.GetString(Header.MagicSignature)} instead.");
        }

        await ReadDataCoreAsync(offset, dataStream, token);
        return this;
    }

    protected abstract ValueTask<long> ReadDataCoreAsync(long currentOffset, Stream dataStream, CancellationToken token);
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
public unsafe struct StarRailBinaryHeader
{
    private fixed byte  _magicSignature[4];
    public        short ParentTypeFlag;
    public        short TypeVersionFlag;
    public        int   HeaderLength;
    public        short SubStructCount;
    public        short SubStructStartOffset;

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

    public override string ToString() => $"{Encoding.UTF8.GetString(MagicSignature)} | ParentTypeFlag: {ParentTypeFlag} | TypeVersionFlag: {TypeVersionFlag} | SubStructCount: {SubStructCount} | SubStructStartOffset: {SubStructStartOffset} | {HeaderLength} bytes";
}
