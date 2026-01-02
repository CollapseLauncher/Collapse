using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.RepairManagement.StarRail.Struct.Assets;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

#pragma warning disable IDE0290 // Shut the fuck up
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher;

file static class StarRailPersistentExtension
{
    public static IEnumerable<T> WhereNotStartWith<T>(this   IEnumerable<T>       enumerable,
                                                      params ReadOnlySpan<string> excludeStartWith)
        where T : StarRailAssetGenericFileInfo
    {
        SearchValues<string> excludeStartWithS = SearchValues.Create(excludeStartWith, StringComparison.OrdinalIgnoreCase);
        return enumerable.Where(Impl);

        bool Impl(T asset)
        {
            ReadOnlySpan<char> filePath = asset.Filename;
            return filePath.IndexOfAny(excludeStartWithS) < 0;
        }
    }

    public static string GetPersistentLangPrefixToLauncherAudioLang(this string str)
        => str switch
           {
               "English" => "English(US)",
               "Chinese(PRC)" => "Chinese",
               _ => str
           };
}

internal partial class StarRailPersistentRefResult
{
    public List<FilePropertiesRemote> GetPersistentFiles(
        List<FilePropertiesRemote> fileList,
        string                     gameDirPath,
        string[]                   installedVoiceLang)
    {
        Dictionary<string, FilePropertiesRemote> oldDic       = fileList.ToDictionary(x => x.N);
        Dictionary<string, FilePropertiesRemote> unusedAssets = new(StringComparer.OrdinalIgnoreCase);

        string[] audioLangPrefix = ["Chinese(PRC)", "Japanese", "Korean", "English"];
        string[] excludedAudioLangPrefix = audioLangPrefix
                                          .Where(x => !installedVoiceLang.Contains(x.GetPersistentLangPrefixToLauncherAudioLang(), StringComparer.OrdinalIgnoreCase))
                                          .ToArray();


        foreach (FilePropertiesRemote asset in fileList)
        {
            oldDic.TryAdd(asset.N, asset);
        }

        if (Metadata.StartBlockV != null)
        {
            AddAdditionalAssets(gameDirPath,
                                BaseDirs.StreamingAsbBlock,
                                BaseDirs.PersistentAsbBlock,
                                BaseUrls.AsbBlock,
                                BaseUrls.AsbBlockPersistent,
                                false,
                                fileList,
                                unusedAssets,
                                oldDic,
                                Metadata.StartBlockV.DataList);
        }

        if (Metadata.BlockV != null)
        {
            AddAdditionalAssets(gameDirPath,
                                BaseDirs.StreamingAsbBlock,
                                BaseDirs.PersistentAsbBlock,
                                BaseUrls.AsbBlock,
                                BaseUrls.AsbBlockPersistent,
                                false,
                                fileList,
                                unusedAssets,
                                oldDic,
                                Metadata.BlockV.DataList);
        }

        if (Metadata.VideoV != null)
        {
            AddAdditionalAssets(gameDirPath,
                                BaseDirs.StreamingVideo,
                                BaseDirs.PersistentVideo,
                                BaseUrls.Video,
                                BaseUrls.Video,
                                true,
                                fileList,
                                unusedAssets,
                                oldDic,
                                Metadata.VideoV.DataList);
        }

        if (Metadata.AudioV != null)
        {
            AddAdditionalAssets(gameDirPath,
                                BaseDirs.StreamingAudio,
                                BaseDirs.PersistentAudio,
                                BaseUrls.Audio,
                                BaseUrls.Audio,
                                true,
                                fileList,
                                unusedAssets,
                                oldDic,
                                Metadata.AudioV!.DataList
                                        .WhereNotStartWith(excludedAudioLangPrefix));

            AddUnusedAudioAssets(gameDirPath,
                                 BaseDirs.StreamingAudio,
                                 BaseDirs.PersistentAudio,
                                 Metadata.AudioV!.DataList,
                                 fileList,
                                 excludedAudioLangPrefix);
        }

        if (Metadata.CacheLua != null)
        {
            AddAdditionalAssets(gameDirPath,
                                BaseDirs.CacheLua!.Replace("Persistent", "StreamingAssets"),
                                BaseDirs.CacheLua ?? "",
                                BaseUrls.CacheLua ?? "",
                                BaseUrls.CacheLua ?? "",
                                false,
                                fileList,
                                unusedAssets,
                                oldDic,
                                Metadata.CacheLua.DataList);
        }

        if (Metadata.CacheIFix != null)
        {
            AddAdditionalAssets(gameDirPath,
                                BaseDirs.CacheIFix ?? "",
                                BaseDirs.CacheIFix ?? "",
                                BaseUrls.CacheIFix ?? "",
                                BaseUrls.CacheIFix ?? "",
                                false,
                                fileList,
                                unusedAssets,
                                oldDic,
                                Metadata.CacheIFix.DataList);
        }

        return unusedAssets.Values.ToList();
    }

