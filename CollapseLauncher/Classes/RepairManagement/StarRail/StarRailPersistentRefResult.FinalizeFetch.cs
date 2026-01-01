using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Sophon;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0130
#nullable enable
namespace CollapseLauncher;

internal partial class StarRailPersistentRefResult
{
    public static async Task FinalizeFetchAsync(StarRailRepair             instance,
                                                HttpClient                 client,
                                                List<FilePropertiesRemote> assetIndex,
                                                string                     persistentDir,
                                                CancellationToken          token)
    {
        // Set total activity string as "Fetching Caches Type: <type>"
        instance.Status.ActivityStatus = string.Format(Locale.Lang._CachesPage.CachesStatusFetchingType, "BinaryVersion.bytes");
        instance.Status.IsProgressAllIndetermined = true;
        instance.Status.IsIncludePerFileIndicator = false;
        instance.UpdateStatus();

        FilePropertiesRemote? binaryVersionFile =
            assetIndex.FirstOrDefault(x => x.N.EndsWith("StreamingAssets\\BinaryVersion.bytes",
                                                        StringComparison.OrdinalIgnoreCase));

        if (binaryVersionFile is not { AssociatedObject: SophonAsset asSophonAsset })
        {
            Logger.LogWriteLine("[StarRailPersistentRefResult::FinalizeFetchAsync] We cannot finalize fetching process as necessary file is not available. The game might behave incorrectly!",
                                LogType.Warning,
                                true);
            return;
        }

        await using MemoryStream tempStream = new();
        await asSophonAsset.WriteToStreamAsync(client, tempStream, token: token);
        tempStream.Position = 0;

        byte[]     buffer     = tempStream.ToArray();
        Span<byte> bufferSpan = buffer.AsSpan()[..^3];

        string binAppIdentityPath          = Path.Combine(persistentDir, "AppIdentity.txt");
        string binDownloadedFullAssetsPath = Path.Combine(persistentDir, "DownloadedFullAssets.txt");
        string binInstallVersionPath       = Path.Combine(persistentDir, "InstallVersion.bin");

        Span<byte> hashSpan = bufferSpan[^36..^4];
        string     hashStr  = Encoding.UTF8.GetString(hashSpan);

        GetVersionNumber(bufferSpan, out uint majorVersion, out uint minorVersion, out uint stockPatchVersion);

        await File.WriteAllTextAsync(binAppIdentityPath, hashStr, token);
        await File.WriteAllTextAsync(binDownloadedFullAssetsPath, hashStr, token);
        await File.WriteAllTextAsync(binInstallVersionPath, $"{hashStr},{majorVersion}.{minorVersion}.{stockPatchVersion}", token);

        return;

        static void GetVersionNumber(ReadOnlySpan<byte> span, out uint major, out uint minor, out uint patch)
        {
            ushort strLen = BinaryPrimitives.ReadUInt16BigEndian(span);
            span  = span[(2 + strLen)..]; // Skip
            patch = BinaryPrimitives.ReadUInt32BigEndian(span);
            major = BinaryPrimitives.ReadUInt32BigEndian(span[4..]);
            minor = BinaryPrimitives.ReadUInt32BigEndian(span[8..]);
        }
    }
}
