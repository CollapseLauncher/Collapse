using CollapseLauncher.Helper;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.RepairManagement;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher;

internal partial class HonkaiRepairV2
{
    private void CheckAssetUnusedType(
        List<FilePropertiesRemote> foundList,
        List<FilePropertiesRemote> assetList)
    {
        string gameDir = GamePath;
        SearchValues<string> searchValuesStartsWith = SearchValues.Create([
            "ScreenShot",
            "@",
            "d3d",
            "dxgi.dll",
            GameVersionManager!.GamePreset.ProfileName!
        ], StringComparison.OrdinalIgnoreCase);

        SearchValues<string> searchValuesContains = SearchValues.Create([
            ".log",
            ".sys",
            "Version.txt",
            "SDKCaches",
            "webCaches",
            "LauncherPlugins",
            ".ini"
        ], StringComparison.OrdinalIgnoreCase);

        HashSet<string> hashSetList = GetExclusionHashSetFromAssetList(foundList, assetList);
        var hashSetLookup = hashSetList.GetAlternateLookup<ReadOnlySpan<char>>();

        Regex? matchIgnoredRegex = null;
        string ignoredFilesPath = Path.Combine(GamePath, "@IgnoredFiles");
        if (File.Exists(ignoredFilesPath))
        {
            try
            {
                string[] ignoredFiles = File.ReadAllLines(ignoredFilesPath);
                string mergedPattern = PatternMatcher.MergeRegexPattern(ignoredFiles);

                matchIgnoredRegex = new Regex(mergedPattern, RegexOptions.IgnoreCase |
                                                             RegexOptions.NonBacktracking |
                                                             RegexOptions.Compiled);

                Logger.LogWriteLine($"Found ignore file settings! Match Regex: {mergedPattern}");
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex);
                Logger.LogWriteLine($"Failed when reading ignore file setting! Ignoring...\r\n{ex}", LogType.Error, true);
            }
        }

        DirectoryInfo dirInfo = new(gameDir);
        foreach (FileInfo fileInfo in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            string fileFullPath = fileInfo.FullName;
            ReadOnlySpan<char> fileRelativePath = TrimToRelativePath(fileFullPath);
            ReadOnlySpan<char> fileName = Path.GetFileName(fileRelativePath);

            if (fileRelativePath.IndexOfAny(searchValuesStartsWith) == 0)
                continue;
            if (fileName.IndexOfAny(searchValuesStartsWith) == 0)
                continue;
            if (fileRelativePath.ContainsAny(searchValuesContains))
                continue;

            if (hashSetLookup.Contains(fileRelativePath))
                continue;

            if (matchIgnoredRegex?.IsMatch(fileRelativePath) ?? false)
                continue;

            FilePropertiesRemote asset = new()
            {
                FT = FileType.Unused,
                N = fileRelativePath.ToString(),
                S = fileInfo.Length
            };
            this.AddBrokenAssetToList(asset, null, 0);
        }

        return;

        ReadOnlySpan<char> TrimToRelativePath(ReadOnlySpan<char> fullPath)
        {
            return fullPath[gameDir.Length..].Trim(Path.DirectorySeparatorChar);
        }
    }

    private static HashSet<string> GetExclusionHashSetFromAssetList(List<FilePropertiesRemote> foundList, List<FilePropertiesRemote> assetList)
    {
        HashSet<string> hashSetList = new(StringComparer.OrdinalIgnoreCase);

        // Adds primary asset list to lookup
        foreach (FilePropertiesRemote asset in assetList)
        {
            hashSetList.Add(asset.N.NormalizePath());
        }

        // Adds old block asset and its patch file to avoid being deleted
        foreach (BlockOldPatchInfo blockPatchAsset in foundList
                                                     .Where(x => x is { FT: FileType.Block, IsPatchApplicable: true })
                                                     .Select(x => x.BlockPatchInfo)
                                                     .OfType<BlockPatchInfo>()
                                                     .SelectMany(x => x.PatchPairs))
        {
            string oldFileName = Path.Combine(AssetBundleExtension.RelativePathBlock, blockPatchAsset.OldName);
            string patchFileName = Path.Combine(AssetBundleExtension.RelativePathBlockPatch, blockPatchAsset.PatchName);
            ConverterTool.NormalizePathInplaceNoTrim(oldFileName);
            ConverterTool.NormalizePathInplaceNoTrim(patchFileName);

            hashSetList.Add(oldFileName);
            hashSetList.Add(patchFileName);
        }

        // Adds audio patch file to avoid being deleted
        foreach (string? audioPatchFilename in foundList
                                              .Where(x => x is { FT: FileType.Audio, IsPatchApplicable: true })
                                              .Select(x => x.AudioPatchInfo?.PatchFilename))
        {
            if (string.IsNullOrEmpty(audioPatchFilename))
            {
                continue;
            }

            string patchFileName = Path.Combine(AssetBundleExtension.RelativePathAudioPatch, audioPatchFilename);
            hashSetList.Add(patchFileName);
        }

        return hashSetList;
    }
}
