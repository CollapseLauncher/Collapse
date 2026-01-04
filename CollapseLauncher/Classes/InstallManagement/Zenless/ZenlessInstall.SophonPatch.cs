using Hi3Helper.Sophon;
using Hi3Helper.Sophon.Infos;
using Hi3Helper.Sophon.Structs;
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
            HashSet<string> exceptMatchFieldHashSet = await GetExceptMatchFieldHashSet(token);
            if (exceptMatchFieldHashSet.Count == 0)
            {
                return;
            }

            FilterSophonAsset(itemList, exceptMatchFieldHashSet);
        }

        private async Task<HashSet<string>> GetExceptMatchFieldHashSet(CancellationToken token)
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

            string          exceptMatchFieldContent = await File.ReadAllTextAsync(gameExceptMatchFieldFile, token);
            HashSet<string> exceptMatchFieldHashSet = CreateExceptMatchFieldHashSet(exceptMatchFieldContent);

            return exceptMatchFieldHashSet;
        }

        // ReSharper disable once IdentifierTypo
        private static void FilterSophonAsset<T>(List<T> itemList, HashSet<string> exceptMatchFieldHashSet)
        {
            List<T> filteredList = [];
            foreach (T asset in itemList)
            {
                if (asset is SophonIdentifiableProperty { MatchingField: { } assetMatchingField } &&
                    exceptMatchFieldHashSet.Contains(assetMatchingField))
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

        internal static HashSet<string> CreateExceptMatchFieldHashSet(string exceptMatchFieldContent)
        {
            const string       lineFeedSeparators = "\r\n";
            HashSet<string>    hashSetReturn      = new(StringComparer.OrdinalIgnoreCase);
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
                hashSetReturn.Add(contentMatch.ToString());
            }

            return hashSetReturn;
        }
    }
}
