using Hi3Helper.Data;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable InvertIf
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.InstallManager.StarRail
{
    internal sealed partial class StarRailInstall
    {
        public override async Task FilterAssetList<T>(
            List<T>           itemList,
            Func<T, string?>  itemPathSelector,
            CancellationToken token)
        {
            string   blackListFilePath = Path.Combine(GamePath, @"StarRail_Data\Persistent\DownloadBlacklist.json");
            FileInfo fileInfo          = new(blackListFilePath);

            if (!fileInfo.Exists)
            {
                return;
            }

            HashSet<string> blackList = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string>.AlternateLookup<ReadOnlySpan<char>> blackListAlt = blackList.GetAlternateLookup<ReadOnlySpan<char>>();

            using StreamReader reader = new(fileInfo.OpenRead());

            while (await reader.ReadLineAsync(token) is { } line)
            {
                ReadOnlySpan<char> span = GetFilePathFromJson(line);
                if (span.IsEmpty)
                {
                    continue;
                }

                // Normalize path
                ConverterTool.NormalizePathInplaceNoTrim(span);
                AddBothPersistentOrStreamingAssets(blackListAlt, span);
            }

            if (blackList.Count == 0)
            {
                return;
            }

            SearchValues<string> searchValues =
                SearchValues.Create(blackList.ToArray(), StringComparison.OrdinalIgnoreCase);

            List<T> listFiltered = [];
            foreach (T patchAsset in itemList)
            {
                if (itemPathSelector(patchAsset) is not {} filePath)
                {
                    listFiltered.Add(patchAsset);
                    continue;
                }

                int indexOfAny = filePath.IndexOfAny(searchValues);
                if (indexOfAny >= 0)
                {
                    continue;
                }

                listFiltered.Add(patchAsset);
            }

            itemList.Clear();
            itemList.AddRange(listFiltered);

            return;

            static ReadOnlySpan<char> GetFilePathFromJson(ReadOnlySpan<char> line)
            {
                const string first = "\"fileName\":\"";
                const char   end   = '\"';

                int firstIndexOf = line.IndexOf(first);
                if (firstIndexOf <= 0)
                {
                    return ReadOnlySpan<char>.Empty;
                }

                line = line[(firstIndexOf + first.Length)..];
                int endIndexOf = line.IndexOf(end);

                if (endIndexOf <= 0)
                {
                    return ReadOnlySpan<char>.Empty;
                }

                return line[..endIndexOf];
            }
        }

        private static void AddBothPersistentOrStreamingAssets(
            HashSet<string>.AlternateLookup<ReadOnlySpan<char>> hashList,
            ReadOnlySpan<char> filePath)
        {
            const string streamingAssetsSegment = @"StarRail_Data\StreamingAssets\";
            const string persistentSegment      = @"StarRail_Data\Persistent\";

            bool isContainStreamingAssets = filePath.Contains(streamingAssetsSegment, StringComparison.OrdinalIgnoreCase);
            bool isContainPersistent      = filePath.Contains(persistentSegment, StringComparison.OrdinalIgnoreCase);

            // Add original path
            hashList.Add(filePath);

            if (isContainStreamingAssets)
            {
                string persistentPath = persistentSegment
                                        + filePath[streamingAssetsSegment.Length..].ToString();
                hashList.Add(persistentPath);
            }

            if (isContainPersistent)
            {
                string streamingAssetsPath = streamingAssetsSegment
                                           + filePath[persistentSegment.Length..].ToString();
                hashList.Add(streamingAssetsPath);
            }
        }
    }
}
