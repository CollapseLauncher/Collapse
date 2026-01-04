using Hi3Helper.Sophon;
using Hi3Helper.Sophon.Infos;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.InstallManager.Zenless
{
    internal partial class ZenlessInstall
    {
        private const StringSplitOptions SplitOptions = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;

        // ReSharper disable once StringLiteralTypo
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<SophonChunksInfo>k__BackingField")]
        private static extern ref SophonChunksInfo GetChunkAssetChunksInfo(SophonAsset element);

        // ReSharper disable once StringLiteralTypo
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<SophonChunksInfoAlt>k__BackingField")]
        private static extern ref SophonChunksInfo GetChunkAssetChunksInfoAlt(SophonAsset element);

        public override async Task FilterAssetList<T>(
            List<T>           itemList,
            Func<T, string?>  itemPathSelector,
            CancellationToken token)
        {
            HashSet<int> exceptMatchFieldHashSet = await GetExceptMatchFieldHashSet(token);
            if (exceptMatchFieldHashSet.Count == 0)
            {
                return;
            }

            FilterSophonAsset(itemList, Selector, exceptMatchFieldHashSet);
            return;

            string? Selector(T item)
            {
                // For Game Update
                if (item is SophonPatchAsset asset)
                {
                    if (asset.MainAssetInfo is not { } assetSelected)
                    {
                        return null;
                    }

                    token.ThrowIfCancellationRequested();
                    ref SophonChunksInfo chunkInfo = ref Unsafe.IsNullRef(ref assetSelected)
                        ? ref Unsafe.NullRef<SophonChunksInfo>()
                        : ref GetChunkAssetChunksInfo(assetSelected);

                    if (Unsafe.IsNullRef(ref chunkInfo))
                    {
                        chunkInfo = ref GetChunkAssetChunksInfoAlt(assetSelected);
                    }

                    return Unsafe.IsNullRef(ref chunkInfo)
                        ? asset.MainAssetInfo.AssetName
                        : chunkInfo.ChunksBaseUrl;
                }

                // TODO: For Game Repair handle

                return null;
            }
        }

        private async Task<HashSet<int>> GetExceptMatchFieldHashSet(CancellationToken token)
        {
            string gameExecDataName =
                Path.GetFileNameWithoutExtension(GameVersionManager.GamePreset.GameExecutableName) ?? "ZenlessZoneZero";
            string gameExecDataPath         = $"{gameExecDataName}_Data";
            string gamePersistentDataPath   = Path.Combine(GamePath,               gameExecDataPath, "Persistent");
            string gameExceptMatchFieldFile = Path.Combine(gamePersistentDataPath, "KDelResource");

            if (!File.Exists(gameExceptMatchFieldFile))
            {
                return [];
            }

            string       exceptMatchFieldContent = await File.ReadAllTextAsync(gameExceptMatchFieldFile, token);
            HashSet<int> exceptMatchFieldHashSet = CreateExceptMatchFieldHashSet<int>(exceptMatchFieldContent);

            return exceptMatchFieldHashSet;
        }

        // ReSharper disable once IdentifierTypo
        private static void FilterSophonAsset<T>(List<T> itemList, Func<T, string?> assetSelector, HashSet<int> exceptMatchFieldHashSet)
        {
            const string       separators    = "/\\";
            scoped Span<Range> urlPathRanges = stackalloc Range[32];

            List<T> filteredList = [];
            foreach (T asset in itemList)
            {
                string? assetPath = assetSelector(asset);
                if (assetPath == null)
                {
                    filteredList.Add(asset);
                    continue;
                }

                ReadOnlySpan<char> manifestUrl = assetPath;
                int                rangeLen    = manifestUrl.SplitAny(urlPathRanges, separators, SplitOptions);
                if (rangeLen <= 0)
                {
                    continue;
                }

                ReadOnlySpan<char> manifestStr = manifestUrl[urlPathRanges[rangeLen - 1]];
                if (int.TryParse(manifestStr, null, out int lookupNumber) &&
                    exceptMatchFieldHashSet.Contains(lookupNumber))
                {
                    continue;
                }

                filteredList.Add(asset);
            }

            if (filteredList.Count == 0)
            {
                return;
            }

            itemList.Clear();
            itemList.AddRange(filteredList);
        }

        internal static HashSet<T> CreateExceptMatchFieldHashSet<T>(string exceptMatchFieldContent)
            where T : ISpanParsable<T>
        {
            const string       lineFeedSeparators = "\r\n";
            HashSet<T>         hashSetReturn      = [];
            scoped Span<Range> contentLineRange   = stackalloc Range[2];

            ReadOnlySpan<char> contentSpan = exceptMatchFieldContent.AsSpan();
            int contentLineLen = contentSpan.SplitAny(contentLineRange, lineFeedSeparators, SplitOptions);

            if (contentLineLen == 0)
            {
                return hashSetReturn;
            }

            contentSpan = contentSpan[contentLineRange[0]];
            const string       separatorsChars = "|;,$#@+ ";
            SearchValues<char> separators      = SearchValues.Create(separatorsChars);

            foreach (Range contentMatchRange in contentSpan.SplitAny(separators))
            {
                if (contentMatchRange.End.Value - contentMatchRange.Start.Value <= 0)
                {
                    continue;
                }

                ReadOnlySpan<char> contentMatch = contentSpan[contentMatchRange].Trim(separatorsChars);
                if (T.TryParse(contentMatch, null, out T? result))
                {
                    hashSetReturn.Add(result);
                }
            }

            return hashSetReturn;
        }
    }
}
