using Hi3Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.Metadata;

internal static partial class MetadataHelper
{
    public static async Task<Dictionary<string, Stamp>> CheckMetadataUpdateAsync()
    {
        if (Interlocked.Exchange(ref _lockIsUpdateInstanceRunning, true))
        {
            return [];
        }

        try
        {
            Dictionary<string, Stamp> diffDict         = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, Stamp> mergedStampsDict = new(StringComparer.OrdinalIgnoreCase);
            UpdateUtil.MergeWithDict(StampMasterKeyDict,     mergedStampsDict);
            UpdateUtil.MergeWithDict(StampGameDict,          mergedStampsDict);
            UpdateUtil.MergeWithDict(StampCommunityToolDict, mergedStampsDict);

            List<Stamp> remoteStamps = await Util.LoadRemoteStreamAsync(LauncherMetadataStampStamp, StampJsonContext.Default.ListStamp);
            UpdateUtil.GetDifferStamps(remoteStamps, mergedStampsDict, diffDict, LauncherMetadataDirectory);

            return diffDict;
        }
        catch (Exception e)
        {
            Exception parentExc = new("An error has occurred while checking for metadata update!", e);
            Logger.LogWriteLine($"[MetadataHelper::CheckMetadataUpdateAsync] {parentExc}",
                                LogType.Error,
                                true);
            ErrorSender.SendException(parentExc);
            return [];
        }
        finally
        {
            Interlocked.Exchange(ref _lockIsUpdateInstanceRunning, false);
        }
    }

    public static void ApplyMetadataUpdate(Dictionary<string, Stamp> diffDict)
    {
        // Apply update by removing all outdated files first, then the new ones will be loaded while re-initialized.
        Util.RemoveFile(LauncherMetadataStampStamp);
        foreach (Stamp stamp in diffDict.Values)
        {
            Util.RemoveFile(stamp);
        }
    }
}

file static class UpdateUtil
{
    public static void GetDifferStamps(
        List<Stamp>               remoteStamps,
        Dictionary<string, Stamp> toCompareDict,
        Dictionary<string, Stamp> diffDict,
        string                    metadataDir)
    {
        foreach (Stamp stamp in remoteStamps)
        {
            // Skip update check for Plugins
            if (stamp.MetadataType == MetadataType.PresetConfigPlugin)
            {
                continue;
            }

            string key = stamp.ToString();

            ref Stamp existingStamp = ref CollectionsMarshal.GetValueRefOrNullRef(toCompareDict, key);
            if (Unsafe.IsNullRef(ref existingStamp)) // Check if stamp is a newly added entry.
            {
                diffDict.TryAdd(key, stamp);
                continue;
            }

            if (existingStamp.LastUpdated != stamp.LastUpdated)
            {
                diffDict.TryAdd(key, stamp);
                continue;
            }

            string filePathLocal = Path.Combine(metadataDir, stamp.MetadataPath);
            if (File.Exists(filePathLocal) &&
                File.GetLastWriteTimeUtc(filePathLocal) is var fileWriteLocalUtc &&
                fileWriteLocalUtc != existingStamp.LastModifiedTimeUtc)
            {
                diffDict.TryAdd(key, stamp);
            }
        }
    }

    public static void MergeWithDict(Dictionary<string, Stamp> source, Dictionary<string, Stamp> target)
    {
        foreach (KeyValuePair<string, Stamp> kvp in source)
        {
            target.Add(kvp.Key, kvp.Value);
        }
    }
}