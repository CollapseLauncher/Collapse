using Hi3Helper;
using Hi3Helper.SentryHelper;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.Metadata;

internal static partial class MetadataHelper
{
    private static async Task InitializeStampAsync()
    {
        StampMasterKeyDict.Clear();
        StampGameDict.Clear();
        StampCommunityToolDict.Clear();

        List<Stamp> stamps = await Util.LoadFileAsync(LauncherMetadataStampStamp, StampJsonContext.Default.ListStamp);
        AddStampDictFromType(stamps, StampMasterKeyDict,     MetadataType.MasterKey);
        AddStampDictFromType(stamps, StampGameDict,          MetadataType.PresetConfigV2);
        AddStampDictFromType(stamps, StampCommunityToolDict, MetadataType.CommunityTools);
        return;

        static void AddStampDictFromType(List<Stamp> sourceList, Dictionary<string, Stamp> dict, MetadataType type)
        {
            foreach (Stamp stamp in sourceList.Where(x => x.MetadataType == type && x.MetadataInclude))
            {
                string key = stamp.ToString();
                if (dict.TryAdd(key, stamp)) continue;

                SentryHelper.ExceptionHandler(new DuplicateNameException($"Stamp type: {type} with key: {key} (HashCode: {stamp.GetHashCode()} was found duplicated!"));
                Logger.LogWriteLine($"[MetadataHelper::InitializeStampAsync] Stamp type: {type} with key: \"{key}\" has been added previously. Skipping...",
                                    LogType.Warning,
                                    true);
            }
        }
    }

}
