using Hi3Helper.Sophon;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable IDE0130

namespace CollapseLauncher.InstallManager.StarRail
{
    internal sealed partial class StarRailInstall
    {
        protected override async Task FilterSophonPatchAssetList(List<SophonPatchAsset> itemList, CancellationToken token)
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

                blackListAlt.Add(span);
            }

            if (blackList.Count == 0)
            {
                return;
            }

            SearchValues<string> searchValues =
                SearchValues.Create(blackList.ToArray(), StringComparison.OrdinalIgnoreCase);

            List<SophonPatchAsset> listFiltered = [];
            foreach (SophonPatchAsset patchAsset in itemList)
            {
                if (patchAsset.TargetFilePath == null)
                {
                    listFiltered.Add(patchAsset);
                    continue;
                }

                string assetPath = Path.Combine(GamePath, patchAsset.TargetFilePath);
                if (assetPath.AsSpan().ContainsAny(searchValues))
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
    }
}