    private static void AddUnusedAudioAssets<T>(
        string                      gameDir,
        string                      streamingDir,
        string                      persistentDir,
        IEnumerable<T>              listEnumerator,
        List<FilePropertiesRemote>  fileList,
        params ReadOnlySpan<string> excludedAudioLang)
        where T : StarRailAssetFlaggable
    {
        if (excludedAudioLang.Length == 4) // Assume the user doesn't have any language installed, so ignore it.
        {
            return;
        }

        string baseStreamingDir  = Path.Combine(gameDir, streamingDir);
        string basePersistentDir = Path.Combine(gameDir, persistentDir);

        SearchValues<string> searchIndexes = SearchValues.Create(excludedAudioLang, StringComparison.OrdinalIgnoreCase);
        foreach (T entry in listEnumerator)
        {
            ReadOnlySpan<char> filename = entry.Filename;
            int                indexOf  = filename.IndexOfAny(searchIndexes);
            if (indexOf != 0)
            {
                continue;
            }

            string filenameStr = entry.Filename ?? "";

            string atStreaming  = Path.Combine(baseStreamingDir,  filenameStr).NormalizePath();
            string atPersistent = Path.Combine(basePersistentDir, filenameStr).NormalizePath();

            string relStreaming  = Path.Combine(streamingDir,  filenameStr).NormalizePath();
            string relPersistent = Path.Combine(persistentDir, filenameStr).NormalizePath();

            if (File.Exists(atStreaming))
            {
                FilePropertiesRemote entryToRemove = new()
                {
                    FT = FileType.Unused,
                    N  = relStreaming
                };
                fileList.Add(entryToRemove);
            }

            // ReSharper disable once InvertIf
            if (File.Exists(atPersistent))
            {
                FilePropertiesRemote entryToRemove = new()
                {
                    FT = FileType.Unused,
                    N  = relPersistent
                };
                fileList.Add(entryToRemove);
            }
        }
    }

    private static void AddAdditionalAssets<T>(
        string                                   gameDirPath,
        string                                   assetDirPathStreaming,
        string                                   assetDirPathPersistent,
        string                                   urlBase,
        string                                   urlBasePersistent,
        bool                                     isHashMarked,
        List<FilePropertiesRemote>               fileList,
        Dictionary<string, FilePropertiesRemote> unusedFileList,
        Dictionary<string, FilePropertiesRemote> fileDic,
        IEnumerable<T>                           flaggableAssets)
        where T : StarRailAssetFlaggable
    {
        foreach (T asset in flaggableAssets)
        {
            string filename = asset.Filename?.NormalizePath() ?? "";

            // Gets relative and absolute paths.
            string relPathInStreaming  = Path.Combine(assetDirPathStreaming,  filename);
            string relPathInPersistent = Path.Combine(assetDirPathPersistent, filename);
            string pathInStreaming     = Path.Combine(gameDirPath,            relPathInStreaming);
            string pathInPersistent    = Path.Combine(gameDirPath,            relPathInPersistent);

            // If file is not persistent while exists on both persistent and streaming, then
            // remove the persistent one.
            if (!asset.IsPersistent &&
                File.Exists(pathInPersistent) &&
                File.Exists(pathInStreaming) &&
                pathInStreaming != pathInPersistent)
            {
                unusedFileList.TryAdd(relPathInPersistent, new FilePropertiesRemote
                {
                    FT = FileType.Unused,
                    N = relPathInPersistent
                });
            }

            // Try to check entry existence
            ref FilePropertiesRemote assetFromDic = ref CollectionsMarshal
               .GetValueRefOrNullRef(fileDic,
                                     relPathInPersistent);

            if (Unsafe.IsNullRef(ref assetFromDic))
            {
                assetFromDic = ref CollectionsMarshal
                   .GetValueRefOrNullRef(fileDic,
                                         relPathInStreaming);
            }

            // Skip if entry already exist and file is not persistent.
            if (!Unsafe.IsNullRef(ref assetFromDic) &&
                !asset.IsPersistent)
            {
                continue;
            }

            // Now, the game will see any files which don't exist on Sophon as persistent files,
            // even though they are not marked as persistent in metadata.
            string url = (asset.IsPersistent
                ? urlBasePersistent
                : urlBase).CombineURLFromString(asset.Filename);

            FilePropertiesRemote file = new()
            {
                RN            = url,
                N             = relPathInPersistent,
                S             = asset.FileSize,
                CRCArray      = asset.MD5Checksum,
                FT            = StarRailRepairV2.DetermineFileTypeFromExtension(asset.Filename ?? ""),
                IsHasHashMark = isHashMarked
            };
            fileDic.TryAdd(relPathInPersistent, file);
            fileList.Add(file);
        }
    }
}
