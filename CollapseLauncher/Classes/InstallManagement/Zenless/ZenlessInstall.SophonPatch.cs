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
        // ReSharper disable once StringLiteralTypo
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<SophonChunksInfo>k__BackingField")]
        private static extern ref SophonChunksInfo GetChunkAssetChunksInfo(SophonAsset element);

        // ReSharper disable once StringLiteralTypo
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<SophonChunksInfoAlt>k__BackingField")]
        private static extern ref SophonChunksInfo GetChunkAssetChunksInfoAlt(SophonAsset element);

        protected override async Task FilterSophonPatchAssetList(List<SophonPatchAsset> itemList, CancellationToken token)
        {
            const StringSplitOptions splitOptions = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;

            string gameExecDataName =
                Path.GetFileNameWithoutExtension(GameVersionManager.GamePreset.GameExecutableName) ?? "ZenlessZoneZero";
            string gameExecDataPath         = $"{gameExecDataName}_Data";
            string gamePersistentDataPath   = Path.Combine(GamePath,               gameExecDataPath, "Persistent");
            string gameExceptMatchFieldFile = Path.Combine(gamePersistentDataPath, "KDelResource");

            if (!File.Exists(gameExceptMatchFieldFile))
            {
                return;
            }

            string          exceptMatchFieldContent = await File.ReadAllTextAsync(gameExceptMatchFieldFile, token);
            HashSet<string> exceptMatchFieldHashSet = CreateHashSet();

            if (exceptMatchFieldHashSet.Count == 0)
            {
                return;
            }

            FilterAsset();

            return;

            void FilterAsset()
            {
                const string       separators    = "/\\";
                scoped Span<Range> urlPathRanges = stackalloc Range[32];

                HashSet<string>.AlternateLookup<ReadOnlySpan<char>> alternateLookup =
                    exceptMatchFieldHashSet.GetAlternateLookup<ReadOnlySpan<char>>();

                List<SophonPatchAsset> filteredList = [];
                foreach (SophonPatchAsset asset in itemList)
                {
                    token.ThrowIfCancellationRequested();
                    ref SophonChunksInfo chunkInfo = ref asset.MainAssetInfo == null
                        ? ref Unsafe.NullRef<SophonChunksInfo>()
                        : ref GetChunkAssetChunksInfo(asset.MainAssetInfo);

                    if (asset.MainAssetInfo != null && Unsafe.IsNullRef(ref chunkInfo))
                    {
                        chunkInfo = ref GetChunkAssetChunksInfoAlt(asset.MainAssetInfo);
                    }

                    if (Unsafe.IsNullRef(ref chunkInfo))
                    {
                        filteredList.Add(asset);
                        continue;
                    }

                    ReadOnlySpan<char> manifestUrl = chunkInfo.ChunksBaseUrl;
                    int                rangeLen    = manifestUrl.SplitAny(urlPathRanges, separators, splitOptions);

                    if (rangeLen <= 0)
                    {
                        continue;
                    }

                    ReadOnlySpan<char> manifestStr = manifestUrl[urlPathRanges[rangeLen - 1]];
                    if (alternateLookup.Contains(manifestStr))
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

            HashSet<string> CreateHashSet()
            {
                const string       lineFeedSeparators = "\r\n";
                HashSet<string>    hashSetReturn      = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                scoped Span<Range> contentLineRange   = stackalloc Range[2];

                ReadOnlySpan<char> contentSpan = exceptMatchFieldContent.AsSpan();
                int contentLineLen = contentSpan.SplitAny(contentLineRange, lineFeedSeparators, splitOptions);

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
                    hashSetReturn.Add(contentMatch.ToString());
                }

                return hashSetReturn;
            }
        }
    }
}
