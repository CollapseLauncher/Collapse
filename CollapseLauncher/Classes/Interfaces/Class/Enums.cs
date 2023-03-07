using System.Text.Json.Serialization;

namespace CollapseLauncher
{
    internal enum RepairAssetType
    {
        General,
        Block,
        Audio,
        AudioUpdate,
        Video,
        Chunk,
        Unused
    }

    internal enum CacheAssetType
    {
        Data,
        Event,
        AI,
        Unused,
        Dispatcher,
        Gateway
    }

    internal enum CacheAssetStatus
    {
        New,
        Obsolete,
        Unused
    }
}
