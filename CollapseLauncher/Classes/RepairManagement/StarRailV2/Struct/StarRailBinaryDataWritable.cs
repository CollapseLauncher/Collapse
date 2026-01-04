using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0290 // Shut the fuck up
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher.RepairManagement.StarRail.Struct;

/// <summary>
/// Generic/Abstract Star Rail Writeable Binary Data. Do not use this class directly.<br/>
/// This implementation inherit these subtypes:<br/>
/// - <see cref="StarRailAssetMetadataIndex"/>
/// </summary>
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

    /// <summary>
    /// Write the binary data to a specified file path.
    /// </summary>
    /// <param name="filePath">Target file path to be written to.</param>
    /// <param name="token">Cancellation token for cancelling asynchronous operations.</param>
    public virtual ValueTask WriteAsync(string filePath, CancellationToken token = default)
        => WriteAsync(new FileInfo(filePath), token);

    /// <summary>
    /// Write the binary data to a specified file.
    /// </summary>
    /// <param name="fileInfo">Target file to be written to.</param>
    /// <param name="token">Cancellation token for cancelling asynchronous operations.</param>
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

    /// <summary>
    /// Write the binary data to a Stream instance.
    /// </summary>
    /// <param name="dataStream">Target Stream to be written to.</param>
    /// <param name="token">Cancellation token for cancelling asynchronous operations.</param>
    public virtual async ValueTask WriteAsync(Stream dataStream, CancellationToken token = default)
    {
        if (StarRailBinaryDataExtension.IsStructEqual(Header, default))
        {
            throw new InvalidOperationException("Header is not initialized!");
        }

        await WriteHeaderCoreAsync(dataStream, token);
        await WriteDataCoreAsync(dataStream, token);
    }

    /// <summary>
    /// Writes the header of the data to the target data stream.
    /// </summary>
    /// <param name="dataStream">Target data stream which the header will be written to.</param>
    /// <param name="token">Cancellation token for cancelling asynchronous operations.</param>
    protected abstract ValueTask WriteHeaderCoreAsync(Stream dataStream, CancellationToken token = default);

    /// <summary>
    /// Writes the data to the target data stream.
    /// </summary>
    /// <param name="dataStream">Target data stream which the data will be written to.</param>
    /// <param name="token">Cancellation token for cancelling asynchronous operations.</param>
    protected abstract ValueTask WriteDataCoreAsync(Stream dataStream, CancellationToken token = default);
}
