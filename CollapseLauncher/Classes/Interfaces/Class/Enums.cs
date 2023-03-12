namespace CollapseLauncher
{
    internal enum RepairAssetType
    {
        General,
        Block,
        BlockUpdate,
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
