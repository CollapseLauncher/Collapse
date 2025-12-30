using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable IDE0290
#pragma warning disable IDE0130

namespace CollapseLauncher.RepairManagement.StarRail.Struct;

internal abstract class StarRailBinaryDataWritable<TAsset> : StarRailBinaryData<TAsset>
{
    protected StarRailBinaryDataWritable(ReadOnlySpan<byte> magicSignature,
                                         short parentTypeFlag,
                                         short typeVersionFlag,
                                         int   headerLength,
                                         short subStructCount,
                                         short subStructSize)
        : base(magicSignature,
               parentTypeFlag,
               typeVersionFlag,
               headerLength,
               subStructCount,
               subStructSize) { }

    public virtual ValueTask WriteAsync(string filePath, CancellationToken token = default)
        => WriteAsync(new FileInfo(filePath), token);

    public virtual async ValueTask WriteAsync(FileInfo fileInfo, CancellationToken token = default)
    {
        fileInfo.Directory?.Create();
        if (fileInfo.Exists)
        {
            fileInfo.IsReadOnly = false;
        }

        await using FileStream dataStream = fileInfo.Create();
        await WriteAsync(dataStream, token);
    }

    public virtual async ValueTask WriteAsync(Stream dataStream, CancellationToken token = default)
    {
        if (StarRailBinaryDataExtension.IsStructEqual(Header, default))
        {
            throw new InvalidOperationException("Header is not initialized!");
        }

        await WriteHeaderCoreAsync(dataStream, token);
        await WriteDataCoreAsync(dataStream, token);
    }

    protected abstract ValueTask WriteHeaderCoreAsync(Stream dataStream, CancellationToken token = default);

    protected abstract ValueTask WriteDataCoreAsync(Stream dataStream, CancellationToken token = default);
}
